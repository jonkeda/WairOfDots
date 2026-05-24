namespace WairOfDots.Core;

public sealed record CityObservation(
    int CityId,
    double Owned,
    double Neutral,
    double Enemy,
    double FriendlyUnits,
    double EnemyUnits,
    double DistanceFromCommander);

public sealed record AiObservation(
    int Tick,
    int PlayerId,
    double OwnedCityRatio,
    double StrengthRatio,
    double ResourceLevel,
    double CommanderHealth,
    double GeneralHealth,
    double TimePressure,
    IReadOnlyList<CityObservation> Cities);

public sealed record AiDecision(
    PlayerDirective Directive,
    int TargetCityId,
    double LightPreference,
    double Aggression,
    string DebugLabel);

public interface IAiController
{
    string GenomeId { get; }
    AiDecision Decide(AiObservation observation);
}

public sealed class GenomeNeatController : IAiController
{
    private readonly double[] _weights;
    private readonly double _targetEnemyBias;
    private readonly double _targetNeutralBias;
    private readonly double _distanceBias;
    private readonly string _archetype;

    private GenomeNeatController(
        string genomeId,
        string archetype,
        double[] weights,
        double targetEnemyBias,
        double targetNeutralBias,
        double distanceBias)
    {
        GenomeId = genomeId;
        _archetype = archetype;
        _weights = weights;
        _targetEnemyBias = targetEnemyBias;
        _targetNeutralBias = targetNeutralBias;
        _distanceBias = distanceBias;
    }

    public string GenomeId { get; }

    public static GenomeNeatController Create(int seed, int playerId)
    {
        var archetypes = new[] { "rush", "turtle", "opportunist", "decap" };
        var archetype = archetypes[(playerId - 1) % archetypes.Length];
        var rng = new Random(HashCode.Combine(seed, playerId, archetype));
        var weights = Enumerable.Range(0, 10).Select(_ => rng.NextDouble() * 2 - 1).ToArray();

        var (enemy, neutral, distance) = archetype switch
        {
            "rush" => (1.15, 0.35, -0.20),
            "turtle" => (0.45, 0.95, -0.55),
            "opportunist" => (0.85, 0.75, -0.30),
            "decap" => (1.30, 0.15, -0.10),
            _ => (0.8, 0.5, -0.25)
        };

        return new GenomeNeatController($"neat-{archetype}-{seed}-{playerId}", archetype, weights, enemy, neutral, distance);
    }

    public AiDecision Decide(AiObservation observation)
    {
        var aggressionRaw =
            observation.StrengthRatio * _weights[0] +
            observation.ResourceLevel * _weights[1] +
            observation.CommanderHealth * _weights[2] +
            observation.GeneralHealth * _weights[3] +
            observation.TimePressure * _weights[4];

        var aggression = Sigmoid(aggressionRaw + (_archetype == "rush" ? 0.35 : 0));
        var lightPreference = Math.Clamp(Sigmoid(_weights[5] + aggression - observation.StrengthRatio * 0.15), 0.25, 0.9);
        var directive = aggression switch
        {
            < 0.35 => PlayerDirective.Defend,
            < 0.58 => PlayerDirective.Hold,
            _ => PlayerDirective.Attack
        };

        var target = observation.Cities
            .OrderByDescending(city => ScoreCity(city, aggression))
            .ThenBy(city => city.CityId)
            .First();

        return new AiDecision(
            directive,
            target.CityId,
            lightPreference,
            aggression,
            $"{_archetype}:a={aggression:F2}:l={lightPreference:F2}:t={target.CityId}");
    }

    private double ScoreCity(CityObservation city, double aggression)
    {
        return city.Enemy * (_targetEnemyBias + aggression * 0.7)
            + city.Neutral * _targetNeutralBias
            + city.EnemyUnits * (0.2 + aggression * 0.3)
            - city.FriendlyUnits * 0.15
            + city.DistanceFromCommander * _distanceBias
            + _weights[6] * city.Owned
            + _weights[7] * city.Neutral
            + _weights[8] * city.Enemy
            + _weights[9] * city.EnemyUnits;
    }

    private static double Sigmoid(double value)
        => 1.0 / (1.0 + Math.Exp(-value));
}
