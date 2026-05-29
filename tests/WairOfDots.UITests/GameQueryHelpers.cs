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
    {
        var snapshot = GetSnapshot(context);
        while (snapshot.Tick < targetTick)
        {
            var ticks = Math.Min(120, targetTick - snapshot.Tick);
            snapshot = StepTicks(context, ticks);
        }

        return snapshot;
    }

    public static StepUntilDto StepUntil(
        IStrideTestContext context,
        string condition,
        string compareValue = "",
        int maxTicks = 120,
        int stepSize = 1)
        => Send<StepUntilDto>(context, "StepUntil", condition, compareValue, maxTicks, stepSize);

    public static FingerprintDto GetFingerprint(IStrideTestContext context)
        => Send<FingerprintDto>(context, "GetFingerprint");

    public static MatchSnapshot SetDirective(IStrideTestContext context, string directive)
        => Send<MatchSnapshot>(context, "SetDirective", directive);

    public static MatchSnapshot SelectTarget(IStrideTestContext context, int cityId)
        => Send<MatchSnapshot>(context, "SelectTarget", cityId);

    public static MatchSnapshot AssignReserveGroup(IStrideTestContext context, int commanderUnitId, int groupSize = 1)
        => Send<MatchSnapshot>(context, "AssignReserveGroup", commanderUnitId, groupSize);

    public static MatchSnapshot RecallCommanderGroup(IStrideTestContext context, int commanderUnitId, int groupSize = 1)
        => Send<MatchSnapshot>(context, "RecallCommanderGroup", commanderUnitId, groupSize);

    public static MapHitTestDto HitTestMap(IStrideTestContext context, double normalizedX, double normalizedY)
        => Send<MapHitTestDto>(context, "HitTestMap", normalizedX, normalizedY);

    public static MapClickDto ClickMap(IStrideTestContext context, double normalizedX, double normalizedY)
        => Send<MapClickDto>(context, "ClickMap", normalizedX, normalizedY);

    public static IReadOnlyList<MapInteractionTargetDto> GetMapInteractionTargets(IStrideTestContext context)
        => Send<IReadOnlyList<MapInteractionTargetDto>>(context, "GetMapInteractionTargets");

    public static MapVisualDiagnosticsDto GetMapVisualDiagnostics(IStrideTestContext context)
        => Send<MapVisualDiagnosticsDto>(context, "GetMapVisualDiagnostics");

    public static MapVisualDiagnosticsDto SetUnitMorale(IStrideTestContext context, int unitId, double morale)
        => Send<MapVisualDiagnosticsDto>(context, "SetUnitMorale", unitId, morale);

    public static MatchSnapshot TogglePause(IStrideTestContext context)
        => Send<MatchSnapshot>(context, "TogglePause");

    public static MatchSnapshot SetSimulationSpeed(IStrideTestContext context, double speed)
        => Send<MatchSnapshot>(context, "SetSimulationSpeed", speed);

    public static MapOverlayDto SetMapOverlay(IStrideTestContext context, string mode)
        => Send<MapOverlayDto>(context, "SetMapOverlay", mode);

    public static MapOverlayDto GetMapOverlay(IStrideTestContext context)
        => Send<MapOverlayDto>(context, "GetMapOverlay");

    public static MapStateDto GetMapState(IStrideTestContext context)
        => Send<MapStateDto>(context, "GetMapState");

    public static UiDiagnosticsDto GetUiDiagnostics(IStrideTestContext context)
        => Send<UiDiagnosticsDto>(context, "GetUiDiagnostics");

    public static IReadOnlyList<StandingDto> GetStandings(IStrideTestContext context)
        => Send<IReadOnlyList<StandingDto>>(context, "GetStandings");

    public static IReadOnlyList<AiStateDto> GetAiStates(IStrideTestContext context)
        => Send<IReadOnlyList<AiStateDto>>(context, "GetAiStates");

    public static IReadOnlyList<PlayerCommandChainDto> GetCommandChains(IStrideTestContext context)
        => Send<IReadOnlyList<PlayerCommandChainDto>>(context, "GetCommandChains");

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

public sealed record StepUntilDto(
    string Condition,
    string CompareValue,
    bool Satisfied,
    int TicksStepped,
    int Attempts,
    string Detail,
    MatchSnapshot Snapshot);

public sealed record MapHitTestDto(
    string Kind,
    int? UnitId,
    int? CityId,
    int? CellX,
    int? CellY,
    double NormalizedX,
    double NormalizedY,
    double DistancePixels,
    string Label);

public sealed record MapClickDto(
    MapHitTestDto Hit,
    MatchSnapshot Snapshot);

public sealed record MapInteractionTargetDto(
    string Kind,
    int Id,
    string Label,
    int? PlayerId,
    double NormalizedX,
    double NormalizedY);

