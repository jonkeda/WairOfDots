using System.Text.Json;

namespace WairOfDots.Core;

public sealed record TrainingSettings(
    int Seed = 1337,
    int AiPlayers = 4,
    int Ticks = 600,
    int Generations = 1,
    string OutputDirectory = ".my/training");

public sealed record TrainingGenerationResult(
    int Generation,
    IReadOnlyList<TrainingRunResult> Results,
    string ChampionGenomeId,
    int ChampionPlayerId,
    double ChampionFitness,
    string Fingerprint);

public sealed record TrainingPipelineResult(
    TrainingSettings Settings,
    IReadOnlyList<TrainingGenerationResult> Generations,
    string ChampionGenomePath);

public static class GenomeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Save(string directory, GenomeDescriptor descriptor)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{descriptor.GenomeId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(descriptor, JsonOptions));
        return path;
    }

    public static GenomeNeatController Load(string path)
        => GenomeNeatController.FromJson(File.ReadAllText(path));

    public static IReadOnlyList<GenomeNeatController> LoadMany(string directory)
        => Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(Load)
                .ToList()
            : [];
}

public static class HeadlessTrainingPipeline
{
    public static TrainingPipelineResult Run(TrainingSettings settings)
    {
        var normalized = settings with
        {
            AiPlayers = Math.Clamp(settings.AiPlayers, 2, 8),
            Ticks = Math.Clamp(settings.Ticks, 120, 7200),
            Generations = Math.Max(1, settings.Generations)
        };

        var generations = new List<TrainingGenerationResult>();
        var championPath = "";
        for (var generation = 0; generation < normalized.Generations; generation++)
        {
            var seed = normalized.Seed + generation;
            var results = DeterministicTrainingRunner.RunSmoke(seed, normalized.AiPlayers, normalized.Ticks);
            var champion = results
                .OrderByDescending(result => result.Fitness)
                .ThenBy(result => result.PlayerId)
                .First();
            var descriptor = GenomeNeatController.Create(seed, champion.PlayerId).ToDescriptor();
            championPath = GenomeRepository.Save(normalized.OutputDirectory, descriptor);

            generations.Add(new TrainingGenerationResult(
                generation,
                results,
                champion.GenomeId,
                champion.PlayerId,
                champion.Fitness,
                champion.Fingerprint));
        }

        return new TrainingPipelineResult(normalized, generations, championPath);
    }
}

public static class BehaviorReviewExporter
{
    public static string ToMarkdown(MatchSnapshot snapshot, IReadOnlyList<TelemetryEvent> telemetry)
    {
        var lines = new List<string>
        {
            "# Behavior Review",
            "",
            $"Tick: {snapshot.Tick}",
            $"Phase: {snapshot.Phase}",
            $"Winner: {snapshot.WinnerName}",
            "",
            "## Standings"
        };

        lines.AddRange(snapshot.Standings.Select(standing =>
            $"- {standing.Name}: score {standing.Score:0.##}, cells {standing.ControlledCellCount}, units {standing.UnitCount}, tax {standing.TaxIncome:0.##}, upkeep {standing.Upkeep:0.##}"));

        lines.Add("");
        lines.Add("## Recent Events");
        lines.AddRange(telemetry
            .TakeLast(20)
            .Select(item => $"- t{item.Tick} p{item.PlayerId} {item.EventType}: {item.Details}"));

        return string.Join(Environment.NewLine, lines);
    }
}
