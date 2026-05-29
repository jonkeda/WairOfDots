using System.Globalization;
using System.Text;

namespace WairOfDots.Core;

public sealed class GameSimulation
{
    private const int ProductionInterval = 6;
    private const int PlanningInterval = 12;
    private const double InfantryCost = 2.0;
    private const double TankCost = 4.5;
    private const double CommanderCost = 9.0;
    private const double CommanderUpkeep = 0.9;
    private const double DiagonalMoveMultiplier = 1.4142135623730951;
    private const int TerritoryBoundaryMinimumComponentCells = 4;
    private const int MaxQuietHoldPlans = 2;
    private const int RecentCombatVisualLifetimeTicks = 4;
    private const int MaxRecentCombatHits = 96;
    private const int MaxRecentDeaths = 24;
    private const int MaxCommandRecordsPerPlayer = 32;
    private const int MaxReportRecordsPerPlayer = 32;
    private const string GeneralToCommanderLayer = "GeneralToCommander";
    private const string GeneralToReserveLayer = "GeneralToReserve";
    private const string CommanderToUnitLayer = "CommanderToUnit";
    private const string AttackRegionCommand = "AttackRegion";
    private const string HoldRegionCommand = "HoldRegion";
    private const string CommanderToGeneralLayer = "CommanderToGeneral";
    private const string UnitToCommanderLayer = "UnitToCommander";
    private const string AssignReserveUnitCommand = "AssignReserveUnit";
    private const string AssignReserveGroupCommand = "AssignReserveGroup";
    private const string RecallCommanderUnitToReserveCommand = "RecallCommanderUnitToReserve";
    private const string RecallCommanderGroupToReserveCommand = "RecallCommanderGroupToReserve";
    private const string GeneralToCityLayer = "GeneralToCity";
    private const string ProduceInfantryCommand = "ProduceInfantry";
    private const string ProduceTankCommand = "ProduceTank";
    private const string ProduceCommanderCommand = "ProduceCommander";
    private const string HoldProductionCommand = "HoldProduction";
    private const string AdvanceToCellCommand = "AdvanceToCell";
    private const string DefendCellCommand = "DefendCell";
    private const string EnemySightedReport = "EnemySighted";
    private const string LowMoraleReport = "LowMorale";
    private const string ReachedTargetReport = "ReachedTarget";
    private const string RegionStableReport = "RegionStable";
    private const string RegionContestedReport = "RegionContested";
    private const string NeedReinforcementsReport = "NeedReinforcements";
    private const string ReserveRecallRequestedReport = "ReserveRecallRequested";
    private const string CommanderThreatenedReport = "CommanderThreatened";
    private const string UnderAttackReport = "UnderAttack";

    private readonly List<CityNode> _cities;
    private readonly IReadOnlyList<TerrainPatch> _terrain;
    private readonly GridMap _grid;
    private readonly List<PlayerState> _players;
    private readonly List<TacticalUnit> _units = [];
    private readonly List<TelemetryEvent> _telemetry = [];
    private readonly Dictionary<int, IGeneralController> _generalControllers = [];
    private readonly ICommanderController _commanderController = new DeterministicCommanderController();
    private readonly IUnitController _unitController = new DeterministicUnitController();
    private readonly Dictionary<GridPoint, CellControlState> _cellControls = [];
    private readonly List<RecentCombatHit> _recentCombatHits = [];
    private readonly List<RecentUnitDeath> _recentDeaths = [];
    private readonly List<CommandRecordSnapshot> _commandRecords = [];
    private readonly List<ReportRecordSnapshot> _reportRecords = [];
    private readonly Dictionary<int, List<CommanderRegionAssignment>> _explicitRegionAssignments = [];
    private readonly Dictionary<int, double> _previousControlledTax = [];
    private readonly Dictionary<int, int> _previousControlledCells = [];
    private readonly Dictionary<int, bool> _previousEnemyGeneralVisible = [];
    private readonly Dictionary<int, HashSet<int>> _previousVisibleEnemyUnitIds = [];
    private int _nextUnitId = 1;
    private int _activeCombatCount;
    private string _lastEvent = "Waiting";

    private sealed record CommanderObjective(
        string CommandType,
        GridPoint TargetCell,
        int? TargetCityId,
        int? TargetRegionId,
        double Priority,
        string ReasonCode);

    private GameSimulation(GameSettings settings, CreatedMap map)
    {
        Settings = settings.Normalized();
        _cities = map.Cities.ToList();
        _terrain = map.Terrain;
        _grid = map.Grid;
        _players = CreatePlayers(Settings, map.HomeCityIds).ToList();

        foreach (var player in _players)
        {
            var home = _cities[player.HomeCityId];
            home.OwnerId = player.Id;
            player.TargetCityId = 0;
            PlaceInitialArmy(player, home);
            InitializeExplicitRegionAssignments(player);

            if (player.Kind == PlayerKind.Ai)
                _generalControllers[player.Id] = new LegacyGeneralControllerAdapter(GenomeNeatController.Create(Settings.Seed, player.Id));
        }

        InitializeCellControls();
        ResolveTerritoryCapture();
        UpdateEconomyState();
        UpdateLeaderHealth();
        UpdateScores();
    }

    public GameSettings Settings { get; }
    public int Tick { get; private set; }
    public MatchPhase Phase { get; private set; } = MatchPhase.Menu;
    public int? WinnerId { get; private set; }
    public IReadOnlyList<CityNode> Cities => _cities;
    public IReadOnlyList<TerrainPatch> Terrain => _terrain;
    public GridMap Grid => _grid;
    public IReadOnlyList<PlayerState> Players => _players;
    public IReadOnlyList<TacticalUnit> Units => _units;
    public IReadOnlyList<TelemetryEvent> Telemetry => _telemetry;
    public IReadOnlyList<CellControlState> CellControls => _cellControls.Values.OrderBy(cell => cell.Point.Y).ThenBy(cell => cell.Point.X).ToList();
    public IReadOnlyList<TerritoryBoundarySegment> TerritoryBoundaries => CreateTerritoryBoundaries();
    public string LastEvent => _lastEvent;
    public int ActiveCombatCount => _activeCombatCount;
    public bool HasHumanPlayer => Settings.Mode == GameMode.HumanVsAi;
    public int MovingUnitCount => _units.Count(unit => UnitParticipates(unit) && unit.IsMoving);
    public int MovingUnitGridStepCount => _units
        .Where(unit => UnitParticipates(unit) && unit.IsMoving)
        .Sum(unit => unit.RemainingGridSteps);

    public static GameSimulation Create(GameSettings settings)
    {
        var normalized = settings.Normalized();
        var playerCount = normalized.Mode == GameMode.AiOnly
            ? normalized.AiPlayers
            : normalized.AiPlayers + 1;
        var map = GameMapFactory.CreateDefault(playerCount);
        return new GameSimulation(normalized, map);
    }

    public void Start()
    {
        Phase = MatchPhase.Running;
        _lastEvent = "Match started";
    }

    public void Pause()
    {
        if (Phase == MatchPhase.Running)
        {
            Phase = MatchPhase.Paused;
            _lastEvent = "Paused";
        }
    }

    public void Resume()
    {
        if (Phase == MatchPhase.Paused)
        {
            Phase = MatchPhase.Running;
            _lastEvent = "Resumed";
        }
    }

    public void TogglePause()
    {
        if (Phase == MatchPhase.Paused)
            Resume();
        else if (Phase == MatchPhase.Running)
            Pause();
    }

    public void ApplyHumanCommand(HumanCommand command)
    {
        if (!HasHumanPlayer)
        {
            _lastEvent = "AI-only mode ignores human orders";
            return;
        }

        var human = Human;
        if (command.Directive.HasValue)
            human.Directive = command.Directive.Value;

        if (command.TargetCityId.HasValue && _cities.Any(city => city.Id == command.TargetCityId.Value))
            human.TargetCityId = command.TargetCityId.Value;

        if (command.LightPreference.HasValue)
            human.LightPreference = Math.Clamp(command.LightPreference.Value, 0.1, 0.95);

        if (command.ControlMode.HasValue)
            human.HumanControlMode = command.ControlMode.Value;

        if (command.TargetRegionId.HasValue)
            human.TargetRegionId = Math.Max(0, command.TargetRegionId.Value);

        if (command.GeneralRelocationCityId.HasValue && _cities.Any(city => city.Id == command.GeneralRelocationCityId.Value))
            human.GeneralRelocationCityId = command.GeneralRelocationCityId.Value;

        if (command.ScoutDirective.HasValue)
            human.ScoutDirective = command.ScoutDirective.Value;

        _lastEvent = $"Human set {human.Directive} on {_cities[human.TargetCityId].Name}";

        if (command.Directive.HasValue || command.TargetCityId.HasValue || command.TargetRegionId.HasValue)
            EmitGeneralRegionCommand(human);

        if (command.AssignReserveUnitId.HasValue)
        {
            var commanderUnitId = command.AssignReserveCommanderUnitId ?? human.CommanderUnitId;
            if (!commanderUnitId.HasValue ||
                !TryAssignReserveUnitToCommander(human.Id, command.AssignReserveUnitId.Value, commanderUnitId.Value))
            {
                _lastEvent = "Reserve assignment failed";
            }
        }

        if (command.AssignReserveGroupCommanderUnitId.HasValue || command.AssignReserveGroupSize.HasValue)
        {
            var commanderUnitId = command.AssignReserveGroupCommanderUnitId ?? human.CommanderUnitId;
            var groupSize = Math.Max(1, command.AssignReserveGroupSize ?? 2);
            if (!commanderUnitId.HasValue ||
                !TryAssignReserveGroupToCommander(human.Id, commanderUnitId.Value, groupSize))
            {
                _lastEvent = "Reserve group assignment failed";
            }
        }

        if (command.RecallCommanderUnitId.HasValue &&
            !TryRecallCommanderUnitToReserve(human.Id, command.RecallCommanderUnitId.Value))
        {
            _lastEvent = "Reserve recall failed";
        }

        if (command.RecallCommanderGroupCommanderUnitId.HasValue || command.RecallCommanderGroupSize.HasValue)
        {
            var commanderUnitId = command.RecallCommanderGroupCommanderUnitId ?? human.CommanderUnitId;
            var groupSize = Math.Max(1, command.RecallCommanderGroupSize ?? 2);
            if (!commanderUnitId.HasValue ||
                !TryRecallCommanderGroupToReserve(human.Id, commanderUnitId.Value, groupSize))
            {
                _lastEvent = "Reserve group recall failed";
            }
        }
    }

    public bool TryAssignReserveUnitToCommander(int playerId, int unitId, int commanderUnitId, string reasonCode = "manual")
    {
        var unit = _units.FirstOrDefault(unit => unit.Id == unitId);
        var commander = _units.FirstOrDefault(unit => unit.Id == commanderUnitId);
        if (playerId < 0 ||
            playerId >= _players.Count ||
            unit == null ||
            commander == null ||
            !UnitParticipates(unit) ||
            !UnitParticipates(commander) ||
            unit.PlayerId != playerId ||
            commander.PlayerId != playerId ||
            !CanAssignToCommander(unit) ||
            unit.AssignedCommanderUnitId.HasValue ||
            commander.Kind != UnitKind.Commander ||
            !commander.CommanderNumber.HasValue)
        {
            return false;
        }

        AssignUnitToCommander(unit, commander);
        AddCommandRecord(new CommandRecordSnapshot(
            Tick,
            playerId,
            GeneralToReserveLayer,
            _players[playerId].GeneralUnitId ?? commander.Id,
            unit.Id,
            commander.CommanderNumber,
            AssignReserveUnitCommand,
            null,
            null,
            null,
            null,
            1.0,
            $"{reasonCode};commanderUnit={commander.Id}",
            true));
        _lastEvent = $"{_players[playerId].Name} assigned {unit.Kind} #{unit.Id} to Commander {commander.CommanderNumber.Value}";
        return true;
    }

    public bool TryAssignReserveGroupToCommander(
        int playerId,
        int commanderUnitId,
        int maxUnits,
        string reasonCode = "manual")
    {
        var commander = _units.FirstOrDefault(unit => unit.Id == commanderUnitId);
        if (playerId < 0 ||
            playerId >= _players.Count ||
            commander == null ||
            !UnitParticipates(commander) ||
            commander.PlayerId != playerId ||
            commander.Kind != UnitKind.Commander ||
            !commander.CommanderNumber.HasValue)
        {
            return false;
        }

        var units = _units
            .Where(unit =>
                UnitParticipates(unit) &&
                unit.PlayerId == playerId &&
                CanAssignToCommander(unit) &&
                IsReserveUnit(unit))
            .OrderBy(unit => unit.Cell.OctileDistanceTo(commander.Cell))
            .ThenBy(unit => unit.Kind == UnitKind.Infantry ? 0 : 1)
            .ThenBy(unit => unit.Id)
            .Take(Math.Max(1, maxUnits))
            .ToList();
        if (units.Count == 0)
            return false;

        foreach (var unit in units)
        {
            AssignUnitToCommander(unit, commander);
            AddCommandRecord(new CommandRecordSnapshot(
                Tick,
                playerId,
                GeneralToReserveLayer,
                _players[playerId].GeneralUnitId ?? commander.Id,
                unit.Id,
                commander.CommanderNumber,
                AssignReserveGroupCommand,
                commander.Cell.X,
                commander.Cell.Y,
                null,
                commander.TargetRegionId,
                0.9,
                $"{reasonCode};commanderUnit={commander.Id};groupSize={units.Count}",
                true));
        }

        _lastEvent = $"{_players[playerId].Name} assigned {units.Count} reserve units to Commander {commander.CommanderNumber.Value}";
        return true;
    }

    public bool TryRecallCommanderUnitToReserve(int playerId, int unitId, string reasonCode = "general-recall")
    {
        var unit = _units.FirstOrDefault(unit => unit.Id == unitId);
        if (playerId < 0 ||
            playerId >= _players.Count ||
            unit == null ||
            !UnitParticipates(unit) ||
            unit.PlayerId != playerId ||
            !CanAssignToCommander(unit) ||
            !unit.AssignedCommanderUnitId.HasValue)
        {
            return false;
        }

        var commanderNumber = unit.AssignedCommanderNumber;
        unit.AssignedCommanderUnitId = null;
        unit.AssignedCommanderNumber = null;
        unit.IsProtectionDetail = false;
        unit.IsScout = false;
        ClearPath(unit, resetVisualState: true);

        AddCommandRecord(new CommandRecordSnapshot(
            Tick,
            playerId,
            GeneralToReserveLayer,
            _players[playerId].GeneralUnitId ?? unit.Id,
            unit.Id,
            commanderNumber,
            RecallCommanderUnitToReserveCommand,
            unit.Cell.X,
            unit.Cell.Y,
            null,
            null,
            1.0,
            reasonCode,
            true));
        _lastEvent = $"{_players[playerId].Name} recalled {unit.Kind} #{unit.Id} to reserve";
        return true;
    }

    public bool TryRecallCommanderGroupToReserve(
        int playerId,
        int commanderUnitId,
        int maxUnits,
        string reasonCode = "manual-recall")
    {
        var commander = _units.FirstOrDefault(unit => unit.Id == commanderUnitId);
        if (playerId < 0 ||
            playerId >= _players.Count ||
            commander == null ||
            !UnitParticipates(commander) ||
            commander.PlayerId != playerId ||
            commander.Kind != UnitKind.Commander ||
            !commander.CommanderNumber.HasValue)
        {
            return false;
        }

        var units = _units
            .Where(unit =>
                UnitParticipates(unit) &&
                unit.PlayerId == playerId &&
                CanAssignToCommander(unit) &&
                unit.AssignedCommanderUnitId == commander.Id)
            .OrderBy(unit => unit.Morale)
            .ThenBy(unit => unit.Health / Math.Max(1.0, TacticalUnit.DefaultHealth(unit.Kind)))
            .ThenByDescending(unit => unit.Cell.ChebyshevDistanceTo(commander.Cell))
            .ThenBy(unit => unit.Id)
            .Take(Math.Max(1, maxUnits))
            .ToList();
        if (units.Count == 0)
            return false;

        foreach (var unit in units)
        {
            unit.AssignedCommanderUnitId = null;
            unit.AssignedCommanderNumber = null;
            unit.IsProtectionDetail = false;
            unit.IsScout = false;
            ClearPath(unit, resetVisualState: true);

            AddCommandRecord(new CommandRecordSnapshot(
                Tick,
                playerId,
                GeneralToReserveLayer,
                _players[playerId].GeneralUnitId ?? commander.Id,
                unit.Id,
                commander.CommanderNumber,
                RecallCommanderGroupToReserveCommand,
                unit.Cell.X,
                unit.Cell.Y,
                null,
                commander.TargetRegionId,
                0.95,
                $"{reasonCode};commanderUnit={commander.Id};groupSize={units.Count}",
                true));
        }

        _lastEvent = $"{_players[playerId].Name} recalled {units.Count} units from Commander {commander.CommanderNumber.Value}";
        return true;
    }

    public void Step(int ticks)
    {
        for (var i = 0; i < ticks; i++)
            StepOne();
    }

