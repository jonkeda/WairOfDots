namespace WairOfDots.Core;

public sealed record CreatedMap(
    IReadOnlyList<CityNode> Cities,
    IReadOnlyList<int> HomeCityIds,
    IReadOnlyList<TerrainPatch> Terrain,
    GridMap Grid);

public static class GameMapFactory
{
    private const int GridWidth = 31;
    private const int GridHeight = 31;
    private const double MapMin = -9.2;
    private const double MapMax = 9.2;

    public static CreatedMap CreateDefault(int playerCount)
    {
        if (playerCount < 2)
            throw new ArgumentOutOfRangeException(nameof(playerCount), "At least two players are required.");

        var cities = new List<CityNode>
        {
            new(0, "Crown", new MapPoint(0, 0), production: 2, capacity: 7),
            new(1, "North Gate", new MapPoint(0, -4), production: 1),
            new(2, "East Gate", new MapPoint(4, 0), production: 1),
            new(3, "South Gate", new MapPoint(0, 4), production: 1),
            new(4, "West Gate", new MapPoint(-4, 0), production: 1),
            new(5, "Forge", new MapPoint(3, -3), production: 2),
            new(6, "Harbor", new MapPoint(-3, 3), production: 2)
        };

        Connect(cities, 0, 1);
        Connect(cities, 0, 2);
        Connect(cities, 0, 3);
        Connect(cities, 0, 4);
        Connect(cities, 1, 5);
        Connect(cities, 2, 5);
        Connect(cities, 3, 6);
        Connect(cities, 4, 6);
        Connect(cities, 1, 4);
        Connect(cities, 2, 3);

        var homeIds = new List<int>();
        var radius = 7.5;
        for (var playerId = 0; playerId < playerCount; playerId++)
        {
            var angle = Math.PI + (Math.PI * 2 * playerId / playerCount);
            var x = Math.Cos(angle) * radius;
            var y = Math.Sin(angle) * radius;
            var id = cities.Count;
            var city = new CityNode(id, playerId == 0 ? "Human Bastion" : $"AI Citadel {playerId}", new MapPoint(x, y), production: 2, capacity: 6)
            {
                OwnerId = playerId
            };

            cities.Add(city);
            homeIds.Add(id);

            var nearestGate = cities
                .Where(c => c.Id is >= 1 and <= 6)
                .OrderBy(c => c.Position.DistanceTo(city.Position))
                .First();

            Connect(cities, id, nearestGate.Id);
            Connect(cities, id, 0);
        }

        var terrain = CreateTerrain();
        var grid = CreateGrid(terrain, cities);
        foreach (var city in cities)
            city.GridPosition = grid.ToGridPoint(city.Position);

        return new CreatedMap(cities, homeIds, terrain, grid);
    }

    private static IReadOnlyList<TerrainPatch> CreateTerrain()
        =>
        [
            new(0, TerrainKind.Water, new MapPoint(-6.4, -2.5), 2.0, 9.5, -10),
            new(1, TerrainKind.Water, new MapPoint(6.3, 3.6), 4.0, 2.0, 8),
            new(2, TerrainKind.Road, new MapPoint(0.0, 0.0), 1.0, 16.0, 18),
            new(3, TerrainKind.Hill, new MapPoint(3.5, 2.4), 6.8, 4.2, -12),
            new(4, TerrainKind.Hill, new MapPoint(-0.3, 3.5), 4.0, 2.0, 25),
            new(5, TerrainKind.Rock, new MapPoint(-2.8, -3.0), 2.1, 1.1, 8),
            new(6, TerrainKind.Rock, new MapPoint(4.4, -1.7), 1.8, 1.1, -15),
            new(7, TerrainKind.Rock, new MapPoint(1.5, 5.4), 1.4, 0.9, 24),
            new(8, TerrainKind.Forest, new MapPoint(-1.9, -5.4), 2.5, 2.8, -18),
            new(9, TerrainKind.Forest, new MapPoint(1.6, -4.9), 1.5, 2.2, 22)
        ];

    private static GridMap CreateGrid(IReadOnlyList<TerrainPatch> terrain, IReadOnlyList<CityNode> cities)
    {
        var cityCells = cities
            .Select(city => ToGridPoint(city.Position))
            .ToHashSet();

        var cells = new List<GridCell>(GridWidth * GridHeight);
        for (var y = 0; y < GridHeight; y++)
        {
            for (var x = 0; x < GridWidth; x++)
            {
                var point = new GridPoint(x, y);
                var mapPoint = ToMapPoint(point);
                var kind = ResolveTerrain(mapPoint, terrain);
                if (cityCells.Contains(point) && kind == TerrainKind.Water)
                    kind = TerrainKind.Grass;

                cells.Add(new GridCell(point, kind, kind != TerrainKind.Water, MoveCost(kind)));
            }
        }

        return new GridMap(GridWidth, GridHeight, MapMin, MapMin, MapMax, MapMax, cells);
    }

    private static TerrainKind ResolveTerrain(MapPoint point, IEnumerable<TerrainPatch> terrain)
    {
        var kind = TerrainKind.Grass;
        var priority = -1;
        foreach (var patch in terrain.Where(patch => Contains(patch, point)))
        {
            var patchPriority = TerrainPriority(patch.Kind);
            if (patchPriority > priority)
            {
                kind = patch.Kind;
                priority = patchPriority;
            }
        }

        return kind;
    }

    private static bool Contains(TerrainPatch patch, MapPoint point)
    {
        var radians = -patch.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = point.X - patch.Center.X;
        var dy = point.Y - patch.Center.Y;
        var rotatedX = dx * cos - dy * sin;
        var rotatedY = dx * sin + dy * cos;

        return Math.Abs(rotatedX) <= patch.Width / 2.0 &&
            Math.Abs(rotatedY) <= patch.Height / 2.0;
    }

    private static int TerrainPriority(TerrainKind kind)
        => kind switch
        {
            TerrainKind.Water => 50,
            TerrainKind.Road => 40,
            TerrainKind.Rock => 30,
            TerrainKind.Hill => 20,
            TerrainKind.Forest => 10,
            _ => 0
        };

    private static double MoveCost(TerrainKind kind)
        => kind switch
        {
            TerrainKind.Road => 0.65,
            TerrainKind.Forest => 1.35,
            TerrainKind.Hill => 1.65,
            TerrainKind.Rock => 2.1,
            TerrainKind.Water => 99,
            _ => 1.0
        };

    private static MapPoint ToMapPoint(GridPoint point)
    {
        var x = MapMin + (point.X + 0.5) * ((MapMax - MapMin) / GridWidth);
        var y = MapMin + (point.Y + 0.5) * ((MapMax - MapMin) / GridHeight);
        return new MapPoint(x, y);
    }

    private static GridPoint ToGridPoint(MapPoint point)
    {
        var x = (int)Math.Floor((point.X - MapMin) / (MapMax - MapMin) * GridWidth);
        var y = (int)Math.Floor((point.Y - MapMin) / (MapMax - MapMin) * GridHeight);
        return new GridPoint(Math.Clamp(x, 0, GridWidth - 1), Math.Clamp(y, 0, GridHeight - 1));
    }

    private static void Connect(List<CityNode> cities, int leftId, int rightId)
    {
        if (!cities[leftId].Neighbors.Contains(rightId))
            cities[leftId].Neighbors.Add(rightId);

        if (!cities[rightId].Neighbors.Contains(leftId))
            cities[rightId].Neighbors.Add(leftId);
    }
}
