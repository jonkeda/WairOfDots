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
        var city = GameQueryHelpers.GetMapInteractionTargets(_fixture.Context)
            .Single(target => target.Kind == "City" && target.Id == 0);
        var hit = GameQueryHelpers.HitTestMap(_fixture.Context, city.NormalizedX, city.NormalizedY);
        Assert.Equal("City", hit.Kind);
        Assert.Equal(0, hit.CityId);
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
        GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);
        var afterPlanning = GameQueryHelpers.GetSnapshot(_fixture.Context);

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
        Assert.Equal(afterPlanning.Players.Count, economies.Count);
        Assert.Equal(afterPlanning.Units.Count(unit => unit.Kind == "Commander"), regions.Count);
        Assert.Equal(afterPlanning.Players.Count, visibility.Count);
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
        var battlefieldRowIndices = expectedOrder
            .Select(standing => visibleElementNames.IndexOf($"StandingBattlefieldRow_{standing.PlayerId}"))
            .ToList();
        var economyRowIndices = expectedOrder
            .Select(standing => visibleElementNames.IndexOf($"StandingEconomyRow_{standing.PlayerId}"))
            .ToList();

        Assert.True(game.StandingsPanel.IsVisible());
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingBattlefieldSection");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingEconomySection");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingBattlefieldHeaderRow");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingScoreHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingCitiesHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingUnitsHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingGeneralHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingEconomyHeaderRow");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingTreasuryHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingTaxHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingUpkeepHeaderLabel");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingPayrollDeficitHeaderLabel");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Score");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Cities");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Units");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "General");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Treasury");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Tax");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Upkeep");
        Assert.Contains(diagnostics.VisibleTexts, text => text == "Deficit");
        Assert.All(battlefieldRowIndices, index => Assert.True(index >= 0));
        Assert.All(economyRowIndices, index => Assert.True(index >= 0));
        Assert.Equal(battlefieldRowIndices.OrderBy(index => index).ToList(), battlefieldRowIndices);
        Assert.Equal(economyRowIndices.OrderBy(index => index).ToList(), economyRowIndices);
        Assert.True(visibleElementNames.IndexOf("StandingEconomySection") > visibleElementNames.IndexOf("StandingBattlefieldSection"));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("StandingLabelColumn_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("StandingStatus_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("StandingSummaryRow_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleTexts, text => text == "in" || text == "out");

        foreach (var standing in expectedOrder)
        {
            Assert.DoesNotContain(diagnostics.VisibleTexts, text => text == standing.Name);
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingBattlefieldPlayerDot_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingEconomyPlayerDot_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingBattlefieldRow_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingEconomyRow_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingScoreBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingCitiesBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingUnitsBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingGeneralBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingTreasuryBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingTaxBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingUpkeepBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleElementNames, name => name == $"StandingPayrollDeficitBar_{standing.PlayerId}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.Score:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.CityCount:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.UnitCount:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.GeneralHealth:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.Resources:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.TaxIncome:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.Upkeep:F0}");
            Assert.Contains(diagnostics.VisibleTexts, text => text == $"{standing.PayrollDeficit:F0}");
        }
    }

    [Fact]
    public void OverlayToggleGroup_ChangesMapOverlayDiagnostics()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        Assert.True(game.OverlayToggleGroup.IsVisible());
        Assert.True(game.OverlayEconomyButton.IsVisible());
        Assert.Equal("Normal", GameQueryHelpers.GetMapOverlay(_fixture.Context).Mode);
        var normalDiagnostics = GameQueryHelpers.GetMapVisualDiagnostics(_fixture.Context);
        Assert.Equal("Normal", normalDiagnostics.OverlayMode);
        Assert.False(normalDiagnostics.EconomyOverlay.IsVisible);
        Assert.Empty(normalDiagnostics.EconomyOverlay.TaxHeatCells);

        game.OverlayEconomyButton.Click();
        game.OverlayMode.AssertTextContains("Economy");
        var economyOverlay = GameQueryHelpers.GetMapOverlay(_fixture.Context);
        var economyDiagnostics = GameQueryHelpers.GetMapVisualDiagnostics(_fixture.Context);

        Assert.Equal("Economy", economyOverlay.Mode);
        Assert.Equal("Economy", economyDiagnostics.OverlayMode);
        Assert.True(economyDiagnostics.EconomyOverlay.IsVisible);
        Assert.True(economyDiagnostics.EconomyOverlay.MaxTaxValue >= 8);
        Assert.NotEmpty(economyDiagnostics.EconomyOverlay.TaxHeatCells);
        Assert.NotEmpty(economyDiagnostics.EconomyOverlay.SpawnCapacity);
        Assert.Equal(economyDiagnostics.EconomyOverlay.PayrollStress.Count, economyDiagnostics.EconomyOverlay.PayrollStress.Select(stress => stress.PlayerId).Distinct().Count());
        Assert.All(economyDiagnostics.EconomyOverlay.TaxHeatCells, cell => Assert.InRange(cell.HeatRatio, 0, 1));
        Assert.All(economyDiagnostics.EconomyOverlay.SpawnCapacity, city => Assert.True(city.Capacity >= 0));

        GameQueryHelpers.StepTicks(_fixture.Context, 24);
        var afterCaptures = GameQueryHelpers.GetMapVisualDiagnostics(_fixture.Context);
        Assert.True(afterCaptures.EconomyOverlay.IsVisible);
        Assert.All(afterCaptures.EconomyOverlay.TerritorySwings, swing => Assert.InRange(swing.AgeTicks, 0, 8));

        game.OverlayCommandButton.Click();
        Assert.Equal("Command", GameQueryHelpers.GetMapOverlay(_fixture.Context).Mode);
        game.OverlayVisibilityButton.Click();
        Assert.Equal("Visibility", GameQueryHelpers.GetMapOverlay(_fixture.Context).Mode);
        game.OverlayAiDebugButton.Click();
        Assert.Equal("AiDebug", GameQueryHelpers.GetMapOverlay(_fixture.Context).Mode);
        game.OverlayNormalButton.Click();
        Assert.Equal("Normal", GameQueryHelpers.GetMapOverlay(_fixture.Context).Mode);
    }

    [Fact]
    public void AiOnly_EconomyStandingsAndOverlayControls_DoNotOverlapHudLayout()
    {
        GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var commandPanel = _fixture.Context.GetElementState("CommandPanel");
        var mapCanvas = _fixture.Context.GetElementState("MapCanvas");
        var overlayGroup = _fixture.Context.GetElementState("OverlayToggleGroup");
        var standingsPanel = _fixture.Context.GetElementState("StandingsPanel");
        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);

        Assert.True(commandPanel.Exists);
        Assert.True(mapCanvas.Exists);
        Assert.True(overlayGroup.Exists);
        Assert.True(standingsPanel.Exists);
        Assert.True(overlayGroup.IsVisible);
        Assert.True(standingsPanel.IsVisible);
        Assert.False(mapCanvas.Bounds.IsEmpty);
        Assert.False(overlayGroup.Bounds.IsEmpty);
        Assert.False(standingsPanel.Bounds.IsEmpty);
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "OverlayToggleGroup");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingBattlefieldSection");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingEconomySection");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingTreasuryBar_0");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingTaxBar_0");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingUpkeepBar_0");
        Assert.Contains(diagnostics.VisibleElementNames, name => name == "StandingPayrollDeficitBar_0");

        var visibleNames = diagnostics.VisibleElementNames.ToList();
        var overlayIndex = visibleNames.IndexOf("OverlayToggleGroup");
        var spectatorIndex = visibleNames.IndexOf("SpectatorControlsPanel");
        var standingsIndex = visibleNames.IndexOf("StandingsPanel");
        Assert.True(overlayIndex >= 0);
        Assert.True(spectatorIndex > overlayIndex);
        Assert.True(standingsIndex > spectatorIndex);
        Assert.True(visibleNames.IndexOf("StandingEconomySection") > visibleNames.IndexOf("StandingBattlefieldSection"));
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
        var mapTargets = GameQueryHelpers.GetMapInteractionTargets(_fixture.Context);
        Assert.Equal(map.CityMarkerCount, mapTargets.Count(target => target.Kind == "City"));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("MapCity_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("MapCityBack_", StringComparison.Ordinal));
        Assert.True(map.UnitMarkerCount >= 5);
        Assert.Equal(10, map.CommanderMarkerCount);
        Assert.Equal(5, map.GeneralMarkerCount);
        Assert.Equal(map.UnitMarkerCount, map.OccupiedCellCount);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
    }

    [Fact]
    public void TerritoryBoundaries_UpdateAsCellsChangeOwner()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var before = GameQueryHelpers.GetTerritoryBoundaries(_fixture.Context).Count;
        var stepped = GameQueryHelpers.StepUntil(
            _fixture.Context,
            "TerritoryBoundaryCountNotEqual",
            before.ToString(),
            maxTicks: 144,
            stepSize: 12);
        var after = GameQueryHelpers.GetTerritoryBoundaries(_fixture.Context).Count;

        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);
        Assert.True(before > 0);
        Assert.True(stepped.Satisfied, stepped.Detail);
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

        Assert.True(game.MapCanvas.IsVisible());
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("MapCity_", StringComparison.Ordinal));
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

        var stepped = GameQueryHelpers.StepUntil(
            _fixture.Context,
            "CityOwnedCountGreaterThan",
            beforeOwnedCount.ToString(),
            maxTicks: 120,
            stepSize: 4);
        var after = GameQueryHelpers.GetMapState(_fixture.Context);

        Assert.True(stepped.Satisfied, stepped.Detail);
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

        var stepped = GameQueryHelpers.StepUntil(
            _fixture.Context,
            "AnyDiagonalVisualTarget",
            maxTicks: 30);
        var visuals = GameQueryHelpers.GetUnitVisuals(_fixture.Context);

        var map = GameQueryHelpers.GetMapState(_fixture.Context);
        Assert.Equal(0, map.DuplicateOccupiedCellCount);
        Assert.True(stepped.Satisfied, stepped.Detail);
        Assert.Contains(visuals, IsDiagonalVisualTarget);
    }

    [Fact]
    public void AiOnly_FastSpeedKeepsSmoothMovementDiagnosticsAndSingleOccupancy()
    {
        GameQueryHelpers.StartAiOnly(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.SetSimulationSpeed(_fixture.Context, 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);
        GameQueryHelpers.StepTicks(_fixture.Context, 1);

        var visuals = GameQueryHelpers.GetUnitVisuals(_fixture.Context);
        if (!visuals.Any(unit => unit.IsInterpolating))
        {
            var stepped = GameQueryHelpers.StepUntil(
                _fixture.Context,
                "AnyInterpolatingUnit",
                maxTicks: 20);
            Assert.True(stepped.Satisfied, stepped.Detail);
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

        var stepped = GameQueryHelpers.StepUntil(
            _fixture.Context,
            "ActiveCombat",
            maxTicks: 240,
            stepSize: 4);

        Assert.True(stepped.Satisfied, stepped.Detail);
    }

    [Fact]
    public void CombatVisualDiagnostics_ReportDamageAndDeathMarkers()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);

        var stepped = GameQueryHelpers.StepUntil(
            _fixture.Context,
            "RecentDeathMarker",
            maxTicks: 900,
            stepSize: 4);
        var diagnostics = GameQueryHelpers.GetMapVisualDiagnostics(_fixture.Context);

        Assert.True(stepped.Satisfied, stepped.Detail);
        Assert.NotEmpty(diagnostics.CombatMarkers);
        Assert.NotEmpty(diagnostics.DeathMarkers);
        Assert.True(diagnostics.CombatMarkers.Count <= 96, $"combat marker count was {diagnostics.CombatMarkers.Count}");
        Assert.True(diagnostics.DeathMarkers.Count <= 24, $"death marker count was {diagnostics.DeathMarkers.Count}");
        Assert.Contains(diagnostics.CombatMarkers, marker => marker.MarkerKind is "DamageFlash" or "LeaderThreatPulse" or "FatalHitFlash");
        Assert.All(diagnostics.DeathMarkers, marker => Assert.Equal("Hidden", marker.MarkerKind));
        Assert.All(diagnostics.CombatMarkers, marker =>
        {
            Assert.InRange(marker.MarkerX, 0, 1);
            Assert.InRange(marker.MarkerY, 0, 1);
            Assert.True(marker.Damage > 0);
        });
        Assert.All(diagnostics.DeathMarkers, marker =>
        {
            Assert.InRange(marker.MarkerX, 0, 1);
            Assert.InRange(marker.MarkerY, 0, 1);
            Assert.InRange(marker.AgeTicks, 0, 4);
        });
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
        var city = GameQueryHelpers.GetMapInteractionTargets(_fixture.Context)
            .Single(target => target.Kind == "City" && target.Id == 3);
        var click = GameQueryHelpers.ClickMap(_fixture.Context, city.NormalizedX, city.NormalizedY);
        game.DefendButton.Click();
        game.HeavyPreferenceButton.Click();

        var snapshot = GameQueryHelpers.GetSnapshot(_fixture.Context);

        Assert.Equal("City", click.Hit.Kind);
        Assert.Equal(3, click.Hit.CityId);
        Assert.Equal(3, snapshot.HumanTargetCityId);
        Assert.Equal("Defend", snapshot.HumanDirective);
        Assert.True(snapshot.HumanLightPreference < 0.5);
    }

    [Fact]
    public void MapHitTesting_ClicksUnitsCitiesAndCellsWithoutUiObjectButtons()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var game = new GamePage(_fixture.Context);
        game.AssertLoaded(true);

        var targets = GameQueryHelpers.GetMapInteractionTargets(_fixture.Context);
        var unitTarget = targets.First(target => target.Kind == "Unit");
        var unitClick = GameQueryHelpers.ClickMap(_fixture.Context, unitTarget.NormalizedX, unitTarget.NormalizedY);

        Assert.Equal("Unit", unitClick.Hit.Kind);
        Assert.Equal(unitTarget.Id, unitClick.Hit.UnitId);
        game.Target.AssertTextContains("Selected");
        game.Target.AssertTextContains($"#{unitTarget.Id}");
        game.Target.AssertTextContains("Command");

        var cityTarget = targets.Single(target => target.Kind == "City" && target.Id == 3);
        var cityClick = GameQueryHelpers.ClickMap(_fixture.Context, cityTarget.NormalizedX, cityTarget.NormalizedY);

        Assert.Equal("City", cityClick.Hit.Kind);
        Assert.Equal(3, cityClick.Hit.CityId);
        Assert.Equal(3, cityClick.Snapshot.HumanTargetCityId);

        var cellHit = GameQueryHelpers.HitTestMap(_fixture.Context, 0.01, 0.01);
        Assert.Equal("Cell", cellHit.Kind);

        var diagnostics = GameQueryHelpers.GetUiDiagnostics(_fixture.Context);
        Assert.DoesNotContain(diagnostics.VisibleElementNames, name => name.StartsWith("MapCity_", StringComparison.Ordinal));
    }

    [Fact]
    public void CommandChains_QueryDownLineAttackOrders()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);

        var chains = GameQueryHelpers.GetCommandChains(_fixture.Context);
        var human = chains.Single(chain => chain.PlayerId == GameConstants.HumanPlayerId);

        Assert.NotEmpty(human.Commanders);
        Assert.All(human.Commanders, commander =>
        {
            Assert.NotNull(commander.GeneralCommand);
            Assert.Equal("AttackRegion", commander.GeneralCommand!.CommandType);
            Assert.NotNull(commander.GeneralCommand.TargetCellX);
            Assert.NotNull(commander.GeneralCommand.TargetCellY);
            Assert.All(commander.AssignedUnits, unit =>
            {
                Assert.NotNull(unit.Command);
                Assert.Equal("AdvanceToCell", unit.Command!.CommandType);
            });
        });
    }

    [Fact]
    public void ReserveGroupCommands_AreQueryableAndShownForGeneral()
    {
        var snapshot = GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        var commander = snapshot.Units.First(unit =>
            unit.PlayerId == GameConstants.HumanPlayerId &&
            unit.Kind == nameof(UnitKind.Commander) &&
            unit.CommanderNumber == 1);
        var general = snapshot.Units.First(unit =>
            unit.PlayerId == GameConstants.HumanPlayerId &&
            unit.Kind == nameof(UnitKind.General));

        GameQueryHelpers.RecallCommanderGroup(_fixture.Context, commander.Id, groupSize: 1);
        var recallChain = GameQueryHelpers.GetCommandChains(_fixture.Context)
            .Single(chain => chain.PlayerId == GameConstants.HumanPlayerId);
        var recall = Assert.Single(recallChain.ReserveCommands, command =>
            command.CommandType == "RecallCommanderGroupToReserve");

        Assert.NotNull(recall.TargetUnitId);
        Assert.Equal(commander.CommanderNumber, recall.CommanderNumber);
        Assert.Contains("manual-recall", recall.ReasonCode);

        var game = new GamePage(_fixture.Context);
        var generalTarget = GameQueryHelpers.GetMapInteractionTargets(_fixture.Context)
            .Single(target => target.Kind == "Unit" && target.Id == general.Id);
        GameQueryHelpers.ClickMap(_fixture.Context, generalTarget.NormalizedX, generalTarget.NormalizedY);
        game.Target.AssertTextContains("Reserve");
        game.Target.AssertTextContains("RecallCommanderGroupToReserve");

        GameQueryHelpers.AssignReserveGroup(_fixture.Context, commander.Id, groupSize: 1);
        var assignChain = GameQueryHelpers.GetCommandChains(_fixture.Context)
            .Single(chain => chain.PlayerId == GameConstants.HumanPlayerId);
        var assignment = Assert.Single(assignChain.ReserveCommands, command =>
            command.CommandType == "AssignReserveGroup");

        Assert.NotNull(assignment.TargetUnitId);
        Assert.Equal(commander.CommanderNumber, assignment.CommanderNumber);
        Assert.Contains("manual", assignment.ReasonCode);
    }

    [Fact]
    public void MapVisualDiagnostics_ExposeUnitMoraleRolesTargetsAndEvents()
    {
        GameQueryHelpers.StartMatch(_fixture.Context, seed: 31, aiPlayers: 4);
        GameQueryHelpers.StepTicks(_fixture.Context, 12);
        GameQueryHelpers.TogglePause(_fixture.Context);

        var diagnostics = GameQueryHelpers.GetMapVisualDiagnostics(_fixture.Context);
        var unitIds = diagnostics.Units.Select(unit => unit.UnitId).ToList();

        Assert.NotEmpty(diagnostics.Units);
        Assert.Equal(unitIds.OrderBy(id => id).ToList(), unitIds);
        Assert.All(diagnostics.Units, unit =>
        {
            Assert.InRange(unit.HealthRatio, 0, 1);
            Assert.InRange(unit.Morale, 0, 1);
            Assert.InRange(unit.MarkerX, 0, 1);
            Assert.InRange(unit.MarkerY, 0, 1);
            Assert.NotEmpty(unit.RoleFlags);
            Assert.False(string.IsNullOrWhiteSpace(unit.VisualState));
        });
        Assert.Contains(diagnostics.Units, unit => unit.IsCommander && unit.CommanderNumber == 1 && unit.CenterLabel == "1" && unit.RoleFlags.Contains("Commander"));
        Assert.Contains(diagnostics.Units, unit => !unit.IsLeader && unit.AssignedCommanderNumber == 1 && unit.CenterLabel == "1" && unit.RoleFlags.Contains("Commander 1"));
        Assert.Contains(diagnostics.Units, unit => unit.IsReserve && unit.HasReservePip && string.IsNullOrEmpty(unit.CenterLabel) && unit.RoleFlags.Contains("Reserve"));
        Assert.Contains(diagnostics.Units, unit => unit.IsGeneral && unit.RoleFlags.Contains("General"));
        Assert.Contains(diagnostics.Units, unit => unit.TargetCityId.HasValue || unit.TargetRegionId.HasValue);
        Assert.NotEmpty(diagnostics.RecentEvents);

        var candidate = diagnostics.Units.First(unit => !unit.IsLeader);
        var routed = GameQueryHelpers.SetUnitMorale(_fixture.Context, candidate.UnitId, 0.10)
            .Units.Single(unit => unit.UnitId == candidate.UnitId);
        var routRisk = GameQueryHelpers.SetUnitMorale(_fixture.Context, candidate.UnitId, 0.25)
            .Units.Single(unit => unit.UnitId == candidate.UnitId);

        Assert.Equal("Routed", routed.MoraleBand);
        Assert.True(routed.IsRouted);
        Assert.False(routed.IsRoutRisk);
        Assert.Equal("RoutedBrokenRing", routed.VisualState);
        Assert.Equal("RoutRisk", routRisk.MoraleBand);
        Assert.False(routRisk.IsRouted);
        Assert.True(routRisk.IsRoutRisk);
        Assert.Equal("RoutRiskSegmentedArc", routRisk.VisualState);
        Assert.NotEqual(routed.VisualState, routRisk.VisualState);
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