    public MatchSnapshot CreateSnapshot()
    {
        UpdateLeaderHealth();
        UpdateEconomyState();
        UpdateScores();
        if (Phase == MatchPhase.Running)
            CheckVictory();

        var playerSnapshots = _players
            .Select(player => new PlayerSnapshot(
                player.Id,
                player.Name,
                player.Kind.ToString(),
                player.IsEliminated,
                player.Directive.ToString(),
                player.TargetCityId,
                Math.Round(player.Resources, 2),
                Math.Round(player.CommanderHealth, 1),
                Math.Round(player.GeneralHealth, 1),
                Math.Round(player.Score, 2),
                player.GenomeId,
                GenomeNeatController.ResolveArchetype(player.GenomeId),
                player.ConsecutiveHoldPlans,
                Math.Round(player.LastTaxIncome, 2),
                Math.Round(player.LastUpkeep, 2),
                Math.Round(player.LastPayrollDeficit, 2),
                player.ControlledCellCount,
                Math.Round(player.ControlledTaxValue, 2),
                player.SpawnCapacity,
                player.StrategyMode.ToString(),
                player.TargetRegionId,
                player.HumanControlMode.ToString()))
            .ToList();

        var citySnapshots = _cities
            .Select(city =>
            {
                var cityCommand = GetActiveCityProductionCommand(city);
                return new CitySnapshot(
                    city.Id,
                    city.Name,
                    city.OwnerId,
                    HasHumanPlayer ? CountUnitsOnCity(city, GameConstants.HumanPlayerId) : 0,
                    HasHumanPlayer ? CountEnemyUnitsOnCity(city, GameConstants.HumanPlayerId) : CountAllUnitsOnCity(city),
                    CountAllUnitsOnCity(city),
                    city.Neighbors.ToArray(),
                    cityCommand?.CommandType ?? "",
                    cityCommand?.ReasonCode ?? "");
            })
            .ToList();

        var unitSnapshots = _units
            .Where(UnitParticipates)
            .OrderBy(unit => unit.Id)
            .Select(unit => new UnitSnapshot(
                unit.Id,
                unit.PlayerId,
                unit.Kind.ToString(),
                unit.Cell.X,
                unit.Cell.Y,
                Math.Round(unit.Health, 2),
                Math.Round(MoraleRules.Clamp(unit.Morale), 3),
                MoraleRules.Band(unit.Morale).ToString(),
                unit.IsLeader,
                unit.CommanderNumber,
                unit.AssignedCommanderUnitId,
                unit.AssignedCommanderNumber,
                IsReserveUnit(unit),
                GetActiveCommandType(unit),
                GetLatestReportType(unit),
                CreateUnitVisibleEnemyIds(unit),
                CreateAssignedRegionIds(unit),
                CountAssignedUnits(unit),
                HasReserveRequest(unit),
                unit.TargetCityId,
                unit.TargetRegionId,
                unit.RemainingGridSteps,
                unit.IsProtectionDetail,
                unit.IsScout))
            .ToList();

        var human = HasHumanPlayer ? Human : null;
        var standings = CreateStandings();
        var cellControls = CreateCellControlSnapshots();
        var territoryBoundaries = CreateTerritoryBoundaries();
        var economies = CreateEconomySnapshots();
        var regions = CreateRegionSnapshots();
        var visibility = CreateVisibilitySnapshots();
        var recentCombatHits = _recentCombatHits
            .OrderBy(hit => hit.Tick)
            .ThenBy(hit => hit.DefenderPlayerId)
            .ThenBy(hit => hit.DefenderUnitId)
            .Select(hit => new RecentCombatHitSnapshot(
                hit.Tick,
                hit.AttackerPlayerId,
                hit.AttackerUnitId,
                hit.DefenderPlayerId,
                hit.DefenderUnitId,
                hit.DefenderKind.ToString(),
                hit.Cell.X,
                hit.Cell.Y,
                hit.Damage,
                hit.WasFatal))
            .ToList();
        var recentDeaths = _recentDeaths
            .OrderBy(death => death.Tick)
            .ThenBy(death => death.PlayerId)
            .ThenBy(death => death.UnitId)
            .Select(death => new RecentUnitDeathSnapshot(
                death.Tick,
                death.UnitId,
                death.PlayerId,
                death.Kind.ToString(),
                death.Cell.X,
                death.Cell.Y))
            .ToList();
        var commands = CreateCommandRecordSnapshots();
        var reports = CreateReportRecordSnapshots();

        return new MatchSnapshot(
            Tick,
            Phase.ToString(),
            WinnerId,
            WinnerId.HasValue ? _players[WinnerId.Value].Name : "",
            human?.TargetCityId ?? 0,
            human?.Directive.ToString() ?? "",
            human == null ? 0 : Math.Round(human.LightPreference, 2),
            MovingUnitCount,
            _lastEvent,
            CreateFingerprint(),
            playerSnapshots,
            citySnapshots,
            unitSnapshots,
            Settings.Mode.ToString(),
            HasHumanPlayer,
            standings,
            cellControls,
            territoryBoundaries,
            economies,
            regions,
            visibility,
            recentCombatHits,
            recentDeaths,
            commands,
            reports);
    }

    private string GetActiveCommandType(TacticalUnit unit)
        => GetActiveCommandForUnit(unit)?.CommandType ?? "";

    private CommandRecordSnapshot? GetActiveCityProductionCommand(CityNode city)
    {
        for (var index = _commandRecords.Count - 1; index >= 0; index--)
        {
            var command = _commandRecords[index];
            if (command.IsActive &&
                command.Layer == GeneralToCityLayer &&
                command.TargetCityId == city.Id)
            {
                return command;
            }
        }

        return null;
    }

    private CommandRecordSnapshot? GetActiveCommandForUnit(TacticalUnit unit, string? layer = null)
    {
        for (var index = _commandRecords.Count - 1; index >= 0; index--)
        {
            var command = _commandRecords[index];
            if (command.IsActive &&
                command.PlayerId == unit.PlayerId &&
                command.TargetUnitId == unit.Id &&
                (layer == null || command.Layer == layer))
            {
                return command;
            }
        }

        return null;
    }

    private string GetLatestReportType(TacticalUnit unit)
    {
        for (var index = _reportRecords.Count - 1; index >= 0; index--)
        {
            var report = _reportRecords[index];
            if (report.PlayerId == unit.PlayerId &&
                (report.SourceUnitId == unit.Id || report.TargetUnitId == unit.Id))
            {
                return report.ReportType;
            }
        }

        return "";
    }

    private IReadOnlyList<int> CreateUnitVisibleEnemyIds(TacticalUnit unit)
        => _units
            .Where(enemy => UnitParticipates(enemy) &&
                enemy.PlayerId != unit.PlayerId &&
                CanUnitSee(unit, enemy.Cell))
            .OrderBy(enemy => enemy.Id)
            .Select(enemy => enemy.Id)
            .ToList();

    private IReadOnlyList<int> CreateAssignedRegionIds(TacticalUnit unit)
        => unit.Kind == UnitKind.Commander
            ? CreateAssignedRegionIds(unit.PlayerId, unit.Id)
            : [];

    private IReadOnlyList<int> CreateAssignedRegionIds(int playerId, int commanderUnitId)
        => CreateCommanderRegionAssignments(playerId)
            .Where(assignment => assignment.CommanderUnitId == commanderUnitId)
            .Select(assignment => assignment.RegionId)
            .OrderBy(id => id)
            .ToList();

    private int CountAssignedUnits(TacticalUnit unit)
        => unit.Kind == UnitKind.Commander
            ? _units.Count(candidate => UnitParticipates(candidate) &&
                candidate.PlayerId == unit.PlayerId &&
                candidate.AssignedCommanderUnitId == unit.Id)
            : 0;

    private bool HasReserveRequest(TacticalUnit unit)
        => unit.Kind == UnitKind.Commander &&
            _reportRecords.Any(report =>
                report.PlayerId == unit.PlayerId &&
                report.SourceUnitId == unit.Id &&
                report.ReportType is NeedReinforcementsReport or ReserveRecallRequestedReport);

    private void AddCommandRecord(CommandRecordSnapshot record)
    {
        if (record.IsActive)
        {
            for (var index = 0; index < _commandRecords.Count; index++)
            {
                if (UsesSameActiveCommandSlot(_commandRecords[index], record))
                    _commandRecords[index] = _commandRecords[index] with { IsActive = false };
            }
        }

        _commandRecords.Add(record);
        TrimCommandRecords(record.PlayerId);
    }

    private static bool UsesSameActiveCommandSlot(CommandRecordSnapshot existing, CommandRecordSnapshot next)
        => existing.IsActive &&
            existing.PlayerId == next.PlayerId &&
            ((next.TargetUnitId.HasValue && existing.TargetUnitId == next.TargetUnitId && existing.Layer == next.Layer) ||
             (next.Layer == GeneralToCityLayer && existing.Layer == GeneralToCityLayer &&
              next.TargetCityId.HasValue && existing.TargetCityId == next.TargetCityId) ||
             (!next.TargetUnitId.HasValue && next.Layer != GeneralToCityLayer &&
              existing.Layer == next.Layer &&
              existing.SourceUnitId == next.SourceUnitId));

    private void TrimCommandRecords(int playerId)
    {
        while (_commandRecords.Count(command => command.PlayerId == playerId) > MaxCommandRecordsPerPlayer)
        {
            var index = _commandRecords.FindIndex(command => command.PlayerId == playerId);
            if (index < 0)
                return;

            _commandRecords.RemoveAt(index);
        }
    }

    private void AddReportRecord(ReportRecordSnapshot record)
    {
        _reportRecords.Add(record);
        TrimReportRecords(record.PlayerId);
    }

    private void TrimReportRecords(int playerId)
    {
        while (_reportRecords.Count(report => report.PlayerId == playerId) > MaxReportRecordsPerPlayer)
        {
            var index = _reportRecords.FindIndex(report => report.PlayerId == playerId);
            if (index < 0)
                return;

            _reportRecords.RemoveAt(index);
        }
    }

    private void EmitGeneralRegionCommand(PlayerState player)
    {
        foreach (var commander in GetPlayerCommanders(player.Id))
        {
            var objective = ResolveCommanderObjective(player, commander);
            AddCommandRecord(new CommandRecordSnapshot(
                Tick,
                player.Id,
                GeneralToCommanderLayer,
                player.GeneralUnitId ?? commander.Id,
                commander.Id,
                commander.CommanderNumber,
                objective.CommandType,
                objective.TargetCell.X,
                objective.TargetCell.Y,
                objective.TargetCityId,
                objective.TargetRegionId,
                objective.Priority,
                objective.ReasonCode,
                true));
        }
    }

    private static string GeneralRegionCommandType(PlayerDirective directive)
        => directive == PlayerDirective.Attack ? AttackRegionCommand : HoldRegionCommand;

    private CommanderObjective ResolveCommanderObjective(PlayerState player, TacticalUnit commander)
    {
        var region = CreateRegionSnapshots()
            .Where(region => region.PlayerId == player.Id && region.AssignedCommanderUnitId == commander.Id)
            .OrderBy(region => region.RegionId)
            .FirstOrDefault();
        var regionTarget = region == null ? commander.Cell : RegionCenterCell(region);

        if (TryGetRecentCommanderReport(player.Id, commander.Id, CommanderThreatenedReport, out var threatened))
        {
            return new CommanderObjective(
                HoldRegionCommand,
                ReportCellOrFallback(threatened, commander.Cell),
                null,
                region?.RegionId ?? player.TargetRegionId,
                1.0,
                $"report={CommanderThreatenedReport};strategy={player.StrategyMode}");
        }

        var reinforcementRequests = CountRecentCommanderReports(player.Id, commander.Id, NeedReinforcementsReport);
        if (reinforcementRequests >= 2)
        {
            return new CommanderObjective(
                HoldRegionCommand,
                regionTarget,
                null,
                region?.RegionId ?? player.TargetRegionId,
                0.9,
                $"report={NeedReinforcementsReport};count={reinforcementRequests};strategy={player.StrategyMode}");
        }

        if (player.Directive == PlayerDirective.Attack &&
            TryGetRecentCommanderReport(player.Id, commander.Id, RegionContestedReport, out var contested))
        {
            var contestedCell = ReportCellOrFallback(contested, regionTarget);
            return new CommanderObjective(
                AttackRegionCommand,
                contestedCell,
                null,
                region?.RegionId ?? player.TargetRegionId,
                1.0,
                $"report={RegionContestedReport};strategy={player.StrategyMode}");
        }

        if (player.Directive == PlayerDirective.Attack)
        {
            var city = ResolveAttackCityForCommander(player, commander);
            if (city != null)
            {
                return new CommanderObjective(
                    AttackRegionCommand,
                    city.GridPosition,
                    city.Id,
                    region?.RegionId ?? player.TargetRegionId,
                    0.85,
                    $"directive={player.Directive};strategy={player.StrategyMode}");
            }
        }

        var holdCity = player.Directive == PlayerDirective.Defend
            ? SelectThreatenedOwnedCity(player) ?? ResolveOwnedCityClosestTo(player, commander.Cell)
            : ResolveOwnedCityClosestTo(player, commander.Cell);
        return new CommanderObjective(
            HoldRegionCommand,
            holdCity?.GridPosition ?? regionTarget,
            holdCity?.Id,
            region?.RegionId ?? player.TargetRegionId,
            0.65,
            $"directive={player.Directive};strategy={player.StrategyMode}");
    }

    private static GridPoint RegionCenterCell(RegionSnapshot region)
        => new(region.BoundsX + Math.Max(0, region.Width - 1) / 2, region.BoundsY + Math.Max(0, region.Height - 1) / 2);

    private static GridPoint ReportCellOrFallback(ReportRecordSnapshot report, GridPoint fallback)
        => report.CellX.HasValue && report.CellY.HasValue
            ? new GridPoint(report.CellX.Value, report.CellY.Value)
            : fallback;

    private bool TryGetRecentCommanderReport(
        int playerId,
        int commanderUnitId,
        string reportType,
        out ReportRecordSnapshot report)
    {
        var found = _reportRecords
            .Where(report =>
                report.PlayerId == playerId &&
                report.SourceUnitId == commanderUnitId &&
                report.ReportType == reportType &&
                Tick - report.Tick <= PlanningInterval * 4)
            .OrderByDescending(report => report.Tick)
            .ThenBy(report => report.SourceUnitId)
            .FirstOrDefault();
        if (found == null)
        {
            report = null!;
            return false;
        }

        report = found;
        return true;
    }

    private int CountRecentCommanderReports(int playerId, int commanderUnitId, string reportType)
        => _reportRecords.Count(report =>
            report.PlayerId == playerId &&
            report.SourceUnitId == commanderUnitId &&
            report.ReportType == reportType &&
            Tick - report.Tick <= PlanningInterval * 4);

    private bool HasRecentCommanderReport(int playerId, string reportType)
        => _reportRecords.Any(report =>
            report.PlayerId == playerId &&
            report.Layer == CommanderToGeneralLayer &&
            report.ReportType == reportType &&
            Tick - report.Tick <= PlanningInterval * 4);

    private int CountRecentCommanderReports(int playerId, string reportType)
        => _reportRecords.Count(report =>
            report.PlayerId == playerId &&
            report.Layer == CommanderToGeneralLayer &&
            report.ReportType == reportType &&
            Tick - report.Tick <= PlanningInterval * 4);

    private void EmitCommanderUnitCommand(
        PlayerState player,
        TacticalUnit unit,
        string commandType,
        GridPoint targetCell,
        string reasonCode)
    {
        if (!CanAssignToCommander(unit) ||
            !unit.AssignedCommanderUnitId.HasValue ||
            unit.AssignedCommanderNumber is null)
        {
            return;
        }

        var commander = _units.FirstOrDefault(candidate =>
            candidate.Id == unit.AssignedCommanderUnitId.Value &&
            UnitParticipates(candidate) &&
            candidate.PlayerId == player.Id &&
            candidate.Kind == UnitKind.Commander);
        if (commander == null)
            return;

        AddCommandRecord(new CommandRecordSnapshot(
            Tick,
            player.Id,
            CommanderToUnitLayer,
            commander.Id,
            unit.Id,
            unit.AssignedCommanderNumber,
            commandType,
            targetCell.X,
            targetCell.Y,
            unit.TargetCityId,
            unit.TargetRegionId,
            commandType == AdvanceToCellCommand ? 0.85 : 0.55,
            reasonCode,
            true));
    }

    private IReadOnlyList<CommandRecordSnapshot> CreateCommandRecordSnapshots()
        => _commandRecords
            .OrderBy(command => command.Tick)
            .ThenBy(command => command.PlayerId)
            .ThenBy(command => command.SourceUnitId)
            .ThenBy(command => command.TargetUnitId ?? int.MaxValue)
            .ThenBy(command => command.CommandType, StringComparer.Ordinal)
            .ToList();

    private IReadOnlyList<ReportRecordSnapshot> CreateReportRecordSnapshots()
        => _reportRecords
            .OrderBy(report => report.Tick)
            .ThenBy(report => report.PlayerId)
            .ThenBy(report => report.SourceUnitId)
            .ThenBy(report => report.TargetUnitId ?? int.MaxValue)
            .ThenBy(report => report.ReportType, StringComparer.Ordinal)
            .ToList();

    public IReadOnlyList<TerritoryBoundarySegment> CreateTerritoryBoundaries()
    {
        var eligibleCells = FindBoundaryEligibleCells();
        var boundaries = new List<TerritoryBoundarySegment>();
        foreach (var cell in _cellControls.Values
                     .OrderBy(cell => cell.Point.Y)
                     .ThenBy(cell => cell.Point.X))
        {
            AddBoundaryIfNeeded(cell, new GridPoint(cell.Point.X + 1, cell.Point.Y), isVertical: true, eligibleCells, boundaries);
            AddBoundaryIfNeeded(cell, new GridPoint(cell.Point.X, cell.Point.Y + 1), isVertical: false, eligibleCells, boundaries);
        }

        return MergeTerritoryBoundarySegments(boundaries);
    }

