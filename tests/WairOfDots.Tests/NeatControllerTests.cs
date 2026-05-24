using WairOfDots.Core;

namespace WairOfDots.Tests;

public class NeatControllerTests
{
    [Fact]
    public void GenomeController_IsDeterministicForSameObservation()
    {
        var controller = GenomeNeatController.Create(seed: 123, playerId: 1);
        var observation = CreateObservation();

        var first = controller.Decide(observation);
        var second = controller.Decide(observation);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenomeController_UsesDifferentGenomeIdsPerAiPlayer()
    {
        var ids = Enumerable.Range(1, 4)
            .Select(playerId => GenomeNeatController.Create(77, playerId).GenomeId)
            .ToList();

        Assert.Equal(4, ids.Distinct().Count());
    }

    [Fact]
    public void GenomeController_ReturnsLegalTargetAndPreference()
    {
        var controller = GenomeNeatController.Create(seed: 91, playerId: 2);

        var decision = controller.Decide(CreateObservation());

        Assert.InRange(decision.TargetCityId, 0, 2);
        Assert.InRange(decision.LightPreference, 0.25, 0.9);
        Assert.True(Enum.IsDefined(decision.Directive));
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
}
