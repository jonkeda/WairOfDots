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

public sealed record GameSettings(int Seed = 1337, int AiPlayers = 4, int MatchLengthTicks = 1800)
{
    public GameSettings Normalized()
        => this with
        {
            AiPlayers = Math.Clamp(AiPlayers, 1, 8),
            MatchLengthTicks = Math.Clamp(MatchLengthTicks, 120, 7200)
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
}

public sealed class TacticalUnit
{
    public int Id { get; init; }
    public int PlayerId { get; init; }
    public UnitKind Kind { get; init; }
    public GridPoint Cell { get; set; }
    public IReadOnlyList<GridPoint> Path { get; set; } = [];
    public int PathIndex { get; set; }
    public double StepProgress { get; set; }
    public MapPoint CurrentPosition { get; set; }
    public double Health { get; set; }
    public double Morale { get; set; } = 1.0;
    public int? TargetCityId { get; set; }

    public bool IsAlive => Health > 0;
    public bool IsLeader => Kind is UnitKind.Commander or UnitKind.General;
    public int RemainingGridSteps => Math.Max(0, Path.Count - PathIndex - 1);
    public bool IsMoving => Path.Count > 1 && PathIndex < Path.Count - 1;

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
    double? LightPreference = null);

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
    double FitnessDelta);

public sealed record CitySnapshot(
    int Id,
    string Name,
    int OwnerId,
    int HumanUnits,
    int EnemyUnits,
    int TotalUnits,
    IReadOnlyList<int> NeighborIds);

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
    string GenomeId);

public sealed record UnitSnapshot(
    int Id,
    int PlayerId,
    string Kind,
    int X,
    int Y,
    double Health,
    bool IsLeader,
    int? TargetCityId,
    int RemainingGridSteps);

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
    IReadOnlyList<UnitSnapshot> Units);

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