    private HashSet<GridPoint> FindBoundaryEligibleCells()
    {
        var eligible = new HashSet<GridPoint>();
        var remaining = _cellControls.Values
            .Where(cell => cell.OwnerId >= 0)
            .Select(cell => cell.Point)
            .ToHashSet();

        while (remaining.Count > 0)
        {
            var start = remaining.First();
            var ownerId = _cellControls[start].OwnerId;
            var component = new List<GridPoint>();
            var queue = new Queue<GridPoint>();
            queue.Enqueue(start);
            remaining.Remove(start);

            while (queue.Count > 0)
            {
                var point = queue.Dequeue();
                component.Add(point);

                foreach (var neighbor in CardinalAdjacentCells(point))
                {
                    if (!remaining.Contains(neighbor) ||
                        !_cellControls.TryGetValue(neighbor, out var neighborCell) ||
                        neighborCell.OwnerId != ownerId)
                    {
                        continue;
                    }

                    remaining.Remove(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            if (component.Count < TerritoryBoundaryMinimumComponentCells)
                continue;

            foreach (var point in component)
                eligible.Add(point);
        }

        return eligible;
    }

    private static IReadOnlyList<TerritoryBoundarySegment> MergeTerritoryBoundarySegments(IReadOnlyList<TerritoryBoundarySegment> boundaries)
    {
        var merged = new List<TerritoryBoundarySegment>();
        foreach (var group in boundaries.GroupBy(segment => new TerritoryBoundaryMergeKey(
                     segment.FromX == segment.ToX,
                     segment.FromX == segment.ToX ? segment.FromX : segment.FromY,
                     segment.OwnerId,
                     segment.NeighborOwnerId)))
        {
            var intervals = group
                .Select(segment => new BoundaryInterval(
                    group.Key.IsVertical ? segment.FromY : segment.FromX,
                    group.Key.IsVertical ? segment.ToY : segment.ToX))
                .OrderBy(interval => interval.Start)
                .ThenBy(interval => interval.End)
                .ToList();

            if (intervals.Count == 0)
                continue;

            var currentStart = intervals[0].Start;
            var currentEnd = intervals[0].End;
            foreach (var interval in intervals.Skip(1))
            {
                if (interval.Start <= currentEnd)
                {
                    currentEnd = Math.Max(currentEnd, interval.End);
                    continue;
                }

                merged.Add(CreateMergedBoundary(group.Key, currentStart, currentEnd));
                currentStart = interval.Start;
                currentEnd = interval.End;
            }

            merged.Add(CreateMergedBoundary(group.Key, currentStart, currentEnd));
        }

        return merged
            .OrderBy(segment => segment.FromY)
            .ThenBy(segment => segment.FromX)
            .ThenBy(segment => segment.ToY)
            .ThenBy(segment => segment.ToX)
            .ThenBy(segment => segment.OwnerId)
            .ThenBy(segment => segment.NeighborOwnerId)
            .ToList();
    }

    private static TerritoryBoundarySegment CreateMergedBoundary(TerritoryBoundaryMergeKey key, int start, int end)
        => key.IsVertical
            ? new TerritoryBoundarySegment(key.Line, start, key.Line, end, key.OwnerId, key.NeighborOwnerId)
            : new TerritoryBoundarySegment(start, key.Line, end, key.Line, key.OwnerId, key.NeighborOwnerId);

    private readonly record struct TerritoryBoundaryMergeKey(bool IsVertical, int Line, int OwnerId, int NeighborOwnerId);

    private readonly record struct BoundaryInterval(int Start, int End);

    private void AddBoundaryIfNeeded(
        CellControlState cell,
        GridPoint neighborPoint,
        bool isVertical,
        IReadOnlySet<GridPoint> eligibleCells,
        List<TerritoryBoundarySegment> boundaries)
    {
        if (!_cellControls.TryGetValue(neighborPoint, out var neighbor) || cell.OwnerId == neighbor.OwnerId)
            return;

        var cellEligible = eligibleCells.Contains(cell.Point);
        var neighborEligible = eligibleCells.Contains(neighborPoint);
        if (!cellEligible && !neighborEligible)
            return;

        var ownerId = cellEligible ? cell.OwnerId : neighbor.OwnerId;
        var neighborOwnerId = cellEligible ? neighbor.OwnerId : cell.OwnerId;

        var segment = isVertical
            ? new TerritoryBoundarySegment(
                cell.Point.X + 1,
                cell.Point.Y,
                cell.Point.X + 1,
                cell.Point.Y + 1,
                ownerId,
                neighborOwnerId)
            : new TerritoryBoundarySegment(
                cell.Point.X,
                cell.Point.Y + 1,
                cell.Point.X + 1,
                cell.Point.Y + 1,
                ownerId,
                neighborOwnerId);

        boundaries.Add(segment);
    }

    public string CreateFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{Tick}|{Phase}|{WinnerId}|{Settings.Mode}|{Settings.HumanControlMode}|");
        foreach (var player in _players.OrderBy(p => p.Id))
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"p{player.Id}:{player.IsEliminated}:{player.Resources:F2}:{player.CommanderHealth:F1}:{player.GeneralHealth:F1}:{player.Directive}:{player.TargetCityId}:{player.TargetRegionId}:{player.LightPreference:F2}:{player.LastTaxIncome:F2}:{player.LastUpkeep:F2}:{player.LastPayrollDeficit:F2}:{player.ConsecutiveHoldPlans};");
        }

        foreach (var city in _cities.OrderBy(c => c.Id))
            builder.Append(CultureInfo.InvariantCulture, $"c{city.Id}:{city.OwnerId};");

        foreach (var cell in _cellControls.Values.OrderBy(cell => cell.Point.Y).ThenBy(cell => cell.Point.X))
            builder.Append(CultureInfo.InvariantCulture, $"t{cell.Point.X},{cell.Point.Y}:{cell.OwnerId};");

        foreach (var unit in _units.Where(unit => unit.IsAlive).OrderBy(unit => unit.Id))
        {
            var next = unit.IsMoving ? unit.Path[unit.PathIndex + 1] : unit.Cell;
            builder.Append(CultureInfo.InvariantCulture,
                $"u{unit.Id}:{unit.PlayerId}:{unit.Kind}:{unit.CommanderNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{unit.AssignedCommanderUnitId?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{unit.AssignedCommanderNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{unit.Cell.X},{unit.Cell.Y}:{next.X},{next.Y}:{unit.TargetCityId}:{unit.TargetRegionId}:{unit.PathIndex}:{unit.StepProgress:F2}:{unit.Health:F1}:{unit.Morale:F2}:{unit.IsProtectionDetail}:{unit.IsScout};");
        }

        return Fingerprint.Create(builder.ToString());
    }

    private PlayerState Human => _players[GameConstants.HumanPlayerId];

    private static IEnumerable<PlayerState> CreatePlayers(GameSettings settings, IReadOnlyList<int> homeCityIds)
    {
        if (settings.Mode == GameMode.AiOnly)
        {
            for (var i = 0; i < settings.AiPlayers; i++)
                yield return new PlayerState(i, $"AI {i}", PlayerKind.Ai, homeCityIds[i], GenomeNeatController.Create(settings.Seed, i).GenomeId);

            yield break;
        }

        yield return new PlayerState(0, "Human", PlayerKind.Human, homeCityIds[0], "human-general")
        {
            HumanControlMode = settings.HumanControlMode
        };

        for (var i = 1; i <= settings.AiPlayers; i++)
            yield return new PlayerState(i, $"AI {i}", PlayerKind.Ai, homeCityIds[i], GenomeNeatController.Create(settings.Seed, i).GenomeId);
    }

    private void PlaceInitialArmy(PlayerState player, CityNode home)
    {
        var homeCell = home.GridPosition;
        var general = CreateUnit(player.Id, UnitKind.General, homeCell);
        player.GeneralUnitId = general.Id;
        player.GeneralCityId = home.Id;

        var commanderOne = CreateUnitNear(player.Id, UnitKind.Commander, Offset(homeCell, 0, -1));
        var commanderTwo = CreateUnitNear(player.Id, UnitKind.Commander, Offset(homeCell, 1, -1));
        player.CommanderUnitId = commanderOne.Id;
        player.CommanderCityId = home.Id;

        AssignUnitToCommander(CreateUnitNear(player.Id, UnitKind.Infantry, Offset(homeCell, 1, 0)), commanderOne);
        AssignUnitToCommander(CreateUnitNear(player.Id, UnitKind.Infantry, Offset(homeCell, 0, 2)), commanderOne);
        AssignUnitToCommander(CreateUnitNear(player.Id, UnitKind.Tank, Offset(homeCell, -2, 0)), commanderTwo);
    }

    private TacticalUnit CreateUnitNear(int playerId, UnitKind kind, GridPoint preferredCell)
    {
        var cell = FindNearestEmptyPassableCell(preferredCell, includePreferred: true, allowCityCells: false)
            ?? throw new InvalidOperationException($"No empty passable cell is available for {kind}.");

        return CreateUnit(playerId, kind, cell);
    }

    private TacticalUnit CreateUnit(int playerId, UnitKind kind, GridPoint cell)
    {
        var unit = new TacticalUnit
        {
            Id = _nextUnitId++,
            PlayerId = playerId,
            Kind = kind,
            Cell = cell,
            CurrentPosition = _grid.ToMapPoint(cell),
            CommanderNumber = kind == UnitKind.Commander ? ReserveCommanderNumber(playerId) : null,
            Health = TacticalUnit.DefaultHealth(kind)
        };
        unit.VisualFromCell = cell;
        unit.VisualToCell = cell;
        unit.VisualFromPosition = unit.CurrentPosition;
        unit.VisualToPosition = unit.CurrentPosition;

        _units.Add(unit);
        return unit;
    }

    private int ReserveCommanderNumber(int playerId)
        => _players[playerId].NextCommanderNumber++;

    private static bool CanAssignToCommander(TacticalUnit unit)
        => unit.Kind is UnitKind.Infantry or UnitKind.Tank;

    private static bool IsReserveUnit(TacticalUnit unit)
        => CanAssignToCommander(unit) && !unit.AssignedCommanderUnitId.HasValue;

    private void ReleaseCommanderUnitsToReserve(TacticalUnit deadCommander)
    {
        foreach (var unit in _units)
        {
            if (unit.AssignedCommanderUnitId == deadCommander.Id &&
                UnitParticipates(unit))
            {
                unit.AssignedCommanderUnitId = null;
                unit.AssignedCommanderNumber = null;
                unit.IsProtectionDetail = false;
                unit.IsScout = false;
            }
        }
    }

    private static void AssignUnitToCommander(TacticalUnit unit, TacticalUnit commander)
    {
        unit.AssignedCommanderUnitId = commander.Id;
        unit.AssignedCommanderNumber = commander.CommanderNumber;
    }

    private IReadOnlyList<TacticalUnit> GetPlayerCommanders(int playerId)
        => _units
            .Where(unit => UnitParticipates(unit) &&
                unit.PlayerId == playerId &&
                unit.Kind == UnitKind.Commander)
            .OrderBy(unit => unit.CommanderNumber ?? int.MaxValue)
            .ThenBy(unit => unit.Id)
            .ToList();

    private IReadOnlyList<CommanderRegionAssignment> CreateCommanderRegionAssignments(int playerId)
    {
        if (_explicitRegionAssignments.TryGetValue(playerId, out var explicit_))
            return explicit_.Where(a => _units.Any(u => u.Id == a.CommanderUnitId && UnitParticipates(u))).ToList();

        return [];
    }

    private void InitializeExplicitRegionAssignments(PlayerState player)
    {
        var commanders = GetPlayerCommanders(player.Id);
        if (commanders.Count == 0)
        {
            _explicitRegionAssignments[player.Id] = [];
            return;
        }

        var assignments = new List<CommanderRegionAssignment>();
        for (var index = 0; index < commanders.Count; index++)
        {
            var bounds = CreateCommanderRegionBounds(index, commanders.Count);
            assignments.Add(new CommanderRegionAssignment(
                player.Id * 100 + (commanders[index].CommanderNumber ?? index + 1),
                commanders[index].Id,
                commanders[index].CommanderNumber,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height));
        }

        _explicitRegionAssignments[player.Id] = assignments;
    }

    private void UpdateExplicitRegionAssignments(PlayerState player)
    {
        if (!_explicitRegionAssignments.TryGetValue(player.Id, out var assignments))
        {
            InitializeExplicitRegionAssignments(player);
            return;
        }

        var livingCommanderIds = GetPlayerCommanders(player.Id).Select(c => c.Id).ToHashSet();
        assignments.RemoveAll(a => !livingCommanderIds.Contains(a.CommanderUnitId));

        if (assignments.Count == 0 && livingCommanderIds.Count > 0)
        {
            InitializeExplicitRegionAssignments(player);
            return;
        }

        var assignedCommanderIds = assignments.Select(a => a.CommanderUnitId).ToHashSet();
        var commandersWithRegions = assignments.Count;
        if (commandersWithRegions > 0)
        {
            var totalWidth = _grid.Width;
            var baseWidth = totalWidth / commandersWithRegions;
            for (var i = 0; i < assignments.Count; i++)
            {
                var x = i * baseWidth;
                var width = i == assignments.Count - 1 ? totalWidth - x : baseWidth;
                assignments[i] = assignments[i] with { BoundsX = x, BoundsY = 0, Width = Math.Max(1, width), Height = _grid.Height };
            }
        }
    }

    private void AssignRegionToCommander(PlayerState player, TacticalUnit commander)
    {
        if (!_explicitRegionAssignments.TryGetValue(player.Id, out var assignments))
        {
            _explicitRegionAssignments[player.Id] = assignments = [];
        }

        if (assignments.Any(a => a.CommanderUnitId == commander.Id))
            return;

        var regionId = player.Id * 100 + (commander.CommanderNumber ?? 1);
        var commandersWithRegions = assignments.Count + 1;
        var baseWidth = _grid.Width / commandersWithRegions;

        assignments.Add(new CommanderRegionAssignment(
            regionId,
            commander.Id,
            commander.CommanderNumber,
            0, 0, 1, _grid.Height));

        for (var i = 0; i < assignments.Count; i++)
        {
            var x = i * baseWidth;
            var width = i == assignments.Count - 1 ? _grid.Width - x : baseWidth;
            assignments[i] = assignments[i] with { BoundsX = x, BoundsY = 0, Width = Math.Max(1, width), Height = _grid.Height };
        }
    }

    private void AssignRegionsToUnassignedCommanders(PlayerState player)
    {
        if (!_explicitRegionAssignments.TryGetValue(player.Id, out var assignments))
            return;

        var assignedCommanderIds = assignments.Select(a => a.CommanderUnitId).ToHashSet();
        foreach (var commander in GetPlayerCommanders(player.Id))
        {
            if (assignedCommanderIds.Contains(commander.Id))
                continue;

            AssignRegionToCommander(player, commander);
            var generalCommand = GetActiveCommandForUnit(commander, GeneralToCommanderLayer);
            if (generalCommand == null)
                EmitGeneralRegionCommand(player);
        }
    }

    private (int X, int Y, int Width, int Height) CreateCommanderRegionBounds(int commanderIndex, int commanderCount)
    {
        var baseWidth = _grid.Width / commanderCount;
        var x = commanderIndex * baseWidth;
        var width = commanderIndex == commanderCount - 1 ? _grid.Width - x : baseWidth;
        return (x, 0, Math.Max(1, width), _grid.Height);
    }

    private void StepOne()
    {
        if (Phase != MatchPhase.Running)
            return;

        Tick++;
        _activeCombatCount = 0;

        if (Tick % ProductionInterval == 0)
            RunEconomyTick();

        ResolveAdjacentCombats();
        ApplyMoraleAuras();
        MoveUnits();
        ResolveAdjacentCombats();
        ResolveCityCapture();
        ResolveTerritoryCapture();
        UpdateLeaderHealth();
        UpdateEconomyState();
        DetectVisibilityEvents();

        if (Tick % PlanningInterval == 0)
            PlanAndDispatch();

        DetectEconomyTelemetry();
        UpdateScores();
        CheckVictory();
    }

