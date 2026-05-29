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
            "StepToTick" => StepToTick(command),
            "StepUntil" => StepUntil(command),
            "StartMatch" => StartMatch(command),
            "StartAiOnly" => StartAiOnly(command),
            "RestartMatch" => RestartMatch(),
            "SetDirective" => SetDirective(command),
            "SelectTarget" => SelectTarget(command),
            "AssignReserveGroup" => AssignReserveGroup(command),
            "RecallCommanderGroup" => RecallCommanderGroup(command),
            "HitTestMap" => HitTestMap(command),
            "ClickMap" => ClickMap(command),
            "GetMapInteractionTargets" => Ok(_game.CreateMapInteractionTargets()
                .Select(ToMapInteractionTargetResponse)
                .ToArray()),
            "SetLightPreference" => SetLightPreference(command),
            "SetSimulationSpeed" => SetSimulationSpeed(command),
            "TogglePause" => TogglePause(),
            "GetTelemetry" => Ok(_simulationProvider().Telemetry.TakeLast(50).ToList()),
            "GetStandings" => Ok(_simulationProvider().CreateSnapshot().Standings),
            "GetAiStates" => Ok(CreateAiStates()),
            "GetCommandChains" => Ok(CreateCommandChains()),
            "GetUnitVisuals" => Ok(_game.CreateUnitVisualStates()),
            "GetMapState" => Ok(CreateMapState()),
            "GetMapVisualDiagnostics" => Ok(_game.CreateMapVisualDiagnostics()),
            "SetMapOverlay" => SetMapOverlay(command),
            "GetMapOverlay" => Ok(new MapOverlayResponse(_game.GetMapOverlayMode().ToString())),
            "SetUnitMorale" => SetUnitMorale(command),
            "GetCellControls" => Ok(_simulationProvider().CreateSnapshot().CellControls),
            "GetTerritoryBoundaries" => Ok(_simulationProvider().CreateTerritoryBoundaries()),
            "GetEconomies" => Ok(_simulationProvider().CreateSnapshot().Economies),
            "GetRegions" => Ok(_simulationProvider().CreateSnapshot().Regions),
            "GetVisibility" => Ok(_simulationProvider().CreateSnapshot().Visibility),
            "RunTrainingSmoke" => RunTrainingSmoke(command),
            "RunTrainingPipeline" => RunTrainingPipeline(command),
            "ExportBehaviorReview" => Ok(BehaviorReviewExporter.ToMarkdown(
                _simulationProvider().CreateSnapshot(),
                _simulationProvider().Telemetry.ToList())),
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
            simulation.ActiveCombatCount,
            simulation.CellControls.Count,
            simulation.CellControls.Count(cell => cell.OwnerId == GameConstants.NeutralPlayerId),
            simulation.CellControls.Count(cell => cell.OwnerId >= 0),
            Math.Round(simulation.CellControls.Sum(cell => cell.TaxValue), 2),
            simulation.TerritoryBoundaries.Count,
            simulation.Cities
                .OrderBy(city => city.Id)
                .Select(city => new CityOwnershipResponse(city.Id, city.Name, city.OwnerId))
                .ToArray());
    }

    private IReadOnlyList<AiStateResponse> CreateAiStates()
    {
        var simulation = _simulationProvider();
        return simulation.Players
            .Where(player => player.Kind == PlayerKind.Ai)
            .OrderBy(player => player.Id)
            .Select(player => new AiStateResponse(
                player.Id,
                player.Name,
                player.GenomeId,
                GenomeNeatController.ResolveArchetype(player.GenomeId),
                player.Directive.ToString(),
                player.TargetCityId,
                player.StrategyMode.ToString(),
                player.ConsecutiveHoldPlans,
                player.IsEliminated,
                simulation.Units.Count(unit => unit.IsAlive && unit.PlayerId == player.Id),
                simulation.Units.Count(unit => unit.IsAlive && unit.PlayerId == player.Id && unit.IsMoving)))
            .ToList();
    }

    private IReadOnlyList<PlayerCommandChainResponse> CreateCommandChains()
    {
        var snapshot = _simulationProvider().CreateSnapshot();
        return snapshot.Players
            .OrderBy(player => player.Id)
            .Select(player =>
            {
                var units = snapshot.Units.Where(unit => unit.PlayerId == player.Id).ToList();
                var commanders = units
                    .Where(unit => unit.Kind == nameof(UnitKind.Commander))
                    .OrderBy(unit => unit.CommanderNumber ?? int.MaxValue)
                    .ThenBy(unit => unit.Id)
                    .Select(commander =>
                    {
                        var generalCommand = snapshot.Commands
                            .Where(command =>
                                command.PlayerId == player.Id &&
                                command.Layer == "GeneralToCommander" &&
                                command.IsActive &&
                                command.TargetUnitId == commander.Id)
                            .OrderByDescending(command => command.Tick)
                            .FirstOrDefault();
                        var assignedUnits = units
                            .Where(unit => unit.AssignedCommanderUnitId == commander.Id)
                            .OrderBy(unit => unit.Id)
                            .Select(unit =>
                            {
                                var unitCommand = snapshot.Commands
                                    .Where(command =>
                                        command.PlayerId == player.Id &&
                                        command.Layer == "CommanderToUnit" &&
                                        command.IsActive &&
                                        command.TargetUnitId == unit.Id)
                                    .OrderByDescending(command => command.Tick)
                                    .FirstOrDefault();
                                return new UnitCommandChainResponse(
                                    unit.Id,
                                    unit.Kind,
                                    unit.CommanderNumber,
                                    ToCommandSummary(unitCommand),
                                    unit.LatestReportType);
                            })
                            .ToArray();
                        var reports = snapshot.Reports
                            .Where(report =>
                                report.PlayerId == player.Id &&
                                report.Layer == "CommanderToGeneral" &&
                                report.SourceUnitId == commander.Id)
                            .OrderByDescending(report => report.Tick)
                            .ThenBy(report => report.ReportType, StringComparer.Ordinal)
                            .Take(4)
                            .Select(report => new ReportSummaryResponse(
                                report.ReportType,
                                report.CellX,
                                report.CellY,
                                report.Urgency))
                            .ToArray();
                        return new CommanderCommandChainResponse(
                            commander.Id,
                            commander.CommanderNumber,
                            ToCommandSummary(generalCommand),
                            assignedUnits,
                            reports);
                    })
                    .ToArray();
                var reserveCommands = snapshot.Commands
                    .Where(command =>
                        command.PlayerId == player.Id &&
                        command.Layer == "GeneralToReserve" &&
                        command.IsActive)
                    .OrderByDescending(command => command.Tick)
                    .ThenBy(command => command.CommanderNumber ?? int.MaxValue)
                    .ThenBy(command => command.TargetUnitId ?? int.MaxValue)
                    .Select(ToCommandSummary)
                    .OfType<CommandSummaryResponse>()
                    .ToArray();
                return new PlayerCommandChainResponse(player.Id, commanders, reserveCommands);
            })
            .ToArray();
    }

    private static CommandSummaryResponse? ToCommandSummary(CommandRecordSnapshot? command)
        => command == null
            ? null
            : new CommandSummaryResponse(
                command.CommandType,
                command.TargetUnitId,
                command.CommanderNumber,
                command.TargetCellX,
                command.TargetCellY,
                command.TargetCityId,
                command.TargetRegionId,
                command.Priority,
                command.ReasonCode);

    private UiDiagnosticsResponse CreateUiDiagnostics()
    {
        var visibleTexts = new List<string>();
        var visibleElementNames = new List<string>();
        var uiEntityNames = new List<string>();
        var componentTypeNames = new List<string>();
        var rootScene = _game.SceneSystem.SceneInstance.RootScene;

        CollectSceneDiagnostics(rootScene, visibleTexts, visibleElementNames, uiEntityNames, componentTypeNames);

        return new UiDiagnosticsResponse(
            visibleTexts,
            visibleElementNames,
            uiEntityNames,
            componentTypeNames,
            componentTypeNames.Count(name => name == nameof(CameraComponent)),
            componentTypeNames.Count(name => name.Contains("MouseLook", StringComparison.OrdinalIgnoreCase)),
            uiEntityNames.Count);
    }

    private static void CollectSceneDiagnostics(
        Scene scene,
        List<string> visibleTexts,
        List<string> visibleElementNames,
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
                CollectVisibleElements(uiComponent.Page.RootElement, visibleTexts, visibleElementNames);
            }
        }

        foreach (var childScene in scene.Children)
            CollectSceneDiagnostics(childScene, visibleTexts, visibleElementNames, uiEntityNames, componentTypeNames);
    }

    private static void CollectVisibleElements(
        UIElement element,
        List<string> visibleTexts,
        List<string> visibleElementNames)
    {
        if (element.Visibility != Visibility.Visible)
            return;

        if (!string.IsNullOrWhiteSpace(element.Name))
            visibleElementNames.Add(element.Name);

        if (element is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
            visibleTexts.Add(textBlock.Text);

        if (element is ContentControl { Content: UIElement contentElement })
            CollectVisibleElements(contentElement, visibleTexts, visibleElementNames);

        if (element is Panel panel)
        {
            foreach (var child in panel.Children)
                CollectVisibleElements(child, visibleTexts, visibleElementNames);
        }
    }

    private AutomationResponse StepTicks(AutomationCommand command)
    {
        var ticks = GetInt(command, 0, 1);
        _game.StepTicks(ticks);
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse StepToTick(AutomationCommand command)
    {
        var targetTick = GetInt(command, 0, 1);
        _game.StepToTick(targetTick);
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse StepUntil(AutomationCommand command)
    {
        var condition = GetString(command, 0, "");
        var compareValue = GetString(command, 1, "");
        var maxTicks = Math.Max(0, GetInt(command, 2, 120));
        var stepSize = Math.Max(1, GetInt(command, 3, 1));
        var ticksStepped = 0;
        var attempts = 0;
        var snapshot = _simulationProvider().CreateSnapshot();
        var (satisfied, detail) = EvaluateStepUntilCondition(condition, compareValue, snapshot);

        while (!satisfied && ticksStepped < maxTicks)
        {
            var ticks = Math.Min(stepSize, maxTicks - ticksStepped);
            _game.StepTicks(ticks);
            ticksStepped += ticks;
            attempts++;
            snapshot = _simulationProvider().CreateSnapshot();
            (satisfied, detail) = EvaluateStepUntilCondition(condition, compareValue, snapshot);
        }

        return Ok(new StepUntilResponse(
            condition,
            compareValue,
            satisfied,
            ticksStepped,
            attempts,
            detail,
            snapshot));
    }

    private (bool Satisfied, string Detail) EvaluateStepUntilCondition(
        string condition,
        string compareValue,
        MatchSnapshot snapshot)
    {
        var simulation = _simulationProvider();
        return condition switch
        {
            "ActiveCombat" => (
                simulation.ActiveCombatCount > 0,
                $"active-combat={simulation.ActiveCombatCount};tick={snapshot.Tick}"),
            "RecentDeathMarker" => (
                snapshot.RecentDeaths.Count > 0,
                $"recent-deaths={snapshot.RecentDeaths.Count};recent-hits={snapshot.RecentCombatHits.Count};tick={snapshot.Tick}"),
            "TerritoryBoundaryCountNotEqual" => (
                int.TryParse(compareValue, out var boundaryCount) && snapshot.TerritoryBoundaries.Count != boundaryCount,
                $"territory-boundaries={snapshot.TerritoryBoundaries.Count};compare={compareValue};tick={snapshot.Tick}"),
            "CityOwnedCountGreaterThan" => (
                int.TryParse(compareValue, out var cityCount) &&
                    snapshot.Cities.Count(city => city.OwnerId != GameConstants.NeutralPlayerId) > cityCount,
                $"owned-cities={snapshot.Cities.Count(city => city.OwnerId != GameConstants.NeutralPlayerId)};compare={compareValue};tick={snapshot.Tick}"),
            "AnyDiagonalVisualTarget" => (
                _game.CreateUnitVisualStates().Any(IsDiagonalVisualTarget),
                $"diagonal-visual-targets={_game.CreateUnitVisualStates().Count(IsDiagonalVisualTarget)};tick={snapshot.Tick}"),
            "AnyInterpolatingUnit" => (
                _game.CreateUnitVisualStates().Any(unit => unit.IsInterpolating),
                $"interpolating-units={_game.CreateUnitVisualStates().Count(unit => unit.IsInterpolating)};tick={snapshot.Tick}"),
            _ => (false, $"unknown-condition={condition};tick={snapshot.Tick}")
        };
    }

    private static bool IsDiagonalVisualTarget(UnitVisualStateResponse unit)
        => unit.IsInterpolating &&
            Math.Abs(unit.VisualFromCellX - unit.VisualToCellX) == 1 &&
            Math.Abs(unit.VisualFromCellY - unit.VisualToCellY) == 1;

    private AutomationResponse StartMatch(AutomationCommand command)
    {
        var seed = GetInt(command, 0, 1337);
        var aiPlayers = GetInt(command, 1, 4);
        var mode = GetMode(command, 2, GameMode.HumanVsAi);
        var humanMode = GetHumanControlMode(command, 3, HumanControlMode.General);
        _game.StartMatch(seed, aiPlayers, mode, WairOfDotsGame.FastSimulationSpeed);
        if (mode == GameMode.HumanVsAi)
            _game.SetHumanControlMode(humanMode);
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse StartAiOnly(AutomationCommand command)
    {
        var seed = GetInt(command, 0, 1337);
        var aiPlayers = GetInt(command, 1, 4);
        _game.StartMatch(seed, aiPlayers, GameMode.AiOnly, WairOfDotsGame.FastSimulationSpeed);
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

    private AutomationResponse AssignReserveGroup(AutomationCommand command)
        => Ok(_game.AssignReserveGroupForHuman(
            GetInt(command, 0, 0),
            Math.Max(1, GetInt(command, 1, 1))));

    private AutomationResponse RecallCommanderGroup(AutomationCommand command)
        => Ok(_game.RecallCommanderGroupForHuman(
            GetInt(command, 0, 0),
            Math.Max(1, GetInt(command, 1, 1))));

    private AutomationResponse HitTestMap(AutomationCommand command)
        => Ok(ToMapHitTestResponse(_game.HitTestMap(
            GetDouble(command, 0, 0),
            GetDouble(command, 1, 0))));

    private AutomationResponse ClickMap(AutomationCommand command)
    {
        var hit = _game.ClickMap(
            GetDouble(command, 0, 0),
            GetDouble(command, 1, 0));
        return Ok(new MapClickResponse(ToMapHitTestResponse(hit), _simulationProvider().CreateSnapshot()));
    }

    private static MapHitTestResponse ToMapHitTestResponse(MapHitResult hit)
        => new(
            hit.Kind.ToString(),
            hit.UnitId,
            hit.CityId,
            hit.CellX,
            hit.CellY,
            hit.NormalizedX,
            hit.NormalizedY,
            hit.DistancePixels,
            hit.Label);

    private static MapInteractionTargetResponse ToMapInteractionTargetResponse(MapInteractionTarget target)
        => new(
            target.Kind,
            target.Id,
            target.Label,
            target.PlayerId,
            target.NormalizedX,
            target.NormalizedY);

    private AutomationResponse SetLightPreference(AutomationCommand command)
    {
        _game.SetLightPreference(GetDouble(command, 0, 0.65));
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse SetSimulationSpeed(AutomationCommand command)
    {
        _game.SetSimulationSpeed(GetDouble(command, 0, WairOfDotsGame.NormalSimulationSpeed));
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse SetMapOverlay(AutomationCommand command)
    {
        var value = GetString(command, 0, MapOverlayMode.Normal.ToString());
        if (!Enum.TryParse<MapOverlayMode>(value, ignoreCase: true, out var mode))
            return AutomationResponse.Fail($"Unknown map overlay: {value}");

        return Ok(new MapOverlayResponse(_game.SetMapOverlayMode(mode).ToString()));
    }

    private AutomationResponse SetUnitMorale(AutomationCommand command)
        => Ok(_game.SetUnitMoraleForDiagnostics(
            GetInt(command, 0, 0),
            GetDouble(command, 1, 1.0)));

    private AutomationResponse TogglePause()
    {
        _game.TogglePause();
        return Ok(_simulationProvider().CreateSnapshot());
    }

    private AutomationResponse RunTrainingSmoke(AutomationCommand command)
    {
        var seed = GetInt(command, 0, 1337);
        var aiPlayers = GetInt(command, 1, 4);
        var ticks = GetInt(command, 2, 120);
        return Ok(DeterministicTrainingRunner.RunSmoke(seed, aiPlayers, ticks));
    }

    private AutomationResponse RunTrainingPipeline(AutomationCommand command)
    {
        var seed = GetInt(command, 0, 1337);
        var aiPlayers = GetInt(command, 1, 4);
        var ticks = GetInt(command, 2, 120);
        var generations = GetInt(command, 3, 1);
        return Ok(HeadlessTrainingPipeline.Run(new TrainingSettings(seed, aiPlayers, ticks, generations)));
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

    private static HumanControlMode GetHumanControlMode(AutomationCommand command, int index, HumanControlMode fallback)
    {
        var value = GetString(command, index, fallback.ToString());
        return Enum.TryParse<HumanControlMode>(value, ignoreCase: true, out var mode) ? mode : fallback;
    }

    private static object? GetArg(AutomationCommand command, int index)
        => command.Args != null && command.Args.Length > index ? command.Args[index] : null;
}

public sealed record FingerprintResponse(string Fingerprint);

public sealed record StepUntilResponse(
    string Condition,
    string CompareValue,
    bool Satisfied,
    int TicksStepped,
    int Attempts,
    string Detail,
    MatchSnapshot Snapshot);

public sealed record MapHitTestResponse(
    string Kind,
    int? UnitId,
    int? CityId,
    int? CellX,
    int? CellY,
    double NormalizedX,
    double NormalizedY,
    double DistancePixels,
    string Label);

public sealed record MapClickResponse(
    MapHitTestResponse Hit,
    MatchSnapshot Snapshot);

public sealed record MapInteractionTargetResponse(
    string Kind,
    int Id,
    string Label,
    int? PlayerId,
    double NormalizedX,
    double NormalizedY);

public sealed record MapOverlayResponse(string Mode);

public sealed record PlayerCommandChainResponse(
    int PlayerId,
    IReadOnlyList<CommanderCommandChainResponse> Commanders,
    IReadOnlyList<CommandSummaryResponse> ReserveCommands);

public sealed record CommanderCommandChainResponse(
    int CommanderUnitId,
    int? CommanderNumber,
    CommandSummaryResponse? GeneralCommand,
    IReadOnlyList<UnitCommandChainResponse> AssignedUnits,
    IReadOnlyList<ReportSummaryResponse> Reports);

public sealed record UnitCommandChainResponse(
    int UnitId,
    string Kind,
    int? CommanderNumber,
    CommandSummaryResponse? Command,
    string LatestReportType);

public sealed record CommandSummaryResponse(
    string CommandType,
    int? TargetUnitId,
    int? CommanderNumber,
    int? TargetCellX,
    int? TargetCellY,
    int? TargetCityId,
    int? TargetRegionId,
    double Priority,
    string ReasonCode);

public sealed record ReportSummaryResponse(
    string ReportType,
    int? CellX,
    int? CellY,
    double Urgency);

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
    int ActiveCombatCount,
    int ControlledCellCount,
    int NeutralControlledCellCount,
    int PlayerControlledCellCount,
    double TotalTaxValue,
    int TerritoryBoundaryCount,
    IReadOnlyList<CityOwnershipResponse> CityOwners);

public sealed record CityOwnershipResponse(
    int CityId,
    string Name,
    int OwnerId);

public sealed record AiStateResponse(
    int PlayerId,
    string Name,
    string GenomeId,
    string Archetype,
    string Directive,
    int TargetCityId,
    string StrategyMode,
    int ConsecutiveHoldPlans,
    bool IsEliminated,
    int UnitCount,
    int MovingUnitCount);

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
    bool IsInterpolating,
    bool IsUsingSmoothedSegment);

public sealed record UiDiagnosticsResponse(
    IReadOnlyList<string> VisibleTexts,
    IReadOnlyList<string> VisibleElementNames,
    IReadOnlyList<string> UiEntityNames,
    IReadOnlyList<string> ComponentTypeNames,
    int CameraComponentCount,
    int MouseLookComponentCount,
    int UiComponentCount);
