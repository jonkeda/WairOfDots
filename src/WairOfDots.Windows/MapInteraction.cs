using WairOfDots.Core;

namespace WairOfDots.Windows;

internal enum MapHitKind
{
    None,
    Unit,
    City,
    Cell
}

internal sealed record MapHitResult(
    MapHitKind Kind,
    int? UnitId,
    int? CityId,
    int? CellX,
    int? CellY,
    double NormalizedX,
    double NormalizedY,
    double DistancePixels,
    string Label)
{
    public static MapHitResult None(double normalizedX, double normalizedY)
        => new(MapHitKind.None, null, null, null, null, normalizedX, normalizedY, 0, "none");
}

internal sealed record MapInteractionTarget(
    string Kind,
    int Id,
    string Label,
    int? PlayerId,
    double NormalizedX,
    double NormalizedY);

internal static class MapCoordinateSystem
{
    internal const double MapMin = -9.2;
    internal const double MapMax = 9.2;
    internal const double MapRange = MapMax - MapMin;

    public static (double X, double Y) ToNormalized(MapPoint point)
        => (
            Math.Clamp((point.X - MapMin) / MapRange, 0.02, 0.98),
            Math.Clamp((point.Y - MapMin) / MapRange, 0.02, 0.98)
        );
}

internal static class MapInteractionService
{
    private const double CityHitRadiusPixels = 18.0;
    private const double MinimumUnitHitRadiusPixels = 13.0;

    public static MapHitResult HitTest(
        GameSimulation simulation,
        Func<TacticalUnit, MapPoint> resolveUnitPosition,
        double normalizedX,
        double normalizedY,
        double viewportWidth,
        double viewportHeight)
    {
        var x = Math.Clamp(normalizedX, 0, 1);
        var y = Math.Clamp(normalizedY, 0, 1);
        var unitHit = FindUnitHit(simulation, resolveUnitPosition, x, y, viewportWidth, viewportHeight);
        if (unitHit != null)
            return unitHit;

        var cityHit = FindCityHit(simulation, x, y, viewportWidth, viewportHeight);
        if (cityHit != null)
            return cityHit;

        var cell = ToGridPoint(simulation.Grid, x, y);
        return new MapHitResult(
            MapHitKind.Cell,
            null,
            null,
            cell.X,
            cell.Y,
            x,
            y,
            0,
            $"cell:{cell.X},{cell.Y}");
    }

    public static IReadOnlyList<MapInteractionTarget> CreateTargets(
        GameSimulation simulation,
        Func<TacticalUnit, MapPoint> resolveUnitPosition)
    {
        var targets = new List<MapInteractionTarget>();
        targets.AddRange(simulation.Cities
            .OrderBy(city => city.Id)
            .Select(city =>
            {
                var point = MapCoordinateSystem.ToNormalized(city.Position);
                return new MapInteractionTarget("City", city.Id, city.Name, city.OwnerId, point.X, point.Y);
            }));

        targets.AddRange(simulation.Units
            .Where(unit => unit.IsAlive && !simulation.Players[unit.PlayerId].IsEliminated)
            .OrderBy(unit => unit.Id)
            .Select(unit =>
            {
                var point = MapCoordinateSystem.ToNormalized(resolveUnitPosition(unit));
                return new MapInteractionTarget("Unit", unit.Id, unit.Kind.ToString(), unit.PlayerId, point.X, point.Y);
            }));

        return targets;
    }

    public static bool TryNormalizeScreenPointToMap(
        double screenNormalizedX,
        double screenNormalizedY,
        double uiWidth,
        double uiHeight,
        double mapLeft,
        double mapTop,
        double mapWidth,
        double mapHeight,
        out double mapNormalizedX,
        out double mapNormalizedY)
    {
        var x = screenNormalizedX * uiWidth;
        var y = screenNormalizedY * uiHeight;
        mapNormalizedX = (x - mapLeft) / mapWidth;
        mapNormalizedY = (y - mapTop) / mapHeight;

        return mapNormalizedX >= 0 &&
            mapNormalizedX <= 1 &&
            mapNormalizedY >= 0 &&
            mapNormalizedY <= 1;
    }

    private static MapHitResult? FindUnitHit(
        GameSimulation simulation,
        Func<TacticalUnit, MapPoint> resolveUnitPosition,
        double normalizedX,
        double normalizedY,
        double viewportWidth,
        double viewportHeight)
        => simulation.Units
            .Where(unit => unit.IsAlive && !simulation.Players[unit.PlayerId].IsEliminated)
            .Select(unit =>
            {
                var point = MapCoordinateSystem.ToNormalized(resolveUnitPosition(unit));
                var targetY = point.Y + MapVisualLayout.UnitVisualYOffsetPixels / viewportHeight;
                var distance = PixelDistance(normalizedX, normalizedY, point.X, targetY, viewportWidth, viewportHeight);
                return new
                {
                    Unit = unit,
                    Distance = distance,
                    Radius = UnitHitRadius(unit)
                };
            })
            .Where(item => item.Distance <= item.Radius)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Unit.Id)
            .Select(item => new MapHitResult(
                MapHitKind.Unit,
                item.Unit.Id,
                null,
                item.Unit.Cell.X,
                item.Unit.Cell.Y,
                normalizedX,
                normalizedY,
                Math.Round(item.Distance, 3),
                $"unit:{item.Unit.Id}:{item.Unit.Kind}"))
            .FirstOrDefault();

    private static MapHitResult? FindCityHit(
        GameSimulation simulation,
        double normalizedX,
        double normalizedY,
        double viewportWidth,
        double viewportHeight)
        => simulation.Cities
            .Select(city =>
            {
                var point = MapCoordinateSystem.ToNormalized(city.Position);
                var distance = PixelDistance(normalizedX, normalizedY, point.X, point.Y, viewportWidth, viewportHeight);
                return new
                {
                    City = city,
                    Distance = distance
                };
            })
            .Where(item => item.Distance <= CityHitRadiusPixels)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.City.Id)
            .Select(item => new MapHitResult(
                MapHitKind.City,
                null,
                item.City.Id,
                item.City.GridPosition.X,
                item.City.GridPosition.Y,
                normalizedX,
                normalizedY,
                Math.Round(item.Distance, 3),
                $"city:{item.City.Id}:{item.City.Name}"))
            .FirstOrDefault();

    private static double UnitHitRadius(TacticalUnit unit)
        => Math.Max(MinimumUnitHitRadiusPixels, MapVisualLayout.UnitMarkerSize(unit.Kind) / 2.0 + 8.0);

    private static double PixelDistance(
        double leftX,
        double leftY,
        double rightX,
        double rightY,
        double viewportWidth,
        double viewportHeight)
    {
        var dx = (leftX - rightX) * viewportWidth;
        var dy = (leftY - rightY) * viewportHeight;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static GridPoint ToGridPoint(GridMap grid, double normalizedX, double normalizedY)
    {
        var x = Math.Clamp((int)Math.Floor(normalizedX * grid.Width), 0, grid.Width - 1);
        var y = Math.Clamp((int)Math.Floor(normalizedY * grid.Height), 0, grid.Height - 1);
        return new GridPoint(x, y);
    }
}
