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
        Assert.True(menu.HumanRoleButton.IsVisible());
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
        game.Score.AssertTextContains("Treasury");
        game.Score.AssertTextContains("Tax");
        Assert.True(game.MapCity(0).IsVisible());
    }

    [Fact]
    public void StartGame_GameQueryCanStartHumanCommanderMode()
    {
        var snapshot = GameQueryHelpers.StartMatch(
            _fixture.Context,
            seed: 2026,
            aiPlayers: 4,
            mode: "HumanVsAi",
            humanMode: "Commander");
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        Assert.Equal("Commander", snapshot.Players.Single(player => player.Id == GameConstants.HumanPlayerId).HumanControlMode);
        Assert.True(game.HumanCommandPanel.IsVisible());
        Assert.False(game.SpectatorControlsPanel.IsVisible());
        game.Target.AssertTextContains("Role Commander");
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
        game.Speed.AssertTextContains("0.5x");
        Assert.False(game.HumanCommandPanel.IsVisible());
        Assert.False(game.AttackButton.IsVisible());
        game.Status.AssertTextContains("AiOnly");
        game.Score.AssertTextContains("Leader");
    }

    [Fact]
    public void AiOnly_GameQueryStartsPlayableMapAndTelemetryForEveryAi()
    {
        var snapshot = GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        Assert.Equal("AiOnly", snapshot.GameMode);
        Assert.False(snapshot.HasHumanPlayer);
        game.Speed.AssertTextContains("8x");

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
        Assert.True(map.ControlledCellCount > 0);
        Assert.True(map.TotalTaxValue > 0);
    }

    [Fact]
    public void EconomyAndVisibilityDiagnostics_AreExposedThroughBrinellQueries()
    {
        var snapshot = GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);

        var cells = GameQueryHelpers.GetCellControls(_fixture.Context);
        var economies = GameQueryHelpers.GetEconomies(_fixture.Context);
        var regions = GameQueryHelpers.GetRegions(_fixture.Context);
        var visibility = GameQueryHelpers.GetVisibility(_fixture.Context);
        var map = GameQueryHelpers.GetMapState(_fixture.Context);
        var boundaries = GameQueryHelpers.GetTerritoryBoundaries(_fixture.Context);
        var training = GameQueryHelpers.RunTrainingSmoke(_fixture.Context, seed: 31, aiPlayers: 4, ticks: 120);

        Assert.Equal(map.ControlledCellCount, cells.Count);
        Assert.True(map.TerritoryBoundaryCount > 0);
        Assert.True(boundaries.Count > 0);
        Assert.Equal(snapshot.Players.Count, economies.Count);
        Assert.Equal(snapshot.Players.Count, regions.Count);
        Assert.Equal(snapshot.Players.Count, visibility.Count);
        Assert.All(economies, economy => Assert.True(economy.ControlledCellCount >= 0));
        Assert.Equal(4, training.Count);
    }

    [Fact]
    public void TelemetryDiagnostics_HudAndQueryExposeEventTypes()
    {
        GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);

        var game = new GamePage(_fixture.Context);
        var telemetry = GameQueryHelpers.GetTelemetry(_fixture.Context);
        var aiStates = GameQueryHelpers.GetAiStates(_fixture.Context);

        Assert.Contains(telemetry, item => item.EventType == "AiPlan");
        Assert.Equal(4, aiStates.Count);
        Assert.Contains(aiStates, state => state.Archetype == "turtle");
        Assert.All(aiStates, state => Assert.True(Enum.TryParse<PlayerDirective>(state.Directive, out _)));
        game.Telemetry.AssertTextContains("AI telemetry");
        game.Telemetry.AssertTextContains("AI states");
        game.Telemetry.AssertTextContains("turtle");
        game.Telemetry.AssertTextContains(telemetry.Last().GenomeId);
    }

    [Fact]
    public void AiOnly_StandingsRenderHeaderLabelsDotsAndBarsInSnapshotOrder()
    {
        var snapshot = GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);
        var expectedOrder = snapshot.Standings.ToList();
        var visibleElementNames = diagnostics.VisibleElementNames.ToList();
        var rowIndices = expectedOrder
            .Select(standing => visibleElementNames.IndexOf($"StandingRow_{standing.PlayerId}"))
            .ToList();

        Assert.True(game.StandingsPanel.IsVisible());
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingHeaderRow");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingScoreHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingCitiesHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingUnitsHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingGeneralHeaderLabel");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Score");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Cities");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Units");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "General");
        Assert.All(rowIndices, index => Assert.True(index >= 0));
        Assert.Equal(rowIndices.OrderBy(index => index).ToList(), rowIndices);
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("StandingLabelColumn_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("StandingStatus_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleTexts, text => text == "in" || text == "out");

        foreach (var standing in expectedOrder)
        {
            Assert.DoesNotContain(diagnostics.VisibleTexts, text => text == standing.Name);
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingPlayerDot_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingScoreBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingCitiesBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingUnitsBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingGeneralBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.Score:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.CityCount:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.UnitCount:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.GeneralHealth:F0}");
        }
    }

    [Fact]
    public void Map_ShowsTerrainCitySquaresAndCircleMarkers()
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
        Assert.Equal(map.CityMarkerCount, map.CityOwners.Count);
        Assert.Contains(map.CityOwners, city => city.OwnerId == GameConstants.NeutralPlayerId);
        Assert.Contains(map.CityOwners, city => city.OwnerId >= 0);
        Assert.True(map.TerritoryBoundaryCount > 0);
        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);
        Assert.Equal(map.CityMarkerCount, diagnostics.VisibleElementNames.Count(name => name.StartsWith("MapCity_", StringComparison.Ordinal)));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("MapCityBack_", StringComparison.Ordinal));
        Assert.True(map.UnitMarkerCount >= 5);
        Assert.Equal(5, map.CommanderMarkerCount);
        Assert.Equal(5, map.GeneralMarkerCount);
        Assert.Equal(map.UnitMarkerCount, map.OccupiedCellCount);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
    }

    [Fact]
    public void TerritoryBoundaries_UpdateAsCellsChangeOwner()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var before = GameQueryHelpers.GetTerritoryBoundaries(_fixture.Context).Count;
        var after = before;

        for (var i = 0; i < 12 && after == before; i++)
        {
            GameQueryHelpers.StepTicks(_fixture.Context, 12);
            after = GameQueryHelpers.GetTerritoryBoundaries(_fixture.Context).Count;
        }

        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);
        Assert.True(before > 0);
        Assert.NotEqual(before, after);
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("TerritoryBoundary_", StringComparison.Ordinal));
    }

    [Fact]
    public void CityHud_RemovesSideListAndMapNumberLabels()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);

        Assert.True(game.MapCity(0).IsVisible());
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("MapCityBack_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name == "CityListPanel");
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("CityButton_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("MapCityLabel_", StringComparison.Ordinal));
    }

    [Fact]
    public void CityOwnershipDiagnostics_UpdateAfterCaptures()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 5, aiPlayers: 4);
        var before = GameQueryHelpers.GetMapState(_fixture.Context);
        var beforeOwnedCount = before.CityOwners.Count(city => city.OwnerId != GameConstants.NeutralPlayerId);

        GameQueryHelpers.StepTicks(_fixture.Context, 80);
        var after = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.Equal(after.CityMarkerCount, after.CityOwners.Count);
        Assert.True(after.CityOwners.Count(city => city.OwnerId != GameConstants.NeutralPlayerId) > beforeOwnedCount);
        Assert.Equal(0, after.DuplicateOccupiedCellCount);
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

        var firstVisuals = GameQueryHelpers.GetUnitVisuals(_fixture.Context)
            .Where(unit => unit.IsInterpolating)
            .ToList();
        UnitVisualStateDto? first = null;
        UnitVisualStateDto? second = null;
        for (var attempt = 0; attempt < 30 && (first == null || second == null); attempt++)
        {
            System.Threading.Thread.Sleep(10);
            var secondVisuals = GameQueryHelpers.GetUnitVisuals(_fixture.Context)
                .Where(unit => unit.IsInterpolating)
                .ToDictionary(unit => unit.UnitId);

            foreach (var candidate in firstVisuals)
            {
                if (secondVisuals.TryGetValue(candidate.UnitId, out var matching) &&
                    VisualPositionChanged(candidate, matching))
                {
                    first = candidate;
                    second = matching;
                    break;
                }
            }

            firstVisuals = secondVisuals.Values.ToList();
        }

        var map = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.Equal(0, map.DuplicateOccupiedCellCount);
        Assert.NotNull(first);
        Assert.NotNull(second);
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
        for (var i = 0; i < 20 && !visuals.Any(unit => unit.IsInterpolating); i++)
        {
            GameQueryHelpers.StepTicks(_fixture.Context, 1);
            visuals = GameQueryHelpers.GetUnitVisuals(_fixture.Context);
        }

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
        var first = GameQueryHelpers.StepToTick(_fixture.Context, 80).Fingerprint;

        GameQueryHelpers.StartMatch(_fixture.Context, seed: 44, aiPlayers: 4);
        var second = GameQueryHelpers.StepToTick(_fixture.Context, 80).Fingerprint;

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
        game.MapCity(3).Click();
        game.DefendButton.Click();
        game.HeavyPreferenceButton.Click();

        var snapshot = GameQueryHelpers.GetSnapshot(_fixture.Context);

        Assert.Equal(3, snapshot.HumanTargetCityId);
        Assert.Equal("Defend", snapshot.HumanDirective);
        Assert.True(snapshot.HumanLightPreference < 0.5);
    }

    [Fact]
    public void RestartCommand_RestartsSameSeed()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 88, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 30);

        var game = new GamePage(_fixture.Context);
        var snapshot = GameQueryHelpers.RestartMatch(_fixture.Context);

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
