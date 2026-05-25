using Stride.Core.Mathematics;
using Stride.Graphics;
using WairOfDots.Core;

namespace WairOfDots.Windows;

internal sealed class MapSpriteRenderer : IDisposable
{
    private const float MapMin = -9.2f;
    private const float MapMax = 9.2f;
    private const float MapRange = MapMax - MapMin;
    private const float BoundarySideThickness = 1.75f;

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly Texture _pixel;
    private readonly Texture _circle;
    private readonly Texture _ring;
    private readonly Texture _square;

    public MapSpriteRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice) { VirtualResolution = new Vector3(1280, 720, 1000) };
        _font = font;
        _pixel = Texture.New2D(graphicsDevice, 1, 1, PixelFormat.R8G8B8A8_UNorm, [Color.White]);
        _circle = CreateCircleTexture(graphicsDevice, filled: true);
        _ring = CreateCircleTexture(graphicsDevice, filled: false);
        _square = CreateSquareTexture(graphicsDevice);
    }

    public void Draw(
        GraphicsContext graphicsContext,
        GameSimulation simulation,
        MatchSnapshot snapshot,
        IReadOnlyDictionary<int, MapPoint> unitPositions,
        IReadOnlySet<int> engagedUnitIds,
        int selectedCityId)
    {
        var viewport = new RectangleF(14, 14, 890, 660);

        _spriteBatch.Begin(graphicsContext, SpriteSortMode.Deferred);
        DrawRectangle(viewport, TerrainColor(TerrainKind.Grass));
        DrawTerrain(simulation, viewport);
        DrawBoundaries(snapshot, simulation.Grid, viewport);
        DrawCities(simulation, snapshot, viewport, selectedCityId);
        DrawUnits(simulation, unitPositions, engagedUnitIds, viewport);
        DrawLegend(viewport);
        _spriteBatch.End();
    }

    private void DrawTerrain(GameSimulation simulation, RectangleF viewport)
    {
        foreach (var patch in simulation.Terrain)
        {
            var center = ToScreen(patch.Center, viewport);
            var width = (float)(patch.Width / MapRange * viewport.Width);
            var height = (float)(patch.Height / MapRange * viewport.Height);
            DrawRectangle(
                new RectangleF(center.X - width / 2f, center.Y - height / 2f, width, height),
                TerrainColor(patch.Kind));
        }
    }

    private void DrawBoundaries(MatchSnapshot snapshot, GridMap grid, RectangleF viewport)
    {
        foreach (var segment in BuildBoundaryDrawSegments(snapshot.CellControls))
        {
            var start = segment.IsVertical
                ? GridEdgeToScreen(segment.Line, segment.Start, grid, viewport)
                : GridEdgeToScreen(segment.Start, segment.Line, grid, viewport);
            var end = segment.IsVertical
                ? GridEdgeToScreen(segment.Line, segment.End, grid, viewport)
                : GridEdgeToScreen(segment.End, segment.Line, grid, viewport);
            var color = BoundaryColor(segment.OwnerId);

            if (segment.IsVertical)
            {
                var y = Math.Min(start.Y, end.Y);
                var height = Math.Max(BoundarySideThickness, Math.Abs(end.Y - start.Y));
                var x = segment.Side == BoundarySide.Negative
                    ? start.X - BoundarySideThickness
                    : start.X;
                DrawRectangle(new RectangleF(x, y, BoundarySideThickness, height), color);
            }
            else
            {
                var x = Math.Min(start.X, end.X);
                var width = Math.Max(BoundarySideThickness, Math.Abs(end.X - start.X));
                var y = segment.Side == BoundarySide.Negative
                    ? start.Y - BoundarySideThickness
                    : start.Y;
                DrawRectangle(new RectangleF(x, y, width, BoundarySideThickness), color);
            }
        }
    }

    private void DrawCities(GameSimulation simulation, MatchSnapshot snapshot, RectangleF viewport, int selectedCityId)
    {
        foreach (var city in simulation.Cities)
        {
            var citySnapshot = snapshot.Cities.First(item => item.Id == city.Id);
            var center = ToScreen(city.Position, viewport);
            var size = city.Id == selectedCityId ? 20f : citySnapshot.TotalUnits > 0 ? 18f : 16f;
            DrawTexture(_square, Centered(center, size), PlayerColor(citySnapshot.OwnerId));
        }
    }

    private void DrawUnits(
        GameSimulation simulation,
        IReadOnlyDictionary<int, MapPoint> unitPositions,
        IReadOnlySet<int> engagedUnitIds,
        RectangleF viewport)
    {
        foreach (var unit in simulation.Units.Where(unit => unit.IsAlive).OrderBy(unit => unit.Id))
        {
            if (!unitPositions.TryGetValue(unit.Id, out var position))
                position = unit.CurrentPosition;

            var center = ToScreen(position, viewport);
            center.Y -= 12;
            var color = PlayerColor(unit.PlayerId);
            var size = unit.Kind switch
            {
                UnitKind.Commander => 18f,
                UnitKind.General => 19f,
                UnitKind.Tank => 15f,
                _ => 12f
            };

            if (engagedUnitIds.Contains(unit.Id))
                DrawTexture(_ring, Centered(center, size + 8), Color.LightYellow);

            if (unit.Kind == UnitKind.Commander)
            {
                DrawTexture(_ring, Centered(center, size), color);
            }
            else if (unit.Kind == UnitKind.General)
            {
                DrawTexture(_circle, Centered(center, size), color);
                DrawTexture(_ring, Centered(center, size + 4), color);
            }
            else
            {
                DrawTexture(_circle, Centered(center, size), color);
            }
        }
    }

    private void DrawLegend(RectangleF viewport)
        => _spriteBatch.DrawString(
            _font,
            "Terrain: green land | blue water | gray hills | black roads",
            14,
            new Vector2(viewport.X + 16, viewport.Y + 16),
            ToColor4(Color.White),
            TextAlignment.Left);

    private void DrawRectangle(RectangleF rectangle, Color color)
        => DrawTexture(_pixel, rectangle, color);

    private void DrawTexture(Texture texture, RectangleF rectangle, Color color)
        => _spriteBatch.Draw(texture, rectangle, ToColor4(color), new Color4(0, 0, 0, 0));

    private static RectangleF Centered(Vector2 center, float size)
        => new(center.X - size / 2f, center.Y - size / 2f, size, size);

    private static Vector2 ToScreen(MapPoint point, RectangleF viewport)
    {
        var x = Math.Clamp((float)((point.X - MapMin) / MapRange), 0.02f, 0.98f);
        var y = Math.Clamp((float)((point.Y - MapMin) / MapRange), 0.02f, 0.98f);
        return new Vector2(viewport.X + x * viewport.Width, viewport.Y + y * viewport.Height);
    }

    private static Vector2 GridEdgeToScreen(int x, int y, GridMap grid, RectangleF viewport)
        => new(
            viewport.X + x / (float)grid.Width * viewport.Width,
            viewport.Y + y / (float)grid.Height * viewport.Height);

    private static Texture CreateCircleTexture(GraphicsDevice graphicsDevice, bool filled)
    {
        const int size = 64;
        const float center = (size - 1) / 2f;
        const float radius = 28f;
        const float ringInnerRadius = 21f;
        var pixels = new Color[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var alpha = filled
                    ? SmoothAlpha(radius - distance)
                    : Math.Min(SmoothAlpha(distance - ringInnerRadius), SmoothAlpha(radius - distance));
                var alphaByte = (byte)Math.Clamp(alpha * 255f, 0, 255);
                pixels[y * size + x] = new Color(alphaByte, alphaByte, alphaByte, alphaByte);
            }
        }

        return Texture.New2D(graphicsDevice, size, size, PixelFormat.R8G8B8A8_UNorm, pixels);
    }

    private static Texture CreateSquareTexture(GraphicsDevice graphicsDevice)
    {
        const int size = 16;
        var pixels = Enumerable.Repeat(Color.White, size * size).ToArray();
        return Texture.New2D(graphicsDevice, size, size, PixelFormat.R8G8B8A8_UNorm, pixels);
    }

    private static float SmoothAlpha(float signedDistance)
        => Math.Clamp(signedDistance + 1f, 0f, 1f);

    private static IReadOnlyList<BoundaryDrawSegment> BuildBoundaryDrawSegments(IEnumerable<CellControlSnapshot> cellControls)
    {
        var owners = cellControls.ToDictionary(cell => new GridPoint(cell.X, cell.Y), cell => cell.OwnerId);
        var edges = new List<BoundaryEdge>();
        foreach (var cell in cellControls)
        {
            if (cell.OwnerId == GameConstants.NeutralPlayerId)
                continue;

            AddCellBoundaryEdgeIfNeeded(owners, edges, cell, cell.X - 1, cell.Y, isVertical: true, line: cell.X, offset: cell.Y, side: BoundarySide.Positive);
            AddCellBoundaryEdgeIfNeeded(owners, edges, cell, cell.X + 1, cell.Y, isVertical: true, line: cell.X + 1, offset: cell.Y, side: BoundarySide.Negative);
            AddCellBoundaryEdgeIfNeeded(owners, edges, cell, cell.X, cell.Y - 1, isVertical: false, line: cell.Y, offset: cell.X, side: BoundarySide.Positive);
            AddCellBoundaryEdgeIfNeeded(owners, edges, cell, cell.X, cell.Y + 1, isVertical: false, line: cell.Y + 1, offset: cell.X, side: BoundarySide.Negative);
        }

        return MergeBoundaryEdges(edges);
    }

    private static void AddCellBoundaryEdgeIfNeeded(
        IReadOnlyDictionary<GridPoint, int> owners,
        List<BoundaryEdge> edges,
        CellControlSnapshot cell,
        int neighborX,
        int neighborY,
        bool isVertical,
        int line,
        int offset,
        BoundarySide side)
    {
        if (owners.TryGetValue(new GridPoint(neighborX, neighborY), out var neighborOwner) &&
            neighborOwner == cell.OwnerId)
        {
            return;
        }

        edges.Add(new BoundaryEdge(isVertical, line, offset, offset + 1, cell.OwnerId, side));
    }

    private static IReadOnlyList<BoundaryDrawSegment> MergeBoundaryEdges(IEnumerable<BoundaryEdge> edges)
    {
        var merged = new List<BoundaryDrawSegment>();
        foreach (var group in edges.GroupBy(edge => new BoundaryRunKey(edge.IsVertical, edge.Line, edge.OwnerId, edge.Side)))
        {
            var intervals = group
                .Select(edge => new BoundaryInterval(edge.Start, edge.End))
                .OrderBy(interval => interval.Start)
                .ThenBy(interval => interval.End)
                .ToList();
            if (intervals.Count == 0)
                continue;

            var currentStart = intervals[0].Start;
            var currentEnd = intervals[0].End;
            foreach (var interval in intervals.Skip(1))
            {
                if (interval.Start <= currentEnd)
                {
                    currentEnd = Math.Max(currentEnd, interval.End);
                    continue;
                }

                merged.Add(new BoundaryDrawSegment(group.Key.IsVertical, group.Key.Line, currentStart, currentEnd, group.Key.OwnerId, group.Key.Side));
                currentStart = interval.Start;
                currentEnd = interval.End;
            }

            merged.Add(new BoundaryDrawSegment(group.Key.IsVertical, group.Key.Line, currentStart, currentEnd, group.Key.OwnerId, group.Key.Side));
        }

        return merged
            .OrderBy(segment => segment.IsVertical ? segment.Start : segment.Line)
            .ThenBy(segment => segment.IsVertical ? segment.Line : segment.Start)
            .ThenBy(segment => segment.End)
            .ThenBy(segment => segment.OwnerId)
            .ThenBy(segment => segment.Side)
            .ToList();
    }

    private static Color TerrainColor(TerrainKind kind)
        => kind switch
        {
            TerrainKind.Water => new Color(42, 154, 232, 255),
            TerrainKind.Road => new Color(20, 20, 24, 255),
            TerrainKind.Hill => new Color(118, 121, 118, 255),
            TerrainKind.Rock => new Color(150, 153, 149, 255),
            TerrainKind.Forest => new Color(45, 128, 53, 255),
            _ => new Color(157, 190, 60, 255)
        };

    private static Color PlayerColor(int playerId)
        => playerId switch
        {
            GameConstants.NeutralPlayerId => Color.Yellow,
            0 => new Color(68, 42, 232, 255),
            1 => Color.Red,
            2 => Color.Orange,
            3 => Color.DeepSkyBlue,
            4 => Color.Magenta,
            5 => Color.Lime,
            6 => Color.Cyan,
            7 => Color.White,
            _ => Color.LightPink
        };

    private static Color BoundaryColor(int playerId)
    {
        var color = PlayerColor(playerId);
        return new Color(color.R, color.G, color.B, playerId == GameConstants.NeutralPlayerId ? (byte)190 : byte.MaxValue);
    }

    private static Color4 ToColor4(Color color)
        => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    public void Dispose()
    {
        _pixel.Dispose();
        _circle.Dispose();
        _ring.Dispose();
        _square.Dispose();
        _spriteBatch.Dispose();
    }

    private enum BoundarySide
    {
        Negative,
        Positive
    }

    private readonly record struct BoundaryDrawSegment(bool IsVertical, int Line, int Start, int End, int OwnerId, BoundarySide Side);
    private readonly record struct BoundaryEdge(bool IsVertical, int Line, int Start, int End, int OwnerId, BoundarySide Side);
    private readonly record struct BoundaryRunKey(bool IsVertical, int Line, int OwnerId, BoundarySide Side);
    private readonly record struct BoundaryInterval(int Start, int End);
}
