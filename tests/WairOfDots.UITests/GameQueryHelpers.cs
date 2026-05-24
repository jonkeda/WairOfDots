using Brinell.Stride.Communication;

namespace WairOfDots.UITests;

public static class GameQueryHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MatchSnapshot StartMatch(IStrideTestContext context, int seed = 1337, int aiPlayers = 4)
        => Send<MatchSnapshot>(context, "StartMatch", seed, aiPlayers);

    public static MatchSnapshot StartAiOnly(IStrideTestContext context, int seed = 1337, int aiPlayers = 4)
        => Send<MatchSnapshot>(context, "StartAiOnly", seed, aiPlayers);

    public static MatchSnapshot GetSnapshot(IStrideTestContext context)
        => Send<MatchSnapshot>(context, "GetSnapshot");

    public static MatchSnapshot StepTicks(IStrideTestContext context, int ticks)
        => Send<MatchSnapshot>(context, "StepTicks", ticks);

    public static FingerprintDto GetFingerprint(IStrideTestContext context)
        => Send<FingerprintDto>(context, "GetFingerprint");

    public static MatchSnapshot SetDirective(IStrideTestContext context, string directive)
        => Send<MatchSnapshot>(context, "SetDirective", directive);

    public static MatchSnapshot SelectTarget(IStrideTestContext context, int cityId)
        => Send<MatchSnapshot>(context, "SelectTarget", cityId);

    public static MatchSnapshot TogglePause(IStrideTestContext context)
        => Send<MatchSnapshot>(context, "TogglePause");

    public static MatchSnapshot SetSimulationSpeed(IStrideTestContext context, int speed)
        => Send<MatchSnapshot>(context, "SetSimulationSpeed", speed);

    public static MapStateDto GetMapState(IStrideTestContext context)
        => Send<MapStateDto>(context, "GetMapState");

    public static UiDiagnosticsDto GetUiDiagnostics(IStrideTestContext context)
        => Send<UiDiagnosticsDto>(context, "GetUiDiagnostics");

    public static IReadOnlyList<StandingDto> GetStandings(IStrideTestContext context)
        => Send<IReadOnlyList<StandingDto>>(context, "GetStandings");

    public static IReadOnlyList<TelemetryDto> GetTelemetry(IStrideTestContext context)
        => Send<IReadOnlyList<TelemetryDto>>(context, "GetTelemetry");

    public static IReadOnlyList<UnitVisualStateDto> GetUnitVisuals(IStrideTestContext context)
        => Send<IReadOnlyList<UnitVisualStateDto>>(context, "GetUnitVisuals");

    private static T Send<T>(IStrideTestContext context, string method, params object[] args)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery(method, args));
        if (!response.Success)
            throw new InvalidOperationException($"{method} failed: {response.Error}");

        var json = ((JsonElement)response.Result!).GetRawText();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"{method} returned null");
    }
}

public sealed record FingerprintDto(string Fingerprint);

public sealed record StandingDto(
    int PlayerId,
    string Name,
    string Kind,
    bool IsEliminated,
    double Score,
    int CityCount,
    int UnitCount,
    double Resources,
    double GeneralHealth,
    string GenomeId);

public sealed record TelemetryDto(
    int Tick,
    int PlayerId,
    string GenomeId,
    string Observation,
    string Action,
    double FitnessDelta);

public sealed record UnitVisualStateDto(
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

public sealed record MapStateDto(
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

public sealed record UiDiagnosticsDto(
    IReadOnlyList<string> VisibleTexts,
    IReadOnlyList<string> UiEntityNames,
    IReadOnlyList<string> ComponentTypeNames,
    int CameraComponentCount,
    int MouseLookComponentCount,
    int UiComponentCount);