public sealed record MapVisualDiagnosticsDto(
    IReadOnlyList<UnitMapVisualDiagnosticDto> Units,
    IReadOnlyList<CombatMapVisualDiagnosticDto> CombatMarkers,
    IReadOnlyList<DeathMapVisualDiagnosticDto> DeathMarkers,
    IReadOnlyList<RecentMapEventDiagnosticDto> RecentEvents,
    string OverlayMode,
    EconomyOverlayDiagnosticDto EconomyOverlay);

public sealed record EconomyOverlayDiagnosticDto(
    bool IsVisible,
    double MaxTaxValue,
    IReadOnlyList<TaxHeatCellDiagnosticDto> TaxHeatCells,
    IReadOnlyList<PayrollStressDiagnosticDto> PayrollStress,
    IReadOnlyList<CitySpawnCapacityDiagnosticDto> SpawnCapacity,
    IReadOnlyList<TerritorySwingDiagnosticDto> TerritorySwings);

public sealed record TaxHeatCellDiagnosticDto(
    int CellX,
    int CellY,
    int OwnerId,
    double TaxValue,
    double HeatRatio);

public sealed record PayrollStressDiagnosticDto(
    int PlayerId,
    double Treasury,
    double TaxIncome,
    double Upkeep,
    double PayrollDeficit,
    double PayrollDeficitRatio,
    string StressBand);

public sealed record CitySpawnCapacityDiagnosticDto(
    int CityId,
    int OwnerId,
    int Capacity,
    bool IsBlocked);

public sealed record TerritorySwingDiagnosticDto(
    int CellX,
    int CellY,
    int OwnerId,
    double TaxValue,
    int ChangedTick,
    int AgeTicks);

public sealed record UnitMapVisualDiagnosticDto(
    int UnitId,
    int PlayerId,
    string Kind,
    int CellX,
    int CellY,
    double MapX,
    double MapY,
    double MarkerX,
    double MarkerY,
    double Health,
    double MaxHealth,
    double HealthRatio,
    double Morale,
    string MoraleBand,
    string VisualState,
    bool IsSelected,
    bool IsLeader,
    bool IsCommander,
    bool IsGeneral,
    int? CommanderNumber,
    int? AssignedCommanderUnitId,
    int? AssignedCommanderNumber,
    bool IsReserve,
    string CenterLabel,
    bool HasReservePip,
    bool IsProtectionDetail,
    bool IsScout,
    string ActiveCommandType,
    string LatestReportType,
    IReadOnlyList<int> VisibleEnemyUnitIds,
    IReadOnlyList<int> AssignedRegionIds,
    int AssignedUnitCount,
    bool ReserveUnitsRequested,
    IReadOnlyList<string> RoleFlags,
    int? TargetCityId,
    int? TargetRegionId,
    string CurrentTarget,
    bool IsRouted,
    bool IsRoutRisk);

public sealed record RecentMapEventDiagnosticDto(
    int Tick,
    int PlayerId,
    string EventType,
    int? UnitId,
    int? TargetUnitId,
    string Label);

public sealed record CombatMapVisualDiagnosticDto(
    int Tick,
    int AgeTicks,
    int AttackerPlayerId,
    int AttackerUnitId,
    int DefenderPlayerId,
    int DefenderUnitId,
    string DefenderKind,
    int CellX,
    int CellY,
    double MarkerX,
    double MarkerY,
    double Damage,
    bool WasFatal,
    bool IsLeaderHit,
    string MarkerKind);

public sealed record DeathMapVisualDiagnosticDto(
    int Tick,
    int AgeTicks,
    int UnitId,
    int PlayerId,
    string Kind,
    int CellX,
    int CellY,
    double MarkerX,
    double MarkerY,
    string MarkerKind);

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
    string GenomeId,
    int ControlledCellCount,
    double TaxIncome,
    double Upkeep,
    double PayrollDeficit,
    int SpawnCapacity);

public sealed record MapOverlayDto(string Mode);

public sealed record PlayerCommandChainDto(
    int PlayerId,
    IReadOnlyList<CommanderCommandChainDto> Commanders,
    IReadOnlyList<CommandSummaryDto> ReserveCommands);

public sealed record CommanderCommandChainDto(
    int CommanderUnitId,
    int? CommanderNumber,
    CommandSummaryDto? GeneralCommand,
    IReadOnlyList<UnitCommandChainDto> AssignedUnits,
    IReadOnlyList<ReportSummaryDto> Reports);

public sealed record UnitCommandChainDto(
    int UnitId,
    string Kind,
    int? CommanderNumber,
    CommandSummaryDto? Command,
    string LatestReportType);

public sealed record CommandSummaryDto(
    string CommandType,
    int? TargetUnitId,
    int? CommanderNumber,
    int? TargetCellX,
    int? TargetCellY,
    int? TargetCityId,
    int? TargetRegionId,
    double Priority,
    string ReasonCode);

public sealed record ReportSummaryDto(
    string ReportType,
    int? CellX,
    int? CellY,
    double Urgency);

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
