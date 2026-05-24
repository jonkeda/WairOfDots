using System.Globalization;
using System.Text;

namespace WairOfDots.Core;

public sealed class GameSimulation
{
    private const int ProductionInterval = 6;
    private const int PlanningInterval = 12;
    private const double LightCost = 2.0;
    private const double HeavyCost = 4.0;

    private readonly List<CityNode> _cities;
    private readonly IReadOnlyList<TerrainPatch> _terrain;
    private readonly GridMap _grid;
    private readonly List<PlayerState> _players;
    private readonly List<MovingGroup> _movingGroups = [];
    private readonly List<TelemetryEvent> _telemetry = [];
    private readonly Dictionary<int, IAiController> _aiControllers = [];
    private int _nextGroupId = 1;
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
            home.GetOrCreateGarrison(player.Id).Light = 4;
            home.GetOrCreateGarrison(player.Id).Heavy = 1;
            player.TargetCityId = 0;

            if (player.Kind == PlayerKind.Ai)
                _aiControllers[player.Id] = GenomeNeatController.Create(Settings.Seed, player.Id);
        }
    }

    public GameSettings Settings { get; }
    public int Tick { get; private set; }
    public MatchPhase Phase { get; private set; } = MatchPhase.Menu;
    public int? WinnerId { get; private set; }
    public IReadOnlyList<CityNode> Cities => _cities;
    public IReadOnlyList<TerrainPatch> Terrain => _terrain;
    public GridMap Grid => _grid;
    public IReadOnlyList<PlayerState> Players => _players;
    public IReadOnlyList<MovingGroup> MovingGroups => _movingGroups;
    public IReadOnlyList<TelemetryEvent> Telemetry => _telemetry;
    public string LastEvent => _lastEvent;

    public static GameSimulation Create(GameSettings settings)
    {
        var normalized = settings.Normalized();
        var map = GameMapFactory.CreateDefault(normalized.AiPlayers + 1);
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
                city.TotalUnitsFor(GameConstants.HumanPlayerId),
                city.TotalEnemyUnitsFor(GameConstants.HumanPlayerId),
                city.Garrisons.Values.Sum(g => g.TotalUnits),
                city.Neighbors.ToArray()))
            .ToList();

        return new MatchSnapshot(
            Tick,
            Phase.ToString(),
            WinnerId,
            WinnerId.HasValue ? _players[WinnerId.Value].Name : "",
            Human.TargetCityId,
            Human.Directive.ToString(),
            Math.Round(Human.LightPreference, 2),
            _movingGroups.Count,
            _lastEvent,
            CreateFingerprint(),
            playerSnapshots,
            citySnapshots);
    }

    public string CreateFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{Tick}|{Phase}|{WinnerId}|");
        foreach (var player in _players.OrderBy(p => p.Id))
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"p{player.Id}:{player.IsEliminated}:{player.Resources:F2}:{player.CommanderHealth:F1}:{player.GeneralHealth:F1}:{player.Directive}:{player.TargetCityId}:{player.LightPreference:F2};");
        }

        foreach (var city in _cities.OrderBy(c => c.Id))
        {
            builder.Append(CultureInfo.InvariantCulture, $"c{city.Id}:{city.OwnerId}:");
            foreach (var pair in city.Garrisons.OrderBy(p => p.Key))
                builder.Append(CultureInfo.InvariantCulture, $"{pair.Key}/{pair.Value.Light}/{pair.Value.Heavy}/{pair.Value.Morale:F2},");
            builder.Append(';');
        }

        foreach (var group in _movingGroups.OrderBy(g => g.Id))
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"m{group.Id}:{group.PlayerId}:{group.FromCityId}>{group.TargetCityId}:{group.CurrentCell.X},{group.CurrentCell.Y}:{group.PathIndex}:{group.StepProgress:F2}:{group.Light}/{group.Heavy}:{group.DistanceRemaining:F2};");
        }

        return Fingerprint.Create(builder.ToString());
    }

    private PlayerState Human => _players[GameConstants.HumanPlayerId];

    private static IEnumerable<PlayerState> CreatePlayers(GameSettings settings, IReadOnlyList<int> homeCityIds)
    {
        yield return new PlayerState(0, "Human", PlayerKind.Human, homeCityIds[0], "human-general");

        for (var i = 1; i <= settings.AiPlayers; i++)
            yield return new PlayerState(i, $"AI {i}", PlayerKind.Ai, homeCityIds[i], GenomeNeatController.Create(settings.Seed, i).GenomeId);
    }

    private void StepOne()
    {
        if (Phase != MatchPhase.Running)
            return;

        Tick++;

        if (Tick % ProductionInterval == 0)
            Produce();

        MoveGroups();
        ResolveCities();

        if (Tick % PlanningInterval == 0)
            PlanAndDispatch();

        UpdateScores();
        CheckVictory();
    }

    private void Produce()
    {
        foreach (var player in _players.Where(p => !p.IsEliminated))
        {
            var ownedCities = _cities.Where(c => c.OwnerId == player.Id).ToList();
            player.Resources += ownedCities.Sum(city => city.Production) * 0.8;

            foreach (var city in ownedCities)
            {
                var garrison = city.GetOrCreateGarrison(player.Id);
                if (garrison.TotalUnits >= city.Capacity || HasEnemyPresence(city, player.Id))
                    continue;

                var shouldBuildLight = player.Resources < HeavyCost ||
                    ((Tick + city.Id + player.Id) % 100) / 100.0 < player.LightPreference;

                if (shouldBuildLight && player.Resources >= LightCost)
                {
                    garrison.Light++;
                    player.Resources -= LightCost;
                }
                else if (player.Resources >= HeavyCost)
                {
                    garrison.Heavy++;
                    player.Resources -= HeavyCost;
                }
            }
        }
    }

    private void MoveGroups()
    {
        for (var i = _movingGroups.Count - 1; i >= 0; i--)
        {
            var group = _movingGroups[i];
            if (group.Path.Count < 2 || group.PathIndex >= group.Path.Count - 1)
            {
                ArriveGroup(i, group);
                continue;
            }

            var movement = Math.Max(0.05, group.Speed);
            while (movement > 0 && group.PathIndex < group.Path.Count - 1)
            {
                var nextCell = group.Path[group.PathIndex + 1];
                var moveCost = Math.Max(0.1, _grid.MoveCost(nextCell));
                var remainingStepCost = (1.0 - group.StepProgress) * moveCost;

                if (movement + 0.00001 >= remainingStepCost)
                {
                    movement -= remainingStepCost;
                    group.PathIndex++;
                    group.CurrentCell = nextCell;
                    group.StepProgress = 0;
                    group.CurrentPosition = _grid.ToMapPoint(nextCell);
                    continue;
                }

                group.StepProgress += movement / moveCost;
                movement = 0;
                group.CurrentPosition = Interpolate(
                    _grid.ToMapPoint(group.CurrentCell),
                    _grid.ToMapPoint(nextCell),
                    group.StepProgress);
            }

            group.DistanceRemaining = group.RemainingGridSteps +
                (group.PathIndex < group.Path.Count - 1 ? 1.0 - group.StepProgress : 0);

            if (group.PathIndex >= group.Path.Count - 1 ||
                group.CurrentCell == _cities[group.TargetCityId].GridPosition)
            {
                ArriveGroup(i, group);
            }
        }
    }

    private void ArriveGroup(int index, MovingGroup group)
    {
        var city = _cities[group.TargetCityId];
        var garrison = city.GetOrCreateGarrison(group.PlayerId);
        garrison.Light += group.Light;
        garrison.Heavy += group.Heavy;
        garrison.Morale = Math.Clamp((garrison.Morale + group.Morale) / 2.0, 0.1, 1.25);
        _movingGroups.RemoveAt(index);
        _lastEvent = $"{_players[group.PlayerId].Name} reached {city.Name}";
    }

    private void ResolveCities()
    {
        foreach (var city in _cities)
        {
            ApplyCapacityPressure(city);

            var active = city.Garrisons
                .Where(pair => pair.Key >= 0 && !IsEliminated(pair.Key) && pair.Value.TotalUnits > 0)
                .ToList();

            if (active.Count == 0)
                continue;

            if (active.Count == 1)
            {
                var newOwner = active[0].Key;
                if (city.OwnerId != newOwner)
                    CaptureCity(city, newOwner);
                continue;
            }

            ResolveCombat(city, active);
        }
    }

    private void ApplyCapacityPressure(CityNode city)
    {
        foreach (var garrison in city.Garrisons.Values.Where(g => g.TotalUnits > city.Capacity))
        {
            garrison.Morale = Math.Max(0.1, garrison.Morale - 0.02);
            if (Tick % 4 == 0)
                RemoveWeakestUnit(garrison);
        }
    }

    private void ResolveCombat(CityNode city, List<KeyValuePair<int, Garrison>> active)
    {
        var totalPower = active.Sum(pair => pair.Value.Power);
        if (totalPower <= 0)
            return;

        foreach (var (playerId, garrison) in active)
        {
            var enemyPower = totalPower - garrison.Power;
            garrison.AttritionDebt += enemyPower * 0.075;
            garrison.Morale = Math.Max(0.05, garrison.Morale - enemyPower * 0.0025);

            while (garrison.AttritionDebt >= 1.0 && garrison.TotalUnits > 0)
            {
                RemoveWeakestUnit(garrison);
                garrison.AttritionDebt -= 1.0;
                _lastEvent = $"Combat at {city.Name}";
            }

            if (garrison.TotalUnits <= 0)
                city.Garrisons.Remove(playerId);
        }
    }

    private void CaptureCity(CityNode city, int newOwner)
    {
        var oldOwner = city.OwnerId;
        city.OwnerId = newOwner;
        var garrison = city.GetOrCreateGarrison(newOwner);
        garrison.Morale = Math.Min(1.25, garrison.Morale + 0.12);
        _lastEvent = $"{_players[newOwner].Name} captured {city.Name}";

        if (oldOwner >= 0 && oldOwner != newOwner)
            DamageLeaderAt(city.Id, oldOwner, newOwner);

        foreach (var player in _players.Where(p => p.Id != newOwner && !p.IsEliminated))
            DamageLeaderAt(city.Id, player.Id, newOwner);
    }

    private void DamageLeaderAt(int cityId, int defenderId, int attackerId)
    {
        var defender = _players[defenderId];
        if (defender.CommanderCityId == cityId && defender.CommanderHealth > 0)
        {
            defender.CommanderHealth = 0;
            PenalizeMorale(defenderId, 0.35);
            _lastEvent = $"{_players[attackerId].Name} broke {defender.Name}'s commander";
        }

        if (defender.GeneralCityId == cityId && defender.GeneralHealth > 0)
        {
            defender.GeneralHealth = 0;
            defender.IsEliminated = true;
            PenalizeMorale(defenderId, 0.8);
            _lastEvent = $"{_players[attackerId].Name} eliminated {defender.Name}'s general";
        }
    }

    private void PenalizeMorale(int playerId, double amount)
    {
        foreach (var garrison in _cities.SelectMany(city => city.Garrisons)
                     .Where(pair => pair.Key == playerId)
                     .Select(pair => pair.Value))
        {
            garrison.Morale = Math.Max(0.05, garrison.Morale - amount);
        }
    }

    private void PlanAndDispatch()
    {
        foreach (var player in _players.Where(p => !p.IsEliminated))
        {
            if (player.Kind == PlayerKind.Ai)
                UpdateAiPlan(player);

            TryDispatch(player);
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
        var commanderCity = _cities[player.CommanderCityId];

        var cityObservations = _cities
            .Select(city =>
            {
                var distance = city.GridPosition.ManhattanDistanceTo(commanderCity.GridPosition);
                return new CityObservation(
                    city.Id,
                    city.OwnerId == player.Id ? 1 : 0,
                    city.OwnerId == GameConstants.NeutralPlayerId ? 1 : 0,
                    city.OwnerId >= 0 && city.OwnerId != player.Id ? 1 : 0,
                    Normalize(city.TotalUnitsFor(player.Id), 10),
                    Normalize(city.TotalEnemyUnitsFor(player.Id), 10),
                    Normalize(distance, _grid.Width + _grid.Height));
            })
            .ToList();

        return new AiObservation(
            Tick,
            player.Id,
            owned / (double)totalCities,
            ownUnits / Math.Max(1.0, ownUnits + enemyUnits),
            Normalize(player.Resources, 20),
            Normalize(player.CommanderHealth, 100),
            Normalize(player.GeneralHealth, 200),
            Tick / (double)Settings.MatchLengthTicks,
            cityObservations);
    }

    private void TryDispatch(PlayerState player)
    {
        if (player.Directive == PlayerDirective.Hold)
            return;

        var target = _cities[player.TargetCityId];
        var source = SelectSourceCity(player, target);
        if (source == null || source.Id == target.Id)
            return;

        var path = FindGridPath(source.GridPosition, target.GridPosition);
        if (path.Count < 2)
            return;

        var garrison = source.GetOrCreateGarrison(player.Id);
        var available = garrison.TotalUnits;
        if (available <= 2)
            return;

        var sendTarget = player.Directive == PlayerDirective.Attack
            ? Math.Max(1, available / 2)
            : Math.Max(1, available / 3);

        var lightToSend = Math.Min(garrison.Light > 1 ? garrison.Light - 1 : garrison.Light, (int)Math.Ceiling(sendTarget * player.LightPreference));
        var heavyToSend = Math.Min(garrison.Heavy, sendTarget - lightToSend);

        if (lightToSend + heavyToSend <= 0)
            return;

        garrison.Light -= lightToSend;
        garrison.Heavy -= heavyToSend;

        var distance = Math.Max(1.0, path.Skip(1).Sum(point => _grid.MoveCost(point)));
        _movingGroups.Add(new MovingGroup
        {
            Id = _nextGroupId++,
            PlayerId = player.Id,
            FromCityId = source.Id,
            ToCityId = target.Id,
            TargetCityId = target.Id,
            CurrentCell = path[0],
            Path = path,
            PathIndex = 0,
            CurrentPosition = _grid.ToMapPoint(path[0]),
            Light = lightToSend,
            Heavy = heavyToSend,
            Morale = garrison.Morale,
            OriginalDistance = distance,
            DistanceRemaining = distance
        });
    }

    private CityNode? SelectSourceCity(PlayerState player, CityNode target)
    {
        if (player.Directive == PlayerDirective.Defend)
        {
            var threatenedOwned = _cities
                .Where(city => city.OwnerId == player.Id && city.TotalEnemyUnitsFor(player.Id) > 0)
                .OrderByDescending(city => city.TotalEnemyUnitsFor(player.Id))
                .FirstOrDefault();

            if (threatenedOwned != null)
                target = threatenedOwned;
        }

        return _cities
            .Where(city => city.OwnerId == player.Id && city.Id != target.Id)
            .OrderByDescending(city => city.TotalUnitsFor(player.Id))
            .ThenBy(city => city.GridPosition.ManhattanDistanceTo(target.GridPosition))
            .FirstOrDefault(city => city.TotalUnitsFor(player.Id) > 2);
    }

    private List<GridPoint> FindGridPath(GridPoint start, GridPoint target)
    {
        if (!_grid.IsPassable(start) || !_grid.IsPassable(target))
            return [];

        var open = new List<GridPoint> { start };
        var cameFrom = new Dictionary<GridPoint, GridPoint?> { [start] = null };
        var costSoFar = new Dictionary<GridPoint, double> { [start] = 0 };

        while (open.Count > 0)
        {
            var current = open
                .OrderBy(point => costSoFar[point] + point.ManhattanDistanceTo(target) * 0.65)
                .ThenBy(point => point.X)
                .ThenBy(point => point.Y)
                .First();
            open.Remove(current);

            if (current == target)
                break;

            foreach (var next in GetGridNeighbors(current, target))
            {
                var newCost = costSoFar[current] + _grid.MoveCost(next);
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

    private IEnumerable<GridPoint> GetGridNeighbors(GridPoint point, GridPoint target)
    {
        var candidates = new[]
        {
            new GridPoint(point.X, point.Y - 1),
            new GridPoint(point.X + 1, point.Y),
            new GridPoint(point.X, point.Y + 1),
            new GridPoint(point.X - 1, point.Y)
        };

        return candidates
            .Where(next => _grid.IsPassable(next))
            .OrderBy(next => next.ManhattanDistanceTo(target))
            .ThenBy(next => next.X)
            .ThenBy(next => next.Y);
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

        if (_cities.All(city => city.OwnerId == GameConstants.HumanPlayerId))
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

    private bool HasEnemyPresence(CityNode city, int playerId)
        => city.Garrisons.Any(pair => pair.Key != playerId && pair.Key >= 0 && pair.Value.TotalUnits > 0);

    private bool IsEliminated(int playerId)
        => playerId >= 0 && _players[playerId].IsEliminated;

    private int CountUnits(int playerId)
        => _cities.Sum(city => city.TotalUnitsFor(playerId))
           + _movingGroups.Where(group => group.PlayerId == playerId).Sum(group => group.TotalUnits);

    private static void RemoveWeakestUnit(Garrison garrison)
    {
        if (garrison.Light > 0)
            garrison.Light--;
        else if (garrison.Heavy > 0)
            garrison.Heavy--;
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
}