    private void RunEconomyTick()
    {
        var occupied = BuildOccupiedCells();
        foreach (var player in _players.Where(p => !p.IsEliminated))
        {
            var ownedCities = _cities.Where(c => c.OwnerId == player.Id).ToList();
            var taxIncome = ComputeTaxIncome(player.Id);
            player.Resources += taxIncome;

            var upkeep = ComputeUpkeep(player.Id);
            var paid = Math.Min(player.Resources, upkeep);
            player.Resources -= paid;
            var deficit = Math.Max(0, upkeep - paid);
            var deficitRatio = upkeep <= 0 ? 0 : deficit / upkeep;

            player.LastTaxIncome = taxIncome;
            player.LastUpkeep = upkeep;
            player.LastPayrollDeficit = deficit;
            player.LastPayrollDeficitRatio = deficitRatio;

            if (deficitRatio > 0)
                ApplyPayrollMoraleLoss(player.Id, deficitRatio);

            var requestedReinforcements = player.ReplenishmentRequest > 0;
            var plannedBudget = player.ReserveBudget > 0 ? player.ReserveBudget : player.Resources;
            var spendingCap = Math.Min(player.Resources, Math.Max(0, plannedBudget) + player.ReplenishmentRequest);
            if (player.StrategyMode == StrategyMode.Rebuild || player.EconomyRisk > 0.65)
                spendingCap = Math.Min(spendingCap, Math.Max(0, player.Resources - upkeep));
            if (requestedReinforcements && player.Resources >= InfantryCost)
                spendingCap = Math.Max(spendingCap, InfantryCost);

            var spent = 0.0;
            foreach (var city in ownedCities)
            {
                if (spent >= spendingCap)
                {
                    EmitCityProductionCommand(player, city, HoldProductionCommand, null, "SpendingCapReached");
                    continue;
                }

                var outputCell = FindAdjacentEmptyCell(city.GridPosition, occupied);
                if (!outputCell.HasValue)
                {
                    EmitCityProductionCommand(player, city, HoldProductionCommand, null, "SpawnBlocked");
                    continue;
                }

                var commanderProductionTarget = ResolveDesiredCommanderCount(player, ownedCities.Count);
                var livingCommanders = GetPlayerCommanders(player.Id);
                var needsCommander = livingCommanders.Count < commanderProductionTarget && player.Resources >= CommanderCost;

                if (needsCommander)
                {
                    if (player.Resources < CommanderCost)
                    {
                        EmitCityProductionCommand(player, city, HoldProductionCommand, UnitKind.Commander, "InsufficientTreasury");
                        continue;
                    }

                    if (player.StrategyMode == StrategyMode.Rebuild && player.Resources < CommanderCost + upkeep)
                    {
                        EmitCityProductionCommand(player, city, HoldProductionCommand, UnitKind.Commander, "RebuildMode");
                        continue;
                    }

                    if (spent + CommanderCost > spendingCap && spent > 0)
                    {
                        EmitCityProductionCommand(player, city, HoldProductionCommand, UnitKind.Commander, "SpendingCapReached");
                        continue;
                    }

                    var commander = CreateUnit(player.Id, UnitKind.Commander, outputCell.Value);
                    occupied[commander.Cell] = commander;
                    player.Resources -= CommanderCost;
                    spent += CommanderCost;
                    EmitCityProductionCommand(player, city, ProduceCommanderCommand, UnitKind.Commander, "CommanderNeeded");
                    _lastEvent = $"{player.Name} produced Commander {commander.CommanderNumber} near {city.Name}";
                    needsCommander = false;
                    continue;
                }

                var buildInfantry = requestedReinforcements ||
                    player.Resources < TankCost ||
                    ((Tick + city.Id + player.Id) % 100) / 100.0 < player.LightPreference;
                var kind = buildInfantry ? UnitKind.Infantry : UnitKind.Tank;
                var cost = kind == UnitKind.Infantry ? InfantryCost : TankCost;

                if (player.StrategyMode == StrategyMode.Rebuild && player.Resources < cost + upkeep)
                {
                    EmitCityProductionCommand(player, city, HoldProductionCommand, kind, "RebuildMode");
                    continue;
                }

                if (player.Resources < cost && kind == UnitKind.Tank)
                {
                    kind = UnitKind.Infantry;
                    cost = InfantryCost;
                }

                if (player.Resources < cost)
                {
                    EmitCityProductionCommand(player, city, HoldProductionCommand, kind, "InsufficientTreasury");
                    continue;
                }

                if (spent + cost > spendingCap && spent > 0)
                {
                    EmitCityProductionCommand(player, city, HoldProductionCommand, kind, "SpendingCapReached");
                    continue;
                }

                var commandType = kind == UnitKind.Infantry ? ProduceInfantryCommand : ProduceTankCommand;
                EmitCityProductionCommand(player, city, commandType, kind, "production");
                var unit = CreateUnit(player.Id, kind, outputCell.Value);
                occupied[unit.Cell] = unit;
                player.Resources -= cost;
                spent += cost;
                player.ReplenishmentRequest = Math.Max(0, player.ReplenishmentRequest - cost);
                _lastEvent = $"{player.Name} bought {kind} near {city.Name}";
            }
        }

        UpdateEconomyState();
    }

    private void EmitCityProductionCommand(
        PlayerState player,
        CityNode city,
        string commandType,
        UnitKind? unitKind,
        string reasonCode)
    {
        AddCommandRecord(new CommandRecordSnapshot(
            Tick,
            player.Id,
            GeneralToCityLayer,
            player.GeneralUnitId ?? -1,
            null,
            null,
            commandType,
            city.GridPosition.X,
            city.GridPosition.Y,
            city.Id,
            null,
            commandType == HoldProductionCommand ? 0.0 : 0.75,
            $"{reasonCode};city={city.Id};kind={unitKind?.ToString() ?? ""};treasury={player.Resources:F1}",
            true));
    }

    private static int ResolveDesiredCommanderCount(PlayerState player, int ownedCityCount)
        => Math.Clamp(ownedCityCount, 2, 4);

    private void MoveUnits()
    {
        var occupied = BuildOccupiedCells();
        var continuingReservations = BuildMovingDestinationReservations();
        var reservedDestinations = new HashSet<GridPoint>();
        foreach (var unit in _units
                     .Where(UnitParticipates)
                     .OrderBy(unit => unit.PlayerId)
                     .ThenBy(unit => unit.Id))
        {
            if (FindAdjacentEnemy(unit, occupied) != null && MoraleRules.Band(unit.Morale) != MoraleBand.Routed)
            {
                ClearPath(unit, resetVisualState: true);
                continue;
            }

            if (unit.TargetCityId.HasValue)
                EnsureUnitPath(unit);

            if (!unit.IsMoving)
                continue;

            var visualFromCell = unit.Cell;
            var visualFromPosition = unit.CurrentPosition;
            var nextCell = unit.Path[unit.PathIndex + 1];
            if (!CanEnterNextCell(unit, nextCell, occupied, continuingReservations, reservedDestinations))
            {
                ClearPath(unit, resetVisualState: true);
                continue;
            }

            reservedDestinations.Add(nextCell);
            var moveCost = MoveStepCost(unit.Cell, nextCell);
            unit.StepProgress += unit.Speed / moveCost;
            if (unit.StepProgress >= 1.0)
            {
                occupied.Remove(unit.Cell);
                unit.Cell = nextCell;
                unit.PathIndex++;
                unit.StepProgress = 0;
                unit.CurrentPosition = ResolveMovementPosition(unit);
                occupied[unit.Cell] = unit;
                RecordVisualMovement(unit, visualFromCell, nextCell, visualFromPosition);

                if (!unit.IsMoving)
                {
                    AddReachedTargetReport(unit);
                    ClearPath(unit);
                }
            }
            else
            {
                unit.CurrentPosition = ResolveMovementPosition(unit);
                RecordVisualMovement(unit, visualFromCell, nextCell, visualFromPosition);
            }
        }
    }

    private void ResolveAdjacentCombats()
    {
        var occupied = BuildOccupiedCells();
        var attacks = new List<(TacticalUnit Attacker, TacticalUnit Defender, double Damage)>();
        foreach (var attacker in _units
                     .Where(UnitParticipates)
                     .OrderBy(unit => unit.PlayerId)
                     .ThenBy(unit => unit.Id))
        {
            var defender = FindAdjacentEnemy(attacker, occupied);
            if (defender == null)
                continue;

            attacks.Add((attacker, defender, CalculateAttackDamage(attacker, defender)));
        }

        _activeCombatCount += attacks.Count;
        foreach (var (attacker, defender, damage) in attacks)
        {
            if (!UnitParticipates(attacker) || !UnitParticipates(defender) || !AreAdjacent(attacker.Cell, defender.Cell))
                continue;
            if (MoraleRules.Band(attacker.Morale) == MoraleBand.Routed)
            {
                ClearPath(attacker, resetVisualState: true);
                AddTelemetry(attacker.PlayerId, "RoutedUnitHeld", $"unit={attacker.Id};enemy={defender.Id}");
                continue;
            }

            defender.Health -= damage;
            defender.Morale = MoraleRules.Clamp(defender.Morale - 0.035);
            attacker.Morale = MoraleRules.Clamp(attacker.Morale + 0.01);
            AddUnderAttackReport(defender, attacker, damage);
            _recentCombatHits.Add(new RecentCombatHit(
                Tick,
                attacker.PlayerId,
                attacker.Id,
                defender.PlayerId,
                defender.Id,
                defender.Kind,
                defender.Cell,
                damage,
                defender.Health <= 0));
            _lastEvent = $"{_players[attacker.PlayerId].Name} attacked {_players[defender.PlayerId].Name}'s {defender.Kind}";

            if (CountFriendlyAttackVectors(attacker.PlayerId, defender.Cell) >= 2)
                AddTelemetry(attacker.PlayerId, "FlankingAttack", $"target={defender.Id}");
            if (defender.Kind == UnitKind.General)
                AddTelemetry(attacker.PlayerId, "DecapitationAttempt", $"target={defender.PlayerId}");
            if (defender.Kind == UnitKind.Commander)
                ApplyCommanderUnderAttackMorale(defender, attacker.PlayerId);
            if (MoraleRules.Band(defender.Morale) is MoraleBand.Routed or MoraleBand.RoutRisk)
                AddTelemetry(attacker.PlayerId, "RoutExploitation", $"target={defender.Id};morale={defender.Morale:F2}");
        }

        RemoveDefeatedUnits();
        TrimRecentCombatVisuals();
    }

    private int CountFriendlyAttackVectors(int attackerPlayerId, GridPoint defenderCell)
        => _units.Count(unit => UnitParticipates(unit) &&
            unit.PlayerId == attackerPlayerId &&
            AreAdjacent(unit.Cell, defenderCell));

    private void AddUnderAttackReport(TacticalUnit defender, TacticalUnit attacker, double damage)
    {
        if (defender.Kind is not (UnitKind.Infantry or UnitKind.Tank) ||
            !defender.AssignedCommanderUnitId.HasValue ||
            !CanUnitSee(defender, attacker.Cell) ||
            defender.PlayerId < 0 ||
            defender.PlayerId >= _players.Count)
        {
            return;
        }

        AddReportRecord(new ReportRecordSnapshot(
            Tick,
            defender.PlayerId,
            UnitToCommanderLayer,
            defender.Id,
            defender.AssignedCommanderUnitId,
            defender.AssignedCommanderNumber,
            UnderAttackReport,
            defender.Cell.X,
            defender.Cell.Y,
            Math.Round(Math.Clamp(damage / TacticalUnit.DefaultHealth(defender.Kind), 0, 1), 3),
            $"attackerPlayer={attacker.PlayerId};attackerUnit={attacker.Id}"));
    }

    private void AddLowMoraleReport(TacticalUnit unit, MoraleBand band)
    {
        if (!CanAssignToCommander(unit) ||
            !unit.AssignedCommanderUnitId.HasValue ||
            unit.PlayerId < 0 ||
            unit.PlayerId >= _players.Count)
        {
            return;
        }

        AddReportRecord(new ReportRecordSnapshot(
            Tick,
            unit.PlayerId,
            UnitToCommanderLayer,
            unit.Id,
            unit.AssignedCommanderUnitId,
            unit.AssignedCommanderNumber,
            LowMoraleReport,
            unit.Cell.X,
            unit.Cell.Y,
            band == MoraleBand.Routed ? 1.0 : 0.7,
            $"band={band};morale={unit.Morale:F2}"));
    }

    private void AddReachedTargetReport(TacticalUnit unit)
    {
        if (!CanAssignToCommander(unit) ||
            !unit.AssignedCommanderUnitId.HasValue ||
            unit.PlayerId < 0 ||
            unit.PlayerId >= _players.Count)
        {
            return;
        }

        AddReportRecord(new ReportRecordSnapshot(
            Tick,
            unit.PlayerId,
            UnitToCommanderLayer,
            unit.Id,
            unit.AssignedCommanderUnitId,
            unit.AssignedCommanderNumber,
            ReachedTargetReport,
            unit.Cell.X,
            unit.Cell.Y,
            0.35,
            $"targetCity={unit.TargetCityId?.ToString(CultureInfo.InvariantCulture) ?? ""};targetRegion={unit.TargetRegionId?.ToString(CultureInfo.InvariantCulture) ?? ""}"));
    }

    private void ApplyCommanderUnderAttackMorale(TacticalUnit commander, int attackerPlayerId)
    {
        if (commander.PlayerId >= 0 &&
            commander.PlayerId < _players.Count)
        {
            AddReportRecord(new ReportRecordSnapshot(
                Tick,
                commander.PlayerId,
                CommanderToGeneralLayer,
                commander.Id,
                _players[commander.PlayerId].GeneralUnitId,
                commander.CommanderNumber,
                CommanderThreatenedReport,
                commander.Cell.X,
                commander.Cell.Y,
                1.0,
                $"attackerPlayer={attackerPlayerId}"));
        }

        foreach (var unit in _units.Where(unit => UnitParticipates(unit) &&
                     unit.PlayerId == commander.PlayerId &&
                     unit.Id != commander.Id &&
                     unit.Cell.ChebyshevDistanceTo(commander.Cell) <= 4))
        {
            unit.Morale = MoraleRules.Clamp(unit.Morale - 0.035);
        }

        AddTelemetry(attackerPlayerId, "CommanderPressure", $"target={commander.PlayerId};commander={commander.Id}");
    }

    private void RemoveDefeatedUnits()
    {
        var defeated = _units.Where(unit => unit.Health <= 0).ToList();
        foreach (var unit in defeated)
        {
            if (unit.PlayerId < 0 || unit.PlayerId >= _players.Count)
                continue;

            var player = _players[unit.PlayerId];
            _recentDeaths.Add(new RecentUnitDeath(Tick, unit.Id, unit.PlayerId, unit.Cell, unit.Kind));
            ApplyFriendlyDeathMorale(unit);
            if (unit.Kind == UnitKind.Commander)
            {
                player.CommanderUnitId = _units
                    .Where(candidate => candidate.Id != unit.Id &&
                        candidate.Health > 0 &&
                        candidate.PlayerId == player.Id &&
                        candidate.Kind == UnitKind.Commander)
                    .OrderBy(candidate => candidate.CommanderNumber ?? int.MaxValue)
                    .ThenBy(candidate => candidate.Id)
                    .Select(candidate => (int?)candidate.Id)
                    .FirstOrDefault();
                player.CommanderHealth = player.CommanderUnitId.HasValue
                    ? _units.First(candidate => candidate.Id == player.CommanderUnitId.Value).Health
                    : 0;
                player.CommanderLeaderlessUntilTick = Tick + 48;
                ReleaseCommanderUnitsToReserve(unit);
                ApplyCommanderDeathMorale(player.Id, unit.Cell);
                _lastEvent = $"{player.Name} lost a commander";
            }
            else if (unit.Kind == UnitKind.General)
            {
                player.GeneralHealth = 0;
                player.GeneralUnitId = null;
                player.IsEliminated = true;
                ApplyGeneralDeathMorale(player.Id, unit.Cell);
                var neutralizedCities = NeutralizeCitiesForPlayer(player.Id);
                var neutralizedCells = NeutralizeCellsForPlayer(player.Id);
                _lastEvent = neutralizedCities + neutralizedCells > 0
                    ? $"{player.Name} lost a general; territory neutralized"
                    : $"{player.Name} lost a general";
            }
        }

        _units.RemoveAll(unit => unit.Health <= 0 ||
            unit.PlayerId >= 0 && unit.PlayerId < _players.Count && _players[unit.PlayerId].IsEliminated);
        TrimRecentCombatVisuals();
    }

    private void TrimRecentCombatVisuals()
    {
        _recentCombatHits.RemoveAll(hit => Tick - hit.Tick > RecentCombatVisualLifetimeTicks);
        if (_recentCombatHits.Count > MaxRecentCombatHits)
            _recentCombatHits.RemoveRange(0, _recentCombatHits.Count - MaxRecentCombatHits);

        _recentDeaths.RemoveAll(death => Tick - death.Tick > RecentCombatVisualLifetimeTicks);
        if (_recentDeaths.Count > MaxRecentDeaths)
            _recentDeaths.RemoveRange(0, _recentDeaths.Count - MaxRecentDeaths);
    }

    private void ResolveCityCapture()
    {
        var occupied = BuildOccupiedCells();
        foreach (var city in _cities)
        {
            if (!occupied.TryGetValue(city.GridPosition, out var occupant) || !UnitParticipates(occupant))
                continue;

            if (city.OwnerId != occupant.PlayerId)
                CaptureCity(city, occupant);
        }
    }

    private void CaptureCity(CityNode city, TacticalUnit occupant)
    {
        city.OwnerId = occupant.PlayerId;
        if (_cellControls.TryGetValue(city.GridPosition, out var cell))
        {
            cell.OwnerId = occupant.PlayerId;
            cell.LastChangedTick = Tick;
        }

        occupant.Morale = MoraleRules.Clamp(occupant.Morale + 0.12);
        _lastEvent = $"{_players[occupant.PlayerId].Name} captured {city.Name}";
    }

    private int NeutralizeCitiesForPlayer(int playerId)
    {
        var neutralizedCities = 0;
        foreach (var city in _cities.Where(city => city.OwnerId == playerId))
        {
            city.OwnerId = GameConstants.NeutralPlayerId;
            neutralizedCities++;
        }

        return neutralizedCities;
    }

    private int NeutralizeCellsForPlayer(int playerId)
    {
        var neutralizedCells = 0;
        foreach (var cell in _cellControls.Values.Where(cell => cell.OwnerId == playerId))
        {
            cell.OwnerId = GameConstants.NeutralPlayerId;
            cell.LastChangedTick = Tick;
            neutralizedCells++;
        }

        return neutralizedCells;
    }

    private void InitializeCellControls()
    {
        var cityCells = _cities.ToDictionary(city => city.GridPosition, city => city);
        foreach (var cell in _grid.Cells.Where(cell => cell.IsPassable))
        {
            cityCells.TryGetValue(cell.Point, out var city);
            var ownerId = city?.OwnerId ?? GameConstants.NeutralPlayerId;
            _cellControls[cell.Point] = new CellControlState(cell.Point, ownerId, TaxValue(cell.Point, cell.Terrain, city != null), city != null);
        }
    }

