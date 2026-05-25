using Brinell.Stride.Communication;
using WairOfDots.UITests.Pages;

namespace WairOfDots.UITests;

public sealed class UiSmokeTests : IAsyncLifetime
{
    private readonly WairTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    [Trait("Category", "Smoke")]
    public void Smoke_MenuAndMatchBootToPlayableMap()
    {
        var menu = new MainMenuPage(_fixture.Context);
        menu.AssertLoaded(true);
        menu.Start(seed: 2026, aiPlayers: 4);

        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var snapshot = GameQueryHelpers.GetSnapshot(_fixture.Context);
        var map = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.Equal("Running", snapshot.Phase);
        Assert.True(game.MapCanvas.IsVisible());
        Assert.True(map.CityMarkerCount >= 7);
        Assert.True(map.UnitMarkerCount >= 5);
        Assert.True(map.TerritoryBoundaryCount > 0);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
        Assert.True(map.PassableCellCount > map.BlockedCellCount);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Smoke_PlayfieldDoesNotShowOraveyControlInstructionsOverlay()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 2026, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);
        var map = GameQueryHelpers.GetMapState(_fixture.Context);
        Assert.Equal(0, diagnostics.MouseLookComponentCount);
        Assert.True(map.TerritoryBoundaryCount > 0);
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("TerritoryBoundary_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleTexts, text => text.Contains("CONTROL INSTRUCTIONS", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(diagnostics.VisibleTexts, text => text.Contains("Toggle Help", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(diagnostics.VisibleTexts, text => text.Contains("Reset Camera", StringComparison.OrdinalIgnoreCase));

        var screenshotPath = TakeScreenshot("wair_smoke_no_oravey_controls");

        using var bitmap = new System.Drawing.Bitmap(screenshotPath);
        var darkRatio = CalculateDarkPixelRatio(bitmap, xRatio: 0.02, yRatio: 0.03, widthRatio: 0.22, heightRatio: 0.32);

        Assert.True(
            darkRatio < 0.35,
            $"Top-left playfield is {darkRatio:P1} dark pixels. The old Oravey control overlay made this region mostly dark. Screenshot: {screenshotPath}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Smoke_TerritoryBoundariesAreVisibleInScreenshot()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var map = GameQueryHelpers.GetMapState(_fixture.Context);
        var cells = GameQueryHelpers.GetCellControls(_fixture.Context);
        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);
        var canvas = _fixture.Context.GetElementState("MapCanvas");

        Assert.True(canvas.Exists, "MapCanvas must be available for screenshot sampling.");
        Assert.Contains(cells, cell => cell.OwnerId != GameConstants.NeutralPlayerId);
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("TerritoryBoundary_", StringComparison.Ordinal));

        var screenshotPath = TakeScreenshot("wair_smoke_territory_boundaries");

        using var bitmap = new System.Drawing.Bitmap(screenshotPath);
        var mapSurface = FindMapSurfaceBounds(bitmap);
        var visibleBoundarySamples = CountVisibleCellBoundarySamples(bitmap, cells, canvas.Bounds, mapSurface, map.GridWidth, map.GridHeight);

