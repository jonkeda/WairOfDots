using Brinell.Automation;
using Brinell.Automation.Communication;
using Stride.Engine;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using System.Text.Json;
using WairOfDots.Core;

namespace WairOfDots.Windows;

public sealed class WairAutomationHandler : IAutomationHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly StrideUIHandler _inner;
    private readonly WairOfDotsGame _game;
    private readonly Func<GameSimulation> _simulationProvider;

    public WairAutomationHandler(Func<UIElement?> uiRootProvider, WairOfDotsGame game, Func<GameSimulation> simulationProvider)
    {
        _inner = new StrideUIHandler(uiRootProvider, () => true, () => false, game);
        _game = game;
        _simulationProvider = simulationProvider;
    }

    public Task<AutomationResponse> HandleCommandAsync(AutomationCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Type == "GameQuery")
        {
            lock (_game.StateLock)
            {
                var response = HandleGameQuery(command);
                if (response != null)
                    return Task.FromResult(response);
            }
        }

        return _inner.HandleCommandAsync(command, cancellationToken);
    }

    private AutomationResponse? HandleGameQuery(AutomationCommand command)
    {
        return command.Method switch
        {
            "GetSnapshot" => Ok(_simulationProvider().CreateSnapshot()),
            "GetFingerprint" => Ok(new FingerprintResponse(_simulationProvider().CreateFingerprint())),
            "StepTicks" => StepTicks(command),
            "StartMatch" => StartMatch(command),
            "StartAiOnly" => StartAiOnly(command),
            "RestartMatch" => RestartMatch(),
            "SetDirective" => SetDirective(command),
            "SelectTarget" => SelectTarget(command),
            "SetLightPreference" => SetLightPreference(command),
            "SetSimulationSpeed" => SetSimulationSpeed(command),
            "TogglePause" => TogglePause(),
            "GetTelemetry" => Ok(_simulationProvider().Telemetry.TakeLast(10).ToList()),
            "GetStandings" => Ok(_simulationProvider().CreateSnapshot().Standings),
            "GetUnitVisuals" => Ok(_game.CreateUnitVisualStates()),
            "GetMapState" => Ok(CreateMapState()),
            "GetUiDiagnostics" => Ok(CreateUiDiagnostics()),
            _ => null
        };
    }

    private MapStateResponse CreateMapState()
    {
        var simulation = _simulationProvider();
        var activeUnits = simulation.Units
            .Where(unit => unit.IsAlive && !simulation.Players[unit.PlayerId].IsEliminated)
            .ToList();
        var occupiedCells = activeUnits.Select(unit => unit.Cell).Distinct().Count();
        var cityOccupiedCells = simulation.Cities.Count(city => activeUnits.Any(unit => unit.Cell == city.GridPosition));

        return new MapStateResponse(
            simulation.Terrain.Count,
            simulation.Terrain.Select(terrain => terrain.Kind.ToString()).Distinct().OrderBy(value => value).ToArray(),
            simulation.Grid.Width,
            simulation.Grid.Height,
            simulation.Grid.Cells.Count(cell => cell.IsPassable),
            simulation.Grid.Cells.Count(cell => !cell.IsPassable),
            simulation.Cities.Count,
            activeUnits.Count,
            simulation.MovingUnitCount,
            simulation.MovingUnitGridStepCount,
            activeUnits.Count(unit => unit.Kind == UnitKind.Commander),
            activeUnits.Count(unit => unit.Kind == UnitKind.General),
            occupiedCells,
            cityOccupiedCells,
            activeUnits.Count - occupiedCells,
            simulation.ActiveCombatCount);
    }

    private UiDiagnosticsResponse CreateUiDiagnostics()
    {
        var visibleTexts = new List<string>();
        var uiEntityNames = new List<string>();
        var componentTypeNames = new List<string>();
        var rootScene = _game.SceneSystem.SceneInstance.RootScene;

        CollectSceneDiagnostics(rootScene, visibleTexts, uiEntityNames, componentTypeNames);

        return new UiDiagnosticsResponse(
            visibleTexts,
            uiEntityNames,
            componentTypeNames,
            componentTypeNames.Count(name => name == nameof(CameraComponent)),
            componentTypeNames.Count(name => name.Contains("MouseLook", StringComparison.OrdinalIgnoreCase)),
            uiEntityNames.Count);
    }

    private static void CollectSceneDiagnostics(
        Scene scene,
        List<string> visibleTexts,
        List<string> uiEntityNames,
        List<string> componentTypeNames)
    {
        foreach (var entity in scene.Entities)
        {
            foreach (var component in entity.Components)
                componentTypeNames.Add(component.GetType().Name);

            var uiComponent = entity.Get<UIComponent>();
            if (uiComponent?.Page?.RootElement != null)
            {
                uiEntityNames.Add(entity.Name);
                CollectVisibleTexts(uiComponent.Page.RootElement, visibleTexts);
            }
        }

        foreach (var childScene in scene.Children)
            CollectSceneDiagnostics(childScene, visibleTexts, uiEntityNames, componentTypeNames);
    }

    private static void CollectVisibleTexts(UIElement element, List<string> visibleTexts)
    {
        if (element.Visibility != Visibility.Visible)
            return;

        if (element is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
            visibleTexts.Add(textBlock.Text);

        if (element is ContentControl { Content: UIElement contentElement })
            CollectVisibleTexts(contentElement, visibleTexts);

        if (element is Panel panel)
        {
            foreach (var child in panel.Children)
                CollectVisibleTexts(child, visibleTexts);
        }
    }

    private AutomationResponse StepTicks(AutomationCommand command)
    {
        var ticks = GetInt(command, 0, 1);
        _game.StepTicks(ticks);
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse StartMatch(AutomationCommand command)
    {
        var seed = GetInt(command, 0, 1337);
        var aiPlayers = GetInt(command, 1, 4);
        var mode = GetMode(command, 2, GameMode.HumanVsAi);
        _game.StartMatch(seed, aiPlayers, mode);
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse StartAiOnly(AutomationCommand command)
    {
        var seed = GetInt(command, 0, 1337);
        var aiPlayers = GetInt(command, 1, 4);
        _game.StartMatch(seed, aiPlayers, GameMode.AiOnly);
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse RestartMatch()
    {
        _game.RestartMatch();
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse SetDirective(AutomationCommand command)
    {
        var value = GetString(command, 0, "Attack");
        if (!Enum.TryParse<PlayerDirective>(value, ignoreCase: true, out var directive))
            return AutomationResponse.Fail($"Unknown directive: {value}");

        _game.SetHumanDirective(directive);
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse SelectTarget(AutomationCommand command)
    {
        _game.SelectTargetCity(GetInt(command, 0, 0));
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse SetLightPreference(AutomationCommand command)
    {
        _game.SetLightPreference(GetDouble(command, 0, 0.65));
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse SetSimulationSpeed(AutomationCommand command)
    {
        _game.SetSimulationSpeed(GetInt(command, 0, 1));
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse TogglePause()
    {
        _game.TogglePause();
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private static AutomationResponse Ok(object value)
        => AutomationResponse.Ok(JsonSerializer.SerializeToElement(value, JsonOptions));

    private static int GetInt(AutomationCommand command, int index, int fallback)
    {
        var arg = GetArg(command, index);
        if (arg is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var number))
                return number;
            if (json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), out number))
                return number;
        }

        return int.TryParse(arg?.ToString(), out var parsed) ? parsed : fallback;
    }

    private static double GetDouble(AutomationCommand command, int index, double fallback)
    {
        var arg = GetArg(command, index);
        if (arg is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var number))
                return number;
            if (json.ValueKind == JsonValueKind.String && double.TryParse(json.GetString(), out number))
                return number;
        }

        return double.TryParse(arg?.ToString(), out var parsed) ? parsed : fallback;
    }

    private static string GetString(AutomationCommand command, int index, string fallback)
    {
        var arg = GetArg(command, index);
        if (arg is JsonElement json)
            return json.ValueKind == JsonValueKind.String ? json.GetString() ?? fallback : json.GetRawText();

        return arg?.ToString() ?? fallback;
    }

    private static GameMode GetMode(AutomationCommand command, int index, GameMode fallback)
    {
        var value = GetString(command, index, fallback.ToString());
        return Enum.TryParse<GameMode>(value, ignoreCase: true, out var mode) ? mode : fallback;
    }

    private static object? GetArg(AutomationCommand command, int index)
        => command.Args != null && command.Args.Length > index ? command.Args[index] : null;
}

public sealed record FingerprintResponse(string Fingerprint);

public sealed record MapStateResponse(
    int TerrainPatchCount,
    IReadOnlyList<string> TerrainTypes,
    int GridWidth,
    int GridHeight,
    int PassableCellCount,
    int BlockedCellCount,
    int CityMarkerCount,
    int UnitMarkerCount,
    int MovingUnitCount,
    int MovingUnitGridStepCount,
    int CommanderMarkerCount,
    int GeneralMarkerCount,
    int OccupiedCellCount,
    int CityOccupiedCellCount,
    int DuplicateOccupiedCellCount,
    int ActiveCombatCount);

public sealed record UnitVisualStateResponse(
    int UnitId,
    int PlayerId,
    string Kind,
    int CellX,
    int CellY,
    int VisualFromCellX,
    int VisualFromCellY,
    int VisualToCellX,
    int VisualToCellY,
    double VisualFromX,
    double VisualFromY,
    double VisualToX,
    double VisualToY,
    double VisualX,
    double VisualY,
    double Progress,
    bool IsInterpolating);

public sealed record UiDiagnosticsResponse(
    IReadOnlyList<string> VisibleTexts,
    IReadOnlyList<string> UiEntityNames,
    IReadOnlyList<string> ComponentTypeNames,
    int CameraComponentCount,
    int MouseLookComponentCount,
    int UiComponentCount);
