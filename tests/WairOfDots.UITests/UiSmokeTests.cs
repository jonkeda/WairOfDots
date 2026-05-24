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
        Assert.Equal(0, diagnostics.MouseLookComponentCount);
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
}
