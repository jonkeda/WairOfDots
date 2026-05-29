using System.Security.Cryptography;
using System.Text;

namespace WairOfDots.Core;

public enum MatchPhase
{
    Menu,
    Running,
    Paused,
    Ended
}

public enum PlayerKind
{
    Human,
    Ai
}

public enum GameMode
{
    HumanVsAi,
    AiOnly
}

public enum HumanControlMode
{
    General,
    Commander,
    GeneralAndCommander,
    DotChaos
}

public enum PlayerDirective
{
    Attack,
    Hold,
    Defend
}

public enum UnitKind
{
    Infantry,
    Tank,
    Commander,
    General
}

public enum TerrainKind
{
    Grass,
    Water,
    Road,
    Hill,
    Rock,
    Forest
}

public enum MoraleBand
{
    Routed,
    RoutRisk,
    Cautious,
    Normal,
    Aggressive
}

public enum StrategyMode
{
    Advance,
    Defend,
    Rebuild,
    ProtectGeneral
}

public sealed record GameSettings(
    int Seed = 1337,
    int AiPlayers = 4,
    int MatchLengthTicks = 1800,
    GameMode Mode = GameMode.HumanVsAi,
    HumanControlMode HumanControlMode = HumanControlMode.General)
{
    public GameSettings Normalized()
        => this with
        {
            AiPlayers = Mode == GameMode.AiOnly
                ? Math.Clamp(AiPlayers, 2, 8)
                : Math.Clamp(AiPlayers, 1, 8),
            MatchLengthTicks = Math.Clamp(MatchLengthTicks, 120, 7200),
            HumanControlMode = Mode == GameMode.AiOnly ? HumanControlMode.General : HumanControlMode
        };
}