    private double TaxValue(GridPoint point, TerrainKind terrain, bool isCity)
    {
        if (isCity)
            return 8;

        var baseValue = terrain switch
        {
            TerrainKind.Water => 0,
            TerrainKind.Road => 2,
            TerrainKind.Hill => 2,
            TerrainKind.Rock => 1,
            TerrainKind.Forest => 1,
            _ => 1
        };

        return baseValue + (_cities.Any(city => city.GridPosition.ChebyshevDistanceTo(point) == 1) ? 2 : 0);
    }

    private void ResolveTerritoryCapture()
    {
        var occupied = BuildOccupiedCells();
        foreach (var unit in _units.Where(UnitParticipates).OrderBy(unit => unit.PlayerId).ThenBy(unit => unit.Id))
        {
            CaptureCell(unit.Cell, unit.PlayerId);
            if (unit.Kind != UnitKind.Tank)
                continue;

            foreach (var cell in AdjacentCells(unit.Cell))
            {
                if (!_grid.IsPassable(cell))
                    continue;
                if (occupied.TryGetValue(cell, out var occupant) && occupant.PlayerId != unit.PlayerId)
                    continue;

                CaptureCell(cell, unit.PlayerId);
            }
        }

        foreach (var city in _cities)
        {
            if (_cellControls.TryGetValue(city.GridPosition, out var control) && city.OwnerId != control.OwnerId)
                city.OwnerId = control.OwnerId;
        }
    }

    private void CaptureCell(GridPoint point, int ownerId)
    {
        if (!_cellControls.TryGetValue(point, out var control) || control.OwnerId == ownerId)
            return;

        control.OwnerId = ownerId;
        control.LastChangedTick = Tick;
    }

    private double ComputeTaxIncome(int playerId)
        => _cellControls.Values
            .Where(cell => cell.OwnerId == playerId)
            .Sum(cell => cell.TaxValue);

    private double ComputeUpkeep(int playerId)
        => _units
            .Where(unit => UnitParticipates(unit) && unit.PlayerId == playerId)
            .Sum(unit => unit.Kind switch
            {
                UnitKind.Infantry => 0.20,
                UnitKind.Tank => 0.45,
                UnitKind.Commander => CommanderUpkeep,
                UnitKind.General => 0.25,
                _ => 0.20
            });

    private void ApplyPayrollMoraleLoss(int playerId, double deficitRatio)
    {
        foreach (var unit in _units.Where(unit => UnitParticipates(unit) && unit.PlayerId == playerId))
            unit.Morale = MoraleRules.Clamp(unit.Morale - deficitRatio * 0.12);

        AddTelemetry(playerId, "PayrollDeficit", $"ratio={deficitRatio:F2}");
    }

    private void ApplyMoraleAuras()
    {
        foreach (var unit in _units.Where(UnitParticipates))
        {
            var beforeBand = MoraleRules.Band(unit.Morale);
            var commanderNear = _units.Any(other => UnitParticipates(other) &&
                other.PlayerId == unit.PlayerId &&
                other.Kind == UnitKind.Commander &&
                other.Cell.ChebyshevDistanceTo(unit.Cell) <= 2);
            if (commanderNear)
                unit.Morale = MoraleRules.Clamp(unit.Morale + 0.006);

            var generalNear = _units.Any(other => UnitParticipates(other) &&
                other.PlayerId == unit.PlayerId &&
                other.Kind == UnitKind.General &&
                other.Cell.ChebyshevDistanceTo(unit.Cell) <= 3);
            var cityNear = _cities.Any(city =>
                city.OwnerId == unit.PlayerId &&
                city.GridPosition.ChebyshevDistanceTo(unit.Cell) <= 2);
            if (unit.Morale < MoraleRules.CautiousThreshold && (generalNear || cityNear))
                unit.Morale = MoraleRules.Clamp(unit.Morale + (generalNear ? 0.01 : 0) + (cityNear ? 0.008 : 0));

            var enemies = _units.Count(other => UnitParticipates(other) &&
                other.PlayerId != unit.PlayerId &&
                other.Cell.ChebyshevDistanceTo(unit.Cell) <= 2);
            var allies = _units.Count(other => UnitParticipates(other) &&
                other.PlayerId == unit.PlayerId &&
                other.Cell.ChebyshevDistanceTo(unit.Cell) <= 2);
            if (enemies > allies + 1)
                unit.Morale = MoraleRules.Clamp(unit.Morale - 0.008 * (enemies - allies));

            var afterBand = MoraleRules.Band(unit.Morale);
            if (beforeBand == MoraleBand.Routed && afterBand != MoraleBand.Routed)
                AddTelemetry(unit.PlayerId, "Rally", $"unit={unit.Id};band={afterBand}");
            if (beforeBand != afterBand && afterBand is MoraleBand.Routed or MoraleBand.RoutRisk)
                AddLowMoraleReport(unit, afterBand);
        }
    }

    private void ApplyFriendlyDeathMorale(TacticalUnit defeated)
    {
        foreach (var unit in _units.Where(unit => UnitParticipates(unit) &&
                     unit.PlayerId == defeated.PlayerId &&
                     unit.Id != defeated.Id &&
                     unit.Cell.ChebyshevDistanceTo(defeated.Cell) <= 3))
        {
            unit.Morale = MoraleRules.Clamp(unit.Morale - 0.08);
        }
    }

    private void ApplyCommanderDeathMorale(int playerId, GridPoint deathCell)
    {
        foreach (var unit in _units.Where(unit => UnitParticipates(unit) && unit.PlayerId == playerId))
        {
            var penalty = unit.Cell.ChebyshevDistanceTo(deathCell) <= 5 ? 0.16 : 0.08;
            unit.Morale = MoraleRules.Clamp(unit.Morale - penalty);
        }
    }

    private void ApplyGeneralDeathMorale(int playerId, GridPoint deathCell)
    {
        foreach (var unit in _units.Where(unit => unit.IsAlive && unit.PlayerId == playerId))
            unit.Morale = MoraleRules.Clamp(unit.Morale - 0.35);
    }

    private void UpdateEconomyState()
    {
        foreach (var player in _players)
        {
            player.ControlledCellCount = _cellControls.Values.Count(cell => cell.OwnerId == player.Id);
            player.ControlledTaxValue = ComputeTaxIncome(player.Id);
            player.LastUpkeep = ComputeUpkeep(player.Id);
            if (Tick == 0 && player.LastTaxIncome <= 0)
                player.LastTaxIncome = player.ControlledTaxValue;
            player.SpawnCapacity = CountSpawnCapacity(player.Id);
            player.EconomyRisk = CalculateEconomyRisk(player);
            player.GeneralSecurity = CalculateGeneralSecurity(player);
            player.ReserveBudget = Math.Max(0, player.Resources - player.LastUpkeep);
        }
    }

    private int CountSpawnCapacity(int playerId)
    {
        var occupied = BuildOccupiedCells();
        return _cities
            .Where(city => city.OwnerId == playerId)
            .Sum(city => CardinalAdjacentCells(city.GridPosition).Count(point =>
                _grid.IsPassable(point) &&
                !occupied.ContainsKey(point) &&
                !IsCityCell(point)));
    }

    private double CalculateEconomyRisk(PlayerState player)
    {
        var taxPressure = player.LastUpkeep <= 0 ? 0 : Math.Clamp((player.LastUpkeep - player.LastTaxIncome) / player.LastUpkeep, 0, 1);
        var deficit = Math.Clamp(player.LastPayrollDeficitRatio, 0, 1);
        var treasuryPressure = player.Resources < InfantryCost ? 0.25 : 0;
        return Math.Clamp(taxPressure * 0.45 + deficit * 0.45 + treasuryPressure, 0, 1);
    }

    private double CalculateGeneralSecurity(PlayerState player)
    {
        var general = GetLeaderUnit(player, UnitKind.General);
        if (general == null)
            return 0;

        var nearestEnemy = _units
            .Where(unit => UnitParticipates(unit) && unit.PlayerId != player.Id)
            .Select(unit => (double?)unit.Cell.OctileDistanceTo(general.Cell))
            .OrderBy(distance => distance)
            .FirstOrDefault();
        if (!nearestEnemy.HasValue)
            return 1;

        return Math.Clamp(nearestEnemy.Value / 8.0, 0, 1);
    }

    private void PlanAndDispatch()
    {
        foreach (var player in _players.Where(p => !p.IsEliminated))
        {
            UpdateExplicitRegionAssignments(player);
            AssignRegionsToUnassignedCommanders(player);

            if (player.Kind == PlayerKind.Ai)
            {
                UpdateAiPlan(player);
                ApplyAiReserveCommands(player);
            }
            else if (player.HumanControlMode is HumanControlMode.Commander or HumanControlMode.GeneralAndCommander or HumanControlMode.DotChaos)
            {
                UpdateHumanCommanderModeStrategy(player);
            }

            AssignUnitOrders(player);
        }
    }

    private void UpdateHumanCommanderModeStrategy(PlayerState player)
    {
        player.StrategyMode = player.EconomyRisk > 0.65
            ? StrategyMode.Rebuild
            : player.GeneralSecurity < 0.45 ? StrategyMode.ProtectGeneral : StrategyMode.Advance;
        player.ReserveBudget = Math.Max(0, player.Resources - player.LastUpkeep);
        player.TargetRegionId = player.Id;
        player.ScoutDirective = player.HumanControlMode != HumanControlMode.Commander;
    }

    private void UpdateAiPlan(PlayerState player)
    {
        var controller = _generalControllers[player.Id];
        var observation = BuildObservation(player);
        var primaryCommander = GetLeaderUnit(player, UnitKind.Commander);
        var hasForeignCity = _cities.Any(city => city.OwnerId != player.Id);
        var threatened = HasRecentCommanderReport(player.Id, CommanderThreatenedReport) || player.GeneralSecurity < 0.45;
        var contested = HasRecentCommanderReport(player.Id, RegionContestedReport);
        var repeatedReinforcementRequests = CountRecentCommanderReports(player.Id, NeedReinforcementsReport) >= 2;
        var plannedDirective = threatened || repeatedReinforcementRequests
            ? PlayerDirective.Hold
            : hasForeignCity || contested ? PlayerDirective.Attack : PlayerDirective.Hold;
        var holdDecay = ResolveHoldDecay(plannedDirective, player.ConsecutiveHoldPlans, HasNearbyEnemyPressure(player));

        player.Directive = holdDecay.Directive;
        player.ConsecutiveHoldPlans = holdDecay.ConsecutiveHoldPlans;
        var targetCity = primaryCommander == null ? null : ResolveAttackCityForCommander(player, primaryCommander);
        if (targetCity != null)
            player.TargetCityId = targetCity.Id;
        var primaryRegion = primaryCommander == null
            ? null
            : CreateRegionSnapshots()
                .Where(region => region.PlayerId == player.Id && region.AssignedCommanderUnitId == primaryCommander.Id)
                .OrderBy(region => region.RegionId)
                .FirstOrDefault();
        if (primaryRegion != null)
            player.TargetRegionId = primaryRegion.RegionId;
        var seedBias = StableUnitInterval(Settings.Seed, player.Id);
        player.LightPreference = Math.Clamp(0.58 + seedBias * 0.12 + player.EconomyRisk * 0.25 + (repeatedReinforcementRequests ? 0.15 : 0), 0.25, 0.9);
        player.StrategyMode = player.EconomyRisk > 0.65
            ? StrategyMode.Rebuild
            : threatened ? StrategyMode.ProtectGeneral : StrategyMode.Advance;
        player.ReserveBudget = Math.Round(Math.Max(0, player.Resources) * Math.Clamp(1.0 - player.EconomyRisk * 0.55, 0.15, 1.0), 2);
        player.ScoutDirective = player.StrategyMode != StrategyMode.Rebuild || player.GeneralSecurity > 0.7;
        var planningLeader = GetLeaderUnit(player, UnitKind.General) ?? GetLeaderUnit(player, UnitKind.Commander);
        player.GeneralRelocationCityId = player.StrategyMode == StrategyMode.ProtectGeneral && planningLeader != null
            ? ResolveRetreatCity(player, planningLeader).Id
            : null;

        AddTelemetry(
            player.Id,
            "AiPlan",
            $"owned={observation.OwnedCityRatio:F2},cells={observation.ControlledCellRatio:F2},strength={observation.StrengthRatio:F2},risk={observation.EconomyRisk:F2}",
            $"simple-attack:{player.Directive}:target={player.TargetCityId}:contested={contested}:threat={threatened}:reinforce={repeatedReinforcementRequests}" +
                (holdDecay.Decayed ? ":hold-decayed" : ""),
            contested ? 0.12 : threatened ? -0.08 : 0.04,
            controller.ControllerId);
    }

    private void ApplyAiReserveCommands(PlayerState player)
    {
        foreach (var commander in GetPlayerCommanders(player.Id))
        {
            var recalled = false;
            if (TryGetRecentCommanderReport(player.Id, commander.Id, ReserveRecallRequestedReport, out _))
                recalled = TryRecallCommanderGroupToReserve(player.Id, commander.Id, maxUnits: 1, "recall-request");

            if (recalled)
                continue;

            if (TryGetRecentCommanderReport(player.Id, commander.Id, NeedReinforcementsReport, out _))
                TryAssignReserveGroupToCommander(player.Id, commander.Id, maxUnits: 2, "reinforcement");
        }
    }

    internal static (PlayerDirective Directive, int ConsecutiveHoldPlans, bool Decayed) ResolveHoldDecay(
        PlayerDirective plannedDirective,
        int previousConsecutiveHoldPlans,
        bool hasNearbyEnemyPressure)
    {
        if (plannedDirective != PlayerDirective.Hold)
            return (plannedDirective, 0, false);

        var holdPlans = previousConsecutiveHoldPlans + 1;
        if (holdPlans > MaxQuietHoldPlans && !hasNearbyEnemyPressure)
            return (PlayerDirective.Attack, 0, true);

        return (PlayerDirective.Hold, holdPlans, false);
    }

    private bool HasNearbyEnemyPressure(PlayerState player)
    {
        var enemyCells = _units
            .Where(unit => UnitParticipates(unit) && unit.PlayerId != player.Id)
            .Select(unit => unit.Cell)
            .ToList();
        if (enemyCells.Count == 0)
            return false;

        var friendlyAnchorCells = _units
            .Where(unit => UnitParticipates(unit) && unit.PlayerId == player.Id)
            .Select(unit => unit.Cell)
            .Concat(_cities.Where(city => city.OwnerId == player.Id).Select(city => city.GridPosition));

        return friendlyAnchorCells.Any(anchor =>
            enemyCells.Any(enemy => enemy.ChebyshevDistanceTo(anchor) <= 4));
    }

    private AiObservation BuildObservation(PlayerState player)
    {
        var totalCities = _cities.Count;
        var owned = _cities.Count(city => city.OwnerId == player.Id);
        var ownUnits = CountUnits(player.Id);
        var enemyUnits = _players.Where(p => p.Id != player.Id && !p.IsEliminated).Sum(p => CountUnits(p.Id));
        var commanderCell = GetLeaderUnit(player, UnitKind.Commander)?.Cell ?? _cities[player.HomeCityId].GridPosition;

        var cityObservations = _cities
            .Select(city =>
            {
                var distance = city.GridPosition.OctileDistanceTo(commanderCell);
                var cityVisible = city.OwnerId == player.Id || IsVisibleTo(player.Id, city.GridPosition);
                return new CityObservation(
                    city.Id,
                    city.OwnerId == player.Id ? 1 : 0,
                    city.OwnerId == GameConstants.NeutralPlayerId ? 1 : 0,
                    city.OwnerId >= 0 && city.OwnerId != player.Id ? 1 : 0,
                    Normalize(CountUnitsNearCity(city, player.Id, 2), 8),
                    cityVisible ? Normalize(CountEnemyUnitsNearCity(city, player.Id, 2), 8) : 0,
                    Normalize(distance, _grid.Width + _grid.Height));
            })
            .ToList();

        return new AiObservation(
            Tick,
            player.Id,
            owned / (double)totalCities,
            ownUnits / Math.Max(1.0, ownUnits + enemyUnits),
            Normalize(player.Resources, 20),
            Normalize(player.CommanderHealth, TacticalUnit.DefaultHealth(UnitKind.Commander)),
            Normalize(player.GeneralHealth, TacticalUnit.DefaultHealth(UnitKind.General)),
            Tick / (double)Settings.MatchLengthTicks,
            cityObservations,
            ControlledCellRatio(player.Id),
            Normalize(player.LastTaxIncome, 30),
            Normalize(player.Resources, 50),
            Normalize(player.LastUpkeep, 20),
            Normalize(player.LastPayrollDeficit, 10),
            Normalize(player.SpawnCapacity, Math.Max(1, _cities.Count)),
            player.EconomyRisk,
            player.GeneralSecurity);
    }

    private GeneralPerception BuildGeneralPerception(PlayerState player, AiObservation observation)
        => new(
            observation,
            player.Id,
            player.TargetRegionId,
            ControlledCellRatio(player.Id),
            player.LastTaxIncome,
            player.Resources,
            player.LastUpkeep,
            player.LastPayrollDeficit,
            player.LastPayrollDeficitRatio,
            player.SpawnCapacity,
            player.EconomyRisk,
            player.GeneralSecurity,
            CreateRegionSnapshots().Where(region => region.PlayerId == player.Id).ToList());