        Assert.True(
            visibleBoundarySamples >= 18,
            $"Expected at least 18 sampled cell-control borders to show player-color pixels, but found {visibleBoundarySamples}. Screenshot: {screenshotPath}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Smoke_EndStatePanelIsVisibleInScreenshot()
    {
        GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var snapshot = GameQueryHelpers.StepToTick(_fixture.Context, 1800);
        var endPanel = _fixture.Context.GetElementState("EndStatePanel");

        Assert.Equal("Ended", snapshot.Phase);
        Assert.True(endPanel.Exists);
        Assert.True(endPanel.IsVisible);
        game.Status.AssertTextContains("Ended");

        var screenshotPath = TakeScreenshot("wair_smoke_end_state_panel");

        using var bitmap = new System.Drawing.Bitmap(screenshotPath);
        var darkRatio = CalculateDarkPixelRatio(bitmap, xRatio: 0.42, yRatio: 0.48, widthRatio: 0.48, heightRatio: 0.38);

        Assert.True(
            darkRatio > 0.25,
            $"Expected the end-state popup to be visibly dark in the screenshot, but sampled {darkRatio:P1} dark pixels. Screenshot: {screenshotPath}");
    }

    private string TakeScreenshot(string name)
    {
        var response = _fixture.Context.SendCommand(AutomationCommand.Action("TakeScreenshot", null, name));
        Assert.True(response.Success, response.Error);

        var path = response.Result switch
        {
            string value => value,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            _ => response.Result?.ToString()
        };

        Assert.False(string.IsNullOrWhiteSpace(path), "Screenshot command returned no path.");
        Assert.True(File.Exists(path), $"Screenshot was not written: {path}");
        return path!;
    }

    private static double CalculateDarkPixelRatio(
        System.Drawing.Bitmap bitmap,
        double xRatio,
        double yRatio,
        double widthRatio,
        double heightRatio)
    {
        var x = Math.Clamp((int)(bitmap.Width * xRatio), 0, bitmap.Width - 1);
        var y = Math.Clamp((int)(bitmap.Height * yRatio), 0, bitmap.Height - 1);
        var width = Math.Clamp((int)(bitmap.Width * widthRatio), 1, bitmap.Width - x);
        var height = Math.Clamp((int)(bitmap.Height * heightRatio), 1, bitmap.Height - y);

        var darkPixels = 0;
        var sampledPixels = 0;
        for (var py = y; py < y + height; py += 2)
        {
            for (var px = x; px < x + width; px += 2)
            {
                sampledPixels++;
                var color = bitmap.GetPixel(px, py);
                if (color.R < 60 && color.G < 70 && color.B < 70)
                    darkPixels++;
            }
        }

        return darkPixels / (double)Math.Max(1, sampledPixels);
    }

    private static int CountVisibleCellBoundarySamples(
        System.Drawing.Bitmap bitmap,
        IReadOnlyList<CellControlSnapshot> cells,
        ElementBounds canvas,
        System.Drawing.Rectangle mapSurface,
        int gridWidth,
        int gridHeight)
    {
        var visible = 0;
        var scale = mapSurface.Width / (double)Math.Max(1, canvas.Width);
        var physicalCanvasWidth = canvas.Width * scale;
        var physicalCanvasHeight = canvas.Height * scale;
        var owners = cells.ToDictionary(cell => (cell.X, cell.Y), cell => cell.OwnerId);

        foreach (var boundary in CreateCellBoundarySamples(cells, owners).Take(64))
        {
            var startX = mapSurface.X + boundary.FromX / (double)gridWidth * physicalCanvasWidth;
            var startY = mapSurface.Y + boundary.FromY / (double)gridHeight * physicalCanvasHeight;
            var endX = mapSurface.X + boundary.ToX / (double)gridWidth * physicalCanvasWidth;
            var endY = mapSurface.Y + boundary.ToY / (double)gridHeight * physicalCanvasHeight;

            if (HasBoundaryColorNear(bitmap, boundary.OwnerId, boundary.IsVertical, startX, startY, endX, endY))
                visible++;
        }

        return visible;
    }

    private static IEnumerable<CellBoundarySample> CreateCellBoundarySamples(
        IReadOnlyList<CellControlSnapshot> cells,
        IReadOnlyDictionary<(int X, int Y), int> owners)
    {
        foreach (var cell in cells
                     .Where(cell => cell.OwnerId != GameConstants.NeutralPlayerId)
                     .OrderBy(cell => cell.Y)
                     .ThenBy(cell => cell.X))
        {
            if (IsBoundaryCellSide(owners, cell, cell.X - 1, cell.Y))
                yield return new CellBoundarySample(cell.X, cell.Y, cell.X, cell.Y + 1, cell.OwnerId, IsVertical: true);
            if (IsBoundaryCellSide(owners, cell, cell.X + 1, cell.Y))
                yield return new CellBoundarySample(cell.X + 1, cell.Y, cell.X + 1, cell.Y + 1, cell.OwnerId, IsVertical: true);
            if (IsBoundaryCellSide(owners, cell, cell.X, cell.Y - 1))
                yield return new CellBoundarySample(cell.X, cell.Y, cell.X + 1, cell.Y, cell.OwnerId, IsVertical: false);
            if (IsBoundaryCellSide(owners, cell, cell.X, cell.Y + 1))
                yield return new CellBoundarySample(cell.X, cell.Y + 1, cell.X + 1, cell.Y + 1, cell.OwnerId, IsVertical: false);
        }
    }

    private static bool IsBoundaryCellSide(
        IReadOnlyDictionary<(int X, int Y), int> owners,
        CellControlSnapshot cell,
        int neighborX,
        int neighborY)
        => !owners.TryGetValue((neighborX, neighborY), out var neighborOwner) || neighborOwner != cell.OwnerId;

    private static System.Drawing.Rectangle FindMapSurfaceBounds(System.Drawing.Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var first = bitmap.Width;
            var last = -1;
            var grassPixels = 0;
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (!IsNearColor(bitmap.GetPixel(x, y), 157, 190, 60, 45))
                    continue;

                first = Math.Min(first, x);
                last = Math.Max(last, x);
                grassPixels++;
            }

            if (grassPixels > bitmap.Width * 0.45 && last > first)
                return new System.Drawing.Rectangle(first, y, last - first + 1, bitmap.Height - y);
        }

