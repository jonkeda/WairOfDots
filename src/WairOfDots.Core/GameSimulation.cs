using System.Globalization;
using System.Text;

namespace WairOfDots.Core;

public sealed class GameSimulation
{
    private const int ProductionInterval = 6;
    private const int PlanningInterval = 12;
    private const double InfantryCost = 2.0;
    private const double TankCost = 4.0;
    private const double DiagonalMoveMultiplier = 1.4142135623730951;

    private readonly List<CityNode> _cities;
    private readonly IReadOnlyList<TerrainPatch> _terrain;
    private readonly GridMap _grid;
    private readonly List<PlayerState> _players;
    private readonly List<TacticalUnit> _units = [];
    private readonly List<TelemetryEvent> _telemetry = [];
    private readonly Dictionary<int, IAiController> _aiControllers = [];
    private int _nextUnitId = 1;
    private int _activeCombatCount;
    private string _lastEvent = "Waiting";

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

            if (player.Kind == PlayerKind.Ai)
                _aiControllers[player.Id] = GenomeNeatController.Create(Settings.Seed, player.Id);
        }

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

        _lastEvent = $"Human set {human.Directive} on {_cities[human.TargetCityId].Name}";
    }

    public void Step(int ticks)
    {
        for (var i = 0; i < ticks; i++)
            StepOne();
    }

    public MatchSnapshot CreateSnapshot()
    {
        UpdateLeaderHealth();

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
                player.GenomeId))
            .ToList();

        var citySnapshots = _cities
            .Select(city => new CitySnapshot(
                city.Id,
                city.Name,
                city.OwnerId,
                HasHumanPlayer ? CountUnitsOnCity(city, GameConstants.HumanPlayerId) : 0,
                HasHumanPlayer ? CountEnemyUnitsOnCity(city, GameConstants.HumanPlayerId) : CountAllUnitsOnCity(city),
                CountAllUnitsOnCity(city),
                city.Neighbors.ToArray()))
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
                unit.IsLeader,
                unit.TargetCityId,
                unit.RemainingGridSteps))
            .ToList();

        var human = HasHumanPlayer ? Human : null;
        var standings = CreateStandings();

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
            standings);
    }

    public string CreateFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{Tick}|{Phase}|{WinnerId}|{Settings.Mode}|");
        foreach (var player in _players.OrderBy(p => p.Id))
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"p{player.Id}:{player.IsEliminated}:{player.Resources:F2}:{player.CommanderHealth:F1}:{player.GeneralHealth:F1}:{player.Directive}:{player.TargetCityId}:{player.LightPreference:F2};");
        }

        foreach (var city in _cities.OrderBy(c => c.Id))
            builder.Append(CultureInfo.InvariantCulture, $"c{city.Id}:{city.OwnerId};");

        foreach (var unit in _units.Where(unit => unit.IsAlive).OrderBy(unit => unit.Id))
        {
            var next = unit.IsMoving ? unit.Path[unit.PathIndex + 1] : unit.Cell;
            builder.Append(CultureInfo.InvariantCulture,
                $"u{unit.Id}:{unit.PlayerId}:{unit.Kind}:{unit.Cell.X},{unit.Cell.Y}:{next.X},{next.Y}:{unit.TargetCityId}:{unit.PathIndex}:{unit.StepProgress:F2}:{unit.Health:F1}:{unit.Morale:F2};");
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

        yield return new PlayerState(0, "Human", PlayerKind.Human, homeCityIds[0], "human-general");

        for (var i = 1; i <= settings.AiPlayers; i++)
            yield return new PlayerState(i, $"AI {i}", PlayerKind.Ai, homeCityIds[i], GenomeNeatController.Create(settings.Seed, i).GenomeId);
    }

    private void PlaceInitialArmy(PlayerState player, CityNode home)
    {
        var homeCell = home.GridPosition;
        var general = CreateUnit(player.Id, UnitKind.General, homeCell);
        player.GeneralUnitId = general.Id;
        player.GeneralCityId = home.Id;

        var commander = CreateUnitNear(player.Id, UnitKind.Commander, Offset(homeCell, 0, -1));
        player.CommanderUnitId = commander.Id;
        player.CommanderCityId = home.Id;

        CreateUnitNear(player.Id, UnitKind.Infantry, Offset(homeCell, 1, 0));
        CreateUnitNear(player.Id, UnitKind.Infantry, Offset(homeCell, 0, 2));
        CreateUnitNear(player.Id, UnitKind.Tank, Offset(homeCell, -2, 0));
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
            Health = TacticalUnit.DefaultHealth(kind)
        };
        unit.VisualFromCell = cell;
        unit.VisualToCell = cell;
        unit.VisualFromPosition = unit.CurrentPosition;
        unit.VisualToPosition = unit.CurrentPosition;

        _units.Add(unit);
        return unit;
    }

    private void StepOne()
    {
        if (Phase != MatchPhase.Running)
            return;

        Tick++;
        _activeCombatCount = 0;

        if (Tick % ProductionInterval == 0)
            Produce();

        ResolveAdjacentCombats();
        MoveUnits();
        ResolveAdjacentCombats();
        ResolveCityCapture();
        UpdateLeaderHealth();

        if (Tick % PlanningInterval == 0)
            PlanAndDispatch();

        UpdateScores();
        CheckVictory();
    }

    private void Produce()
    {
        var occupied = BuildOccupiedCells();
        foreach (var player in _players.Where(p => !p.IsEliminated))
        {
            var ownedCities = _cities.Where(c => c.OwnerId == player.Id).ToList();
            player.Resources += ownedCities.Sum(city => city.Production) * 0.8;

            foreach (var city in ownedCities)
            {
                var outputCell = FindAdjacentEmptyCell(city.GridPosition, occupied);
                if (!outputCell.HasValue)
                    continue;

                var buildInfantry = player.Resources < TankCost ||
                    ((Tick + city.Id + player.Id) % 100) / 100.0 < player.LightPreference;
                var kind = buildInfantry ? UnitKind.Infantry : UnitKind.Tank;
                var cost = kind == UnitKind.Infantry ? InfantryCost : TankCost;

                if (player.Resources < cost && kind == UnitKind.Tank)
                {
                    kind = UnitKind.Infantry;
                    cost = InfantryCost;
                }

                if (player.Resources < cost)
                    continue;

                var unit = CreateUnit(player.Id, kind, outputCell.Value);
                occupied[unit.Cell] = unit;
                player.Resources -= cost;
                _lastEvent = $"{player.Name} trained {kind} near {city.Name}";
            }
        }
    }

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
            if (FindAdjacentEnemy(unit, occupied) != null)
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
                unit.CurrentPosition = _grid.ToMapPoint(unit.Cell);
                occupied[unit.Cell] = unit;
                RecordVisualMovement(unit, visualFromCell, nextCell, visualFromPosition);

                if (!unit.IsMoving)
                    ClearPath(unit);
            }
            else
            {
                unit.CurrentPosition = Interpolate(
                    _grid.ToMapPoint(unit.Cell),
                    _grid.ToMapPoint(nextCell),
                    unit.StepProgress);
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

            defender.Health -= damage;
            defender.Morale = Math.Max(0.25, defender.Morale - 0.02);
            attacker.Morale = Math.Min(1.25, attacker.Morale + 0.005);
            _lastEvent = $"{_players[attacker.PlayerId].Name} attacked {_players[defender.PlayerId].Name}'s {defender.Kind}";
        }

        RemoveDefeatedUnits();
    }

    private void RemoveDefeatedUnits()
    {
        var defeated = _units.Where(unit => unit.Health <= 0).ToList();
        foreach (var unit in defeated)
        {
            if (unit.PlayerId < 0 || unit.PlayerId >= _players.Count)
                continue;

            var player = _players[unit.PlayerId];
            if (unit.Kind == UnitKind.Commander)
            {
                player.CommanderHealth = 0;
                player.CommanderUnitId = null;
                _lastEvent = $"{player.Name} lost a commander";
            }
            else if (unit.Kind == UnitKind.General)
            {
                player.GeneralHealth = 0;
                player.GeneralUnitId = null;
                player.IsEliminated = true;
                _lastEvent = $"{player.Name} lost a general";
            }
        }

        _units.RemoveAll(unit => unit.Health <= 0 ||
            unit.PlayerId >= 0 && unit.PlayerId < _players.Count && _players[unit.PlayerId].IsEliminated);
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
        occupant.Morale = Math.Min(1.25, occupant.Morale + 0.12);
        _lastEvent = $"{_players[occupant.PlayerId].Name} captured {city.Name}";
    }

    private void PlanAndDispatch()
    {
        foreach (var player in _players.Where(p => !p.IsEliminated))
        {
            if (player.Kind == PlayerKind.Ai)
                UpdateAiPlan(player);

            AssignUnitOrders(player);
        }
    }

    private void UpdateAiPlan(PlayerState player)
    {
        var controller = _aiControllers[player.Id];
        var observation = BuildObservation(player);
        var decision = controller.Decide(observation);

        player.Directive = decision.Directive;
        player.TargetCityId = decision.TargetCityId;
        player.LightPreference = decision.LightPreference;

        _telemetry.Add(new TelemetryEvent(
            Tick,
            player.Id,
            controller.GenomeId,
            $"owned={observation.OwnedCityRatio:F2},strength={observation.StrengthRatio:F2}",
            decision.DebugLabel,
            decision.Aggression));

        if (_telemetry.Count > 500)
            _telemetry.RemoveRange(0, _telemetry.Count - 500);
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
                return new CityObservation(
                    city.Id,
                    city.OwnerId == player.Id ? 1 : 0,
                    city.OwnerId == GameConstants.NeutralPlayerId ? 1 : 0,
                    city.OwnerId >= 0 && city.OwnerId != player.Id ? 1 : 0,
                    Normalize(CountUnitsNearCity(city, player.Id, 2), 8),
                    Normalize(CountEnemyUnitsNearCity(city, player.Id, 2), 8),
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
            cityObservations);
    }

    private void AssignUnitOrders(PlayerState player)
    {
        if (player.Directive == PlayerDirective.Hold)
        {
            foreach (var unit in _units.Where(unit => UnitParticipates(unit) && unit.PlayerId == player.Id))
                ClearPath(unit, resetVisualState: true);
            return;
        }

        var target = ResolveTargetCity(player);
        player.TargetCityId = target.Id;

        foreach (var unit in _units
                     .Where(unit => UnitParticipates(unit) && unit.PlayerId == player.Id)
                     .OrderBy(unit => unit.Kind == UnitKind.General ? 1 : 0)
                     .ThenBy(unit => unit.Id))
        {
            AssignPathToTarget(unit, target);
        }
    }

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
        unit.TargetCityId = target.Id;
        var destination = SelectDestinationCell(unit, target);
        if (!destination.HasValue || destination.Value == unit.Cell)
        {
            ClearPath(unit, resetVisualState: true);
            unit.TargetCityId = target.Id;
            return;
        }

        var blocked = BuildOccupiedCells(unit.Id).Keys.ToHashSet();
        var path = FindGridPath(unit.Cell, destination.Value, blocked);
        if (path.Count < 2)
        {
            ClearPath(unit, resetVisualState: true);
            unit.TargetCityId = target.Id;
            return;
        }

        unit.Path = path;
        unit.PathIndex = 0;
        unit.StepProgress = 0;
        unit.CurrentPosition = _grid.ToMapPoint(unit.Cell);
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
                player.IsEliminated = true;
        }
    }

    private TacticalUnit? GetLeaderUnit(PlayerState player, UnitKind kind)
    {
        var unitId = kind == UnitKind.Commander ? player.CommanderUnitId : player.GeneralUnitId;
        if (!unitId.HasValue)
            return null;

        return _units.FirstOrDefault(unit => unit.Id == unitId.Value && unit.IsAlive);
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
                player.GenomeId))
            .ToList();

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

    private double MoveStepCost(GridPoint from, GridPoint to)
        => Math.Max(0.1, _grid.MoveCost(to)) * (IsDiagonalStep(from, to) ? DiagonalMoveMultiplier : 1.0);

    private static bool IsDiagonalStep(GridPoint from, GridPoint to)
        => Math.Abs(from.X - to.X) == 1 && Math.Abs(from.Y - to.Y) == 1;

    private static IEnumerable<GridPoint> DiagonalSideCells(GridPoint from, GridPoint to)
    {
        yield return new GridPoint(from.X, to.Y);
        yield return new GridPoint(to.X, from.Y);
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