    private void AssignUnitOrders(PlayerState player)
    {
        EmitGeneralRegionCommand(player);
        AssignSimpleCommanderRoles(player);

        foreach (var unit in _units
                     .Where(unit => UnitParticipates(unit) && unit.PlayerId == player.Id && IsReserveUnit(unit))
                     .OrderBy(unit => unit.Id))
        {
            ClearPath(unit, resetVisualState: true);
        }

        foreach (var commander in GetPlayerCommanders(player.Id))
        {
            var generalCommand = GetActiveCommandForUnit(commander, GeneralToCommanderLayer);
            if (generalCommand == null)
                continue;

            ApplyGeneralCommandToCommander(player, commander, generalCommand);
            ApplyCommanderCommandToAssignedUnits(player, commander, generalCommand);
        }

        ApplyGeneralSelfMovement(player);
    }

    private void ApplyGeneralSelfMovement(PlayerState player)
    {
        var general = GetLeaderUnit(player, UnitKind.General);
        if (general == null)
            return;

        if (player.GeneralRelocationCityId.HasValue)
        {
            TryAssignGeneralRelocationPath(player, general, ResolveTargetCity(player));
            return;
        }

        if (player.Kind == PlayerKind.Human &&
            player.Directive == PlayerDirective.Attack &&
            player.TargetCityId >= 0 &&
            player.TargetCityId < _cities.Count)
        {
            AssignPathToTarget(general, _cities[player.TargetCityId]);
            return;
        }

        if (player.StrategyMode == StrategyMode.ProtectGeneral || player.GeneralSecurity < 0.45)
            AssignPathToTarget(general, ResolveRetreatCity(player, general));
    }

    private void ApplyGeneralCommandToCommander(
        PlayerState player,
        TacticalUnit commander,
        CommandRecordSnapshot command)
    {
        commander.TargetRegionId = command.TargetRegionId;
        commander.TargetCityId = command.TargetCityId;
        if (!TryGetCommandTargetCell(command, out var targetCell))
        {
            ClearPath(commander, resetVisualState: true);
            return;
        }

        if (command.CommandType == HoldRegionCommand && commander.Cell.ChebyshevDistanceTo(targetCell) <= 1)
        {
            ClearPath(commander, resetVisualState: true);
            return;
        }

        AssignPathToCommandTarget(commander, targetCell, command.TargetCityId);
    }

    private void ApplyCommanderCommandToAssignedUnits(
        PlayerState player,
        TacticalUnit commander,
        CommandRecordSnapshot generalCommand)
    {
        if (!TryGetCommandTargetCell(generalCommand, out var generalTargetCell))
            generalTargetCell = commander.Cell;

        var assignedUnits = _units
            .Where(unit => UnitParticipates(unit) &&
                unit.PlayerId == player.Id &&
                CanAssignToCommander(unit) &&
                unit.AssignedCommanderUnitId == commander.Id)
            .OrderBy(unit => unit.Id)
            .ToList();

        foreach (var unit in assignedUnits)
        {
            unit.TargetRegionId = generalCommand.TargetRegionId;
            unit.TargetCityId = generalCommand.TargetCityId;
            var moraleBand = MoraleRules.Band(unit.Morale);
            var enemyAdjacent = FindAdjacentEnemy(unit, BuildOccupiedCells(unit.Id)) != null;
            if (moraleBand == MoraleBand.Routed ||
                !enemyAdjacent && unit.Morale < MoraleRules.CautiousThreshold)
            {
                var retreat = ResolveRetreatCity(player, unit);
                EmitCommanderUnitCommand(player, unit, DefendCellCommand, retreat.GridPosition, "morale-retreat");
                AssignPathToTarget(unit, retreat);
                continue;
            }

            if (generalCommand.CommandType == HoldRegionCommand)
            {
                var defendCell = ResolveDefendCell(unit, commander, generalTargetCell);
                EmitCommanderUnitCommand(player, unit, DefendCellCommand, defendCell, "general=HoldRegion");
                if (unit.Cell.ChebyshevDistanceTo(defendCell) <= 1)
                    ClearPath(unit, resetVisualState: true);
                else
                    AssignPathToCommandTarget(unit, defendCell, generalCommand.TargetCityId);
                continue;
            }

            EmitCommanderUnitCommand(player, unit, AdvanceToCellCommand, generalTargetCell, "general=AttackRegion");
            AssignPathToCommandTarget(unit, generalTargetCell, generalCommand.TargetCityId);
        }
    }

    private static bool TryGetCommandTargetCell(CommandRecordSnapshot command, out GridPoint targetCell)
    {
        if (command.TargetCellX.HasValue && command.TargetCellY.HasValue)
        {
            targetCell = new GridPoint(command.TargetCellX.Value, command.TargetCellY.Value);
            return true;
        }

        targetCell = default;
        return false;
    }

    private GridPoint ResolveDefendCell(TacticalUnit unit, TacticalUnit commander, GridPoint targetCell)
        => FindNearestEmptyPassableCell(
                targetCell,
                includePreferred: true,
                exceptUnitId: unit.Id,
                allowCityCells: false)
            ?? commander.Cell;

    private void AssignPathToCommandTarget(TacticalUnit unit, GridPoint targetCell, int? targetCityId)
    {
        if (targetCityId.HasValue && targetCityId.Value >= 0 && targetCityId.Value < _cities.Count)
        {
            AssignPathToTarget(unit, _cities[targetCityId.Value]);
            return;
        }

        var destination = FindNearestEmptyPassableCell(
            targetCell,
            includePreferred: true,
            exceptUnitId: unit.Id,
            allowCityCells: !IsCityCell(targetCell));
        AssignPathToCell(unit, destination ?? targetCell, targetCityId);
    }

    private void AssignSimpleCommanderRoles(PlayerState player)
    {
        ClearSpecialRoles(player);
        foreach (var commander in GetPlayerCommanders(player.Id))
        {
            var assigned = _units
                .Where(unit => UnitParticipates(unit) &&
                    unit.PlayerId == player.Id &&
                    unit.Kind is UnitKind.Infantry or UnitKind.Tank &&
                    unit.AssignedCommanderUnitId == commander.Id)
                .OrderBy(unit => unit.Cell.OctileDistanceTo(commander.Cell))
                .ThenBy(unit => unit.Id)
                .ToList();
            var protection = assigned.FirstOrDefault();
            if (protection != null)
                protection.IsProtectionDetail = true;

            if (!player.ScoutDirective || player.StrategyMode == StrategyMode.Rebuild)
                continue;

            var scout = assigned
                .Where(unit => unit.Id != protection?.Id)
                .OrderBy(unit => unit.Kind == UnitKind.Infantry ? 0 : 1)
                .ThenByDescending(unit => DistanceToNearestForeignCity(player, unit.Cell))
                .ThenBy(unit => unit.Id)
                .FirstOrDefault();
            if (scout != null)
                scout.IsScout = true;
        }
    }

    private void ApplyCommanderRegionOrder(PlayerState player)
    {
        var commander = GetLeaderUnit(player, UnitKind.Commander);
        if (commander == null)
        {
            ClearSpecialRoles(player);
            return;
        }

        var region = CreateRegionSnapshots()
            .Where(region => region.PlayerId == player.Id)
            .OrderBy(region => region.AssignedCommanderUnitId == commander.Id ? 0 : 1)
            .ThenBy(region => region.RegionId)
            .First();
        var action = _commanderController.Decide(new CommanderPerception(
            Tick,
            player.Id,
            commander.Id,
            region,
            player.Directive,
            player.LastPayrollDeficitRatio));

        player.TargetRegionId = action.TargetRegionId;
        player.ReplenishmentRequest = action.RequestReinforcements ? Math.Max(InfantryCost, player.LastUpkeep * 0.5) : 0;
        if (action.RequestReinforcements || player.HumanControlMode != HumanControlMode.General)
            player.LightPreference = Math.Clamp(action.InfantryPreference, 0.1, 0.95);
        commander.TargetRegionId = action.TargetRegionId;
        EmitGeneralRegionCommand(player);
        if (player.Kind == PlayerKind.Ai || player.HumanControlMode != HumanControlMode.General)
            AssignSpecialRoles(player, commander, action.RequestReinforcements);
        else
            ClearSpecialRoles(player);
    }

    private void ClearSpecialRoles(PlayerState player)
    {
        foreach (var unit in _units.Where(unit => UnitParticipates(unit) && unit.PlayerId == player.Id))
        {
            unit.IsProtectionDetail = false;
            unit.IsScout = false;
        }
    }

    private void AssignSpecialRoles(PlayerState player, TacticalUnit commander, bool requestReinforcements)
    {
        ClearSpecialRoles(player);
        var assignable = _units
            .Where(unit => UnitParticipates(unit) &&
                unit.PlayerId == player.Id &&
                unit.Kind is UnitKind.Infantry or UnitKind.Tank &&
                unit.AssignedCommanderUnitId == commander.Id)
            .OrderBy(unit => unit.Cell.OctileDistanceTo(commander.Cell))
            .ThenBy(unit => unit.Id)
            .ToList();

        var protectionCount = player.StrategyMode == StrategyMode.ProtectGeneral || requestReinforcements ? 2 : 1;
        foreach (var unit in assignable.Take(protectionCount))
            unit.IsProtectionDetail = true;

        if (!player.ScoutDirective || player.StrategyMode == StrategyMode.Rebuild)
            return;

        var scout = assignable
            .Where(unit => !unit.IsProtectionDetail)
            .OrderBy(unit => unit.Kind == UnitKind.Infantry ? 0 : 1)
            .ThenByDescending(unit => DistanceToNearestForeignCity(player, unit.Cell))
            .ThenBy(unit => unit.Id)
            .FirstOrDefault();
        if (scout != null)
            scout.IsScout = true;
    }

    private double DistanceToNearestForeignCity(PlayerState player, GridPoint cell)
        => _cities
            .Where(city => city.OwnerId != player.Id)
            .Select(city => (double?)city.GridPosition.OctileDistanceTo(cell))
            .OrderBy(distance => distance)
            .FirstOrDefault()
            ?? 0;

    private bool TryAssignProtectionDetailPath(PlayerState player, TacticalUnit unit)
    {
        if (!unit.IsProtectionDetail)
            return false;

        var commander = GetLeaderUnit(player, UnitKind.Commander);
        if (commander == null)
            return false;

        if (unit.Cell.ChebyshevDistanceTo(commander.Cell) <= 1)
        {
            EmitCommanderUnitCommand(player, unit, DefendCellCommand, unit.Cell, "protection-detail");
            ClearPath(unit, resetVisualState: true);
            return true;
        }

        var destination = FindNearestEmptyPassableCell(
            commander.Cell,
            includePreferred: false,
            exceptUnitId: unit.Id,
            allowCityCells: false);
        if (!destination.HasValue)
            return false;

        EmitCommanderUnitCommand(player, unit, DefendCellCommand, destination.Value, "protection-detail");
        AssignPathToCell(unit, destination.Value, player.CommanderCityId);
        return true;
    }

    private bool TryAssignScoutPath(PlayerState player, TacticalUnit unit)
    {
        if (!unit.IsScout)
            return false;

        var target = ResolveScoutTarget(player, unit);
        if (target == null)
            return false;

        EmitCommanderUnitCommand(player, unit, AdvanceToCellCommand, target.GridPosition, "scout");
        AssignPathToTarget(unit, target);
        return true;
    }

    private CityNode? ResolveScoutTarget(PlayerState player, TacticalUnit unit)
        => _cities
            .Where(city => city.OwnerId != player.Id)
            .OrderBy(city => city.OwnerId == GameConstants.NeutralPlayerId ? 0 : 1)
            .ThenByDescending(city => city.Production + (city.OwnerId >= 0 ? 2 : 0))
            .ThenBy(city => city.GridPosition.OctileDistanceTo(unit.Cell))
            .ThenBy(city => city.Id)
            .FirstOrDefault();

    private bool TryAssignGeneralRelocationPath(PlayerState player, TacticalUnit general, CityNode strategicTarget)
    {
        if (player.GeneralRelocationCityId.HasValue &&
            player.GeneralRelocationCityId.Value >= 0 &&
            player.GeneralRelocationCityId.Value < _cities.Count)
        {
            AssignPathToTarget(general, _cities[player.GeneralRelocationCityId.Value]);
            return true;
        }

        if (player.StrategyMode == StrategyMode.ProtectGeneral || player.GeneralSecurity < 0.45)
        {
            AssignPathToTarget(general, ResolveRetreatCity(player, general));
            return true;
        }

        if (player.Kind == PlayerKind.Human &&
            player.HumanControlMode is HumanControlMode.GeneralAndCommander or HumanControlMode.DotChaos)
        {
            AssignPathToTarget(general, strategicTarget);
            return true;
        }

        return false;
    }

    private UnitPerception BuildUnitPerception(TacticalUnit unit)
    {
        var occupied = BuildOccupiedCells(unit.Id);
        return new UnitPerception(
            Tick,
            unit.Id,
            unit.PlayerId,
            unit.Kind,
            unit.Cell,
            Normalize(unit.Health, TacticalUnit.DefaultHealth(unit.Kind)),
            MoraleRules.Clamp(unit.Morale),
            FindAdjacentEnemy(unit, occupied) != null,
            AdjacentCells(unit.Cell).Count(point =>
                _cellControls.TryGetValue(point, out var control) && control.OwnerId == unit.PlayerId));
    }

    private CityNode ResolveRetreatCity(PlayerState player, TacticalUnit unit)
        => _cities
            .Where(city => city.OwnerId == player.Id)
            .OrderBy(city => city.GridPosition.OctileDistanceTo(unit.Cell))
            .ThenBy(city => city.Id)
            .FirstOrDefault()
            ?? _cities[player.HomeCityId];

    private CityNode ResolveTargetCity(PlayerState player)
    {
        if (player.Directive == PlayerDirective.Defend)
        {
            var threatened = SelectThreatenedOwnedCity(player);
            if (threatened != null)
                return threatened;

            if (player.TargetCityId >= 0 && player.TargetCityId < _cities.Count)
                return _cities[player.TargetCityId];
        }

        if (player.TargetCityId >= 0 &&
            player.TargetCityId < _cities.Count &&
            _cities[player.TargetCityId].OwnerId != player.Id)
        {
            return _cities[player.TargetCityId];
        }

        var origin = GetLeaderUnit(player, UnitKind.Commander)?.Cell ?? _cities[player.HomeCityId].GridPosition;
        return _cities
            .Where(city => city.OwnerId != player.Id)
            .OrderBy(city => city.GridPosition.OctileDistanceTo(origin))
            .ThenBy(city => city.Id)
            .FirstOrDefault()
            ?? _cities[player.HomeCityId];
    }

    private CityNode? ResolveAttackCityForCommander(PlayerState player, TacticalUnit commander)
    {
        if (player.TargetCityId >= 0 &&
            player.TargetCityId < _cities.Count &&
            _cities[player.TargetCityId].OwnerId != player.Id)
        {
            return _cities[player.TargetCityId];
        }

        return _cities
            .Where(city => city.OwnerId != player.Id)
            .OrderBy(city => city.OwnerId == GameConstants.NeutralPlayerId ? 0 : 1)
            .ThenBy(city => city.GridPosition.OctileDistanceTo(commander.Cell))
            .ThenByDescending(city => city.Production)
            .ThenBy(city => city.Id)
            .FirstOrDefault();
    }

    private CityNode? ResolveOwnedCityClosestTo(PlayerState player, GridPoint cell)
        => _cities
            .Where(city => city.OwnerId == player.Id)
            .OrderBy(city => city.GridPosition.OctileDistanceTo(cell))
            .ThenBy(city => city.Id)
            .FirstOrDefault();

    private CityNode? SelectThreatenedOwnedCity(PlayerState player)
        => _cities
            .Where(city => city.OwnerId == player.Id)
            .Select(city => new
            {
                City = city,
                Pressure = CountEnemyUnitsNearCity(city, player.Id, 3)
            })
            .Where(item => item.Pressure > 0)
            .OrderByDescending(item => item.Pressure)
            .ThenBy(item => item.City.Id)
            .Select(item => item.City)
            .FirstOrDefault();

    private void EnsureUnitPath(TacticalUnit unit)
    {
        if (!unit.TargetCityId.HasValue || unit.TargetCityId.Value < 0 || unit.TargetCityId.Value >= _cities.Count)
            return;

        if (unit.IsMoving && unit.PathIndex < unit.Path.Count && unit.Path[unit.PathIndex] == unit.Cell)
            return;

        AssignPathToTarget(unit, _cities[unit.TargetCityId.Value]);
    }

    private void AssignPathToTarget(TacticalUnit unit, CityNode target)
    {
        var destination = SelectDestinationCell(unit, target);
        AssignPathToCell(unit, destination, target.Id);
    }

    private void AssignPathToCell(TacticalUnit unit, GridPoint? destination, int? targetCityId)
    {
        unit.TargetCityId = targetCityId;
        if (!destination.HasValue || destination.Value == unit.Cell)
        {
            ClearPath(unit, resetVisualState: true);
            unit.TargetCityId = targetCityId;
            return;
        }

        var blocked = BuildOccupiedCells(unit.Id).Keys.ToHashSet();
        var path = FindGridPath(unit.Cell, destination.Value, blocked);
        if (path.Count < 2)
        {
            ClearPath(unit, resetVisualState: true);
            unit.TargetCityId = targetCityId;
            return;
        }

        unit.Path = path;
        unit.PathIndex = 0;
        unit.StepProgress = 0;
        unit.CurrentPosition = _grid.ToMapPoint(unit.Cell);
        unit.SmoothedPathIndices = [];
    }

    private GridPoint? SelectDestinationCell(TacticalUnit unit, CityNode target)
    {
        var occupied = BuildOccupiedCells(unit.Id);
        if (!occupied.ContainsKey(target.GridPosition))
            return target.GridPosition;

        return FindNearestEmptyPassableCell(
            target.GridPosition,
            includePreferred: false,
            exceptUnitId: unit.Id,
            allowCityCells: false);
    }

