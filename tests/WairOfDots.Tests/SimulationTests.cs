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
    public void Dispatch_CreatesGridPathForMovingGroup()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();

        simulation.Step(12);

        var group = Assert.Single(simulation.MovingGroups, group => group.PlayerId == GameConstants.HumanPlayerId);
        Assert.True(group.Path.Count > 2);
        Assert.Equal(simulation.Cities[group.FromCityId].GridPosition, group.Path[0]);
        Assert.Equal(simulation.Cities[group.TargetCityId].GridPosition, group.Path[^1]);
        Assert.All(group.Path, point => Assert.True(simulation.Grid.IsPassable(point)));
    }

    [Fact]
    public void MovingGroups_AdvanceAcrossGridCells()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5));
        simulation.ApplyHumanCommand(new HumanCommand(PlayerDirective.Attack, TargetCityId: 0));
        simulation.Start();
        simulation.Step(12);
        var group = Assert.Single(simulation.MovingGroups, group => group.PlayerId == GameConstants.HumanPlayerId);
        var groupId = group.Id;
        var pathIndex = group.PathIndex;
        var position = group.CurrentPosition;

        simulation.Step(1);

        var moved = simulation.MovingGroups.Single(group => group.Id == groupId);
        Assert.True(moved.PathIndex > pathIndex || moved.CurrentPosition.DistanceTo(position) > 0.01);
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
}