public readonly record struct MapPoint(double X, double Y)
{
    public double DistanceTo(MapPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public readonly record struct GridPoint(int X, int Y)
{
    public int ManhattanDistanceTo(GridPoint other)
        => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public int ChebyshevDistanceTo(GridPoint other)
        => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public double OctileDistanceTo(GridPoint other)
    {
        var dx = Math.Abs(X - other.X);
        var dy = Math.Abs(Y - other.Y);
        var diagonal = Math.Min(dx, dy);
        var cardinal = Math.Max(dx, dy) - diagonal;
        return cardinal + diagonal * Math.Sqrt(2);
    }
}

public sealed record GridCell(
    GridPoint Point,
    TerrainKind Terrain,
    bool IsPassable,
    double MoveCost);

public sealed class GridMap
{
    private readonly GridCell[] _cells;

    public GridMap(int width, int height, double minX, double minY, double maxX, double maxY, IEnumerable<GridCell> cells)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
        _cells = cells.OrderBy(cell => cell.Point.Y).ThenBy(cell => cell.Point.X).ToArray();

        if (_cells.Length != Width * Height)
            throw new ArgumentException("Grid cell count must match width and height.", nameof(cells));
    }

    public int Width { get; }
    public int Height { get; }
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public IReadOnlyList<GridCell> Cells => _cells;

    public bool Contains(GridPoint point)
        => point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;

    public GridCell GetCell(GridPoint point)
    {
        if (!Contains(point))
            throw new ArgumentOutOfRangeException(nameof(point), point, "Grid point is outside the map.");

        return _cells[point.Y * Width + point.X];
    }

    public bool IsPassable(GridPoint point)
        => Contains(point) && GetCell(point).IsPassable;

    public double MoveCost(GridPoint point)
        => GetCell(point).MoveCost;

    public MapPoint ToMapPoint(GridPoint point)
    {
        var clampedX = Math.Clamp(point.X, 0, Width - 1);
        var clampedY = Math.Clamp(point.Y, 0, Height - 1);
        var x = MinX + (clampedX + 0.5) * ((MaxX - MinX) / Width);
        var y = MinY + (clampedY + 0.5) * ((MaxY - MinY) / Height);
        return new MapPoint(x, y);
    }

    public GridPoint ToGridPoint(MapPoint point)
    {
        var x = (int)Math.Floor((point.X - MinX) / (MaxX - MinX) * Width);
        var y = (int)Math.Floor((point.Y - MinY) / (MaxY - MinY) * Height);
        return new GridPoint(Math.Clamp(x, 0, Width - 1), Math.Clamp(y, 0, Height - 1));
    }
}

public sealed class CityNode
{
    public CityNode(int id, string name, MapPoint position, int production = 1, int capacity = 5)
    {
        Id = id;
        Name = name;
        Position = position;
        Production = production;
        Capacity = capacity;
    }

    public int Id { get; }
    public string Name { get; }
    public MapPoint Position { get; }
    public int Production { get; }
    public int Capacity { get; }
    public GridPoint GridPosition { get; set; }
    public int OwnerId { get; set; } = GameConstants.NeutralPlayerId;
    public List<int> Neighbors { get; } = [];
}

public sealed class PlayerState
{
    public PlayerState(int id, string name, PlayerKind kind, int homeCityId, string genomeId)
    {
        Id = id;
        Name = name;
        Kind = kind;
        HomeCityId = homeCityId;
        CommanderCityId = homeCityId;
        GeneralCityId = homeCityId;
        TargetCityId = 0;
        GenomeId = genomeId;
    }

    public int Id { get; }
    public string Name { get; }
    public PlayerKind Kind { get; }
    public int HomeCityId { get; }
    public int CommanderCityId { get; set; }
    public int GeneralCityId { get; set; }
    public int? CommanderUnitId { get; set; }
    public int? GeneralUnitId { get; set; }
    public string GenomeId { get; }
    public bool IsEliminated { get; set; }
    public double Resources { get; set; } = 4;
    public double CommanderHealth { get; set; } = TacticalUnit.DefaultHealth(UnitKind.Commander);
    public double GeneralHealth { get; set; } = TacticalUnit.DefaultHealth(UnitKind.General);
    public PlayerDirective Directive { get; set; } = PlayerDirective.Attack;
    public int TargetCityId { get; set; }
    public double LightPreference { get; set; } = 0.65;
    public double Score { get; set; }
    public double LastTaxIncome { get; set; }
    public double LastUpkeep { get; set; }
    public double LastPayrollDeficit { get; set; }
    public double LastPayrollDeficitRatio { get; set; }
    public int ControlledCellCount { get; set; }
    public double ControlledTaxValue { get; set; }
    public int SpawnCapacity { get; set; }
    public double ReserveBudget { get; set; }
    public double ReplenishmentRequest { get; set; }
    public double EconomyRisk { get; set; }
    public double GeneralSecurity { get; set; } = 1;
    public int TargetRegionId { get; set; }
    public int? GeneralRelocationCityId { get; set; }
    public bool ScoutDirective { get; set; } = true;
    public StrategyMode StrategyMode { get; set; } = StrategyMode.Advance;
    public int CommanderLeaderlessUntilTick { get; set; }
    public int NextCommanderNumber { get; set; } = 1;
    public HumanControlMode HumanControlMode { get; set; } = HumanControlMode.General;
    public int ConsecutiveHoldPlans { get; set; }
}

public sealed class TacticalUnit
{
    public int Id { get; init; }
    public int PlayerId { get; init; }
    public UnitKind Kind { get; init; }
    public GridPoint Cell { get; set; }
    public IReadOnlyList<GridPoint> Path { get; set; } = [];
    public IReadOnlyList<int> SmoothedPathIndices { get; set; } = [];
    public int PathIndex { get; set; }
    public double StepProgress { get; set; }
    public MapPoint CurrentPosition { get; set; }
    public GridPoint VisualFromCell { get; set; }
    public GridPoint VisualToCell { get; set; }
    public MapPoint VisualFromPosition { get; set; }
    public MapPoint VisualToPosition { get; set; }
    public int VisualMoveTick { get; set; } = -1;
    public double Health { get; set; }
    public double Morale { get; set; } = 1.0;
    public int? CommanderNumber { get; set; }
    public int? AssignedCommanderUnitId { get; set; }
    public int? AssignedCommanderNumber { get; set; }
    public int? TargetCityId { get; set; }
    public int? TargetRegionId { get; set; }
    public bool IsProtectionDetail { get; set; }
    public bool IsScout { get; set; }

    public bool IsAlive => Health > 0;
    public bool IsLeader => Kind is UnitKind.Commander or UnitKind.General;
    public int RemainingGridSteps => Math.Max(0, Path.Count - PathIndex - 1);
    public bool IsMoving => Path.Count > 1 && PathIndex < Path.Count - 1;
    public bool IsUsingSmoothedSegment => SmoothedPathIndices.Count >= 2;
    public bool HasVisualMovement => VisualMoveTick >= 0 && VisualFromPosition.DistanceTo(VisualToPosition) > 0.0001;

    public double Speed
        => Kind switch
        {
            UnitKind.Infantry => 1.15,
            UnitKind.Tank => 0.82,
            UnitKind.Commander => 1.0,
            UnitKind.General => 0.72,
            _ => 1.0
        };

    public double AttackPower
        => Kind switch
        {
            UnitKind.Infantry => 2.0,
            UnitKind.Tank => 3.7,
            UnitKind.Commander => 1.8,
            UnitKind.General => 1.4,
            _ => 1.0
        };

    public static double DefaultHealth(UnitKind kind)
        => kind switch
        {
            UnitKind.Infantry => 10,
            UnitKind.Tank => 18,
            UnitKind.Commander => 24,
            UnitKind.General => 32,
            _ => 10
        };
}

public sealed record HumanCommand(
    PlayerDirective? Directive = null,
    int? TargetCityId = null,
    double? LightPreference = null,
    HumanControlMode? ControlMode = null,
    int? TargetRegionId = null,
    int? GeneralRelocationCityId = null,
    bool? ScoutDirective = null,
    int? AssignReserveUnitId = null,
    int? AssignReserveCommanderUnitId = null,
    int? AssignReserveGroupCommanderUnitId = null,
    int? AssignReserveGroupSize = null,
    int? RecallCommanderUnitId = null,
    int? RecallCommanderGroupCommanderUnitId = null,
    int? RecallCommanderGroupSize = null);

public sealed record TerrainPatch(
    int Id,
    TerrainKind Kind,
    MapPoint Center,
    double Width,
    double Height,
    double Rotation = 0);

public sealed record TelemetryEvent(
    int Tick,
    int PlayerId,
    string GenomeId,
    string Observation,
    string Action,
    double FitnessDelta,
    string EventType = "AiPlan",
    string Details = "");

public sealed record CellControlSnapshot(
    int X,
    int Y,
    int OwnerId,
    double TaxValue,
    string Terrain,
    bool IsCityCell);

public sealed record TerritoryBoundarySegment(
    int FromX,
    int FromY,
    int ToX,
    int ToY,
    int OwnerId,
    int NeighborOwnerId);

public sealed record EconomySnapshot(
    int PlayerId,
    int ControlledCellCount,
    double ControlledTaxValue,
    double TaxIncome,
    double Treasury,
    double Upkeep,
    double PayrollDeficit,
    double PayrollDeficitRatio,
    int SpawnCapacity,
    double ReserveBudget,
    double ReplenishmentRequest,
    double EconomyRisk);

public sealed record RegionSnapshot(
    int RegionId,
    int PlayerId,
    int CommanderUnitId,
    int AnchorCityId,
    int ControlledCellCount,
    double ControlledTaxValue,
    int FriendlyUnitCount,
    int EnemyUnitCount,
    int OwnedCityCount,
    int SpawnCapacity,
    double PayrollPressure,
    int LeaderlessTicksRemaining,
    double Priority,
    int BoundsX,
    int BoundsY,
    int Width,
    int Height,
    int? AssignedCommanderUnitId,
    int? AssignedCommanderNumber,
    int VisibleEnemyUnitCount,
    bool Contested);

public sealed record VisibilitySnapshot(
    int PlayerId,
    int VisibleCellCount,
    int VisibleEnemyUnitCount,
    bool EnemyGeneralVisible,
    IReadOnlyList<int> VisibleEnemyUnitIds);

public sealed record RecentCombatHitSnapshot(
    int Tick,
    int AttackerPlayerId,
    int AttackerUnitId,
    int DefenderPlayerId,
    int DefenderUnitId,
    string DefenderKind,
    int CellX,
    int CellY,
    double Damage,
    bool WasFatal);

public sealed record RecentUnitDeathSnapshot(
    int Tick,
    int UnitId,
    int PlayerId,
    string Kind,
    int CellX,
    int CellY);

public sealed record CommandRecordSnapshot(
    int Tick,
    int PlayerId,
    string Layer,
    int SourceUnitId,
    int? TargetUnitId,
    int? CommanderNumber,
    string CommandType,
    int? TargetCellX,
    int? TargetCellY,
    int? TargetCityId,
    int? TargetRegionId,
    double Priority,
    string ReasonCode,
    bool IsActive);

public sealed record ReportRecordSnapshot(
    int Tick,
    int PlayerId,
    string Layer,
    int SourceUnitId,
    int? TargetUnitId,
    int? CommanderNumber,
    string ReportType,
    int? CellX,
    int? CellY,
    double Urgency,
    string Details);

public sealed record CitySnapshot(
    int Id,
    string Name,
    int OwnerId,
    int HumanUnits,
    int EnemyUnits,
    int TotalUnits,
    IReadOnlyList<int> NeighborIds,
    string ActiveProductionCommand,
    string ActiveProductionReason);

public sealed record PlayerSnapshot(
    int Id,
    string Name,
    string Kind,
    bool IsEliminated,
    string Directive,
    int TargetCityId,
    double Resources,
    double CommanderHealth,
    double GeneralHealth,
    double Score,
    string GenomeId,
    string AiArchetype,
    int ConsecutiveHoldPlans,
    double TaxIncome,
    double Upkeep,
    double PayrollDeficit,
    int ControlledCellCount,
    double ControlledTaxValue,
    int SpawnCapacity,
    string StrategyMode,
    int TargetRegionId,
    string HumanControlMode);

public sealed record UnitSnapshot(
    int Id,
    int PlayerId,
    string Kind,
    int X,
    int Y,
    double Health,
    double Morale,
    string MoraleBand,
    bool IsLeader,
    int? CommanderNumber,
    int? AssignedCommanderUnitId,
    int? AssignedCommanderNumber,
    bool IsReserve,
    string ActiveCommandType,
    string LatestReportType,
    IReadOnlyList<int> VisibleEnemyUnitIds,
    IReadOnlyList<int> AssignedRegionIds,
    int AssignedUnitCount,
    bool ReserveUnitsRequested,
    int? TargetCityId,
    int? TargetRegionId,
    int RemainingGridSteps,
    bool IsProtectionDetail,
    bool IsScout);

public sealed record StandingSnapshot(
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

public sealed record MatchSnapshot(
    int Tick,
    string Phase,
    int? WinnerId,
    string WinnerName,
    int HumanTargetCityId,
    string HumanDirective,
    double HumanLightPreference,
    int MovingUnitCount,
    string LastEvent,
    string Fingerprint,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<CitySnapshot> Cities,
    IReadOnlyList<UnitSnapshot> Units,
    string GameMode,
    bool HasHumanPlayer,
    IReadOnlyList<StandingSnapshot> Standings,
    IReadOnlyList<CellControlSnapshot> CellControls,
    IReadOnlyList<TerritoryBoundarySegment> TerritoryBoundaries,
    IReadOnlyList<EconomySnapshot> Economies,
    IReadOnlyList<RegionSnapshot> Regions,
    IReadOnlyList<VisibilitySnapshot> Visibility,
    IReadOnlyList<RecentCombatHitSnapshot> RecentCombatHits,
    IReadOnlyList<RecentUnitDeathSnapshot> RecentDeaths,
    IReadOnlyList<CommandRecordSnapshot> Commands,
    IReadOnlyList<ReportRecordSnapshot> Reports);

public static class GameConstants
{
    public const int HumanPlayerId = 0;
    public const int NeutralPlayerId = -1;
}

public static class Fingerprint
{
    public static string Create(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