    private List<GridPoint> FindGridPath(GridPoint start, GridPoint target, IReadOnlySet<GridPoint>? blockedCells = null)
    {
        if (!_grid.IsPassable(start) || !_grid.IsPassable(target))
            return [];

        blockedCells ??= new HashSet<GridPoint>();
        var open = new List<GridPoint> { start };
        var cameFrom = new Dictionary<GridPoint, GridPoint?> { [start] = null };
        var costSoFar = new Dictionary<GridPoint, double> { [start] = 0 };

        while (open.Count > 0)
        {
            var current = open
                .OrderBy(point => costSoFar[point] + point.OctileDistanceTo(target) * 0.65)
                .ThenBy(point => point.X)
                .ThenBy(point => point.Y)
                .First();
            open.Remove(current);

            if (current == target)
                break;

            foreach (var next in GetGridNeighbors(current, target, blockedCells))
            {
                var newCost = costSoFar[current] + MoveStepCost(current, next);
                if (costSoFar.TryGetValue(next, out var oldCost) && newCost >= oldCost)
                    continue;

                cameFrom[next] = current;
                costSoFar[next] = newCost;
                if (!open.Contains(next))
                    open.Add(next);
            }
        }

        if (!cameFrom.ContainsKey(target))
            return [];

        var path = new List<GridPoint>();
        GridPoint? node = target;
        while (node.HasValue)
        {
            path.Add(node.Value);
            node = cameFrom[node.Value];
        }

        path.Reverse();
        return path;
    }

    private IEnumerable<GridPoint> GetGridNeighbors(GridPoint point, GridPoint target, IReadOnlySet<GridPoint> blockedCells)
        => AdjacentCells(point)
            .Where(next => CanPathEnterCell(point, next, target, blockedCells))
            .OrderBy(next => MoveStepCost(point, next) + next.OctileDistanceTo(target))
            .ThenBy(next => IsDiagonalStep(point, next) ? 1 : 0)
            .ThenBy(next => next.X)
            .ThenBy(next => next.Y);

    private TacticalUnit? FindAdjacentEnemy(TacticalUnit unit, IReadOnlyDictionary<GridPoint, TacticalUnit> occupied)
        => AdjacentCells(unit.Cell)
            .Select(cell => occupied.TryGetValue(cell, out var occupant) ? occupant : null)
            .Where(enemy => enemy != null &&
                UnitParticipates(enemy) &&
                enemy.PlayerId != unit.PlayerId)
            .OrderBy(enemy => enemy!.Kind == UnitKind.General ? 0 : enemy.Kind == UnitKind.Commander ? 1 : 2)
            .ThenBy(enemy => enemy!.Id)
            .FirstOrDefault();

    private double CalculateAttackDamage(TacticalUnit attacker, TacticalUnit defender)
    {
        var attackerTerrain = _grid.GetCell(attacker.Cell).Terrain;
        var defenderTerrain = _grid.GetCell(defender.Cell).Terrain;
        var damage = attacker.AttackPower *
            TerrainAttackModifier(attackerTerrain) *
            LeaderSupportModifier(attacker) *
            Math.Clamp(attacker.Morale, 0.4, 1.25);

        damage /= TerrainDefenseModifier(defenderTerrain);

        if (attacker.Kind == UnitKind.Tank && defenderTerrain is TerrainKind.Forest or TerrainKind.Rock)
            damage *= 0.85;

        return Math.Round(Math.Max(0.25, damage), 3);
    }

    private double LeaderSupportModifier(TacticalUnit attacker)
    {
        var modifier = 1.0;
        if (_units.Any(unit => UnitParticipates(unit) &&
                unit.PlayerId == attacker.PlayerId &&
                unit.Id != attacker.Id &&
                unit.Kind == UnitKind.Commander &&
                unit.Cell.ChebyshevDistanceTo(attacker.Cell) <= 2))
        {
            modifier += 0.15;
        }

        if (_units.Any(unit => UnitParticipates(unit) &&
                unit.PlayerId == attacker.PlayerId &&
                unit.Id != attacker.Id &&
                unit.Kind == UnitKind.General &&
                unit.Cell.ChebyshevDistanceTo(attacker.Cell) <= 3))
        {
            modifier += 0.10;
        }

        return modifier;
    }

    private static double TerrainAttackModifier(TerrainKind terrain)
        => terrain switch
        {
            TerrainKind.Road => 1.08,
            TerrainKind.Hill => 1.1,
            TerrainKind.Forest => 0.95,
            TerrainKind.Rock => 0.9,
            _ => 1.0
        };

    private static double TerrainDefenseModifier(TerrainKind terrain)
        => terrain switch
        {
            TerrainKind.Road => 0.92,
            TerrainKind.Forest => 1.15,
            TerrainKind.Hill => 1.25,
            TerrainKind.Rock => 1.4,
            _ => 1.0
        };

    private void UpdateLeaderHealth()
    {
        foreach (var player in _players)
        {
            var commander = GetLeaderUnit(player, UnitKind.Commander);
            var general = GetLeaderUnit(player, UnitKind.General);

            player.CommanderHealth = commander?.Health ?? 0;
            player.GeneralHealth = general?.Health ?? 0;

            if (general == null)
            {
                var wasEliminated = player.IsEliminated;
                player.IsEliminated = true;
                var neutralizedCities = NeutralizeCitiesForPlayer(player.Id);
                var neutralizedCells = NeutralizeCellsForPlayer(player.Id);
                if (!wasEliminated && neutralizedCities + neutralizedCells > 0)
                    _lastEvent = $"{player.Name} lost a general; territory neutralized";
            }
        }
    }

    private TacticalUnit? GetLeaderUnit(PlayerState player, UnitKind kind)
    {
        var unitId = kind == UnitKind.Commander ? player.CommanderUnitId : player.GeneralUnitId;
        if (!unitId.HasValue)
            return kind == UnitKind.Commander ? GetPlayerCommanders(player.Id).FirstOrDefault() : null;

        return _units.FirstOrDefault(unit => unit.Id == unitId.Value && unit.IsAlive) ??
            (kind == UnitKind.Commander ? GetPlayerCommanders(player.Id).FirstOrDefault() : null);
    }

    private void UpdateScores()
    {
        foreach (var player in _players)
        {
            var ownedCities = _cities.Count(city => city.OwnerId == player.Id);
            var units = CountUnits(player.Id);
            player.Score = ownedCities * 10 + units + player.Resources * 0.25 + (player.IsEliminated ? -100 : 0);
        }
    }

    private void CheckVictory()
    {
        var active = _players.Where(player => !player.IsEliminated).ToList();
        if (active.Count == 1)
        {
            EndWithWinner(active[0].Id);
            return;
        }

        if (HasHumanPlayer && _cities.All(city => city.OwnerId == GameConstants.HumanPlayerId))
        {
            EndWithWinner(GameConstants.HumanPlayerId);
            return;
        }

        if (Tick >= Settings.MatchLengthTicks)
        {
            var winner = active.OrderByDescending(player => player.Score).ThenBy(player => player.Id).First();
            EndWithWinner(winner.Id);
        }
    }

    private void EndWithWinner(int winnerId)
    {
        WinnerId = winnerId;
        Phase = MatchPhase.Ended;
        _lastEvent = $"{_players[winnerId].Name} wins";
    }

    private Dictionary<GridPoint, TacticalUnit> BuildOccupiedCells(int? exceptUnitId = null)
    {
        var occupied = new Dictionary<GridPoint, TacticalUnit>();
        foreach (var unit in _units.Where(unit => UnitParticipates(unit) && unit.Id != exceptUnitId))
        {
            if (!occupied.ContainsKey(unit.Cell))
                occupied[unit.Cell] = unit;
        }

        return occupied;
    }

    private GridPoint? FindAdjacentEmptyCell(GridPoint origin, IReadOnlyDictionary<GridPoint, TacticalUnit> occupied)
    {
        var candidates = CardinalAdjacentCells(origin)
            .Where(point => _grid.IsPassable(point) &&
                !occupied.ContainsKey(point) &&
                !IsCityCell(point))
            .OrderBy(point => _grid.MoveCost(point))
            .ThenBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();

        return occupied.ContainsKey(origin) && candidates.Count <= 1
            ? null
            : candidates.FirstOrDefaultOrNull();
    }

    private GridPoint? FindNearestEmptyPassableCell(
        GridPoint origin,
        bool includePreferred,
        int? exceptUnitId = null,
        bool allowCityCells = true)
    {
        var occupied = BuildOccupiedCells(exceptUnitId);
        var maxDistance = _grid.Width + _grid.Height;
        for (var distance = includePreferred ? 0 : 1; distance <= maxDistance; distance++)
        {
            foreach (var candidate in PointsAtDistance(origin, distance)
                         .Where(_grid.Contains)
                         .Distinct()
                         .OrderBy(point => point.X)
                         .ThenBy(point => point.Y))
            {
                if (!_grid.IsPassable(candidate) ||
                    occupied.ContainsKey(candidate) ||
                    !allowCityCells && IsCityCell(candidate))
                {
                    continue;
                }

                return candidate;
            }
        }

        return null;
    }

    private bool IsOccupiedByOtherUnit(
        GridPoint point,
        int unitId,
        IReadOnlyDictionary<GridPoint, TacticalUnit> occupied)
        => occupied.TryGetValue(point, out var occupant) && occupant.Id != unitId;

    private bool CanEnterNextCell(
        TacticalUnit unit,
        GridPoint nextCell,
        IReadOnlyDictionary<GridPoint, TacticalUnit> occupied,
        IReadOnlyDictionary<GridPoint, int> continuingReservations,
        IReadOnlySet<GridPoint> reservedDestinations)
    {
        if (!_grid.IsPassable(nextCell) ||
            IsReservedByOtherUnit(nextCell, unit.Id, continuingReservations) ||
            reservedDestinations.Contains(nextCell) ||
            IsOccupiedByOtherUnit(nextCell, unit.Id, occupied))
        {
            return false;
        }

        if (!IsDiagonalStep(unit.Cell, nextCell))
            return true;

        return DiagonalSideCells(unit.Cell, nextCell).All(side =>
            _grid.IsPassable(side) &&
            !IsReservedByOtherUnit(side, unit.Id, continuingReservations) &&
            !reservedDestinations.Contains(side) &&
            !IsOccupiedByOtherUnit(side, unit.Id, occupied));
    }

    private bool CanPathEnterCell(
        GridPoint from,
        GridPoint next,
        GridPoint target,
        IReadOnlySet<GridPoint> blockedCells)
    {
        if (!_grid.IsPassable(next) || blockedCells.Contains(next) && next != target)
            return false;

        if (!IsDiagonalStep(from, next))
            return true;

        return DiagonalSideCells(from, next).All(side =>
            _grid.IsPassable(side) && !blockedCells.Contains(side));
    }

    private Dictionary<GridPoint, int> BuildMovingDestinationReservations()
    {
        var reservations = new Dictionary<GridPoint, int>();
        foreach (var unit in _units
                     .Where(unit => UnitParticipates(unit) && unit.IsMoving && unit.StepProgress > 0)
                     .OrderBy(unit => unit.PlayerId)
                     .ThenBy(unit => unit.Id))
        {
            var nextCell = unit.Path[unit.PathIndex + 1];
            if (_grid.IsPassable(nextCell) && !reservations.ContainsKey(nextCell))
                reservations[nextCell] = unit.Id;
        }

        return reservations;
    }

    private static bool IsReservedByOtherUnit(
        GridPoint point,
        int unitId,
        IReadOnlyDictionary<GridPoint, int> reservations)
        => reservations.TryGetValue(point, out var reservedByUnitId) && reservedByUnitId != unitId;

    private bool UnitParticipates(TacticalUnit? unit)
        => unit is { IsAlive: true } &&
            unit.PlayerId >= 0 &&
            unit.PlayerId < _players.Count &&
            !_players[unit.PlayerId].IsEliminated;

    private int CountUnits(int playerId)
        => _units.Count(unit => UnitParticipates(unit) && unit.PlayerId == playerId);

    private List<StandingSnapshot> CreateStandings()
        => _players
            .OrderBy(player => player.Id)
            .Select(player => new StandingSnapshot(
                player.Id,
                player.Name,
                player.Kind.ToString(),
                player.IsEliminated,
                Math.Round(player.Score, 2),
                _cities.Count(city => city.OwnerId == player.Id),
                CountUnits(player.Id),
                Math.Round(player.Resources, 2),
                Math.Round(player.GeneralHealth, 1),
                player.GenomeId,
                player.ControlledCellCount,
                Math.Round(player.LastTaxIncome, 2),
                Math.Round(player.LastUpkeep, 2),
                Math.Round(player.LastPayrollDeficit, 2),
                player.SpawnCapacity))
            .ToList();

    private IReadOnlyList<CellControlSnapshot> CreateCellControlSnapshots()
        => _cellControls.Values
            .OrderBy(cell => cell.Point.Y)
            .ThenBy(cell => cell.Point.X)
            .Select(cell => new CellControlSnapshot(
                cell.Point.X,
                cell.Point.Y,
                cell.OwnerId,
                Math.Round(cell.TaxValue, 2),
                _grid.GetCell(cell.Point).Terrain.ToString(),
                cell.IsCityCell))
            .ToList();

    private IReadOnlyList<EconomySnapshot> CreateEconomySnapshots()
        => _players
            .OrderBy(player => player.Id)
            .Select(player => new EconomySnapshot(
                player.Id,
                player.ControlledCellCount,
                Math.Round(player.ControlledTaxValue, 2),
                Math.Round(player.LastTaxIncome, 2),
                Math.Round(player.Resources, 2),
                Math.Round(player.LastUpkeep, 2),
                Math.Round(player.LastPayrollDeficit, 2),
                Math.Round(player.LastPayrollDeficitRatio, 3),
                player.SpawnCapacity,
                Math.Round(player.ReserveBudget, 2),
                Math.Round(player.ReplenishmentRequest, 2),
                Math.Round(player.EconomyRisk, 3)))
            .ToList();

    private IReadOnlyList<RegionSnapshot> CreateRegionSnapshots()
    {
        var regions = new List<RegionSnapshot>();
        foreach (var player in _players.OrderBy(player => player.Id))
        {
            var assignments = CreateCommanderRegionAssignments(player.Id);
            if (assignments.Count == 0)
            {
                var anchorCity = ResolveRegionAnchorCity(player);
                regions.Add(CreateRegionSnapshot(
                    player.Id,
                    player,
                    null,
                    anchorCity,
                    0,
                    0,
                    _grid.Width,
                    _grid.Height));
                continue;
            }

            foreach (var assignment in assignments)
            {
                regions.Add(CreateRegionSnapshot(
                    assignment.RegionId,
                    player,
                    assignment,
                    ResolveRegionAnchorCity(player),
                    assignment.BoundsX,
                    assignment.BoundsY,
                    assignment.Width,
                    assignment.Height));
            }
        }

        return regions
            .OrderBy(region => region.PlayerId)
            .ThenBy(region => region.RegionId)
            .ToList();
    }

    private RegionSnapshot CreateRegionSnapshot(
        int regionId,
        PlayerState player,
        CommanderRegionAssignment? assignment,
        CityNode anchorCity,
        int boundsX,
        int boundsY,
        int width,
        int height)
    {
        var cells = _cellControls.Values
            .Where(cell => cell.OwnerId == player.Id && IsInsideBounds(cell.Point, boundsX, boundsY, width, height))
            .ToList();
        var friendly = _units.Count(unit => UnitParticipates(unit) &&
            unit.PlayerId == player.Id &&
            IsInsideBounds(unit.Cell, boundsX, boundsY, width, height));
        var visibleEnemies = _units.Count(unit => UnitParticipates(unit) &&
            unit.PlayerId != player.Id &&
            IsInsideBounds(unit.Cell, boundsX, boundsY, width, height) &&
            IsVisibleTo(player.Id, unit.Cell));
        var leaderless = Math.Max(0, player.CommanderLeaderlessUntilTick - Tick);
        var priority = Math.Clamp(visibleEnemies / Math.Max(1.0, friendly) + player.EconomyRisk + (player.GeneralSecurity < 0.5 ? 0.4 : 0), 0, 2);

        return new RegionSnapshot(
            regionId,
            player.Id,
            assignment?.CommanderUnitId ?? player.CommanderUnitId ?? -1,
            anchorCity.Id,
            cells.Count,
            Math.Round(cells.Sum(cell => cell.TaxValue), 2),
            friendly,
            visibleEnemies,
            _cities.Count(city => city.OwnerId == player.Id && IsInsideBounds(city.GridPosition, boundsX, boundsY, width, height)),
            player.SpawnCapacity,
            Math.Round(player.LastPayrollDeficitRatio, 3),
            leaderless,
            Math.Round(priority, 3),
            boundsX,
            boundsY,
            width,
            height,
            assignment?.CommanderUnitId,
            assignment?.CommanderNumber,
            visibleEnemies,
            visibleEnemies > 0);
    }

    private static bool IsInsideBounds(GridPoint point, int x, int y, int width, int height)
        => point.X >= x &&
            point.X < x + width &&
            point.Y >= y &&
            point.Y < y + height;

    private CityNode ResolveRegionAnchorCity(PlayerState player)
    {
        if (player.TargetCityId >= 0 && player.TargetCityId < _cities.Count)
            return _cities[player.TargetCityId];

        return _cities[player.HomeCityId];
    }

