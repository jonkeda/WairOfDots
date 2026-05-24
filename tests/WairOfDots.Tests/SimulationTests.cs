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
    public void AttackCenter_CanCaptureANeutralCity()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();

        simulation.Step(80);

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
    }

    private static IEnumerable<GridPoint> AdjacentCells(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y);
        yield return new GridPoint(point.X, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y);
    }
}