        throw new InvalidOperationException("Could not locate the map surface in the screenshot.");
    }

    private static bool HasBoundaryColorNear(
        System.Drawing.Bitmap bitmap,
        int ownerId,
        bool isVertical,
        double startX,
        double startY,
        double endX,
        double endY)
    {
        foreach (var t in new[] { 0.25, 0.5, 0.75 })
        {
            var centerX = (int)Math.Round(startX + (endX - startX) * t);
            var centerY = (int)Math.Round(startY + (endY - startY) * t);
            var halfWidth = isVertical ? 10 : 16;
            var halfHeight = isVertical ? 16 : 10;

            for (var y = Math.Max(0, centerY - halfHeight); y <= Math.Min(bitmap.Height - 1, centerY + halfHeight); y++)
            {
                for (var x = Math.Max(0, centerX - halfWidth); x <= Math.Min(bitmap.Width - 1, centerX + halfWidth); x++)
                {
                    if (IsNearPlayerColor(bitmap.GetPixel(x, y), ownerId))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsNearPlayerColor(System.Drawing.Color pixel, int playerId)
    {
        var expected = ExpectedPlayerColor(playerId);
        return IsNearColor(pixel, expected.R, expected.G, expected.B, 85);
    }

    private static bool IsNearColor(System.Drawing.Color pixel, int red, int green, int blue, int tolerance)
    {
        var dr = pixel.R - red;
        var dg = pixel.G - green;
        var db = pixel.B - blue;
        return dr * dr + dg * dg + db * db <= tolerance * tolerance;
    }

    private static System.Drawing.Color ExpectedPlayerColor(int playerId)
        => playerId switch
        {
            GameConstants.NeutralPlayerId => System.Drawing.Color.Yellow,
            0 => System.Drawing.Color.FromArgb(68, 42, 232),
            1 => System.Drawing.Color.Red,
            2 => System.Drawing.Color.Orange,
            3 => System.Drawing.Color.DeepSkyBlue,
            4 => System.Drawing.Color.Magenta,
            5 => System.Drawing.Color.Lime,
            6 => System.Drawing.Color.Cyan,
            7 => System.Drawing.Color.White,
            _ => System.Drawing.Color.LightPink
        };

    private sealed record CellBoundarySample(
        int FromX,
        int FromY,
        int ToX,
        int ToY,
        int OwnerId,
        bool IsVertical);
}
