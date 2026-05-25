namespace WairOfDots.Core;

public sealed class CellControlState
{
    public CellControlState(GridPoint point, int ownerId, double taxValue, bool isCityCell)
    {
        Point = point;
        OwnerId = ownerId;
        TaxValue = taxValue;
        IsCityCell = isCityCell;
    }

    public GridPoint Point { get; }
    public int OwnerId { get; set; }
    public double TaxValue { get; }
    public bool IsCityCell { get; }
    public int LastChangedTick { get; set; }
}

public sealed record FitnessMetrics(
    int PlayerId,
    double Score,
    double TerritoryControl,
    double TaxEfficiency,
    double PayrollStability,
    double ArmySustainability,
    double GeneralSurvival,
    double TotalFitness);

public sealed record TrainingRunResult(
    string GenomeId,
    int Seed,
    int PlayerId,
    double Fitness,
    string Fingerprint);

public static class FitnessEvaluator
{
    public static FitnessMetrics Evaluate(MatchSnapshot snapshot, int playerId)
    {
        var standing = snapshot.Standings.First(standing => standing.PlayerId == playerId);
        var economy = snapshot.Economies.First(economy => economy.PlayerId == playerId);
        var totalCells = Math.Max(1, snapshot.CellControls.Count(cell => cell.TaxValue > 0));
        var territoryControl = economy.ControlledCellCount / (double)totalCells;
        var taxEfficiency = economy.ControlledTaxValue <= 0
            ? 0
            : Math.Clamp(economy.TaxIncome / economy.ControlledTaxValue, 0, 1);
        var payrollStability = 1.0 - Math.Clamp(economy.PayrollDeficitRatio, 0, 1);
        var armySustainability = standing.UnitCount <= 0
            ? 0
            : Math.Clamp(economy.TaxIncome / Math.Max(1, economy.Upkeep), 0, 1.5) / 1.5;
        var generalSurvival = standing.IsEliminated ? 0 : Math.Clamp(standing.GeneralHealth / TacticalUnit.DefaultHealth(UnitKind.General), 0, 1);
        var scoreFitness = Math.Clamp((standing.Score + 100) / 220, 0, 1);
        var total = scoreFitness * 0.30 +
            territoryControl * 0.20 +
            taxEfficiency * 0.15 +
            payrollStability * 0.15 +
            armySustainability * 0.10 +
            generalSurvival * 0.10;

        return new FitnessMetrics(
            playerId,
            standing.Score,
            territoryControl,
            taxEfficiency,
            payrollStability,
            armySustainability,
            generalSurvival,
            Math.Round(total, 4));
    }
}

public static class DeterministicTrainingRunner
{
    public static IReadOnlyList<TrainingRunResult> RunSmoke(int seed, int aiPlayers, int ticks)
    {
        var simulation = GameSimulation.Create(new GameSettings(seed, aiPlayers, ticks, GameMode.AiOnly));
        simulation.Start();
        simulation.Step(ticks);
        var snapshot = simulation.CreateSnapshot();

        return snapshot.Standings
            .OrderBy(standing => standing.PlayerId)
            .Select(standing =>
            {
                var fitness = FitnessEvaluator.Evaluate(snapshot, standing.PlayerId);
                return new TrainingRunResult(
                    standing.GenomeId,
                    seed,
                    standing.PlayerId,
                    fitness.TotalFitness,
                    snapshot.Fingerprint);
            })
            .ToList();
    }
}
