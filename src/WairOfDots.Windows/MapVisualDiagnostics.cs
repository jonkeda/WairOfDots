using WairOfDots.Core;

namespace WairOfDots.Windows;

internal enum MapOverlayMode
{
    Normal,
    Economy,
    Command,
    Visibility,
    AiDebug
}

internal sealed record MapVisualDiagnostics(
    IReadOnlyList<UnitMapVisualDiagnostic> Units,
    IReadOnlyList<CombatMapVisualDiagnostic> CombatMarkers,
    IReadOnlyList<DeathMapVisualDiagnostic> DeathMarkers,
    IReadOnlyList<RecentMapEventDiagnostic> RecentEvents,
    string OverlayMode,
    EconomyOverlayDiagnostic EconomyOverlay);

internal sealed record EconomyOverlayDiagnostic(
    bool IsVisible,
    double MaxTaxValue,
    IReadOnlyList<TaxHeatCellDiagnostic> TaxHeatCells,
    IReadOnlyList<PayrollStressDiagnostic> PayrollStress,
    IReadOnlyList<CitySpawnCapacityDiagnostic> SpawnCapacity,
    IReadOnlyList<TerritorySwingDiagnostic> TerritorySwings);

internal sealed record TaxHeatCellDiagnostic(
    int CellX,
    int CellY,
    int OwnerId,
    double TaxValue,
    double HeatRatio);

internal sealed record PayrollStressDiagnostic(
    int PlayerId,
    double Treasury,
    double TaxIncome,
    double Upkeep,
    double PayrollDeficit,
    double PayrollDeficitRatio,
    string StressBand);

internal sealed record CitySpawnCapacityDiagnostic(
    int CityId,
    int OwnerId,
    int Capacity,
    bool IsBlocked);

internal sealed record TerritorySwingDiagnostic(
    int CellX,
    int CellY,
    int OwnerId,
    double TaxValue,
    int ChangedTick,
    int AgeTicks);

