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
        Assert.True(menu.HumanPlayerCheckBox.IsVisible());
        Assert.True(menu.HumanPlayerCheckBox.IsChecked());
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
    public void StartMenu_HumanUnchecked_StartsAiOnlySpectatorMode()
    {
        var menu = new MainMenuPage(_fixture.Context);
        menu.Start(seed: 2026, aiPlayers: 4, human: false);

        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);
        var snapshot = GameQueryHelpers.GetSnapshot(_fixture.Context);

        Assert.Equal("AiOnly", snapshot.GameMode);
        Assert.False(snapshot.HasHumanPlayer);
        Assert.DoesNotContain(snapshot.Players, player => player.Kind == "Human");
        Assert.Equal("AI 0", snapshot.Players.Single(player => player.Id == 0).Name);
        Assert.True(game.SpectatorControlsPanel.IsVisible());
        Assert.True(game.StandingsPanel.IsVisible());
        Assert.True(game.SpeedFastButton.IsVisible());
        Assert.False(game.HumanCommandPanel.IsVisible());
        Assert.False(game.AttackButton.IsVisible());
        game.Status.AssertTextContains("AiOnly");
        game.Score.AssertTextContains("Leader");
    }

    [Fact]
    public void AiOnly_GameQueryStartsPlayableMapAndTelemetryForEveryAi()
    {
        var snapshot = GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        Assert.Equal("AiOnly", snapshot.GameMode);
        Assert.False(snapshot.HasHumanPlayer);

        GameQueryHelpers.StepTicks(_fixture.Context, 12);
        var afterPlanning = GameQueryHelpers.GetSnapshot(_fixture.Context);
        var map = GameQueryHelpers.GetMapState(_fixture.Context);
        var standings = GameQueryHelpers.GetStandings(_fixture.Context);
        var telemetry = GameQueryHelpers.GetTelemetry(_fixture.Context);

        Assert.Equal(4, afterPlanning.Players.Count(player => player.Kind == "Ai"));
        Assert.Equal(4, afterPlanning.Players.Select(player => player.GenomeId).Distinct().Count());
        Assert.Equal(4, standings.Count);
        Assert.Equal(4, telemetry.Select(item => item.PlayerId).Distinct().Count());
        Assert.True(map.UnitMarkerCount >= 16);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
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
    public void SmoothMovement_ReportsChangingVisualPositionBetweenTicks()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);
        GameQueryHelpers.StepTicks(_fixture.Context, 1);

        var first = GameQueryHelpers.GetUnitVisuals(_fixture.Context)
            .First(unit => unit.IsInterpolating);

        var second = first;
        for (var attempt = 0; attempt < 20 && !VisualPositionChanged(first, second); attempt++)
        {
            System.Threading.Thread.Sleep(25);
            second = GameQueryHelpers.GetUnitVisuals(_fixture.Context)
                .Single(unit => unit.UnitId == first.UnitId);
        }

        var map = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.Equal(0, map.DuplicateOccupiedCellCount);
        Assert.InRange(first.Progress, 0.0, 1.0);
        Assert.InRange(second.Progress, 0.0, 1.0);
        Assert.True(
            VisualPositionChanged(first, second),
            $"Unit {first.UnitId} visual position did not change. First=({first.VisualX},{first.VisualY}) p{first.Progress}; Second=({second.VisualX},{second.VisualY}) p{second.Progress}.");
    }

    [Fact]
    public void DiagonalMovement_ReportsDiagonalVisualTarget()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);

        var sawDiagonalMove = false;
        for (var i = 0; i < 30; i++)
        {
            GameQueryHelpers.StepTicks(_fixture.Context, 1);
            var visuals = GameQueryHelpers.GetUnitVisuals(_fixture.Context);
            if (visuals.Any(IsDiagonalVisualTarget))
            {
                sawDiagonalMove = true;
                break;
            }
        }

        var map = GameQueryHelpers.GetMapState(_fixture.Context);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
        Assert.True(sawDiagonalMove);
    }

    [Fact]
    public void AiOnly_FastSpeedKeepsSmoothMovementDiagnosticsAndSingleOccupancy()
    {
        GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.SetSimulationSpeed(_fixture.Context, 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);
        GameQueryHelpers.StepTicks(_fixture.Context, 1);

        var visuals = GameQueryHelpers.GetUnitVisuals(_fixture.Context);
        var map = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.Equal(0, map.DuplicateOccupiedCellCount);
        Assert.Contains(visuals, unit => unit.IsInterpolating);
        Assert.All(visuals, unit => Assert.InRange(unit.Progress, 0.0, 1.0));
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

    private static bool VisualPositionChanged(UnitVisualStateDto first, UnitVisualStateDto second)
        => Math.Abs(first.VisualX - second.VisualX) > 0.0001 ||
            Math.Abs(first.VisualY - second.VisualY) > 0.0001;

    private static bool IsDiagonalVisualTarget(UnitVisualStateDto unit)
        => unit.IsInterpolating &&
            Math.Abs(unit.VisualFromCellX - unit.VisualToCellX) == 1 &&
            Math.Abs(unit.VisualFromCellY - unit.VisualToCellY) == 1;
}
