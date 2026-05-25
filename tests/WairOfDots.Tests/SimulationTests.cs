using WairOfDots.Core;

namespace WairOfDots.Tests;

public class SimulationTests
{
    [Fact]
    public void NewSimulation_WaitsUntilStarted()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 42));

        simulation.Step(20);

        Assert.Equal(0, simulation.Tick);
        Assert.Equal(MatchPhase.Menu, simulation.Phase);
    }

    [Fact]
    public void SameSeed_ProducesSameFingerprintAfterTicks()
    {
        var left = GameSimulation.Create(new GameSettings(Seed: 42));
        var right = GameSimulation.Create(new GameSettings(Seed: 42));

        left.Start();
        right.Start();
        left.Step(96);
        right.Step(96);

        Assert.Equal(left.CreateFingerprint(), right.CreateFingerprint());
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentFingerprintsAfterAiPlanning()
    {
        var left = GameSimulation.Create(new GameSettings(Seed: 42));
        var right = GameSimulation.Create(new GameSettings(Seed: 99));

        left.Start();
        right.Start();
        left.Step(96);
        right.Step(96);

        Assert.NotEqual(left.CreateFingerprint(), right.CreateFingerprint());
    }

    [Fact]
    public void Pause_FreezesTickAndFingerprint()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 7));
        simulation.Start();
        simulation.Step(24);
        simulation.Pause();
        var tick = simulation.Tick;
        var fingerprint = simulation.CreateFingerprint();

        simulation.Step(30);

        Assert.Equal(tick, simulation.Tick);
        Assert.Equal(fingerprint, simulation.CreateFingerprint());
    }

    [Fact]
    public void HumanCommand_UpdatesDirectiveTargetAndUnitPreference()
    {
        var simulation = GameSimulation.Create(new GameSettings());
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Defend, TargetCityId: 3, LightPreference: 0.25));

        var snapshot = simulation.CreateSnapshot();

        Assert.Equal("Defend", snapshot.HumanDirective);
        Assert.Equal(3, snapshot.HumanTargetCityId);
        Assert.Equal(0.25, snapshot.HumanLightPreference, precision: 2);
    }

    [Fact]
    public void HumanMode_CreatesOneHumanAndConfiguredAiPlayers()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4));

        Assert.True(simulation.HasHumanPlayer);
        Assert.Equal(GameMode.HumanVsAi, simulation.Settings.Mode);
        Assert.Single(simulation.Players, player => player.Kind == PlayerKind.Human);
        Assert.Equal(4, simulation.Players.Count(player => player.Kind == PlayerKind.Ai));
        Assert.Equal("Human", simulation.Players[GameConstants.HumanPlayerId].Name);
    }

    [Fact]
    public void AiOnly_CreatesNoHumanAndPlayerZeroIsAi()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4, Mode: GameMode.AiOnly));

        Assert.False(simulation.HasHumanPlayer);
        Assert.Equal(GameMode.AiOnly, simulation.Settings.Mode);
        Assert.DoesNotContain(simulation.Players, player => player.Kind == PlayerKind.Human);
        Assert.Equal(PlayerKind.Ai, simulation.Players[0].Kind);
        Assert.Equal("AI 0", simulation.Players[0].Name);
    }

    [Fact]
    public void AiOnly_GivesEveryAiAUniqueGenomeId()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4, Mode: GameMode.AiOnly));

        Assert.Equal(4, simulation.Players.Select(player => player.GenomeId).Distinct().Count());
        Assert.All(simulation.Players, player => Assert.StartsWith("neat-", player.GenomeId));
    }

    [Fact]
    public void AiOnly_SameSeedProducesSameFingerprint()
    {
        var left = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4, Mode: GameMode.AiOnly));
        var right = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4, Mode: GameMode.AiOnly));

        left.Start();
        right.Start();
        left.Step(96);
        right.Step(96);

        Assert.Equal(left.CreateFingerprint(), right.CreateFingerprint());
    }

    [Fact]
    public void AiOnly_DifferentSeedsProduceDifferentFingerprintsAfterPlanning()
    {
        var left = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4, Mode: GameMode.AiOnly));
        var right = GameSimulation.Create(new GameSettings(Seed: 99, AiPlayers: 4, Mode: GameMode.AiOnly));

        left.Start();
        right.Start();
        left.Step(96);
        right.Step(96);

        Assert.NotEqual(left.CreateFingerprint(), right.CreateFingerprint());
    }

    [Fact]
    public void AiOnly_AdvancesAndEndsWithConsistentCityOwnership()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 12, AiPlayers: 4, MatchLengthTicks: 120, Mode: GameMode.AiOnly));
        simulation.Start();

        simulation.Step(130);
        var eliminatedPlayerIds = simulation.Players
            .Where(player => player.IsEliminated)
            .Select(player => player.Id)
            .ToHashSet();

        Assert.True(simulation.Tick > 0);
        Assert.DoesNotContain(simulation.Cities, city => eliminatedPlayerIds.Contains(city.OwnerId));
        Assert.Equal(MatchPhase.Ended, simulation.Phase);
        Assert.NotNull(simulation.WinnerId);
    }

    [Fact]
    public void Snapshot_EndsRunningMatchWhenOnlyOnePlayerRemains()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 12, AiPlayers: 4, Mode: GameMode.AiOnly));
        simulation.Start();

        foreach (var general in simulation.Units.Where(unit => unit.PlayerId != 0 && unit.Kind == UnitKind.General))
            general.Health = 0;

        var snapshot = simulation.CreateSnapshot();

        Assert.Equal(MatchPhase.Ended, simulation.Phase);
        Assert.Equal("Ended", snapshot.Phase);
        Assert.Equal(0, snapshot.WinnerId);
        Assert.Single(snapshot.Players, player => !player.IsEliminated);
    }

    [Fact]
    public void AiOnly_HumanCommandDoesNotChangeAiPlan()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4, Mode: GameMode.AiOnly));
        var directive = simulation.Players[0].Directive;
        var target = simulation.Players[0].TargetCityId;
        var preference = simulation.Players[0].LightPreference;

        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Defend, TargetCityId: 3, LightPreference: 0.25));

        Assert.Equal(directive, simulation.Players[0].Directive);
        Assert.Equal(target, simulation.Players[0].TargetCityId);
        Assert.Equal(preference, simulation.Players[0].LightPreference);
    }

    [Fact]
    public void AiPlanning_EmitsTelemetryWithDifferentGenomeIds()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 123, AiPlayers: 4));
        simulation.Start();

        simulation.Step(12);

        var aiPlayers = simulation.Players.Where(player => player.Kind == PlayerKind.Ai).ToList();
        Assert.Equal(4, aiPlayers.Select(player => player.GenomeId).Distinct().Count());
        Assert.NotEmpty(simulation.Telemetry);
    }

    [Fact]
    public void AiHoldDecay_ForcesAttackAfterRepeatedQuietHolds()
    {
        var first = GameSimulation.ResolveHoldDecay(PlayerDirective.Hold, previousConsecutiveHoldPlans: 0, hasNearbyEnemyPressure: false);
        var second = GameSimulation.ResolveHoldDecay(PlayerDirective.Hold, first.ConsecutiveHoldPlans, hasNearbyEnemyPressure: false);
        var third = GameSimulation.ResolveHoldDecay(PlayerDirective.Hold, second.ConsecutiveHoldPlans, hasNearbyEnemyPressure: false);
        var pressured = GameSimulation.ResolveHoldDecay(PlayerDirective.Hold, previousConsecutiveHoldPlans: 2, hasNearbyEnemyPressure: true);
        var attack = GameSimulation.ResolveHoldDecay(PlayerDirective.Attack, previousConsecutiveHoldPlans: 2, hasNearbyEnemyPressure: false);

        Assert.Equal(PlayerDirective.Hold, first.Directive);
        Assert.Equal(1, first.ConsecutiveHoldPlans);
        Assert.Equal(PlayerDirective.Hold, second.Directive);
        Assert.Equal(2, second.ConsecutiveHoldPlans);
        Assert.Equal(PlayerDirective.Attack, third.Directive);
        Assert.True(third.Decayed);
        Assert.Equal(0, third.ConsecutiveHoldPlans);
        Assert.Equal(PlayerDirective.Hold, pressured.Directive);
        Assert.Equal(3, pressured.ConsecutiveHoldPlans);
        Assert.Equal(PlayerDirective.Attack, attack.Directive);
        Assert.Equal(0, attack.ConsecutiveHoldPlans);
    }

    [Fact]
    public void DefaultMap_CreatesPassableGridWithBlockedWater()
    {
        var map = GameMapFactory.CreateDefault(playerCount: 5);

        Assert.Equal(31, map.Grid.Width);
        Assert.Equal(31, map.Grid.Height);
        Assert.Contains(map.Grid.Cells, cell => cell.Terrain == TerrainKind.Water && !cell.IsPassable);
        Assert.Contains(map.Grid.Cells, cell => cell.Terrain == TerrainKind.Road && cell.IsPassable);
        Assert.Contains(map.Grid.Cells, cell => cell.Terrain == TerrainKind.Forest && cell.IsPassable);
        Assert.All(map.Cities, city => Assert.True(map.Grid.IsPassable(city.GridPosition)));
    }

    [Fact]
    public void DefaultSetup_HasSingleOccupancyForEveryUnit()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));

        var duplicateCells = simulation.Units
            .Where(unit => unit.IsAlive)
            .GroupBy(unit => unit.Cell)
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicateCells);
    }

    [Fact]
    public void DefaultSetup_CitiesHaveAtMostOneUnitAndLeadersCountAsUnits()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));

        Assert.All(simulation.Cities, city =>
            Assert.True(simulation.Units.Count(unit => unit.IsAlive && unit.Cell == city.GridPosition) <= 1));
        Assert.Equal(5, simulation.Units.Count(unit => unit.Kind == UnitKind.Commander));
        Assert.Equal(5, simulation.Units.Count(unit => unit.Kind == UnitKind.General));
        Assert.All(simulation.Players, player =>
            Assert.Contains(simulation.Units, unit => unit.PlayerId == player.Id && unit.Kind == UnitKind.General && unit.Cell == simulation.Cities[player.HomeCityId].GridPosition));
    }

    [Fact]
    public void Production_CreatesUnitOnAdjacentEmptyCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var beforeIds = simulation.Units.Select(unit => unit.Id).ToHashSet();
        simulation.Start();

        simulation.Step(6);

        var created = simulation.Units.Where(unit => !beforeIds.Contains(unit.Id)).ToList();
        Assert.NotEmpty(created);
        Assert.All(created, unit =>
        {
            Assert.True(unit.Kind is UnitKind.Infantry or UnitKind.Tank);
            Assert.Contains(simulation.Cities, city =>
                city.OwnerId == unit.PlayerId &&
                unit.Cell.ManhattanDistanceTo(city.GridPosition) == 1);
        });
    }

    [Fact]
    public void Production_PausesWhenAdjacentCellsAreOccupiedOrBlocked()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        foreach (var player in simulation.Players.Where(player => player.Id != GameConstants.HumanPlayerId))
            player.IsEliminated = true;

        var home = simulation.Cities[simulation.Players[GameConstants.HumanPlayerId].HomeCityId];
        var outputCells = AdjacentCells(home.GridPosition)
            .Where(point => simulation.Grid.IsPassable(point) &&
                !simulation.Cities.Any(city => city.GridPosition == point))
            .ToList();
        var blockers = simulation.Units
            .Where(unit => unit.PlayerId == GameConstants.HumanPlayerId && unit.Kind != UnitKind.General)
            .Take(outputCells.Count)
            .ToList();

        Assert.Equal(outputCells.Count, blockers.Count);
        for (var i = 0; i < outputCells.Count; i++)
            PlaceUnit(simulation, blockers[i], outputCells[i]);

        var beforeIds = simulation.Units.Select(unit => unit.Id).ToHashSet();
        simulation.Players[GameConstants.HumanPlayerId].Resources = 20;
        simulation.Start();

        simulation.Step(6);

        Assert.DoesNotContain(simulation.Units, unit =>
            unit.PlayerId == GameConstants.HumanPlayerId &&
            !beforeIds.Contains(unit.Id));
    }

    [Fact]
    public void Dispatch_CreatesGridPathForMovingUnit()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();

        simulation.Step(12);

        var unit = Assert.Single(simulation.Units, unit =>
            unit.PlayerId == GameConstants.HumanPlayerId &&
            unit.Kind == UnitKind.General &&
            unit.IsMoving);
        Assert.True(unit.Path.Count > 2);
        Assert.Equal(unit.Cell, unit.Path[0]);
        Assert.Equal(simulation.Cities[unit.TargetCityId!.Value].GridPosition, unit.Path[^1]);
        Assert.All(unit.Path, point => Assert.True(simulation.Grid.IsPassable(point)));
    }

    [Fact]
    public void Dispatch_CanCreateDiagonalPathStep()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.General);
        var enemyGeneral = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, enemyGeneral);
        var targetCity = simulation.Cities[0];
        var start = FindDiagonalApproachToCell(simulation, targetCity.GridPosition);
        PlaceUnit(simulation, mover, start);
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: targetCity.Id));
        simulation.Start();

        simulation.Step(12);

        Assert.True(mover.Path.Count >= 2);
        Assert.True(IsDiagonalStep(mover.Path[0], mover.Path[1]));
        Assert.Equal(targetCity.GridPosition, mover.Path[^1]);
    }

    [Fact]
    public void MovingUnits_AdvanceAcrossGridCells()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();
        simulation.Step(12);
        var unit = simulation.Units.First(unit =>
            unit.PlayerId == GameConstants.HumanPlayerId &&
            unit.IsMoving);
        var unitId = unit.Id;
        var pathIndex = unit.PathIndex;
        var position = unit.CurrentPosition;

        simulation.Step(1);

        var moved = simulation.Units.Single(unit => unit.Id == unitId);
        Assert.True(moved.PathIndex > pathIndex || moved.CurrentPosition.DistanceTo(position) > 0.01);
    }

    [Fact]
    public void CompletedGridStep_RecordsVisualMovementBetweenCells()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var general = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, general);
        var step = FindAdjacentMove(simulation, mover, cell => cell.MoveCost <= mover.Speed);
        PlaceUnit(simulation, mover, step.Start);
        mover.Path = [step.Start, step.Destination];
        var startPosition = simulation.Grid.ToMapPoint(step.Start);
        var destinationPosition = simulation.Grid.ToMapPoint(step.Destination);
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(step.Destination, mover.Cell);
        Assert.Equal(step.Start, mover.VisualFromCell);
        Assert.Equal(step.Destination, mover.VisualToCell);
        Assert.Equal(startPosition, mover.VisualFromPosition);
        Assert.Equal(destinationPosition, mover.VisualToPosition);
        Assert.Equal(simulation.Tick, mover.VisualMoveTick);
        Assert.True(mover.HasVisualMovement);
    }

    [Fact]
    public void SlowGridStep_ChangesVisualPositionBeforeAuthoritativeCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var general = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, general);
        var step = FindAdjacentMove(simulation, mover, cell => cell.MoveCost > mover.Speed);
        PlaceUnit(simulation, mover, step.Start);
        mover.Path = [step.Start, step.Destination];
        var startPosition = simulation.Grid.ToMapPoint(step.Start);
        var destinationPosition = simulation.Grid.ToMapPoint(step.Destination);
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(step.Start, mover.Cell);
        Assert.InRange(mover.StepProgress, 0.0001, 0.9999);
        Assert.Equal(step.Start, mover.VisualFromCell);
        Assert.Equal(step.Destination, mover.VisualToCell);
        Assert.True(mover.CurrentPosition.DistanceTo(startPosition) > 0.01);
        Assert.True(mover.CurrentPosition.DistanceTo(destinationPosition) > 0.01);
        Assert.Equal(mover.CurrentPosition, mover.VisualToPosition);
        Assert.True(mover.HasVisualMovement);
    }

    [Fact]
    public void DiagonalGridStep_UsesSqrtTwoMovementCost()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var playerGeneral = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        var enemyGeneral = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, playerGeneral, enemyGeneral);
        var step = FindOpenDiagonalMove(simulation);
        PlaceUnit(simulation, mover, step.Start);
        mover.Path = [step.Start, step.Destination];
        var expectedProgress = mover.Speed / (simulation.Grid.MoveCost(step.Destination) * Math.Sqrt(2));
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(step.Start, mover.Cell);
        Assert.Equal(expectedProgress, mover.StepProgress, precision: 6);
        Assert.Equal(step.Destination, mover.VisualToCell);
    }

    [Fact]
    public void DiagonalGridStep_IsBlockedByCornerTerrain()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var playerGeneral = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        var enemyGeneral = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, playerGeneral, enemyGeneral);
        var step = FindTerrainBlockedDiagonalMove(simulation);
        PlaceUnit(simulation, mover, step.Start);
        mover.Path = [step.Start, step.Destination];
        var startPosition = simulation.Grid.ToMapPoint(step.Start);
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(step.Start, mover.Cell);
        Assert.False(mover.IsMoving);
        Assert.Equal(startPosition, mover.CurrentPosition);
        Assert.Equal(step.Start, mover.VisualToCell);
    }

    [Fact]
    public void DiagonalGridStep_IsBlockedByOccupiedCornerCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var blocker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var playerGeneral = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        var enemyGeneral = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, blocker, playerGeneral, enemyGeneral);
        var step = FindOpenDiagonalMove(simulation);
        var sideCell = DiagonalSideCells(step.Start, step.Destination).First();
        PlaceUnit(simulation, mover, step.Start);
        PlaceUnit(simulation, blocker, sideCell);
        mover.Path = [step.Start, step.Destination];
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(step.Start, mover.Cell);
        Assert.False(mover.IsMoving);
        AssertNoDuplicateOccupiedCells(simulation);
    }

    [Fact]
    public void DiagonalGridStep_IsBlockedByReservedCornerCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var reserver = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var playerGeneral = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        var enemyGeneral = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, reserver, playerGeneral, enemyGeneral);
        var step = FindOpenDiagonalMove(simulation);
        var reservedSide = DiagonalSideCells(step.Start, step.Destination).First();
        var reservationStart = FindCardinalApproachToCell(
            simulation,
            reservedSide,
            excluded: new HashSet<GridPoint> { step.Start, step.Destination });
        PlaceUnit(simulation, mover, step.Start);
        PlaceUnit(simulation, reserver, reservationStart);
        mover.Path = [step.Start, step.Destination];
        reserver.Path = [reservationStart, reservedSide];
        reserver.StepProgress = 0.1;
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(step.Start, mover.Cell);
        Assert.False(mover.IsMoving);
        AssertNoDuplicateOccupiedCells(simulation);
    }

    [Fact]
    public void BlockedGridStep_ClearsVisualMovementAndKeepsAuthoritativeCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var blocker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var general = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, blocker, general);
        var step = FindAdjacentPair(simulation, mover, blocker);
        PlaceUnit(simulation, mover, step.Attacker);
        PlaceUnit(simulation, blocker, step.Defender);

        var startPosition = simulation.Grid.ToMapPoint(step.Attacker);
        var blockedPosition = simulation.Grid.ToMapPoint(step.Defender);
        var midway = new MapPoint(
            (startPosition.X + blockedPosition.X) / 2,
            (startPosition.Y + blockedPosition.Y) / 2);
        mover.Path = [step.Attacker, step.Defender];
        mover.StepProgress = 0.5;
        mover.CurrentPosition = midway;
        mover.VisualFromCell = step.Attacker;
        mover.VisualToCell = step.Defender;
        mover.VisualFromPosition = startPosition;
        mover.VisualToPosition = midway;
        mover.VisualMoveTick = 0;
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(step.Attacker, mover.Cell);
        Assert.False(mover.IsMoving);
        Assert.Equal(0, mover.StepProgress);
        Assert.Equal(startPosition, mover.CurrentPosition);
        Assert.Equal(step.Attacker, mover.VisualFromCell);
        Assert.Equal(step.Attacker, mover.VisualToCell);
        Assert.Equal(startPosition, mover.VisualFromPosition);
        Assert.Equal(startPosition, mover.VisualToPosition);
        Assert.Equal(-1, mover.VisualMoveTick);
    }

    [Fact]
    public void AdjacentCombat_ClearsPartialVisualMovementAtAuthoritativeCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        var playerGeneral = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        var defenderGeneral = simulation.Units.First(unit => unit.PlayerId == defender.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, defender, playerGeneral, defenderGeneral);
        var step = FindAdjacentPair(simulation, mover, defender);
        PlaceUnit(simulation, mover, step.Attacker);
        PlaceUnit(simulation, defender, step.Defender);

        var startPosition = simulation.Grid.ToMapPoint(step.Attacker);
        var defenderPosition = simulation.Grid.ToMapPoint(step.Defender);
        var midway = new MapPoint(
            (startPosition.X + defenderPosition.X) / 2,
            (startPosition.Y + defenderPosition.Y) / 2);
        mover.Path = [step.Attacker, step.Defender];
        mover.StepProgress = 0.5;
        mover.CurrentPosition = midway;
        mover.VisualFromCell = step.Attacker;
        mover.VisualToCell = step.Defender;
        mover.VisualFromPosition = startPosition;
        mover.VisualToPosition = midway;
        mover.VisualMoveTick = 0;
        var defenderHealth = defender.Health;
        simulation.Start();

        simulation.Step(1);

        Assert.True(defender.Health < defenderHealth);
        Assert.Equal(step.Attacker, mover.Cell);
        Assert.False(mover.IsMoving);
        Assert.Equal(startPosition, mover.CurrentPosition);
        Assert.Equal(step.Attacker, mover.VisualFromCell);
        Assert.Equal(step.Attacker, mover.VisualToCell);
        Assert.Equal(-1, mover.VisualMoveTick);
    }

    [Fact]
    public void AdjacentEnemyUnits_AttackEachOther()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        DisableOtherUnits(simulation, attacker, defender);
        var pair = FindAdjacentPair(simulation, attacker, defender);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, defender, pair.Defender);
        var defenderHealth = defender.Health;
        simulation.Start();

        simulation.Step(1);

        Assert.True(defender.Health < defenderHealth);
    }

    [Fact]
    public void RoutedUnit_DoesNotAttackAdjacentEnemy()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        DisableOtherUnits(simulation, attacker, defender);
        var pair = FindAdjacentPair(simulation, attacker, defender);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, defender, pair.Defender);
        attacker.Morale = MoraleRules.RoutThreshold / 2;
        var defenderHealth = defender.Health;
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(defenderHealth, defender.Health);
        Assert.Contains(simulation.Telemetry, item => item.EventType == "RoutedUnitHeld");
    }

    [Fact]
    public void RoutedUnit_CanRallyNearGeneralAndFriendlyCity()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var unit = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        unit.Morale = MoraleRules.RoutThreshold - 0.004;
        simulation.Start();

        simulation.Step(1);

        Assert.NotEqual(MoraleBand.Routed, MoraleRules.Band(unit.Morale));
        Assert.Contains(simulation.Telemetry, item => item.EventType == "Rally" && item.PlayerId == unit.PlayerId);
    }

    [Fact]
    public void CommanderUnderAttack_DropsNearbyFriendlyMoraleAndEmitsTelemetry()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var commander = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Commander);
        var nearbyAlly = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        DisableOtherUnits(simulation, attacker, commander, nearbyAlly);
        var pair = FindAdjacentPair(simulation, attacker, commander);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, commander, pair.Defender);
        var allyCell = AdjacentCells(pair.Defender)
            .First(point => simulation.Grid.IsPassable(point) && point != pair.Attacker);
        PlaceUnit(simulation, nearbyAlly, allyCell);
        var morale = nearbyAlly.Morale;
        simulation.Start();

        simulation.Step(1);

        Assert.True(nearbyAlly.Morale < morale);
        Assert.Contains(simulation.Telemetry, item => item.EventType == "CommanderPressure");
    }

    [Fact]
    public void DiagonalEnemyUnits_AttackEachOther()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        var playerGeneral = simulation.Units.First(unit => unit.PlayerId == attacker.PlayerId && unit.Kind == UnitKind.General);
        var defenderGeneral = simulation.Units.First(unit => unit.PlayerId == defender.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, attacker, defender, playerGeneral, defenderGeneral);
        var step = FindOpenDiagonalMove(simulation);
        PlaceUnit(simulation, attacker, step.Start);
        PlaceUnit(simulation, defender, step.Destination);
        var defenderHealth = defender.Health;
        simulation.Start();

        simulation.Step(1);

        Assert.True(defender.Health < defenderHealth);
    }

    [Fact]
    public void NonAdjacentEnemyUnits_DoNotAttackEachOther()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        DisableOtherUnits(simulation, attacker, defender);
        var pair = FindSeparatedPair(simulation, attacker, defender);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, defender, pair.Defender);
        var defenderHealth = defender.Health;
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(defenderHealth, defender.Health);
    }

    [Fact]
    public void FriendlyUnitBlocksMovementIntoOccupiedCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var blocker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var pair = FindAdjacentPair(simulation, mover, blocker, cell => cell.Terrain != TerrainKind.Water);
        PlaceUnit(simulation, mover, pair.Attacker);
        PlaceUnit(simulation, blocker, pair.Defender);
        mover.Path = [mover.Cell, blocker.Cell];
        mover.PathIndex = 0;
        var start = mover.Cell;
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(start, mover.Cell);
        AssertNoDuplicateOccupiedCells(simulation);
    }

    [Fact]
    public void Terrain_ModifiesCombatDefense()
    {
        var grassDamage = DamageDefenderOnTerrain(TerrainKind.Grass);
        var rockDamage = DamageDefenderOnTerrain(TerrainKind.Rock);

        Assert.True(rockDamage < grassDamage);
    }

    [Fact]
    public void Combat_RemovesDefeatedUnitAndClearsOccupancy()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        var defenderId = defender.Id;
        var pair = FindAdjacentPair(simulation, attacker, defender);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, defender, pair.Defender);
        defender.Health = 0.25;
        simulation.Start();

        simulation.Step(1);

        Assert.DoesNotContain(simulation.Units, unit => unit.Id == defenderId);
        AssertNoDuplicateOccupiedCells(simulation);
    }

    [Fact]
    public void GeneralDefeat_EliminatesPlayer()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        var defenderId = defender.Id;
        var pair = FindAdjacentPair(simulation, attacker, defender);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, defender, pair.Defender);
        defender.Health = 0.25;
        simulation.Start();

        simulation.Step(1);

        Assert.True(simulation.Players[1].IsEliminated);
        Assert.DoesNotContain(simulation.Units, unit => unit.Id == defenderId);
    }

    [Fact]
    public void GeneralDefeat_NeutralizesOwnedCitiesAndAllowsRecapture()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        var extraCity = simulation.Cities.First(city =>
            city.OwnerId == GameConstants.NeutralPlayerId &&
            simulation.Units.All(unit => unit.Cell != city.GridPosition));
        extraCity.OwnerId = defender.PlayerId;
        var defeatedCityIds = simulation.Cities
            .Where(city => city.OwnerId == defender.PlayerId)
            .Select(city => city.Id)
            .ToList();
        var otherPlayerHome = simulation.Cities[simulation.Players[2].HomeCityId];
        var pair = FindAdjacentPair(simulation, attacker, defender);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, defender, pair.Defender);
        defender.Health = 0.25;
        simulation.Start();

        simulation.Step(1);

        Assert.True(simulation.Players[1].IsEliminated);
        Assert.All(defeatedCityIds, cityId => Assert.Equal(GameConstants.NeutralPlayerId, simulation.Cities[cityId].OwnerId));
        Assert.Equal(2, otherPlayerHome.OwnerId);
        var defeatedStanding = simulation.CreateSnapshot().Standings.Single(standing => standing.PlayerId == 1);
        Assert.Equal(0, defeatedStanding.CityCount);
        Assert.True(defeatedStanding.Score < 0);

        PlaceUnit(simulation, attacker, extraCity.GridPosition);
        simulation.Step(1);

        Assert.Equal(attacker.PlayerId, extraCity.OwnerId);
    }

    [Fact]
    public void MissingGeneral_NeutralizesOwnedCitiesDuringLeaderHealthRefresh()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var general = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        var extraCity = simulation.Cities.First(city => city.OwnerId == GameConstants.NeutralPlayerId);
        extraCity.OwnerId = general.PlayerId;
        var defeatedCityIds = simulation.Cities
            .Where(city => city.OwnerId == general.PlayerId)
            .Select(city => city.Id)
            .ToList();
        general.Health = 0;

        var snapshot = simulation.CreateSnapshot();

        Assert.True(snapshot.Players.Single(player => player.Id == 1).IsEliminated);
        Assert.All(defeatedCityIds, cityId => Assert.Equal(GameConstants.NeutralPlayerId, simulation.Cities[cityId].OwnerId));
        Assert.Equal(0, snapshot.Standings.Single(standing => standing.PlayerId == 1).CityCount);
        Assert.True(snapshot.Standings.Single(standing => standing.PlayerId == 1).Score < 0);
    }

    [Fact]
    public void AttackCenter_CanCaptureANeutralCity()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();

        simulation.Step(160);

        Assert.NotEqual(GameConstants.NeutralPlayerId, simulation.Cities[0].OwnerId);
    }

    [Fact]
    public void Match_EndsWhenTimerExpires()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 11, MatchLengthTicks: 120));
        simulation.Start();

        simulation.Step(130);

        Assert.Equal(MatchPhase.Ended, simulation.Phase);
        Assert.NotNull(simulation.WinnerId);
    }

    private static double DamageDefenderOnTerrain(TerrainKind defenderTerrain)
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var attacker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var defender = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        DisableOtherUnits(simulation, attacker, defender);
        var pair = FindAdjacentPair(
            simulation,
            attacker,
            defender,
            cell => cell.Terrain == defenderTerrain);
        PlaceUnit(simulation, attacker, pair.Attacker);
        PlaceUnit(simulation, defender, pair.Defender);
        var before = defender.Health;
        simulation.Start();

        simulation.Step(1);

        return before - defender.Health;
    }

    private static void AssertNoDuplicateOccupiedCells(GameSimulation simulation)
        => Assert.DoesNotContain(
            simulation.Units.Where(unit => unit.IsAlive).GroupBy(unit => unit.Cell),
            group => group.Count() > 1);

    private static void DisableOtherUnits(GameSimulation simulation, params TacticalUnit[] keep)
    {
        var keepIds = keep.Select(unit => unit.Id).ToHashSet();
        foreach (var unit in simulation.Units.Where(unit => !keepIds.Contains(unit.Id)))
            unit.Health = 0;
    }

    private static (GridPoint Attacker, GridPoint Defender) FindAdjacentPair(
        GameSimulation simulation,
        TacticalUnit attacker,
        TacticalUnit defender,
        Func<GridCell, bool>? defenderCellPredicate = null)
    {
        defenderCellPredicate ??= _ => true;
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive && unit.Id != attacker.Id && unit.Id != defender.Id)
            .Select(unit => unit.Cell)
            .ToHashSet();

        foreach (var defenderCell in simulation.Grid.Cells
                     .Where(cell => cell.IsPassable && defenderCellPredicate(cell))
                     .OrderBy(cell => cell.Point.Y)
                     .ThenBy(cell => cell.Point.X))
        {
            if (occupied.Contains(defenderCell.Point))
                continue;

            foreach (var attackerCell in AdjacentCells(defenderCell.Point)
                         .Where(point => simulation.Grid.IsPassable(point) && !occupied.Contains(point))
                         .OrderBy(point => point.Y)
                         .ThenBy(point => point.X))
            {
                return (attackerCell, defenderCell.Point);
            }
        }

        throw new InvalidOperationException($"No adjacent passable pair found for {defenderCellPredicate}.");
    }

    private static (GridPoint Start, GridPoint Destination) FindAdjacentMove(
        GameSimulation simulation,
        TacticalUnit mover,
        Func<GridCell, bool> destinationPredicate)
    {
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive && unit.Id != mover.Id)
            .Select(unit => unit.Cell)
            .ToHashSet();

        foreach (var destinationCell in simulation.Grid.Cells
                     .Where(cell => cell.IsPassable && destinationPredicate(cell) && !occupied.Contains(cell.Point))
                     .OrderBy(cell => cell.Point.Y)
                     .ThenBy(cell => cell.Point.X))
        {
            foreach (var start in AdjacentCells(destinationCell.Point)
                         .Where(point => simulation.Grid.IsPassable(point) && !occupied.Contains(point))
                         .OrderBy(point => point.Y)
                         .ThenBy(point => point.X))
            {
                return (start, destinationCell.Point);
            }
        }

        throw new InvalidOperationException("No adjacent movement pair found.");
    }

    private static GridPoint FindDiagonalApproachToCell(GameSimulation simulation, GridPoint destination)
    {
        foreach (var start in DiagonalCells(destination)
                     .Where(point => simulation.Grid.IsPassable(point))
                     .OrderBy(point => point.Y)
                     .ThenBy(point => point.X))
        {
            if (DiagonalSideCells(start, destination).All(simulation.Grid.IsPassable))
                return start;
        }

        throw new InvalidOperationException("No diagonal approach found.");
    }

    private static (GridPoint Start, GridPoint Destination) FindOpenDiagonalMove(GameSimulation simulation)
    {
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive)
            .Select(unit => unit.Cell)
            .ToHashSet();

        foreach (var startCell in simulation.Grid.Cells
                     .Where(cell => cell.IsPassable && !occupied.Contains(cell.Point))
                     .OrderBy(cell => cell.Point.Y)
                     .ThenBy(cell => cell.Point.X))
        {
            foreach (var destination in DiagonalCells(startCell.Point)
                         .Where(point => simulation.Grid.IsPassable(point) && !occupied.Contains(point))
                         .OrderBy(point => point.Y)
                         .ThenBy(point => point.X))
            {
                if (DiagonalSideCells(startCell.Point, destination).All(side =>
                        simulation.Grid.IsPassable(side) && !occupied.Contains(side)))
                    return (startCell.Point, destination);
            }
        }

        throw new InvalidOperationException("No open diagonal movement pair found.");
    }

    private static (GridPoint Start, GridPoint Destination) FindTerrainBlockedDiagonalMove(GameSimulation simulation)
    {
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive)
            .Select(unit => unit.Cell)
            .ToHashSet();

        foreach (var startCell in simulation.Grid.Cells
                     .Where(cell => cell.IsPassable && !occupied.Contains(cell.Point))
                     .OrderBy(cell => cell.Point.Y)
                     .ThenBy(cell => cell.Point.X))
        {
            foreach (var destination in DiagonalCells(startCell.Point)
                         .Where(point => simulation.Grid.IsPassable(point) && !occupied.Contains(point))
                         .OrderBy(point => point.Y)
                         .ThenBy(point => point.X))
            {
                if (DiagonalSideCells(startCell.Point, destination).Any(side => !simulation.Grid.IsPassable(side)))
                    return (startCell.Point, destination);
            }
        }

        throw new InvalidOperationException("No terrain-blocked diagonal movement pair found.");
    }

    private static GridPoint FindCardinalApproachToCell(
        GameSimulation simulation,
        GridPoint destination,
        IReadOnlySet<GridPoint> excluded)
    {
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive)
            .Select(unit => unit.Cell)
            .ToHashSet();

        foreach (var start in AdjacentCells(destination)
                     .Where(point => simulation.Grid.IsPassable(point) &&
                         !excluded.Contains(point) &&
                         !occupied.Contains(point))
                     .OrderBy(point => point.Y)
                     .ThenBy(point => point.X))
        {
            return start;
        }

        throw new InvalidOperationException("No cardinal approach found.");
    }

    private static (GridPoint Attacker, GridPoint Defender) FindSeparatedPair(
        GameSimulation simulation,
        TacticalUnit attacker,
        TacticalUnit defender)
    {
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive && unit.Id != attacker.Id && unit.Id != defender.Id)
            .Select(unit => unit.Cell)
            .ToHashSet();
        var cells = simulation.Grid.Cells
            .Where(cell => cell.IsPassable && !occupied.Contains(cell.Point))
            .Select(cell => cell.Point)
            .ToList();

        foreach (var left in cells)
        {
            var right = cells
                .Cast<GridPoint?>()
                .FirstOrDefault(point => point.HasValue && point.Value.ManhattanDistanceTo(left) >= 8);
            if (right.HasValue)
                return (left, right.Value);
        }

        throw new InvalidOperationException("No separated passable pair found.");
    }

    private static void PlaceUnit(GameSimulation simulation, TacticalUnit unit, GridPoint cell)
    {
        unit.Cell = cell;
        unit.CurrentPosition = simulation.Grid.ToMapPoint(cell);
        unit.Path = [];
        unit.PathIndex = 0;
        unit.StepProgress = 0;
        unit.SmoothedPathIndices = [];
        unit.VisualFromCell = cell;
        unit.VisualToCell = cell;
        unit.VisualFromPosition = unit.CurrentPosition;
        unit.VisualToPosition = unit.CurrentPosition;
        unit.VisualMoveTick = -1;
    }

    private static MapPoint Interpolate(MapPoint from, MapPoint to, double progress)
    {
        var clamped = Math.Clamp(progress, 0, 1);
        return new MapPoint(
            from.X + (to.X - from.X) * clamped,
            from.Y + (to.Y - from.Y) * clamped);
    }

    private static IEnumerable<GridPoint> AdjacentCells(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y);
        yield return new GridPoint(point.X, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y);
    }

    private static IEnumerable<GridPoint> DiagonalCells(GridPoint point)
    {
        yield return new GridPoint(point.X - 1, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y + 1);
    }

    private static bool IsDiagonalStep(GridPoint from, GridPoint to)
        => Math.Abs(from.X - to.X) == 1 && Math.Abs(from.Y - to.Y) == 1;

    private static IEnumerable<GridPoint> DiagonalSideCells(GridPoint from, GridPoint to)
    {
        yield return new GridPoint(from.X, to.Y);
        yield return new GridPoint(to.X, from.Y);
    }

    // ── Path Smoothing Tests ──────────────────────────────────────────

    [Fact]
    public void LineOfSight_PassesThroughClearCells()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var start = FindPassableCell(simulation, 5, 5);
        var end = FindPassableCellInDirection(simulation, start, 3, 0);

        Assert.True(simulation.HasLineOfSight(start, end));
    }

    [Fact]
    public void LineOfSight_FailsOnWaterOrBlockedTerrain()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var waterCell = simulation.Grid.Cells.First(cell => cell.Terrain == TerrainKind.Water);
        var before = FindPassableNeighbor(simulation, waterCell.Point, dx: -1, dy: 0);
        var after = FindPassableNeighbor(simulation, waterCell.Point, dx: 1, dy: 0);

        if (before.HasValue && after.HasValue)
            Assert.False(simulation.HasLineOfSight(before.Value, after.Value));
    }

    [Fact]
    public void LineOfSight_SameCell_ReturnsTrue()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var cell = FindPassableCell(simulation, 5, 5);

        Assert.True(simulation.HasLineOfSight(cell, cell));
    }

    [Fact]
    public void SmoothGridPath_RemovesIntermediateCellsOnOpenTerrain()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var openPath = FindOpenCardinalPath(simulation, minLength: 5);

        var smoothed = simulation.SmoothGridPath(openPath);

        Assert.True(smoothed.Count < openPath.Count,
            $"Smoothed path ({smoothed.Count} waypoints) should have fewer waypoints than grid path ({openPath.Count} cells)");
        Assert.Equal(0, smoothed[0]);
        Assert.Equal(openPath.Count - 1, smoothed[^1]);
    }

    [Fact]
    public void SmoothGridPath_PreservesCellsAroundObstacles()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var general = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, general);
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();
        simulation.Step(12);

        if (!mover.IsMoving || mover.Path.Count < 3)
            return;

        var smoothed = simulation.SmoothGridPath(mover.Path);
        Assert.True(smoothed.Count >= 2);
        Assert.Equal(0, smoothed[0]);
        Assert.Equal(mover.Path.Count - 1, smoothed[^1]);
    }

    [Fact]
    public void SmoothGridPath_ShortPath_ReturnsAllIndices()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var cell = FindPassableCell(simulation, 10, 10);
        var neighbor = AdjacentCells(cell)
            .First(p => simulation.Grid.IsPassable(p));
        var path = new List<GridPoint> { cell, neighbor };

        var smoothed = simulation.SmoothGridPath(path);

        Assert.Equal([0, 1], smoothed);
    }

    [Fact]
    public void Dispatch_LeavesVisualMovementBoundToGridSteps()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();

        simulation.Step(12);

        var unit = simulation.Units.FirstOrDefault(unit =>
            unit.PlayerId == GameConstants.HumanPlayerId &&
            unit.IsMoving &&
            unit.Path.Count >= 3);

        if (unit == null)
            return;

        Assert.Empty(unit.SmoothedPathIndices);
        Assert.False(unit.IsUsingSmoothedSegment);
    }

    [Fact]
    public void VisualMovement_IgnoresLongSmoothedSegmentsAndStaysOnCurrentGridStep()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var general = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, general);
        var openPath = FindOpenCardinalPath(simulation, minLength: 4);
        PlaceUnit(simulation, mover, openPath[0]);
        mover.Path = openPath;
        mover.SmoothedPathIndices = [0, openPath.Count - 1];
        simulation.Start();

        simulation.Step(1);

        if (!mover.IsMoving)
            return;

        var current = simulation.Grid.ToMapPoint(mover.Cell);
        var next = simulation.Grid.ToMapPoint(mover.Path[mover.PathIndex + 1]);
        var expected = Interpolate(current, next, mover.StepProgress);

        Assert.Equal(expected.X, mover.CurrentPosition.X, precision: 6);
        Assert.Equal(expected.Y, mover.CurrentPosition.Y, precision: 6);
    }

    [Fact]
    public void SmoothedMovement_KeepsSingleOccupancy()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();

        simulation.Step(24);

        AssertNoDuplicateOccupiedCells(simulation);
    }

    [Fact]
    public void SmoothedMovement_PreservesDeterminism()
    {
        var left = GameSimulation.Create(new GameSettings(Seed: 42));
        var right = GameSimulation.Create(new GameSettings(Seed: 42));

        left.Start();
        right.Start();
        left.Step(96);
        right.Step(96);

        Assert.Equal(left.CreateFingerprint(), right.CreateFingerprint());
    }

    [Fact]
    public void BlockedPath_ClearsSmoothedPathIndices()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var mover = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var blocker = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var general = simulation.Units.First(unit => unit.PlayerId == mover.PlayerId && unit.Kind == UnitKind.General);
        DisableOtherUnits(simulation, mover, blocker, general);
        var step = FindAdjacentPair(simulation, mover, blocker);
        PlaceUnit(simulation, mover, step.Attacker);
        PlaceUnit(simulation, blocker, step.Defender);
        mover.Path = [step.Attacker, step.Defender];
        mover.SmoothedPathIndices = [0, 1];
        simulation.Start();

        simulation.Step(1);

        Assert.Empty(mover.SmoothedPathIndices);
        Assert.False(mover.IsUsingSmoothedSegment);
    }

    // ── Path Smoothing Helpers ────────────────────────────────────────

    private static GridPoint FindPassableCell(GameSimulation simulation, int startX, int startY)
    {
        for (var radius = 0; radius < simulation.Grid.Width; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;
                    var point = new GridPoint(startX + dx, startY + dy);
                    if (simulation.Grid.Contains(point) && simulation.Grid.IsPassable(point))
                        return point;
                }
            }
        }

        throw new InvalidOperationException("No passable cell found.");
    }

    private static GridPoint FindPassableCellInDirection(
        GameSimulation simulation, GridPoint start, int dx, int dy)
    {
        var point = new GridPoint(start.X + dx, start.Y + dy);
        while (simulation.Grid.Contains(point) && simulation.Grid.IsPassable(point))
        {
            var next = new GridPoint(point.X + Math.Sign(dx), point.Y + Math.Sign(dy));
            if (!simulation.Grid.Contains(next) || !simulation.Grid.IsPassable(next))
                break;
            point = next;
        }

        return point;
    }

    private static GridPoint? FindPassableNeighbor(
        GameSimulation simulation, GridPoint cell, int dx, int dy)
    {
        for (var distance = 1; distance <= 5; distance++)
        {
            var candidate = new GridPoint(cell.X + dx * distance, cell.Y + dy * distance);
            if (simulation.Grid.Contains(candidate) && simulation.Grid.IsPassable(candidate))
                return candidate;
        }

        return null;
    }

    private static List<GridPoint> FindOpenCardinalPath(
        GameSimulation simulation, int minLength)
    {
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive)
            .Select(unit => unit.Cell)
            .ToHashSet();

        foreach (var startCell in simulation.Grid.Cells
                     .Where(cell => cell.IsPassable && !occupied.Contains(cell.Point))
                     .OrderBy(cell => cell.Point.Y)
                     .ThenBy(cell => cell.Point.X))
        {
            var path = new List<GridPoint> { startCell.Point };
            var current = startCell.Point;

            for (var step = 0; step < 10; step++)
            {
                GridPoint? best = null;
                foreach (var next in new[]
                         {
                             new GridPoint(current.X + 1, current.Y),
                             new GridPoint(current.X, current.Y + 1),
                             new GridPoint(current.X + 1, current.Y + 1)
                         })
                {
                    if (simulation.Grid.Contains(next) &&
                        simulation.Grid.IsPassable(next) &&
                        !occupied.Contains(next) &&
                        !path.Contains(next))
                    {
                        best = next;
                        break;
                    }
                }

                if (!best.HasValue)
                    break;

                path.Add(best.Value);
                current = best.Value;
            }

            if (path.Count >= minLength)
                return path;
        }

        throw new InvalidOperationException($"No open path of length >= {minLength} found.");
    }
}