internal sealed record UnitMapVisualDiagnostic(
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

internal sealed record RecentMapEventDiagnostic(
    int Tick,
    int PlayerId,
    string EventType,
    int? UnitId,
    int? TargetUnitId,
    string Label);

internal sealed record CombatMapVisualDiagnostic(
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

internal sealed record DeathMapVisualDiagnostic(
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

internal static class MapVisualLayout
{
    public const double UnitVisualYOffsetPixels = 0.0;

    public static float UnitMarkerSize(UnitKind kind)
        => kind switch
        {
            UnitKind.Commander => 18f,
            UnitKind.General => 19f,
            UnitKind.Tank => 15f,
            _ => 12f
        };

    public static double HealthRatio(UnitKind kind, double health)
        => Math.Clamp(health / Math.Max(1, TacticalUnit.DefaultHealth(kind)), 0, 1);

    public static (double X, double Y) ToUnitMarkerNormalized(MapPoint point, double viewportHeight)
    {
        var normalized = MapCoordinateSystem.ToNormalized(point);
        return (
            normalized.X,
            Math.Clamp(normalized.Y + UnitVisualYOffsetPixels / Math.Max(1, viewportHeight), 0, 1)
        );
    }
}

internal static class MapVisualDiagnosticsService
{
    public static MapVisualDiagnostics Create(
        GameSimulation simulation,
        MatchSnapshot snapshot,
        Func<TacticalUnit, MapPoint> resolveUnitPosition,
        int? selectedUnitId,
        double viewportHeight,
        MapOverlayMode overlayMode)
    {
        var unitsById = simulation.Units.ToDictionary(unit => unit.Id);
        var cityNames = snapshot.Cities.ToDictionary(city => city.Id, city => city.Name);
        var units = snapshot.Units
            .OrderBy(unit => unit.Id)
            .Select(unit => CreateUnitDiagnostic(unit, unitsById, cityNames, resolveUnitPosition, selectedUnitId, viewportHeight))
            .ToList();

        return new MapVisualDiagnostics(
            units,
            CreateCombatMarkers(snapshot, simulation.Grid),
            CreateDeathMarkers(snapshot, simulation.Grid),
            CreateRecentEvents(simulation.Telemetry),
            overlayMode.ToString(),
            CreateEconomyOverlay(simulation, snapshot, overlayMode));
    }

    private static EconomyOverlayDiagnostic CreateEconomyOverlay(
        GameSimulation simulation,
        MatchSnapshot snapshot,
        MapOverlayMode overlayMode)
    {
        var isVisible = overlayMode == MapOverlayMode.Economy;
        if (!isVisible)
        {
            return new EconomyOverlayDiagnostic(
                false,
                0,
                [],
                [],
                [],
                []);
        }

        var maxTaxValue = Math.Max(1, snapshot.CellControls.Max(cell => cell.TaxValue));
        var cityCells = simulation.Cities.Select(city => city.GridPosition).ToHashSet();
        var occupiedCells = snapshot.Units.Select(unit => new GridPoint(unit.X, unit.Y)).ToHashSet();

        return new EconomyOverlayDiagnostic(
            true,
            Math.Round(maxTaxValue, 2),
            CreateTaxHeatCells(snapshot, maxTaxValue),
            CreatePayrollStress(snapshot),
            CreateSpawnCapacity(simulation, snapshot, cityCells, occupiedCells),
            CreateTerritorySwings(simulation, snapshot));
    }

    private static IReadOnlyList<TaxHeatCellDiagnostic> CreateTaxHeatCells(MatchSnapshot snapshot, double maxTaxValue)
        => snapshot.CellControls
            .Where(cell => cell.TaxValue > 0)
            .OrderBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .Select(cell => new TaxHeatCellDiagnostic(
                cell.X,
                cell.Y,
                cell.OwnerId,
                cell.TaxValue,
                Math.Round(Math.Clamp(cell.TaxValue / maxTaxValue, 0, 1), 4)))
            .ToList();

    private static IReadOnlyList<PayrollStressDiagnostic> CreatePayrollStress(MatchSnapshot snapshot)
        => snapshot.Economies
            .OrderBy(economy => economy.PlayerId)
            .Select(economy => new PayrollStressDiagnostic(
                economy.PlayerId,
                economy.Treasury,
                economy.TaxIncome,
                economy.Upkeep,
                economy.PayrollDeficit,
                economy.PayrollDeficitRatio,
                PayrollStressBand(economy.PayrollDeficitRatio)))
            .ToList();

    private static IReadOnlyList<CitySpawnCapacityDiagnostic> CreateSpawnCapacity(
        GameSimulation simulation,
        MatchSnapshot snapshot,
        IReadOnlySet<GridPoint> cityCells,
        IReadOnlySet<GridPoint> occupiedCells)
    {
        var cityOwners = snapshot.Cities.ToDictionary(city => city.Id, city => city.OwnerId);
        return simulation.Cities
            .OrderBy(city => city.Id)
            .Where(city => cityOwners.TryGetValue(city.Id, out var ownerId) && ownerId >= 0)
            .Select(city =>
            {
                var ownerId = cityOwners[city.Id];
                var capacity = CardinalAdjacentCells(city.GridPosition)
                    .Count(point =>
                        simulation.Grid.IsPassable(point) &&
                        !occupiedCells.Contains(point) &&
                        !cityCells.Contains(point));
                return new CitySpawnCapacityDiagnostic(city.Id, ownerId, capacity, capacity == 0);
            })
            .ToList();
    }

    private static IReadOnlyList<TerritorySwingDiagnostic> CreateTerritorySwings(GameSimulation simulation, MatchSnapshot snapshot)
        => simulation.CellControls
            .Select(cell => new
            {
                Cell = cell,
                AgeTicks = snapshot.Tick - cell.LastChangedTick
            })
            .Where(item => item.Cell.LastChangedTick > 0 && item.AgeTicks >= 0 && item.AgeTicks <= 8)
            .OrderBy(item => item.Cell.LastChangedTick)
            .ThenBy(item => item.Cell.OwnerId)
            .ThenBy(item => item.Cell.Point.Y)
            .ThenBy(item => item.Cell.Point.X)
            .Select(item => new TerritorySwingDiagnostic(
                item.Cell.Point.X,
                item.Cell.Point.Y,
                item.Cell.OwnerId,
                Math.Round(item.Cell.TaxValue, 2),
                item.Cell.LastChangedTick,
                item.AgeTicks))
            .ToList();

    private static UnitMapVisualDiagnostic CreateUnitDiagnostic(
        UnitSnapshot snapshot,
        IReadOnlyDictionary<int, TacticalUnit> unitsById,
        IReadOnlyDictionary<int, string> cityNames,
        Func<TacticalUnit, MapPoint> resolveUnitPosition,
        int? selectedUnitId,
        double viewportHeight)
    {
        var kind = ParseKind(snapshot.Kind);
        var maxHealth = TacticalUnit.DefaultHealth(kind);
        var mapPosition = unitsById.TryGetValue(snapshot.Id, out var unit)
            ? resolveUnitPosition(unit)
            : new MapPoint(0, 0);
        var mapNormalized = MapCoordinateSystem.ToNormalized(mapPosition);
        var markerNormalized = MapVisualLayout.ToUnitMarkerNormalized(mapPosition, viewportHeight);
        var moraleBand = ParseMoraleBand(snapshot.MoraleBand);
        var roleFlags = CreateRoleFlags(snapshot, kind);

        return new UnitMapVisualDiagnostic(
            snapshot.Id,
            snapshot.PlayerId,
            snapshot.Kind,
            snapshot.X,
            snapshot.Y,
            Math.Round(mapNormalized.X, 4),
            Math.Round(mapNormalized.Y, 4),
            Math.Round(markerNormalized.X, 4),
            Math.Round(markerNormalized.Y, 4),
            snapshot.Health,
            maxHealth,
            Math.Round(MapVisualLayout.HealthRatio(kind, snapshot.Health), 4),
            snapshot.Morale,
            snapshot.MoraleBand,
            VisualState(moraleBand),
            selectedUnitId == snapshot.Id,
            snapshot.IsLeader,
            kind == UnitKind.Commander,
            kind == UnitKind.General,
            snapshot.CommanderNumber,
            snapshot.AssignedCommanderUnitId,
            snapshot.AssignedCommanderNumber,
            snapshot.IsReserve,
            CenterLabel(snapshot, kind),
            snapshot.IsReserve,
            snapshot.IsProtectionDetail,
            snapshot.IsScout,
            snapshot.ActiveCommandType,
            snapshot.LatestReportType,
            snapshot.VisibleEnemyUnitIds,
            snapshot.AssignedRegionIds,
            snapshot.AssignedUnitCount,
            snapshot.ReserveUnitsRequested,
            roleFlags,
            snapshot.TargetCityId,
            snapshot.TargetRegionId,
            FormatTarget(snapshot.TargetCityId, snapshot.TargetRegionId, cityNames),
            moraleBand == MoraleBand.Routed,
            moraleBand == MoraleBand.RoutRisk);
    }

    private static IReadOnlyList<RecentMapEventDiagnostic> CreateRecentEvents(IEnumerable<TelemetryEvent> telemetry)
        => telemetry
            .TakeLast(12)
            .Select(item => new RecentMapEventDiagnostic(
                item.Tick,
                item.PlayerId,
                item.EventType,
                ParseDetailInt(item.Details, "unit") ?? ParseDetailInt(item.Details, "commander"),
                ParseDetailInt(item.Details, "target") ?? ParseDetailInt(item.Details, "enemy"),
                string.IsNullOrWhiteSpace(item.Details) ? item.EventType : $"{item.EventType}:{item.Details}"))
            .OrderBy(item => item.Tick)
            .ThenBy(item => item.PlayerId)
            .ThenBy(item => item.EventType, StringComparer.Ordinal)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<CombatMapVisualDiagnostic> CreateCombatMarkers(MatchSnapshot snapshot, GridMap grid)
        => snapshot.RecentCombatHits
            .OrderBy(hit => hit.Tick)
            .ThenBy(hit => hit.DefenderPlayerId)
            .ThenBy(hit => hit.DefenderUnitId)
            .Select(hit =>
            {
                var point = MapCoordinateSystem.ToNormalized(grid.ToMapPoint(new GridPoint(hit.CellX, hit.CellY)));
                var isLeaderHit = hit.DefenderKind is nameof(UnitKind.Commander) or nameof(UnitKind.General);
                return new CombatMapVisualDiagnostic(
                    hit.Tick,
                    Math.Max(0, snapshot.Tick - hit.Tick),
                    hit.AttackerPlayerId,
                    hit.AttackerUnitId,
                    hit.DefenderPlayerId,
                    hit.DefenderUnitId,
                    hit.DefenderKind,
                    hit.CellX,
                    hit.CellY,
                    Math.Round(point.X, 4),
                    Math.Round(point.Y, 4),
                    hit.Damage,
                    hit.WasFatal,
                    isLeaderHit,
                    isLeaderHit ? "LeaderThreatPulse" : hit.WasFatal ? "FatalHitFlash" : "DamageFlash");
            })
            .ToList();

    private static IReadOnlyList<DeathMapVisualDiagnostic> CreateDeathMarkers(MatchSnapshot snapshot, GridMap grid)
        => snapshot.RecentDeaths
            .OrderBy(death => death.Tick)
            .ThenBy(death => death.PlayerId)
            .ThenBy(death => death.UnitId)
            .Select(death =>
            {
                var point = MapCoordinateSystem.ToNormalized(grid.ToMapPoint(new GridPoint(death.CellX, death.CellY)));
                return new DeathMapVisualDiagnostic(
                    death.Tick,
                    Math.Max(0, snapshot.Tick - death.Tick),
                    death.UnitId,
                    death.PlayerId,
                    death.Kind,
                    death.CellX,
                    death.CellY,
                    Math.Round(point.X, 4),
                    Math.Round(point.Y, 4),
                    "Hidden");
            })
            .ToList();

    private static IReadOnlyList<string> CreateRoleFlags(UnitSnapshot snapshot, UnitKind kind)
    {
        var flags = new List<string>();
        if (kind == UnitKind.General)
            flags.Add("General");
        else if (kind == UnitKind.Commander)
        {
            flags.Add("Commander");
            if (snapshot.CommanderNumber.HasValue)
                flags.Add($"Commander {snapshot.CommanderNumber.Value}");
        }
        else
        {
            flags.Add("Line");
            if (snapshot.IsReserve)
                flags.Add("Reserve");
            else if (snapshot.AssignedCommanderNumber.HasValue)
                flags.Add($"Commander {snapshot.AssignedCommanderNumber.Value}");
        }

        if (snapshot.IsProtectionDetail)
            flags.Add("Protection");
        if (snapshot.IsScout)
            flags.Add("Scout");

        return flags;
    }

    private static string CenterLabel(UnitSnapshot snapshot, UnitKind kind)
    {
        if (kind == UnitKind.Commander && snapshot.CommanderNumber.HasValue)
            return snapshot.CommanderNumber.Value.ToString();
        if (kind is UnitKind.Infantry or UnitKind.Tank && snapshot.AssignedCommanderNumber.HasValue)
            return snapshot.AssignedCommanderNumber.Value.ToString();

        return "";
    }

    private static string FormatTarget(
        int? targetCityId,
        int? targetRegionId,
        IReadOnlyDictionary<int, string> cityNames)
    {
        var parts = new List<string>();
        if (targetCityId.HasValue)
        {
            var cityName = cityNames.TryGetValue(targetCityId.Value, out var name) ? name : "Unknown";
            parts.Add($"City {targetCityId.Value} {cityName}");
        }

        if (targetRegionId.HasValue)
            parts.Add($"Region {targetRegionId.Value}");

        return parts.Count == 0 ? "None" : string.Join(" / ", parts);
    }

    private static string VisualState(MoraleBand band)
        => band switch
        {
            MoraleBand.Routed => "RoutedBrokenRing",
            MoraleBand.RoutRisk => "RoutRiskSegmentedArc",
            MoraleBand.Cautious => "CautiousArc",
            MoraleBand.Aggressive => "AggressiveArc",
            _ => "NormalArc"
        };

    private static string PayrollStressBand(double deficitRatio)
        => deficitRatio switch
        {
            >= 0.65 => "Collapse",
            > 0 => "Deficit",
            _ => "Stable"
        };

    private static UnitKind ParseKind(string kind)
        => Enum.TryParse<UnitKind>(kind, ignoreCase: true, out var parsed) ? parsed : UnitKind.Infantry;

    private static MoraleBand ParseMoraleBand(string band)
        => Enum.TryParse<MoraleBand>(band, ignoreCase: true, out var parsed) ? parsed : MoraleBand.Normal;

    private static int? ParseDetailInt(string details, string key)
    {
        if (string.IsNullOrWhiteSpace(details))
            return null;

        foreach (var part in details.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (split.Length == 2 &&
                string.Equals(split[0], key, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(split[1], out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<GridPoint> CardinalAdjacentCells(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y);
        yield return new GridPoint(point.X, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y);
    }
}
