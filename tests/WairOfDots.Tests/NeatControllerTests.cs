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
    public void GenomeController_ResolvesArchetypeFromGenomeId()
    {
        var controller = GenomeNeatController.Create(seed: 77, playerId: 2);

        var archetype = GenomeNeatController.ResolveArchetype(controller.GenomeId);

        Assert.Equal("turtle", archetype);
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

    [Fact]
    public void GenomeController_RoundTripsThroughJson()
    {
        var controller = GenomeNeatController.Create(seed: 91, playerId: 2);
        var json = controller.ToJson();

        var loaded = GenomeNeatController.FromJson(json);

        Assert.Equal(controller.GenomeId, loaded.GenomeId);
        Assert.Equal(controller.Decide(CreateObservation()), loaded.Decide(CreateObservation()));
    }

    [Fact]
    public void ExternalGeneralController_FallsBackWhenAdapterCannotDecide()
    {
        var fallback = new LegacyGeneralControllerAdapter(GenomeNeatController.Create(seed: 44, playerId: 1));
        var adapter = new DelegateExternalGeneralControllerAdapter(
            "onnx",
            _ => new ExternalControllerResponse(false, null, "offline"));
        var controller = new FallbackExternalGeneralController(adapter, fallback, "model.onnx");
        var perception = CreateGeneralPerception();

        var action = controller.Decide(perception);

        Assert.Equal(fallback.Decide(perception), action);
        Assert.Equal("onnx:" + fallback.ControllerId, controller.ControllerId);
    }

    [Fact]
    public void ExternalManifest_DescribesGeneralActionAdapter()
    {
        var manifest = ExternalControllerManifests.CreateGeneralOnnxManifest("model.onnx", "fallback");

        Assert.Contains("GeneralPerception", manifest.InputSchema);
        Assert.Contains("GeneralAction", manifest.OutputSchema);
        Assert.Equal("fallback", manifest.FallbackControllerId);
        Assert.Empty(ExternalControllerManifests.Validate(manifest));
        Assert.Contains("ModelPath", ExternalControllerManifests.Validate(manifest with { ModelPath = "" }).Single());
    }

    [Fact]
    public void ExternalAdapterTimeout_ReturnsFailureResponse()
    {
        var slow = new DelegateExternalGeneralControllerAdapter("llm", _ =>
        {
            Thread.Sleep(50);
            return new ExternalControllerResponse(true, null);
        });
        var timed = new TimeoutExternalGeneralControllerAdapter(slow, TimeSpan.FromMilliseconds(1));

        var response = timed.Decide(new ExternalControllerRequest("llm", "local", "fallback", CreateGeneralPerception()));

        Assert.False(response.Success);
        Assert.Contains("timeout", response.Error);
    }

    [Fact]
    public void NeuralHierarchyTrace_ReportsGeneralCommanderAndUnitTiers()
    {
        var simulation = GameSimulation.Create(new GameSettings(Seed: 5, AiPlayers: 3, Mode: GameMode.AiOnly));
        simulation.Start();
        simulation.Step(12);

        var trace = NeuralHierarchyTraceFactory.Create(
            simulation.CreateSnapshot(),
            playerId: 0,
            generalControllerId: "general",
            commanderControllerId: "commander",
            unitControllerId: "unit");

        Assert.Equal(3, trace.Reports.Count);
        Assert.Contains(trace.Reports, report => report.Tier == "General");
        Assert.Contains(trace.Reports, report => report.Tier == "Commander");
        Assert.Contains(trace.Reports, report => report.Tier == "Unit");
    }

    private static GeneralPerception CreateGeneralPerception()
        => new(
            CreateObservation(),
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
            []);

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
