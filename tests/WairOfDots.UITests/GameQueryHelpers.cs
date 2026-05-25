using Brinell.Stride.Communication;

namespace WairOfDots.UITests;

public static class GameQueryHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MatchSnapshot StartMatch(
        IStrideTestContext context,
        int seed = 1337,
        int aiPlayers = 4,
        string mode = "HumanVsAi",
        string humanMode = "General")
        => Send<MatchSnapshot>(context, "StartMatch", seed, aiPlayers, mode, humanMode);

    public static MatchSnapshot StartAiOnly(IStrideTestContext context, int seed = 1337, int aiPlayers = 4)
        => Send<MatchSnapshot>(context, "StartAiOnly", seed, aiPlayers);

    public static MatchSnapshot RestartMatch(IStrideTestContext context)
        => Send<MatchSnapshot>(context, "RestartMatch");

    public static MatchSnapshot GetSnapshot(IStrideTestContext context)
        => Send<MatchSnapshot>(context, "GetSnapshot");

    public static MatchSnapshot StepTicks(IStrideTestContext context, int ticks)
        => Send<MatchSnapshot>(context, "StepTicks", ticks);

    public static MatchSnapshot StepToTick(IStrideTestContext context, int targetTick)
        => Send<MatchSnapshot>(context, "StepToTick", targetTick);

    public static FingerprintDto GetFingerprint(IStrideTestContext context)
        => Send<FingerprintDto>(context, "GetFingerprint");

    public static MatchSnapshot SetDirective(IStrideTestContext context, string directive)
        => Send<MatchSnapshot>(context, "SetDirective", directive);

    public static MatchSnapshot SelectTarget(IStrideTestContext context, int cityId)
        => Send<MatchSnapshot>(context, "SelectTarget", cityId);

    public static MatchSnapshot TogglePause(IStrideTestContext context)
        => Send<MatchSnapshot>(context, "TogglePause");

    public static MatchSnapshot SetSimulationSpeed(IStrideTestContext context, double speed)
        => Send<MatchSnapshot>(context, "SetSimulationSpeed", speed);

    public static MapStateDto GetMapState(IStrideTestContext context)
        => Send<MapStateDto>(context, "GetMapState");

    public static UiDiagnosticsDto GetUiDiagnostics(IStrideTestContext context)
        => Send<UiDiagnosticsDto>(context, "GetUiDiagnostics");

    public static IReadOnlyList<StandingDto> GetStandings(IStrideTestContext context)
        => Send<IReadOnlyList<StandingDto>>(context, "GetStandings");

    public static IReadOnlyList<AiStateDto> GetAiStates(IStrideTestContext context)
        => Send<IReadOnlyList<AiStateDto>>(context, "GetAiStates");

    public static IReadOnlyList<TelemetryDto> GetTelemetry(IStrideTestContext context)
        => Send<IReadOnlyList<TelemetryDto>>(context, "GetTelemetry");

    public static IReadOnlyList<UnitVisualStateDto> GetUnitVisuals(IStrideTestContext context)
        => Send<IReadOnlyList<UnitVisualStateDto>>(context, "GetUnitVisuals");

    public static IReadOnlyList<CellControlSnapshot> GetCellControls(IStrideTestContext context)
        => Send<IReadOnlyList<CellControlSnapshot>>(context, "GetCellControls");

    public static IReadOnlyList<TerritoryBoundarySegment> GetTerritoryBoundaries(IStrideTestContext context)
        => Send<IReadOnlyList<TerritoryBoundarySegment>>(context, "GetTerritoryBoundaries");

    public static IReadOnlyList<EconomySnapshot> GetEconomies(IStrideTestContext context)
        => Send<IReadOnlyList<EconomySnapshot>>(context, "GetEconomies");

    public static IReadOnlyList<RegionSnapshot> GetRegions(IStrideTestContext context)
        => Send<IReadOnlyList<RegionSnapshot>>(context, "GetRegions");

    public static IReadOnlyList<VisibilitySnapshot> GetVisibility(IStrideTestContext context)
        => Send<IReadOnlyList<VisibilitySnapshot>>(context, "GetVisibility");

    public static IReadOnlyList<TrainingRunResult> RunTrainingSmoke(IStrideTestContext context, int seed = 1337, int aiPlayers = 4, int ticks = 120)
        => Send<IReadOnlyList<TrainingRunResult>>(context, "RunTrainingSmoke", seed, aiPlayers, ticks);

    public static TrainingPipelineResult RunTrainingPipeline(
        IStrideTestContext context,
        int seed = 1337,
        int aiPlayers = 4,
        int ticks = 120,
        int generations = 1)
        => Send<TrainingPipelineResult>(context, "RunTrainingPipeline", seed, aiPlayers, ticks, generations);

    public static string ExportBehaviorReview(IStrideTestContext context)
        => Send<string>(context, "ExportBehaviorReview");

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

public sealed record AiStateDto(
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

public sealed record TelemetryDto(
    int Tick,
    int PlayerId,
    string GenomeId,
    string Observation,
    string Action,
    double FitnessDelta,
    string EventType,
    string Details);

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
    int ActiveCombatCount,
    int ControlledCellCount,
    int NeutralControlledCellCount,
    int PlayerControlledCellCount,
    double TotalTaxValue,
    int TerritoryBoundaryCount,
    IReadOnlyList<CityOwnershipDto> CityOwners);

public sealed record CityOwnershipDto(
    int CityId,
    string Name,
    int OwnerId);

public sealed record UiDiagnosticsDto(
    IReadOnlyList<string> VisibleTexts,
    IReadOnlyList<string> VisibleElementNames,
    IReadOnlyList<string> UiEntityNames,
    IReadOnlyList<string> ComponentTypeNames,
    int CameraComponentCount,
    int MouseLookComponentCount,
    int UiComponentCount);
