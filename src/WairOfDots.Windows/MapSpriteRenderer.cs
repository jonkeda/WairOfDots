using Stride.Core.Mathematics;
using Stride.Graphics;
using WairOfDots.Core;

namespace WairOfDots.Windows;

internal sealed class MapSpriteRenderer : IDisposable
{
    private const float BoundarySideThickness = 1.75f;

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly Texture _pixel;
    private readonly Texture _circle;
    private readonly Texture _ring;
    private readonly Texture _square;
    private readonly Texture _healthArcTrack;
    private readonly Texture[] _healthArcSteps;
    private readonly Texture _moraleArc;
    private readonly Texture _segmentedMoraleArc;
    private readonly Texture _brokenMoraleRing;
    private readonly Texture _impactFlash;

    public MapSpriteRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice) { VirtualResolution = new Vector3(1280, 720, 1000) };
        _font = font;
        _pixel = Texture.New2D(graphicsDevice, 1, 1, PixelFormat.R8G8B8A8_UNorm, [Color.White]);
        _circle = CreateCircleTexture(graphicsDevice, filled: true);
        _ring = CreateCircleTexture(graphicsDevice, filled: false);
        _square = CreateSquareTexture(graphicsDevice);
        _healthArcTrack = CreateArcTexture(graphicsDevice, MathF.PI + 0.24f, MathF.PI * 2f - 0.24f);
        _healthArcSteps = Enumerable.Range(0, 11)
            .Select(step => CreateArcTexture(graphicsDevice, MathF.PI + 0.24f, MathF.PI * 2f - 0.24f, completion: step / 10f))
            .ToArray();
        _moraleArc = CreateArcTexture(graphicsDevice, 0.24f, MathF.PI - 0.24f);
        _segmentedMoraleArc = CreateArcTexture(graphicsDevice, 0.24f, MathF.PI - 0.24f, segmented: true);
        _brokenMoraleRing = CreateBrokenRingTexture(graphicsDevice);
        _impactFlash = CreateImpactFlashTexture(graphicsDevice);
    }

    public void Draw(
        GraphicsContext graphicsContext,
        GameSimulation simulation,
        MatchSnapshot snapshot,
        IReadOnlyDictionary<int, MapPoint> unitPositions,
        IReadOnlySet<int> engagedUnitIds,
        int selectedCityId,
        int? selectedUnitId,
        MapOverlayMode overlayMode)
    {
        var viewport = new RectangleF(14, 14, 890, 660);

        _spriteBatch.Begin(graphicsContext, SpriteSortMode.Deferred);
        DrawRectangle(viewport, TerrainColor(TerrainKind.Grass));
        DrawTerrain(simulation, viewport);
        if (overlayMode == MapOverlayMode.Economy)
            DrawEconomyCellOverlay(simulation, snapshot, viewport);
        DrawBoundaries(snapshot, simulation.Grid, viewport);
        DrawCities(simulation, snapshot, viewport, selectedCityId);
        if (overlayMode == MapOverlayMode.Economy)
            DrawEconomyCityOverlay(simulation, snapshot, viewport);
        DrawUnits(simulation, unitPositions, engagedUnitIds, selectedUnitId, viewport);
        DrawCombatMarkers(snapshot, simulation.Grid, viewport);
        DrawLegend(viewport);
        _spriteBatch.End();
    }

    private void DrawTerrain(GameSimulation simulation, RectangleF viewport)
    {
        foreach (var patch in simulation.Terrain)
        {
            var center = ToScreen(patch.Center, viewport);
            var width = (float)(patch.Width / MapCoordinateSystem.MapRange * viewport.Width);
            var height = (float)(patch.Height / MapCoordinateSystem.MapRange * viewport.Height);
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

    private void DrawEconomyCellOverlay(GameSimulation simulation, MatchSnapshot snapshot, RectangleF viewport)
    {
        var maxTaxValue = Math.Max(1, snapshot.CellControls.Max(cell => cell.TaxValue));
        foreach (var cell in snapshot.CellControls
                     .Where(cell => cell.TaxValue > 0)
                     .OrderBy(cell => cell.Y)
                     .ThenBy(cell => cell.X))
        {
            var rect = CellToScreen(new GridPoint(cell.X, cell.Y), simulation.Grid, viewport, insetPixels: 1.2f);
            DrawRectangle(rect, TaxHeatColor(cell.TaxValue / maxTaxValue));
        }

        foreach (var control in simulation.CellControls
                     .Select(cell => new { Cell = cell, AgeTicks = snapshot.Tick - cell.LastChangedTick })
                     .Where(item => item.Cell.LastChangedTick > 0 && item.AgeTicks >= 0 && item.AgeTicks <= 8)
                     .OrderBy(item => item.Cell.LastChangedTick)
                     .ThenBy(item => item.Cell.OwnerId)
                     .ThenBy(item => item.Cell.Point.Y)
                     .ThenBy(item => item.Cell.Point.X))
        {
            var rect = CellToScreen(control.Cell.Point, simulation.Grid, viewport, insetPixels: 2.0f);
            var alpha = FadeAlpha(control.AgeTicks, lifetimeTicks: 8);
            DrawCellBorder(rect, new Color(255, 246, 184, (byte)Math.Clamp(alpha * 0.65f, 0, 255)), thickness: 1.6f);
        }
    }

    private void DrawEconomyCityOverlay(GameSimulation simulation, MatchSnapshot snapshot, RectangleF viewport)
    {
        var occupiedCells = snapshot.Units.Select(unit => new GridPoint(unit.X, unit.Y)).ToHashSet();
        var cityCells = simulation.Cities.Select(city => city.GridPosition).ToHashSet();
        var cityOwners = snapshot.Cities.ToDictionary(city => city.Id, city => city.OwnerId);
        var economies = snapshot.Economies.ToDictionary(economy => economy.PlayerId);

        foreach (var city in simulation.Cities.OrderBy(city => city.Id))
        {
            if (!cityOwners.TryGetValue(city.Id, out var ownerId) || ownerId < 0)
                continue;

            var center = ToScreen(city.Position, viewport);
            var candidates = CardinalAdjacentCells(city.GridPosition)
                .Where(point =>
                    simulation.Grid.IsPassable(point) &&
                    !occupiedCells.Contains(point) &&
                    !cityCells.Contains(point))
                .OrderBy(point => simulation.Grid.MoveCost(point))
                .ThenBy(point => point.X)
                .ThenBy(point => point.Y)
                .ToList();

            foreach (var point in candidates.Take(4))
            {
                var pipCenter = ToScreen(simulation.Grid.ToMapPoint(point), viewport);
                DrawTexture(_circle, Centered(pipCenter, 5.4f), new Color(178, 245, 150, 220));
            }

            if (candidates.Count == 0)
                DrawTexture(_ring, Centered(center, 25f), new Color(245, 96, 92, 210));

            if (!economies.TryGetValue(ownerId, out var economy) || economy.PayrollDeficitRatio <= 0)
                continue;

            var stressColor = PayrollStressColor(economy.PayrollDeficitRatio);
            DrawTexture(_ring, Centered(center, 28f + (float)Math.Min(4, economy.PayrollDeficitRatio * 4)), stressColor);
            DrawRectangle(new RectangleF(center.X - 9f, center.Y + 12f, 18f, 2.6f), stressColor);
        }
    }

    private void DrawUnits(
        GameSimulation simulation,
        IReadOnlyDictionary<int, MapPoint> unitPositions,
        IReadOnlySet<int> engagedUnitIds,
        int? selectedUnitId,
        RectangleF viewport)
    {
        foreach (var unit in simulation.Units.Where(unit => unit.IsAlive).OrderBy(unit => unit.Id))
        {
            if (!unitPositions.TryGetValue(unit.Id, out var position))
                position = unit.CurrentPosition;

            var center = ToScreen(position, viewport);
            center.Y += (float)MapVisualLayout.UnitVisualYOffsetPixels;
            var color = PlayerColor(unit.PlayerId);
            var size = MapVisualLayout.UnitMarkerSize(unit.Kind);

            if (engagedUnitIds.Contains(unit.Id))
                DrawTexture(_ring, Centered(center, size + 8), Color.LightYellow);

            if (selectedUnitId == unit.Id)
                DrawTexture(_ring, Centered(center, size + 12), Color.White);

            DrawMoraleMarker(unit, center, size);

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

            DrawHealthMarker(unit, center, size);
            DrawUnitCenterMarker(unit, center);
        }
    }

    private void DrawUnitCenterMarker(TacticalUnit unit, Vector2 center)
    {
        if (unit.Kind == UnitKind.Commander && unit.CommanderNumber.HasValue)
        {
            DrawCenterLabel(unit.CommanderNumber.Value.ToString(), center, 12f, 10f);
            return;
        }

        if (unit.Kind is not (UnitKind.Infantry or UnitKind.Tank))
            return;

        if (unit.AssignedCommanderNumber.HasValue)
        {
            DrawCenterLabel(unit.AssignedCommanderNumber.Value.ToString(), center, 10f, 8.5f);
            return;
        }

        DrawReservePip(center);
    }

    private void DrawCenterLabel(string label, Vector2 center, float backingSize, float labelSize)
    {
        DrawTexture(_circle, Centered(center, backingSize), new Color(8, 12, 18, 210));
        var measured = _font.MeasureString(label, labelSize);
        var position = new Vector2(center.X - measured.X / 2f, center.Y - measured.Y / 2f);
        var shadow = new Color4(0, 0, 0, 0.95f);
        _spriteBatch.DrawString(_font, label, labelSize, position + new Vector2(1f, 1f), shadow, TextAlignment.Left);
        _spriteBatch.DrawString(_font, label, labelSize, position, ToColor4(Color.White), TextAlignment.Left);
    }

    private void DrawReservePip(Vector2 center)
    {
        DrawTexture(_circle, Centered(center, 6.5f), new Color(8, 12, 18, 225));
        DrawTexture(_circle, Centered(center, 3.6f), new Color(255, 235, 128, 245));
    }

    private void DrawHealthMarker(TacticalUnit unit, Vector2 center, float unitSize)
    {
        var ratio = (float)MapVisualLayout.HealthRatio(unit.Kind, unit.Health);
        var markerSize = unitSize + 9f;
        var step = Math.Clamp((int)Math.Ceiling(ratio * 10f), 0, _healthArcSteps.Length - 1);

        DrawTexture(_healthArcTrack, Centered(center, markerSize), new Color(12, 16, 20, 205));
        if (step > 0)
            DrawTexture(_healthArcSteps[step], Centered(center, markerSize), HealthColor(ratio));
    }

    private void DrawMoraleMarker(TacticalUnit unit, Vector2 center, float unitSize)
    {
        var band = MoraleRules.Band(unit.Morale);
        var color = MoraleColor(band);
        var markerSize = unitSize + (band == MoraleBand.Routed ? 15f : 11f);
        var texture = band switch
        {
            MoraleBand.Routed => _brokenMoraleRing,
            MoraleBand.RoutRisk => _segmentedMoraleArc,
            _ => _moraleArc
        };

        DrawTexture(texture, Centered(center, markerSize), color);

        if (band == MoraleBand.Routed)
        {
            var y = center.Y + unitSize / 2f + 4f;
            DrawRectangle(new RectangleF(center.X - 5f, y, 10f, 3f), color);
        }
    }

    private void DrawCombatMarkers(MatchSnapshot snapshot, GridMap grid, RectangleF viewport)
    {
        var deathKeys = snapshot.RecentDeaths
            .Select(death => (death.Tick, death.UnitId))
            .ToHashSet();

        foreach (var hit in snapshot.RecentCombatHits
                     .OrderBy(hit => hit.Tick)
                     .ThenBy(hit => hit.DefenderPlayerId)
                     .ThenBy(hit => hit.DefenderUnitId))
        {
            if (hit.WasFatal && deathKeys.Contains((hit.Tick, hit.DefenderUnitId)))
                continue;

            var center = ToScreen(grid.ToMapPoint(new GridPoint(hit.CellX, hit.CellY)), viewport);
            var age = Math.Max(0, snapshot.Tick - hit.Tick);
            var alpha = FadeAlpha(age, lifetimeTicks: 4);
            if (alpha <= 0)
                continue;

            var color = hit.WasFatal
                ? new Color(255, 228, 176, alpha)
                : new Color(255, 248, 188, alpha);
            DrawTexture(_impactFlash, Centered(center, hit.WasFatal ? 27f : 21f), color);

            if (hit.DefenderKind is nameof(UnitKind.Commander) or nameof(UnitKind.General))
            {
                DrawTexture(_ring, Centered(center, 35f + age * 0.35f), new Color(255, 84, 112, alpha));
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
        var normalized = MapCoordinateSystem.ToNormalized(point);
        var x = (float)normalized.X;
        var y = (float)normalized.Y;
        return new Vector2(viewport.X + x * viewport.Width, viewport.Y + y * viewport.Height);
    }

    private static Vector2 GridEdgeToScreen(int x, int y, GridMap grid, RectangleF viewport)
        => new(
            viewport.X + x / (float)grid.Width * viewport.Width,
            viewport.Y + y / (float)grid.Height * viewport.Height);

    private static RectangleF CellToScreen(GridPoint point, GridMap grid, RectangleF viewport, float insetPixels)
    {
        var left = viewport.X + point.X / (float)grid.Width * viewport.Width;
        var top = viewport.Y + point.Y / (float)grid.Height * viewport.Height;
        var width = viewport.Width / grid.Width;
        var height = viewport.Height / grid.Height;
        return new RectangleF(
            left + insetPixels,
            top + insetPixels,
            Math.Max(1, width - insetPixels * 2),
            Math.Max(1, height - insetPixels * 2));
    }

    private void DrawCellBorder(RectangleF rectangle, Color color, float thickness)
    {
        DrawRectangle(new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        DrawRectangle(new RectangleF(rectangle.X, rectangle.Y + rectangle.Height - thickness, rectangle.Width, thickness), color);
        DrawRectangle(new RectangleF(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        DrawRectangle(new RectangleF(rectangle.X + rectangle.Width - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

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

    private static Texture CreateArcTexture(
        GraphicsDevice graphicsDevice,
        float startAngle,
        float endAngle,
        bool segmented = false,
        float completion = 1f)
    {
        const int size = 64;
        const float center = (size - 1) / 2f;
        const float innerRadius = 22f;
        const float outerRadius = 29f;
        var pixels = new Color[size * size];
        var clampedCompletion = Math.Clamp(completion, 0, 1);
        var completedEndAngle = startAngle + (endAngle - startAngle) * clampedCompletion;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var angle = MathF.Atan2(dy, dx);
                if (angle < 0)
                    angle += MathF.PI * 2f;

                var inArc = angle >= startAngle && angle <= completedEndAngle;
                if (segmented && inArc)
                {
                    var segmentPosition = (angle - startAngle) / (endAngle - startAngle) * 5f;
                    inArc = segmentPosition - MathF.Floor(segmentPosition) < 0.68f;
                }

                var alpha = inArc
                    ? Math.Min(SmoothAlpha(distance - innerRadius), SmoothAlpha(outerRadius - distance))
                    : 0f;
                var alphaByte = (byte)Math.Clamp(alpha * 255f, 0, 255);
                pixels[y * size + x] = new Color(alphaByte, alphaByte, alphaByte, alphaByte);
            }
        }

        return Texture.New2D(graphicsDevice, size, size, PixelFormat.R8G8B8A8_UNorm, pixels);
    }

    private static Texture CreateBrokenRingTexture(GraphicsDevice graphicsDevice)
    {
        const int size = 64;
        const float center = (size - 1) / 2f;
        const float innerRadius = 22f;
        const float outerRadius = 29f;
        var pixels = new Color[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var angle = MathF.Atan2(dy, dx);
                if (angle < 0)
                    angle += MathF.PI * 2f;

                var segmentPosition = angle / (MathF.PI / 4f);
                var inSegment = segmentPosition - MathF.Floor(segmentPosition) < 0.58f;
                var alpha = inSegment
                    ? Math.Min(SmoothAlpha(distance - innerRadius), SmoothAlpha(outerRadius - distance))
                    : 0f;
                var alphaByte = (byte)Math.Clamp(alpha * 255f, 0, 255);
                pixels[y * size + x] = new Color(alphaByte, alphaByte, alphaByte, alphaByte);
            }
        }

        return Texture.New2D(graphicsDevice, size, size, PixelFormat.R8G8B8A8_UNorm, pixels);
    }

    private static Texture CreateImpactFlashTexture(GraphicsDevice graphicsDevice)
    {
        const int size = 64;
        const float center = (size - 1) / 2f;
        var pixels = new Color[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var cross = Math.Min(MathF.Abs(dx), MathF.Abs(dy));
                var diagonal = Math.Min(MathF.Abs(dx - dy), MathF.Abs(dx + dy));
                var ring = Math.Min(SmoothAlpha(distance - 18f), SmoothAlpha(24f - distance));
                var spark = cross < 2.4f || diagonal < 2.0f ? SmoothAlpha(27f - distance) : 0f;
                var alpha = Math.Max(ring * 0.9f, spark);
                var alphaByte = (byte)Math.Clamp(alpha * 255f, 0, 255);
                pixels[y * size + x] = new Color(alphaByte, alphaByte, alphaByte, alphaByte);
            }
        }

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

    private static Color HealthColor(float ratio)
        => ratio switch
        {
            < 0.34f => new Color(242, 78, 92, 245),
            < 0.67f => new Color(255, 196, 74, 245),
            _ => new Color(88, 226, 132, 245)
        };

    private static Color MoraleColor(MoraleBand band)
        => band switch
        {
            MoraleBand.Routed => new Color(38, 112, 255, 255),
            MoraleBand.RoutRisk => new Color(47, 158, 255, 250),
            MoraleBand.Cautious => new Color(93, 190, 255, 238),
            MoraleBand.Aggressive => new Color(155, 229, 255, 228),
            _ => new Color(119, 202, 246, 210)
        };

    private static Color TaxHeatColor(double ratio)
    {
        var clamped = Math.Clamp(ratio, 0, 1);
        var low = new Color(178, 223, 140, 0);
        var high = new Color(255, 143, 82, 0);
        var alpha = (byte)Math.Clamp(24 + clamped * 72, 24, 96);
        return new Color(
            LerpByte(low.R, high.R, clamped),
            LerpByte(low.G, high.G, clamped),
            LerpByte(low.B, high.B, clamped),
            alpha);
    }

    private static Color PayrollStressColor(double deficitRatio)
    {
        var alpha = (byte)Math.Clamp(150 + Math.Clamp(deficitRatio, 0, 1) * 95, 150, 245);
        return deficitRatio >= 0.65
            ? new Color(255, 58, 78, alpha)
            : new Color(255, 132, 85, alpha);
    }

    private static byte LerpByte(byte start, byte end, double ratio)
        => (byte)Math.Clamp(start + (end - start) * ratio, 0, 255);

    private static Color4 ToColor4(Color color)
        => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    private static byte FadeAlpha(int ageTicks, int lifetimeTicks)
    {
        if (ageTicks > lifetimeTicks)
            return 0;

        var ratio = 1f - ageTicks / (float)Math.Max(1, lifetimeTicks);
        return (byte)Math.Clamp(255f * Math.Max(0.2f, ratio), 0, 255);
    }

    private static IEnumerable<GridPoint> CardinalAdjacentCells(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y);
        yield return new GridPoint(point.X, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y);
    }

    public void Dispose()
    {
        _pixel.Dispose();
        _circle.Dispose();
        _ring.Dispose();
        _square.Dispose();
        _healthArcTrack.Dispose();
        foreach (var texture in _healthArcSteps)
            texture.Dispose();
        _moraleArc.Dispose();
        _segmentedMoraleArc.Dispose();
        _brokenMoraleRing.Dispose();
        _impactFlash.Dispose();
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