    private IReadOnlyList<VisibilitySnapshot> CreateVisibilitySnapshots()
        => _players
            .OrderBy(player => player.Id)
            .Select(player =>
            {
                var visibleEnemies = _units
                    .Where(unit => UnitParticipates(unit) && unit.PlayerId != player.Id && IsVisibleTo(player.Id, unit.Cell))
                    .OrderBy(unit => unit.Id)
                    .ToList();
                return new VisibilitySnapshot(
                    player.Id,
                    _grid.Cells.Count(cell => cell.IsPassable && IsVisibleTo(player.Id, cell.Point)),
                    visibleEnemies.Count,
                    visibleEnemies.Any(unit => unit.Kind == UnitKind.General),
                    visibleEnemies.Select(unit => unit.Id).ToList());
            })
            .ToList();

    private bool IsVisibleTo(int playerId, GridPoint cell)
        => _units.Any(unit => UnitParticipates(unit) &&
            unit.PlayerId == playerId &&
            CanUnitSee(unit, cell));

    private static bool CanUnitSee(TacticalUnit unit, GridPoint cell)
        => unit.Cell.ChebyshevDistanceTo(cell) <= VisibilityRadius(unit);

    internal static int VisibilityRadius(TacticalUnit unit)
        => VisibilityRadius(unit.Kind) + (unit.IsScout ? 2 : 0);

    internal static int VisibilityRadius(UnitKind kind)
        => kind switch
        {
            UnitKind.General => 10,
            UnitKind.Commander => 8,
            UnitKind.Tank => 6,
            _ => 5
        };

    private double ControlledCellRatio(int playerId)
    {
        var capturable = Math.Max(1, _cellControls.Count);
        return _cellControls.Values.Count(cell => cell.OwnerId == playerId) / (double)capturable;
    }

    private void DetectVisibilityEvents()
    {
        foreach (var player in _players.Where(player => !player.IsEliminated))
        {
            var enemyGeneralVisible = _units.Any(unit => UnitParticipates(unit) &&
                unit.PlayerId != player.Id &&
                unit.Kind == UnitKind.General &&
                IsVisibleTo(player.Id, unit.Cell));
            var visibleEnemyUnitIds = _units
                .Where(unit => UnitParticipates(unit) &&
                    unit.PlayerId != player.Id &&
                    IsVisibleTo(player.Id, unit.Cell))
                .Select(unit => unit.Id)
                .ToHashSet();
            var previousVisible = _previousVisibleEnemyUnitIds.TryGetValue(player.Id, out var previous)
                ? previous
                : new HashSet<int>();
            var newContacts = visibleEnemyUnitIds.Except(previousVisible).OrderBy(id => id).ToList();

            if (enemyGeneralVisible && (!_previousEnemyGeneralVisible.TryGetValue(player.Id, out var wasVisible) || !wasVisible))
                AddTelemetry(player.Id, "ScoutDiscovery", "enemy-general");
            if (_previousVisibleEnemyUnitIds.TryGetValue(player.Id, out var knownPreviousVisible))
            {
                var lostContacts = knownPreviousVisible.Except(visibleEnemyUnitIds).OrderBy(id => id).ToList();
                if (lostContacts.Count > 0)
                    AddTelemetry(player.Id, "ScoutLostContact", $"units={string.Join(",", lostContacts.Take(5))}");
            }

            foreach (var enemyId in newContacts)
            {
                var enemy = _units.FirstOrDefault(unit => unit.Id == enemyId);
                if (enemy != null)
                    AddEnemySightedReport(player, enemy);
            }

            if (Tick % PlanningInterval == 0 || newContacts.Count > 0)
                AddCommanderRegionSummaryReports(player);

            _previousEnemyGeneralVisible[player.Id] = enemyGeneralVisible;
            _previousVisibleEnemyUnitIds[player.Id] = visibleEnemyUnitIds;
        }
    }

    private void AddEnemySightedReport(PlayerState player, TacticalUnit enemy)
    {
        var observer = _units
            .Where(unit => UnitParticipates(unit) &&
                unit.PlayerId == player.Id &&
                CanAssignToCommander(unit) &&
                unit.AssignedCommanderUnitId.HasValue &&
                CanUnitSee(unit, enemy.Cell))
            .OrderBy(unit => unit.Cell.ChebyshevDistanceTo(enemy.Cell))
            .ThenBy(unit => unit.Id)
            .FirstOrDefault();
        if (observer == null)
            return;

        AddReportRecord(new ReportRecordSnapshot(
            Tick,
            player.Id,
            UnitToCommanderLayer,
            observer.Id,
            observer.AssignedCommanderUnitId,
            observer.AssignedCommanderNumber,
            EnemySightedReport,
            enemy.Cell.X,
            enemy.Cell.Y,
            enemy.Kind == UnitKind.General ? 1.0 : 0.65,
            $"enemyUnit={enemy.Id};enemyKind={enemy.Kind}"));
    }

    private void AddCommanderRegionSummaryReports(PlayerState player)
    {
        foreach (var region in CreateRegionSnapshots().Where(region => region.PlayerId == player.Id))
        {
            if (!region.AssignedCommanderUnitId.HasValue)
                continue;

            var assignedUnits = _units
                .Where(unit => UnitParticipates(unit) &&
                    unit.PlayerId == player.Id &&
                    CanAssignToCommander(unit) &&
                    unit.AssignedCommanderUnitId == region.AssignedCommanderUnitId.Value)
                .ToList();
            var lowMoraleCount = assignedUnits.Count(unit => MoraleRules.Band(unit.Morale) is MoraleBand.RoutRisk or MoraleBand.Routed);
            var tooFewAssignedUnits = assignedUnits.Count == 0;
            var needsReinforcements =
                tooFewAssignedUnits ||
                region.VisibleEnemyUnitCount > assignedUnits.Count ||
                lowMoraleCount > 0;
            var reportType = region.Contested ? RegionContestedReport : RegionStableReport;
            AddReportRecord(new ReportRecordSnapshot(
                Tick,
                player.Id,
                CommanderToGeneralLayer,
                region.AssignedCommanderUnitId.Value,
                player.GeneralUnitId,
                region.AssignedCommanderNumber,
                reportType,
                region.BoundsX + region.Width / 2,
                region.BoundsY + region.Height / 2,
                region.Contested ? Math.Clamp(region.VisibleEnemyUnitCount / Math.Max(1.0, region.FriendlyUnitCount), 0.25, 1.0) : 0.1,
                $"region={region.RegionId};visibleEnemies={region.VisibleEnemyUnitCount}"));

            if (needsReinforcements)
            {
                AddReportRecord(new ReportRecordSnapshot(
                    Tick,
                    player.Id,
                    CommanderToGeneralLayer,
                    region.AssignedCommanderUnitId.Value,
                    player.GeneralUnitId,
                    region.AssignedCommanderNumber,
                    NeedReinforcementsReport,
                    region.BoundsX + region.Width / 2,
                    region.BoundsY + region.Height / 2,
                    0.8,
                    $"region={region.RegionId};assigned={assignedUnits.Count};lowMorale={lowMoraleCount};visibleEnemies={region.VisibleEnemyUnitCount}"));
            }

            if (lowMoraleCount > 0)
            {
                AddReportRecord(new ReportRecordSnapshot(
                    Tick,
                    player.Id,
                    CommanderToGeneralLayer,
                    region.AssignedCommanderUnitId.Value,
                    player.GeneralUnitId,
                    region.AssignedCommanderNumber,
                    ReserveRecallRequestedReport,
                    region.BoundsX + region.Width / 2,
                    region.BoundsY + region.Height / 2,
                    0.7,
                    $"region={region.RegionId};lowMorale={lowMoraleCount}"));
            }
        }
    }

    private void DetectEconomyTelemetry()
    {
        foreach (var player in _players.Where(player => !player.IsEliminated))
        {
            _previousControlledTax.TryGetValue(player.Id, out var previousTax);
            _previousControlledCells.TryGetValue(player.Id, out var previousCells);

            if (previousTax > 0 && player.ControlledTaxValue < previousTax * 0.75)
                AddTelemetry(player.Id, "TaxRaid", $"tax={previousTax:F1}->{player.ControlledTaxValue:F1}");
            if (Math.Abs(player.ControlledCellCount - previousCells) >= 5 && previousCells > 0)
                AddTelemetry(player.Id, "TerritorySwing", $"cells={previousCells}->{player.ControlledCellCount}");
            if (player.LastUpkeep > player.LastTaxIncome * 1.5 && CountUnits(player.Id) >= 6)
                AddTelemetry(player.Id, "Overbuilding", $"upkeep={player.LastUpkeep:F1};tax={player.LastTaxIncome:F1}");
            if (player.LastPayrollDeficitRatio > 0.65)
                AddTelemetry(player.Id, "EconomicCollapse", $"deficit={player.LastPayrollDeficitRatio:F2}");
            if (Tick % PlanningInterval == 0 && player.GeneralSecurity < 0.35)
                AddTelemetry(player.Id, "GeneralThreat", $"security={player.GeneralSecurity:F2}");

            _previousControlledTax[player.Id] = player.ControlledTaxValue;
            _previousControlledCells[player.Id] = player.ControlledCellCount;
        }
    }

    private void AddTelemetry(
        int playerId,
        string eventType,
        string details,
        string action = "",
        double fitnessDelta = 0,
        string? genomeId = null)
    {
        if (playerId < 0 || playerId >= _players.Count)
            return;

        var player = _players[playerId];
        _telemetry.Add(new TelemetryEvent(
            Tick,
            playerId,
            genomeId ?? player.GenomeId,
            details,
            string.IsNullOrWhiteSpace(action) ? eventType : action,
            fitnessDelta,
            eventType,
            details));

        if (_telemetry.Count > 500)
            _telemetry.RemoveRange(0, _telemetry.Count - 500);
    }

    private int CountUnitsOnCity(CityNode city, int playerId)
        => _units.Count(unit => UnitParticipates(unit) && unit.PlayerId == playerId && unit.Cell == city.GridPosition);

    private int CountEnemyUnitsOnCity(CityNode city, int playerId)
        => _units.Count(unit => UnitParticipates(unit) && unit.PlayerId != playerId && unit.Cell == city.GridPosition);

    private int CountAllUnitsOnCity(CityNode city)
        => _units.Count(unit => UnitParticipates(unit) && unit.Cell == city.GridPosition);

    private int CountUnitsNearCity(CityNode city, int playerId, int radius)
        => _units.Count(unit => UnitParticipates(unit) &&
            unit.PlayerId == playerId &&
            unit.Cell.ChebyshevDistanceTo(city.GridPosition) <= radius);

    private int CountEnemyUnitsNearCity(CityNode city, int playerId, int radius)
        => _units.Count(unit => UnitParticipates(unit) &&
            unit.PlayerId != playerId &&
            unit.Cell.ChebyshevDistanceTo(city.GridPosition) <= radius);

    private bool IsCityCell(GridPoint point)
        => _cities.Any(city => city.GridPosition == point);

    private static bool AreAdjacent(GridPoint left, GridPoint right)
        => left != right && left.ChebyshevDistanceTo(right) == 1;

    private static GridPoint Offset(GridPoint point, int dx, int dy)
        => new(point.X + dx, point.Y + dy);

    private static IEnumerable<GridPoint> AdjacentCells(GridPoint point)
    {
        foreach (var cell in CardinalAdjacentCells(point))
            yield return cell;

        yield return new GridPoint(point.X - 1, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y + 1);
    }

    private static IEnumerable<GridPoint> CardinalAdjacentCells(GridPoint point)
    {
        yield return new GridPoint(point.X, point.Y - 1);
        yield return new GridPoint(point.X + 1, point.Y);
        yield return new GridPoint(point.X, point.Y + 1);
        yield return new GridPoint(point.X - 1, point.Y);
    }

    private static IEnumerable<GridPoint> PointsAtDistance(GridPoint origin, int distance)
    {
        if (distance == 0)
        {
            yield return origin;
            yield break;
        }

        for (var dx = -distance; dx <= distance; dx++)
        {
            var dy = distance - Math.Abs(dx);
            yield return new GridPoint(origin.X + dx, origin.Y + dy);
            if (dy != 0)
                yield return new GridPoint(origin.X + dx, origin.Y - dy);
        }
    }

    private void ClearPath(TacticalUnit unit, bool resetVisualState = false)
    {
        unit.Path = [];
        unit.PathIndex = 0;
        unit.StepProgress = 0;
        unit.SmoothedPathIndices = [];

        if (resetVisualState)
            ResetVisualState(unit);
    }

    private void ResetVisualState(TacticalUnit unit)
    {
        unit.CurrentPosition = _grid.ToMapPoint(unit.Cell);
        unit.VisualFromCell = unit.Cell;
        unit.VisualToCell = unit.Cell;
        unit.VisualFromPosition = unit.CurrentPosition;
        unit.VisualToPosition = unit.CurrentPosition;
        unit.VisualMoveTick = -1;
    }

    private void RecordVisualMovement(TacticalUnit unit, GridPoint fromCell, GridPoint toCell, MapPoint fromPosition)
    {
        if (fromPosition.DistanceTo(unit.CurrentPosition) <= 0.0001)
            return;

        unit.VisualFromCell = fromCell;
        unit.VisualToCell = toCell;
        unit.VisualFromPosition = fromPosition;
        unit.VisualToPosition = unit.CurrentPosition;
        unit.VisualMoveTick = Tick;
    }

    private static MapPoint Interpolate(MapPoint from, MapPoint to, double progress)
    {
        var clamped = Math.Clamp(progress, 0, 1);
        return new MapPoint(
            from.X + (to.X - from.X) * clamped,
            from.Y + (to.Y - from.Y) * clamped);
    }

    private static double Normalize(double value, double max)
        => Math.Clamp(value / max, 0, 1);

    private static double StableUnitInterval(int seed, int playerId)
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + seed;
            hash = hash * 31 + playerId;
            hash ^= hash >> 13;
            return (hash & 0x7fffffff) % 1000 / 999.0;
        }
    }

    private double MoveStepCost(GridPoint from, GridPoint to)
        => Math.Max(0.1, _grid.MoveCost(to)) * (IsDiagonalStep(from, to) ? DiagonalMoveMultiplier : 1.0);

    private static bool IsDiagonalStep(GridPoint from, GridPoint to)
        => Math.Abs(from.X - to.X) == 1 && Math.Abs(from.Y - to.Y) == 1;

    private static IEnumerable<GridPoint> DiagonalSideCells(GridPoint from, GridPoint to)
    {
        yield return new GridPoint(from.X, to.Y);
        yield return new GridPoint(to.X, from.Y);
    }

    internal bool HasLineOfSight(GridPoint from, GridPoint to, IReadOnlySet<GridPoint>? blockedCells = null)
    {
        if (from == to)
            return true;
        if (!_grid.Contains(from) || !_grid.Contains(to))
            return false;

        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var nx = Math.Abs(dx);
        var ny = Math.Abs(dy);
        var signX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
        var signY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);

        var x = from.X;
        var y = from.Y;
        var ix = 0;
        var iy = 0;

        while (ix < nx || iy < ny)
        {
            var decision = (long)(2 * ix + 1) * ny - (long)(2 * iy + 1) * nx;

            if (decision < 0)
            {
                x += signX;
                ix++;
            }
            else if (decision > 0)
            {
                y += signY;
                iy++;
            }
            else
            {
                if (!IsCellClear(new GridPoint(x + signX, y), blockedCells) ||
                    !IsCellClear(new GridPoint(x, y + signY), blockedCells))
                    return false;
                x += signX;
                y += signY;
                ix++;
                iy++;
            }

            if (!IsCellClear(new GridPoint(x, y), blockedCells))
                return false;
        }

        return true;
    }

    private bool IsCellClear(GridPoint cell, IReadOnlySet<GridPoint>? blockedCells)
        => _grid.Contains(cell) && _grid.IsPassable(cell) && !(blockedCells?.Contains(cell) ?? false);

    internal List<int> SmoothGridPath(IReadOnlyList<GridPoint> path, IReadOnlySet<GridPoint>? blockedCells = null)
    {
        if (path.Count < 3)
            return Enumerable.Range(0, path.Count).ToList();

        var indices = new List<int> { 0 };
        var current = 0;

        while (current < path.Count - 1)
        {
            var best = current + 1;
            for (var i = current + 2; i < path.Count; i++)
            {
                if (HasLineOfSight(path[current], path[i], blockedCells))
                    best = i;
            }

            indices.Add(best);
            current = best;
        }

        return indices;
    }

    private MapPoint ResolveMovementPosition(TacticalUnit unit)
    {
        if (!unit.IsMoving || unit.Path.Count < 2 || unit.StepProgress == 0)
            return _grid.ToMapPoint(unit.Cell);

        return Interpolate(
            _grid.ToMapPoint(unit.Cell),
            _grid.ToMapPoint(unit.Path[unit.PathIndex + 1]),
            unit.StepProgress);
    }
}

internal static class EnumerableGridPointExtensions
{
    public static GridPoint? FirstOrDefaultOrNull(this IEnumerable<GridPoint> source)
    {
        foreach (var point in source)
            return point;

        return null;
    }
}

internal sealed record RecentCombatHit(
    int Tick,
    int AttackerPlayerId,
    int AttackerUnitId,
    int DefenderPlayerId,
    int DefenderUnitId,
    UnitKind DefenderKind,
    GridPoint Cell,
    double Damage,
    bool WasFatal);

internal sealed record RecentUnitDeath(int Tick, int UnitId, int PlayerId, GridPoint Cell, UnitKind Kind);

internal sealed record CommanderRegionAssignment(
    int RegionId,
    int CommanderUnitId,
    int? CommanderNumber,
    int BoundsX,
    int BoundsY,
    int Width,
    int Height);
