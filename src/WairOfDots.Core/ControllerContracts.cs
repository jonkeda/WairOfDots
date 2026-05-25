namespace WairOfDots.Core;

public sealed record GeneralPerception(
    AiObservation LegacyObservation,
    int PlayerId,
    int TargetRegionId,
    double ControlledCellRatio,
    double TaxIncome,
    double Treasury,
    double Upkeep,
    double PayrollDeficit,
    double PayrollDeficitRatio,
    int SpawnCapacity,
    double EconomyRisk,
    double GeneralSecurity,
    IReadOnlyList<RegionSnapshot> Regions);

public sealed record GeneralAction(
    PlayerDirective Directive,
    int TargetCityId,
    int TargetRegionId,
    double LightPreference,
    StrategyMode StrategyMode,
    double ResourceBudgetRatio,
    UnitKind? PurchaseIntent,
    string DebugLabel,
    double FitnessDelta);

public interface IGeneralController
{
    string ControllerId { get; }
    GeneralAction Decide(GeneralPerception perception);
}

public sealed record ControllerTierReport(
    string Tier,
    string ControllerId,
    int PlayerId,
    string Intent,
    double FitnessDelta);

public sealed record ControllerHierarchyTrace(
    int Tick,
    int PlayerId,
    IReadOnlyList<ControllerTierReport> Reports);

public static class NeuralHierarchyTraceFactory
{
    public static ControllerHierarchyTrace Create(
        MatchSnapshot snapshot,
        int playerId,
        string generalControllerId,
        string commanderControllerId,
        string unitControllerId)
    {
        var player = snapshot.Players.First(player => player.Id == playerId);
        var region = snapshot.Regions.FirstOrDefault(region => region.PlayerId == playerId);
        var units = snapshot.Units.Where(unit => unit.PlayerId == playerId).ToList();
        var protection = units.Count(unit => unit.IsProtectionDetail);
        var scouts = units.Count(unit => unit.IsScout);
        var regionPriority = region?.Priority ?? 0;
        var payrollPressure = region?.PayrollPressure ?? 0;

        return new ControllerHierarchyTrace(
            snapshot.Tick,
            playerId,
            [
                new ControllerTierReport(
                    "General",
                    generalControllerId,
                    playerId,
                    $"{player.Directive}:{player.StrategyMode}:target-city={player.TargetCityId}",
                    player.Score),
                new ControllerTierReport(
                    "Commander",
                    commanderControllerId,
                    playerId,
                    $"region={player.TargetRegionId}:priority={regionPriority:0.###}:reinforce={payrollPressure:0.###}",
                    regionPriority),
                new ControllerTierReport(
                    "Unit",
                    unitControllerId,
                    playerId,
                    $"units={units.Count}:protection={protection}:scouts={scouts}",
                    units.Count == 0 ? 0 : units.Average(unit => unit.Morale))
            ]);
    }
}

public sealed record CommanderPerception(
    int Tick,
    int PlayerId,
    int CommanderUnitId,
    RegionSnapshot Region,
    PlayerDirective Directive,
    double PayrollPressure);

public sealed record CommanderAction(
    int TargetCityId,
    int TargetRegionId,
    PlayerDirective Directive,
    double InfantryPreference,
    bool RequestReinforcements);

public interface ICommanderController
{
    string ControllerId { get; }
    CommanderAction Decide(CommanderPerception perception);
}

public sealed record UnitPerception(
    int Tick,
    int UnitId,
    int PlayerId,
    UnitKind Kind,
    GridPoint Cell,
    double Health,
    double Morale,
    bool IsEnemyAdjacent,
    int ControlledAdjacentCellCount);

public sealed record UnitAction(
    int? TargetCityId,
    int? TargetRegionId,
    bool Hold,
    bool Retreat);

public interface IUnitController
{
    string ControllerId { get; }
    UnitAction Decide(UnitPerception perception);
}

public sealed class LegacyGeneralControllerAdapter : IGeneralController
{
    private readonly IAiController _legacy;

    public LegacyGeneralControllerAdapter(IAiController legacy)
    {
        _legacy = legacy;
    }

    public string ControllerId => _legacy.GenomeId;

    public GeneralAction Decide(GeneralPerception perception)
    {
        var decision = _legacy.Decide(perception.LegacyObservation);
        var economyRisk = perception.EconomyRisk;
        var strategyMode = economyRisk > 0.65
            ? StrategyMode.Rebuild
            : perception.GeneralSecurity < 0.45 ? StrategyMode.ProtectGeneral : StrategyMode.Advance;
        var budgetRatio = Math.Clamp(1.0 - economyRisk * 0.55, 0.15, 1.0);
        UnitKind? purchaseIntent = perception.Treasury < 2 || perception.PayrollDeficitRatio > 0.35
            ? null
            : decision.LightPreference >= 0.5 ? UnitKind.Infantry : UnitKind.Tank;

        return new GeneralAction(
            decision.Directive,
            decision.TargetCityId,
            perception.TargetRegionId,
            decision.LightPreference,
            strategyMode,
            budgetRatio,
            purchaseIntent,
            decision.DebugLabel,
            decision.Aggression);
    }
}

public sealed class DeterministicCommanderController : ICommanderController
{
    public string ControllerId => "deterministic-commander";

    public CommanderAction Decide(CommanderPerception perception)
    {
        var directive = perception.Region.EnemyUnitCount > perception.Region.FriendlyUnitCount
            ? PlayerDirective.Defend
            : perception.Directive;

        return new CommanderAction(
            perception.Region.AnchorCityId,
            perception.Region.RegionId,
            directive,
            perception.PayrollPressure > 0.25 ? 0.8 : 0.6,
            perception.Region.FriendlyUnitCount < Math.Max(2, perception.Region.EnemyUnitCount));
    }
}

public sealed class DeterministicUnitController : IUnitController
{
    public string ControllerId => "deterministic-unit";

    public UnitAction Decide(UnitPerception perception)
    {
        var retreat = perception.Morale < MoraleRules.RoutThreshold && !perception.IsEnemyAdjacent;
        return new UnitAction(null, null, perception.Morale < MoraleRules.RoutRiskThreshold && perception.IsEnemyAdjacent, retreat);
    }
}

public static class MoraleRules
{
    public const double RoutThreshold = 0.18;
    public const double RoutRiskThreshold = 0.35;
    public const double CautiousThreshold = 0.55;
    public const double AggressiveThreshold = 0.82;

    public static double Clamp(double value)
        => Math.Clamp(value, 0, 1);

    public static MoraleBand Band(double morale)
        => Clamp(morale) switch
        {
            < RoutThreshold => MoraleBand.Routed,
            < RoutRiskThreshold => MoraleBand.RoutRisk,
            < CautiousThreshold => MoraleBand.Cautious,
            < AggressiveThreshold => MoraleBand.Normal,
            _ => MoraleBand.Aggressive
        };
}
