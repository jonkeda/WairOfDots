using WairOfDots.Core;

namespace WairOfDots.Tests;

public class EconomyStrategyTests
{
    [Fact]
    public void DefaultSetup_InitializesTaxableCellControlsForPassableCells()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));

        Assert.Equal(simulation.Grid.Cells.Count(cell => cell.IsPassable), simulation.CellControls.Count);
        Assert.DoesNotContain(simulation.CellControls, cell => simulation.Grid.GetCell(cell.Point).Terrain == TerrainKind.Water);
        Assert.All(simulation.Cities, city =>
        {
            var control = simulation.CellControls.Single(cell => cell.Point == city.GridPosition);
            Assert.True(control.TaxValue >= 8);
            Assert.Equal(city.OwnerId, control.OwnerId);
        });
    }

    [Fact]
    public void InfantryCapturesOnlyOccupiedCell()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var infantry = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var target = FindNeutralPatch(simulation, requireAdjacentNeutral: true);
        PlaceUnit(simulation, infantry, target);
        var adjacent = AdjacentCells(target)
            .Where(point => simulation.CellControls.Any(cell => cell.Point == point))
            .ToList();
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(0, simulation.CellControls.Single(cell => cell.Point == target).OwnerId);
        Assert.All(adjacent, point => Assert.NotEqual(0, simulation.CellControls.Single(cell => cell.Point == point).OwnerId));
    }

    [Fact]
    public void TankCapturesOccupiedAndAdjacentPassableCells()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var tank = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Tank);
        var target = FindNeutralPatch(simulation, requireAdjacentNeutral: true);
        PlaceUnit(simulation, tank, target);
        var adjacent = AdjacentCells(target)
            .Where(point => simulation.Grid.IsPassable(point) && simulation.CellControls.Any(cell => cell.Point == point))
            .ToList();
        simulation.Start();

        simulation.Step(1);

        Assert.Equal(0, simulation.CellControls.Single(cell => cell.Point == target).OwnerId);
        Assert.Contains(adjacent, point => simulation.CellControls.Single(cell => cell.Point == point).OwnerId == 0);
    }

    [Fact]
    public void EconomyTick_CollectsTaxesDeductsUpkeepAndReportsEconomy()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.Start();

        simulation.Step(6);
        var snapshot = simulation.CreateSnapshot();
        var humanEconomy = snapshot.Economies.Single(economy => economy.PlayerId == 0);

        Assert.True(humanEconomy.ControlledCellCount > 0);
        Assert.True(humanEconomy.TaxIncome > 0);
        Assert.True(humanEconomy.Upkeep > 0);
        Assert.True(humanEconomy.Treasury >= 0);
        Assert.Equal(humanEconomy.ControlledCellCount, snapshot.Players.Single(player => player.Id == 0).ControlledCellCount);
    }

    [Fact]
    public void PayrollDeficitLowersMorale()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        foreach (var cell in simulation.CellControls.Where(cell => cell.OwnerId == 0))
            cell.OwnerId = GameConstants.NeutralPlayerId;
        var before = simulation.Units
            .Where(unit => unit.PlayerId == 0)
            .Average(unit => unit.Morale);
        simulation.Start();
        simulation.Step(5);
        foreach (var cell in simulation.CellControls.Where(cell => cell.OwnerId == 0))
            cell.OwnerId = GameConstants.NeutralPlayerId;
        simulation.Players[0].Resources = 0;

        simulation.Step(1);

        var after = simulation.Units
            .Where(unit => unit.PlayerId == 0)
            .Average(unit => unit.Morale);
        Assert.True(simulation.Players[0].LastPayrollDeficit > 0);
        Assert.True(after < before);
    }

    [Fact]
    public void GeneralDefeatNeutralizesControlledCells()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var aiGeneral = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        var extraCell = simulation.CellControls.First(cell => cell.OwnerId == GameConstants.NeutralPlayerId);
        extraCell.OwnerId = 1;
        aiGeneral.Health = 0;

        var snapshot = simulation.CreateSnapshot();

        Assert.True(snapshot.Players.Single(player => player.Id == 1).IsEliminated);
        Assert.DoesNotContain(simulation.CellControls, cell => cell.OwnerId == 1);
        Assert.DoesNotContain(snapshot.CellControls, cell => cell.OwnerId == 1);
    }

    [Fact]
    public void GeneralControllerAdapterPreservesLegacyDecisionShape()
    {
        var legacy = GenomeNeatController.Create(seed: 77, playerId: 1);
        var adapter = new LegacyGeneralControllerAdapter(legacy);
        var observation = CreateObservation();
        var legacyDecision = legacy.Decide(observation);

        var action = adapter.Decide(new GeneralPerception(
            observation,
            1,
            1,
            0.25,
            8,
            12,
            2,
            0,
            0,
            2,
            0.1,
            0.9,
            []));

        Assert.Equal(legacyDecision.Directive, action.Directive);
        Assert.Equal(legacyDecision.TargetCityId, action.TargetCityId);
        Assert.Equal(legacyDecision.LightPreference, action.LightPreference, precision: 6);
    }

    [Fact]
    public void SnapshotIncludesRegionsVisibilityAndFitnessInputs()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 42, AiPlayers: 4, Mode: GameMode.AiOnly));
        simulation.Start();
        simulation.Step(24);

        var snapshot = simulation.CreateSnapshot();
        var fitness = FitnessEvaluator.Evaluate(snapshot, 0);

        Assert.Equal(simulation.Units.Count(unit => unit.Kind == UnitKind.Commander), snapshot.Regions.Count);
        Assert.All(snapshot.Regions, region =>
        {
            Assert.True(region.Width > 0);
            Assert.True(region.Height > 0);
            Assert.True(region.AssignedCommanderUnitId.HasValue);
        });
        Assert.Equal(simulation.Players.Count, snapshot.Visibility.Count);
        Assert.InRange(fitness.TotalFitness, 0, 1);
        Assert.Contains(simulation.Telemetry, item => item.EventType == "AiPlan");
    }

    [Fact]
    public void DeterministicTrainingSmokeProducesStableResults()
    {
        var left = DeterministicTrainingRunner.RunSmoke(seed: 88, aiPlayers: 4, ticks: 120);
        var right = DeterministicTrainingRunner.RunSmoke(seed: 88, aiPlayers: 4, ticks: 120);

        Assert.Equal(left, right);
        Assert.Equal(4, left.Count);
        Assert.All(left, result => Assert.InRange(result.Fitness, 0, 1));
    }

    [Fact]
    public void CommanderPlanningAssignsProtectionAndScoutRoles()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5, AiPlayers: 4, Mode: GameMode.AiOnly));
        simulation.Start();

        simulation.Step(12);
        var snapshot = simulation.CreateSnapshot();
        var humanUnits = snapshot.Units.Where(unit => unit.PlayerId == 0).ToList();

        Assert.Contains(humanUnits, unit => unit.IsProtectionDetail);
        Assert.Contains(humanUnits, unit => unit.IsScout);
    }

    [Fact]
    public void GeneralRelocationCommandChangesGeneralTarget()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var target = simulation.Cities.First(city => city.Id != simulation.Players[0].HomeCityId);
        var general = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.General);
        simulation.ApplyHumanCommand(new HumanCommand(GeneralRelocationCityId: target.Id));
        simulation.Start();

        simulation.Step(12);

        Assert.Equal(target.Id, general.TargetCityId);
        Assert.True(general.IsMoving || general.Cell == target.GridPosition);
    }

    [Fact]
    public void ScoutRoleIncreasesVisibilitySnapshot()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var scout = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var general = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.General);
        var occupied = simulation.Units
            .Where(unit => unit.IsAlive && unit.Id != scout.Id)
            .Select(unit => unit.Cell)
            .ToHashSet();
        var scoutCell = simulation.Grid.Cells
            .Where(cell => cell.IsPassable && !occupied.Contains(cell.Point))
            .OrderByDescending(cell => cell.Point.OctileDistanceTo(general.Cell))
            .ThenBy(cell => cell.Point.Y)
            .ThenBy(cell => cell.Point.X)
            .First()
            .Point;
        PlaceUnit(simulation, scout, scoutCell);
        var snapshotBefore = simulation.CreateSnapshot();
        var visibilityBefore = snapshotBefore.Visibility.Single(visibility => visibility.PlayerId == 0).VisibleCellCount;
        scout.IsScout = true;

        var snapshotAfter = simulation.CreateSnapshot();
        var visibilityAfter = snapshotAfter.Visibility.Single(visibility => visibility.PlayerId == 0).VisibleCellCount;

        Assert.True(visibilityAfter > visibilityBefore);
    }

    [Fact]
    public void ScoutLostContactTelemetryFiresWhenVisibleEnemyLeavesSight()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var scout = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        var friendlyGeneral = simulation.Units.First(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.General);
        var enemy = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.Infantry);
        var enemyGeneral = simulation.Units.First(unit => unit.PlayerId == 1 && unit.Kind == UnitKind.General);
        foreach (var unit in simulation.Units.Where(unit => unit.Id != scout.Id && unit.Id != friendlyGeneral.Id && unit.Id != enemy.Id && unit.Id != enemyGeneral.Id))
            unit.Health = 0;

        scout.IsScout = true;
        scout.Health = 100;
        enemy.Health = 100;
        var visiblePair = FindAdjacentControlledCells(simulation);
        PlaceUnit(simulation, scout, visiblePair.Left.Point);
        PlaceUnit(simulation, enemy, visiblePair.Right.Point);
        simulation.Start();
        simulation.Step(1);

        var occupied = simulation.Units
            .Where(unit => unit.IsAlive && unit.Id != enemy.Id)
            .Select(unit => unit.Cell)
            .ToHashSet();
        var farCell = simulation.Grid.Cells
            .Where(cell => cell.IsPassable && !occupied.Contains(cell.Point))
            .OrderByDescending(cell => Math.Min(
                cell.Point.OctileDistanceTo(scout.Cell),
                cell.Point.OctileDistanceTo(friendlyGeneral.Cell)))
            .ThenBy(cell => cell.Point.Y)
            .ThenBy(cell => cell.Point.X)
            .First()
            .Point;
        PlaceUnit(simulation, enemy, farCell);

        simulation.Step(1);

        Assert.Contains(simulation.Telemetry, item => item.EventType == "ScoutLostContact" && item.PlayerId == 0);
    }

    [Fact]
    public void EconomyTickSpendsReplenishmentRequestOnInfantry()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var player = simulation.Players[0];
        player.Resources = 8;
        player.LightPreference = 0;
        player.ReplenishmentRequest = 4;
        var infantryBefore = simulation.Units.Count(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        simulation.Start();

        simulation.Step(6);

        var infantryAfter = simulation.Units.Count(unit => unit.PlayerId == 0 && unit.Kind == UnitKind.Infantry);
        Assert.True(infantryAfter > infantryBefore);
        Assert.True(player.ReplenishmentRequest < 4);
    }

    [Fact]
    public void HeadlessTrainingPipelineSavesLoadableChampionGenome()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wair-training-" + Guid.NewGuid().ToString("N"));

        var result = HeadlessTrainingPipeline.Run(new TrainingSettings(
            Seed: 12,
            AiPlayers: 3,
            Ticks: 120,
            Generations: 1,
            OutputDirectory: directory));

        Assert.True(File.Exists(result.ChampionGenomePath));
        var loaded = GenomeRepository.Load(result.ChampionGenomePath);
        Assert.Equal(result.Generations.Single().ChampionGenomeId, loaded.GenomeId);
    }

    [Fact]
    public void BehaviorReviewExporterSummarizesStandingsAndTelemetry()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.Start();
        simulation.Step(24);

        var markdown = BehaviorReviewExporter.ToMarkdown(simulation.CreateSnapshot(), simulation.Telemetry);

        Assert.Contains("# Behavior Review", markdown);
        Assert.Contains("## Standings", markdown);
        Assert.Contains("AiPlan", markdown);
    }

    [Fact]
    public void TerritoryBoundariesAppearOnlyBetweenDifferentOwners()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        var (leftBlock, rightBlock, leftEdge, rightEdge) = FindAdjacentControlledBlocks(simulation);
        foreach (var cell in leftBlock)
            cell.OwnerId = 0;
        foreach (var cell in rightBlock)
            cell.OwnerId = 0;

        var sameOwnerBoundaries = simulation.CreateTerritoryBoundaries();

        Assert.DoesNotContain(sameOwnerBoundaries, segment => IsBoundaryBetween(segment, leftEdge, rightEdge));

        foreach (var cell in rightBlock)
            cell.OwnerId = 1;

        var differentOwnerBoundaries = simulation.CreateTerritoryBoundaries();

        Assert.Contains(differentOwnerBoundaries, segment => IsBoundaryBetween(segment, leftEdge, rightEdge));
    }

    [Fact]
    public void TerritoryBoundaries_AreDeterministicForSameSeed()
    {
        var left = GameSimulation.Create(new GameSettings(Seed: 42));
        var right = GameSimulation.Create(new GameSettings(Seed: 42));
        left.Start();
        right.Start();
        left.Step(36);
        right.Step(36);

        Assert.Equal(left.CreateTerritoryBoundaries(), right.CreateTerritoryBoundaries());
    }

    private static AiObservation CreateObservation()
        => new(
            Tick: 24,
            PlayerId: 1,
            OwnedCityRatio: 0.25,
            StrengthRatio: 0.45,
            ResourceLevel: 0.5,
            CommanderHealth: 1,
            GeneralHealth: 1,
            TimePressure: 0.2,
            Cities:
            [
                new CityObservation(0, Owned: 0, Neutral: 1, Enemy: 0, FriendlyUnits: 0, EnemyUnits: 0, DistanceFromCommander: 0.3),
                new CityObservation(1, Owned: 1, Neutral: 0, Enemy: 0, FriendlyUnits: 0.5, EnemyUnits: 0, DistanceFromCommander: 0.1),
                new CityObservation(2, Owned: 0, Neutral: 0, Enemy: 1, FriendlyUnits: 0, EnemyUnits: 0.4, DistanceFromCommander: 0.6)
            ]);

    private static (CellControlState Left, CellControlState Right) FindAdjacentControlledCells(GameSimulation simulation)
    {
        foreach (var left in simulation.CellControls.OrderBy(cell => cell.Point.Y).ThenBy(cell => cell.Point.X))
        {
            var rightPoint = new GridPoint(left.Point.X + 1, left.Point.Y);
            var right = simulation.CellControls.FirstOrDefault(cell => cell.Point == rightPoint);
            if (right != null)
                return (left, right);

            var downPoint = new GridPoint(left.Point.X, left.Point.Y + 1);
            var down = simulation.CellControls.FirstOrDefault(cell => cell.Point == downPoint);
            if (down != null)
                return (left, down);
        }

        throw new InvalidOperationException("No adjacent controlled cells found.");
    }

    private static (
        IReadOnlyList<CellControlState> LeftBlock,
        IReadOnlyList<CellControlState> RightBlock,
        GridPoint LeftEdge,
        GridPoint RightEdge) FindAdjacentControlledBlocks(GameSimulation simulation)
    {
        var controls = simulation.CellControls.ToDictionary(cell => cell.Point, cell => cell);
        for (var y = 0; y < simulation.Grid.Height - 1; y++)
        {
            for (var x = 0; x < simulation.Grid.Width - 3; x++)
            {
                var leftPoints = new[]
                {
                    new GridPoint(x, y),
                    new GridPoint(x + 1, y),
                    new GridPoint(x, y + 1),
                    new GridPoint(x + 1, y + 1)
                };
                var rightPoints = new[]
                {
                    new GridPoint(x + 2, y),
                    new GridPoint(x + 3, y),
                    new GridPoint(x + 2, y + 1),
                    new GridPoint(x + 3, y + 1)
                };

                if (leftPoints.Concat(rightPoints).All(controls.ContainsKey))
                {
                    return (
                        leftPoints.Select(point => controls[point]).ToArray(),
                        rightPoints.Select(point => controls[point]).ToArray(),
                        new GridPoint(x + 1, y),
                        new GridPoint(x + 2, y));
                }
            }
        }

        throw new InvalidOperationException("No adjacent controlled blocks found.");
    }

    private static bool IsBoundaryBetween(TerritoryBoundarySegment segment, GridPoint left, GridPoint right)
    {
        if (left.Y == right.Y && Math.Abs(left.X - right.X) == 1)
        {
            var minX = Math.Min(left.X, right.X);
            return segment.FromX == minX + 1 &&
                segment.ToX == minX + 1 &&
                segment.FromY <= left.Y &&
                segment.ToY >= left.Y + 1;
        }

        if (left.X == right.X && Math.Abs(left.Y - right.Y) == 1)
        {
            var minY = Math.Min(left.Y, right.Y);
            return segment.FromX <= left.X &&
                segment.ToX >= left.X + 1 &&
                segment.FromY == minY + 1 &&
                segment.ToY == minY + 1;
        }

        return false;
    }

    private static GridPoint FindNeutralPatch(GameSimulation simulation, bool requireAdjacentNeutral)
    {
        var occupied = simulation.Units.Where(unit => unit.IsAlive).Select(unit => unit.Cell).ToHashSet();
        foreach (var cell in simulation.CellControls
                     .Where(cell => cell.OwnerId == GameConstants.NeutralPlayerId &&
                         simulation.Grid.IsPassable(cell.Point) &&
                         !occupied.Contains(cell.Point))
                     .OrderBy(cell => cell.Point.Y)
                     .ThenBy(cell => cell.Point.X))
        {
            if (!requireAdjacentNeutral)
                return cell.Point;

            var adjacent = AdjacentCells(cell.Point)
                .Where(point => simulation.CellControls.Any(control => control.Point == point))
                .ToList();
            if (adjacent.Count > 0 &&
                adjacent.All(point => simulation.CellControls.Single(control => control.Point == point).OwnerId == GameConstants.NeutralPlayerId))
            {
                return cell.Point;
            }
        }

        throw new InvalidOperationException("No neutral patch found.");
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

    private static IEnumerable<GridPoint> AdjacentCells(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y);
        yield return new GridPoint(point.X, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y);
        yield return new GridPoint(point.X - 1, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y + 1);
    }
}
