using WairOfDots.UITests.Pages;

namespace WairOfDots.UITests;

public sealed class MenuAndGameplayTests : IAsyncLifetime
{
    private readonly WairTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void MainMenu_Loads_WithSeedAndStartControls()
    {
        var menu = new MainMenuPage(_fixture.Context);

        menu.AssertLoaded(true);
        menu.Title.AssertText("Wair of Dots");
        Assert.True(menu.SeedInput.IsVisible());
        Assert.True(menu.AiCountInput.IsVisible());
        Assert.True(menu.StartGameButton.IsVisible());
    }

    [Fact]
    public void StartGame_ShowsHudAndCities()
    {
        var menu = new MainMenuPage(_fixture.Context);
        menu.Start(seed: 2026, aiPlayers: 4);

        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);
        game.GameTitle.AssertText("Wair of Dots");
        Assert.True(game.MapCanvas.IsVisible());
        game.Status.AssertTextContains("Seed 2026");
        game.Score.AssertTextContains("Human score");
        Assert.True(game.CityButton(0).IsVisible());
        Assert.True(game.MapCity(0).IsVisible());
    }

    [Fact]
    public void Map_ShowsTerrainAndCircleMarkers()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var map = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.True(game.MapCanvas.IsVisible());
        Assert.Contains("Water", map.TerrainTypes);
        Assert.Contains("Road", map.TerrainTypes);
        Assert.Contains("Hill", map.TerrainTypes);
        Assert.Contains("Rock", map.TerrainTypes);
        Assert.Contains("Forest", map.TerrainTypes);
        Assert.True(map.TerrainPatchCount >= 8);
        Assert.Equal(31, map.GridWidth);
        Assert.Equal(31, map.GridHeight);
        Assert.True(map.PassableCellCount > map.BlockedCellCount);
        Assert.True(map.BlockedCellCount > 0);
        Assert.True(map.CityMarkerCount >= 7);
        Assert.True(map.UnitMarkerCount >= 5);
        Assert.Equal(5, map.CommanderMarkerCount);
        Assert.Equal(5, map.GeneralMarkerCount);
        Assert.Equal(map.UnitMarkerCount, map.OccupiedCellCount);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
    }

    [Fact]
    public void GridMovement_ReportsMovingUnitsOnCells()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);

        var map = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.True(map.MovingUnitCount > 0);
        Assert.True(map.MovingUnitGridStepCount > 0);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
    }

    [Fact]
    public void Combat_ReportsEngagementThroughMapState()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);

        var observedCombat = false;
        for (var i = 0; i < 60; i++)
        {
            var snapshot = GameQueryHelpers.StepTicks(_fixture.Context, 4);
            var map = GameQueryHelpers.GetMapState(_fixture.Context);
            if (map.ActiveCombatCount > 0 ||
                snapshot.LastEvent.Contains("attacked", StringComparison.OrdinalIgnoreCase))
            {
                observedCombat = true;
                break;
            }
        }

        Assert.True(observedCombat);
    }

    [Fact]
    public void FixedSeed_ReplaysDeterministically()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 44, aiPlayers: 4);
        var first = GameQueryHelpers.StepTicks(_fixture.Context, 80).Fingerprint;

        GameQueryHelpers.StartMatch(_fixture.Context, seed: 44, aiPlayers: 4);
        var second = GameQueryHelpers.StepTicks(_fixture.Context, 80).Fingerprint;

        Assert.Equal(first, second);
    }

    [Fact]
    public void PauseButton_FreezesSimulation()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 9, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 20);

        var game = new GamePage(_fixture.Context);
        game.PauseButton.Click();
        var before = GameQueryHelpers.GetSnapshot(_fixture.Context);

        GameQueryHelpers.StepTicks(_fixture.Context, 20);
        var after = GameQueryHelpers.GetSnapshot(_fixture.Context);

        Assert.Equal("Paused", after.Phase);
        Assert.Equal(before.Fingerprint, after.Fingerprint);
    }

    [Fact]
    public void CityAndDirectiveButtons_UpdateHumanPlan()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 12, aiPlayers: 4);

        var game = new GamePage(_fixture.Context);
        game.CityButton(3).Click();
        game.DefendButton.Click();
        game.HeavyPreferenceButton.Click();

        var snapshot = GameQueryHelpers.GetSnapshot(_fixture.Context);

        Assert.Equal(3, snapshot.HumanTargetCityId);
        Assert.Equal("Defend", snapshot.HumanDirective);
        Assert.True(snapshot.HumanLightPreference < 0.5);
    }

    [Fact]
    public void RestartButton_RestartsSameSeed()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 88, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 30);

        var game = new GamePage(_fixture.Context);
        game.RestartButton.Click();
        var snapshot = GameQueryHelpers.GetSnapshot(_fixture.Context);

        Assert.Equal(0, snapshot.Tick);
        Assert.Equal("Running", snapshot.Phase);
        game.Status.AssertTextContains("Seed 88");
    }
}
