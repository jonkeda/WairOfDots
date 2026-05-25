using Brinell.Automation;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using WairOfDots.Core;

namespace WairOfDots.Windows;

public sealed class WairOfDotsGame : Game
{
    private const int UiWidth = 1280;
    private const int UiHeight = 720;
    private const float MapCanvasWidth = 890f;
    private const float MapCanvasHeight = 660f;
    private const double FixedTickSeconds = 1.0 / 8.0;
    internal const double SlowSimulationSpeed = 0.5;
    internal const double NormalSimulationSpeed = 1.0;
    internal const double FastSimulationSpeed = 8.0;
    private const double MinimumSimulationSpeed = 0.25;

    private GameSimulation _simulation = GameSimulation.Create(new GameSettings());
    private readonly object _stateLock = new();
    private double _tickAccumulator;
    private double _visualTickProgress = 1.0;
    private UIElement? _mainUi;
    private UIComponent? _uiComponent;
    private SpriteFont? _font;
    private MapSpriteRenderer? _mapRenderer;
    private MatchSnapshot? _lastSnapshot;
    private StackPanel? _menuPanel;
    private StackPanel? _hudPanel;
    private Canvas? _mapCanvas;
    private StackPanel? _settingsPanel;
    private StackPanel? _endPanel;
    private StackPanel? _humanCommandPanel;
    private StackPanel? _spectatorControlsPanel;
    private StackPanel? _standingsPanel;
    private TextBlock? _statusText;
    private TextBlock? _scoreText;
    private TextBlock? _targetText;
    private TextBlock? _lastEventText;
    private TextBlock? _telemetryText;
    private TextBlock? _endText;
    private TextBlock? _pauseButtonText;
    private TextBlock? _humanToggleText;
    private TextBlock? _humanRoleText;
    private TextBlock? _speedText;
    private EditText? _seedInput;
    private EditText? _aiCountInput;
    private ToggleButton? _humanPlayerToggle;
    private ToggleButton? _humanRoleToggle;
    private bool _mapHitTargetsReady;
    private int _selectedTargetCityId;
    private HumanControlMode _selectedHumanControlMode = HumanControlMode.General;
    private double _simulationSpeed = SlowSimulationSpeed;

    public UIElement? MainUI => _mainUi;
    public GameSimulation Simulation => _simulation;
    internal object StateLock => _stateLock;

    protected override void BeginRun()
    {
        base.BeginRun();

        SetupWorld();
        CreateUi();

        var handler = new WairAutomationHandler(() => _mainUi, this, () => _simulation);
        this.UseAutomation(handler, new AutomationServerOptions
        {
            PipeName = ResolvePipeName(),
            VerboseLogging = false
        });
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        lock (_stateLock)
        {
            if (Input.IsKeyPressed(Stride.Input.Keys.Escape))
                TogglePause();

            if (Input.IsKeyPressed(Stride.Input.Keys.R))
                RestartMatch();

            if (_simulation.Phase == MatchPhase.Running)
            {
                _tickAccumulator += gameTime.Elapsed.TotalSeconds * _simulationSpeed;
                while (_tickAccumulator >= FixedTickSeconds)
                {
                    _simulation.Step(1);
                    _tickAccumulator -= FixedTickSeconds;
                }

                _visualTickProgress = Math.Clamp(_tickAccumulator / FixedTickSeconds, 0, 1);
            }
            else
            {
                _visualTickProgress = Math.Clamp(_visualTickProgress, 0, 1);
            }

            RefreshUi();
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);

        lock (_stateLock)
        {
            if (_mapRenderer == null ||
                _lastSnapshot == null ||
                _hudPanel?.Visibility != Visibility.Visible ||
                _endPanel?.Visibility == Visibility.Visible)
            {
                return;
            }

            var unitPositions = _simulation.Units
                .Where(unit => unit.IsAlive)
                .ToDictionary(unit => unit.Id, ResolveUnitRenderPosition);
            var engagedUnitIds = _simulation.Units
                .Where(unit => unit.IsAlive && IsEngaged(unit))
                .Select(unit => unit.Id)
                .ToHashSet();
            var selectedCityId = _lastSnapshot.HasHumanPlayer
                ? _lastSnapshot.HumanTargetCityId
                : _selectedTargetCityId;

            _mapRenderer.Draw(GraphicsContext, _simulation, _lastSnapshot, unitPositions, engagedUnitIds, selectedCityId);
        }
    }

    public void StartMatch(int? seed = null, int? aiPlayers = null, GameMode? mode = null, double? simulationSpeed = null)
    {
        lock (_stateLock)
        {
            var parsedSeed = seed ?? ParseInt(_seedInput?.Text, 1337);
            var parsedAiPlayers = aiPlayers ?? ParseInt(_aiCountInput?.Text, 4);
            var parsedMode = mode ?? ResolveModeFromMenu();
            var parsedHumanMode = ResolveHumanControlModeFromMenu();
            SetHumanToggle(parsedMode == GameMode.HumanVsAi);
            _simulation = GameSimulation.Create(new GameSettings(
                Seed: parsedSeed,
                AiPlayers: parsedAiPlayers,
                Mode: parsedMode,
                HumanControlMode: parsedHumanMode));
            _selectedTargetCityId = 0;
            _mapHitTargetsReady = false;
            if (_simulation.HasHumanPlayer)
                _simulation.ApplyHumanCommand(new HumanCommand(TargetCityId: _selectedTargetCityId));

            _simulation.Start();
            _tickAccumulator = 0;
            _visualTickProgress = 1.0;
            _simulationSpeed = NormalizeSimulationSpeed(simulationSpeed ?? SlowSimulationSpeed);
            ShowGame();
            RefreshUi();
        }
    }

    public void RestartMatch()
    {
        lock (_stateLock)
        {
            StartMatch(_simulation.Settings.Seed, _simulation.Settings.AiPlayers, _simulation.Settings.Mode, _simulationSpeed);
        }
    }

    public void StepTicks(int ticks)
    {
        lock (_stateLock)
        {
            _simulation.Step(ticks);
            _tickAccumulator = 0;
            _visualTickProgress = 0;
            RefreshUi();
        }
    }

    public void StepToTick(int targetTick)
    {
        lock (_stateLock)
        {
            var remainingTicks = Math.Max(0, targetTick - _simulation.Tick);
            _simulation.Step(remainingTicks);
            _tickAccumulator = 0;
            _visualTickProgress = 0;
            RefreshUi();
        }
    }

    public void SetHumanDirective(PlayerDirective directive)
    {
        lock (_stateLock)
        {
            if (_simulation.HasHumanPlayer)
                _simulation.ApplyHumanCommand(new HumanCommand(Directive: directive));
            RefreshUi();
        }
    }

    public void SelectTargetCity(int cityId)
    {
        lock (_stateLock)
        {
            _selectedTargetCityId = cityId;
            if (_simulation.HasHumanPlayer)
                _simulation.ApplyHumanCommand(new HumanCommand(TargetCityId: cityId));
            RefreshUi();
        }
    }

    public void SetLightPreference(double value)
    {
        lock (_stateLock)
        {
            if (_simulation.HasHumanPlayer)
                _simulation.ApplyHumanCommand(new HumanCommand(LightPreference: value));
            RefreshUi();
        }
    }

    public void SetHumanControlMode(HumanControlMode mode)
    {
        lock (_stateLock)
        {
            if (_simulation.HasHumanPlayer)
                _simulation.ApplyHumanCommand(new HumanCommand(ControlMode: mode));

            SetHumanRoleToggle(mode);
            RefreshUi();
        }
    }

    public void SetSimulationSpeed(double speed)
    {
        lock (_stateLock)
        {
            _simulationSpeed = NormalizeSimulationSpeed(speed);
            RefreshUi();
        }
    }

    public void TogglePause()
    {
        lock (_stateLock)
        {
            _simulation.TogglePause();
            RefreshUi();
        }
    }

    private void SetupWorld()
    {
        this.SetupBase3D();
        _font = Content.Load<SpriteFont>("StrideDefaultFont");
        _mapRenderer = new MapSpriteRenderer(GraphicsDevice, _font);

        var uiEntity = new Entity("UI");
        _uiComponent = new UIComponent
        {
            Resolution = new Vector3(UiWidth, UiHeight, 1000),
            ResolutionStretch = ResolutionStretch.FixedWidthAdaptableHeight,
            IsFullScreen = true,
            RenderGroup = RenderGroup.Group31
        };
        uiEntity.Add(_uiComponent);
        SceneSystem.SceneInstance.RootScene.Entities.Add(uiEntity);
    }

    private void CreateUi()
    {
        var root = new Grid
        {
            Name = "MainPanel",
            MinimumWidth = UiWidth,
            MinimumHeight = UiHeight,
            MaximumWidth = UiWidth,
            MaximumHeight = UiHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BackgroundColor = new Color(14, 16, 20, 255)
        };

        _menuPanel = CreateMenu();
        _hudPanel = CreateHud();
        _settingsPanel = CreateSettings();
        _endPanel = CreateEndPanel();

        root.Children.Add(_menuPanel);
        root.Children.Add(_hudPanel);
        root.Children.Add(_settingsPanel);
        root.Children.Add(_endPanel);

        _mainUi = root;
        _uiComponent!.Page = new UIPage { RootElement = root };
        ShowMenu();
    }

    private StackPanel CreateMenu()
    {
        var panel = new StackPanel
        {
            Name = "MainMenuPanel",
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BackgroundColor = new Color(28, 36, 46, 240),
            Margin = new Thickness(40, 40, 40, 40)
        };

        panel.Children.Add(Text("MenuTitle", "Wair of Dots", 34, Color.White));
        panel.Children.Add(Text("MenuSubtitle", "One human General. Four evolved rivals. Capture the grid.", 15, Color.LightGray));
        panel.Children.Add(Spacer(12));

        _seedInput = Edit("SeedInput", "1337");
        _aiCountInput = Edit("AiCountInput", "4");
        panel.Children.Add(LabeledRow("Seed", _seedInput));
        panel.Children.Add(LabeledRow("AI Players", _aiCountInput));
        panel.Children.Add(CreateHumanToggleRow());
        panel.Children.Add(CreateHumanRoleRow());
        panel.Children.Add(Spacer(12));
        panel.Children.Add(Button("StartGameButton", "Start Match", () => StartMatch()));
        panel.Children.Add(Button("SettingsButton", "Settings", ShowSettings));

        return panel;
    }

    private StackPanel CreateHud()
    {
        var panel = new StackPanel
        {
            Name = "HUD",
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(14, 14, 14, 14)
        };

        _mapCanvas = new Canvas
        {
            Name = "MapCanvas",
            MinimumWidth = MapCanvasWidth,
            MinimumHeight = MapCanvasHeight,
            MaximumWidth = MapCanvasWidth,
            MaximumHeight = MapCanvasHeight,
            BackgroundColor = new Color(157, 190, 60, 255),
            Margin = new Thickness(0, 0, 14, 0)
        };
        panel.Children.Add(_mapCanvas);

        var commandPanel = new StackPanel
        {
            Name = "CommandPanel",
            Orientation = Orientation.Vertical,
            MinimumWidth = 340,
            MaximumWidth = 340,
            BackgroundColor = new Color(10, 12, 16, 220)
        };

        commandPanel.Children.Add(Text("GameTitle", "Wair of Dots", 22, Color.White));
        _statusText = Text("StatusDisplay", "", 14, Color.LightGray);
        _scoreText = Text("ScoreDisplay", "", 14, Color.White);
        _targetText = Text("TargetDisplay", "", 14, Color.LightGreen);
        _lastEventText = Text("LastEventDisplay", "", 13, Color.LightYellow);
        _telemetryText = Text("TelemetryDisplay", "", 12, Color.LightSkyBlue);

        commandPanel.Children.Add(_statusText);
        commandPanel.Children.Add(_scoreText);
        commandPanel.Children.Add(_targetText);
        commandPanel.Children.Add(_lastEventText);
        commandPanel.Children.Add(_telemetryText);
        commandPanel.Children.Add(Spacer(8));

        _humanCommandPanel = new StackPanel
        {
            Name = "HumanCommandPanel",
            Orientation = Orientation.Vertical
        };

        var commandRow = new StackPanel { Orientation = Orientation.Horizontal };
        commandRow.Children.Add(Button("AttackButton", "Attack", () => SetHumanDirective(PlayerDirective.Attack)));
        commandRow.Children.Add(Button("HoldButton", "Hold", () => SetHumanDirective(PlayerDirective.Hold)));
        commandRow.Children.Add(Button("DefendButton", "Defend", () => SetHumanDirective(PlayerDirective.Defend)));
        _humanCommandPanel.Children.Add(commandRow);

        var prefRow = new StackPanel { Orientation = Orientation.Horizontal };
        prefRow.Children.Add(Button("LightPreferenceButton", "More Infantry", () => SetLightPreference(0.8)));
        prefRow.Children.Add(Button("HeavyPreferenceButton", "More Tanks", () => SetLightPreference(0.35)));
        _humanCommandPanel.Children.Add(prefRow);
        commandPanel.Children.Add(_humanCommandPanel);

        _spectatorControlsPanel = new StackPanel
        {
            Name = "SpectatorControlsPanel",
            Orientation = Orientation.Vertical
        };
        _speedText = Text("SpeedDisplay", "", 13, Color.White);
        _spectatorControlsPanel.Children.Add(_speedText);
        var speedRow = new StackPanel { Orientation = Orientation.Horizontal };
        speedRow.Children.Add(Button("SpeedSlowButton", "Slower", () => SetSimulationSpeed(_simulationSpeed / 2)));
        speedRow.Children.Add(Button("SpeedNormalButton", "Normal", () => SetSimulationSpeed(NormalSimulationSpeed)));
        speedRow.Children.Add(Button("SpeedFastButton", "Faster", () => SetSimulationSpeed(_simulationSpeed * 2)));
        _spectatorControlsPanel.Children.Add(speedRow);
        commandPanel.Children.Add(_spectatorControlsPanel);

        var systemRow = new StackPanel { Orientation = Orientation.Horizontal };
        _pauseButtonText = new TextBlock { Text = "Pause", Font = _font, TextColor = Color.White };
        var pauseButton = new Button
        {
            Name = "PauseButton",
            Content = _pauseButtonText,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 4, 6, 0)
        };
        pauseButton.Click += (_, _) => TogglePause();
        systemRow.Children.Add(pauseButton);
        systemRow.Children.Add(Button("RestartButton", "Restart", RestartMatch));
        systemRow.Children.Add(Button("QuitToMenuButton", "Menu", ShowMenu));
        commandPanel.Children.Add(systemRow);

        commandPanel.Children.Add(Spacer(8));
        _standingsPanel = new StackPanel
        {
            Name = "StandingsPanel",
            Orientation = Orientation.Vertical
        };
        commandPanel.Children.Add(_standingsPanel);

        panel.Children.Add(commandPanel);

        return panel;
    }

    private StackPanel CreateSettings()
    {
        var panel = new StackPanel
        {
            Name = "SettingsPanel",
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BackgroundColor = new Color(32, 40, 52, 245),
            Margin = new Thickness(40, 40, 40, 40)
        };

        panel.Children.Add(Text("SettingsTitle", "Settings", 26, Color.White));
        panel.Children.Add(Text("SettingsBody", "Audio and graphics are fixed for the MVP. Gameplay settings live on the start menu.", 14, Color.LightGray));
        panel.Children.Add(Button("SettingsBackButton", "Back", ShowMenu));
        return panel;
    }

    private StackPanel CreateEndPanel()
    {
        var panel = new StackPanel
        {
            Name = "EndStatePanel",
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BackgroundColor = new Color(20, 24, 30, 245),
            MinimumWidth = 440,
            MinimumHeight = 150,
            Margin = new Thickness(40, 40, 40, 40),
            Visibility = Visibility.Collapsed
        };

        _endText = Text("EndStateText", "", 28, Color.White);
        panel.Children.Add(_endText);
        panel.Children.Add(Button("EndRestartButton", "Restart", RestartMatch));
        panel.Children.Add(Button("EndMenuButton", "Menu", ShowMenu));
        return panel;
    }

    private void RefreshUi()
    {
        var snapshot = _simulation.CreateSnapshot();
        _lastSnapshot = snapshot;
        var humanPlayer = snapshot.HasHumanPlayer
            ? snapshot.Players.First(p => p.Id == GameConstants.HumanPlayerId)
            : null;

        if (_statusText != null)
            _statusText.Text = $"Tick {snapshot.Tick} | {snapshot.Phase} | {snapshot.GameMode} | Seed {_simulation.Settings.Seed} | AI {_simulation.Settings.AiPlayers}";

        if (_scoreText != null)
        {
            if (snapshot.HasHumanPlayer)
            {
                _scoreText.Text = $"Human score {humanPlayer!.Score:F0} | Treasury {humanPlayer.Resources:F1} | Tax {humanPlayer.TaxIncome:F1} | Upkeep {humanPlayer.Upkeep:F1} | General {humanPlayer.GeneralHealth:F0}";
            }
            else
            {
                var leader = snapshot.Standings.OrderByDescending(player => player.Score).ThenBy(player => player.PlayerId).First();
                _scoreText.Text = $"Leader {leader.Name} | Score {leader.Score:F0} | Cells {leader.ControlledCellCount} | Tax {leader.TaxIncome:F1}";
            }
        }

        if (_targetText != null)
        {
            var selectedCityId = snapshot.HasHumanPlayer ? snapshot.HumanTargetCityId : _selectedTargetCityId;
            var target = snapshot.Cities.FirstOrDefault(c => c.Id == selectedCityId);
            _targetText.Text = snapshot.HasHumanPlayer
                ? $"Directive {snapshot.HumanDirective} | Target {target?.Name ?? "None"} | Infantry {snapshot.HumanLightPreference:P0} | Role {humanPlayer?.HumanControlMode ?? "General"}"
                : $"Spectating {target?.Name ?? "None"} | Owner {OwnerLabel(target?.OwnerId ?? GameConstants.NeutralPlayerId, snapshot.HasHumanPlayer)} | Units {target?.TotalUnits ?? 0}";
        }

        if (_lastEventText != null)
            _lastEventText.Text = snapshot.LastEvent;

        if (_telemetryText != null)
        {
            var latest = _simulation.Telemetry.LastOrDefault();
            var aiStates = FormatAiStateSummary(snapshot);
            _telemetryText.Text = latest == null
                ? $"AI telemetry pending | {aiStates}"
                : $"AI telemetry {latest.GenomeId}: {latest.Action} | {aiStates}";
        }

        if (_pauseButtonText != null)
            _pauseButtonText.Text = _simulation.Phase == MatchPhase.Paused ? "Resume" : "Pause";

        if (_speedText != null)
            _speedText.Text = $"Spectator speed {FormatSimulationSpeed(_simulationSpeed)}";

        _humanCommandPanel?.SetVisible(snapshot.HasHumanPlayer);
        _spectatorControlsPanel?.SetVisible(!snapshot.HasHumanPlayer);
        _standingsPanel?.SetVisible(!snapshot.HasHumanPlayer);

        RefreshStandings(snapshot);
        RefreshMap(snapshot);

        if (_endPanel != null)
            _endPanel.Visibility = _simulation.Phase == MatchPhase.Ended ? Visibility.Visible : Visibility.Collapsed;

        if (_endText != null && _simulation.Phase == MatchPhase.Ended)
            _endText.Text = snapshot.HasHumanPlayer
                ? snapshot.WinnerId == GameConstants.HumanPlayerId ? "Victory: the dots answer to you." : $"Defeat: {snapshot.WinnerName} wins."
                : $"Winner: {snapshot.WinnerName} wins.";
    }

    private void RefreshStandings(MatchSnapshot snapshot)
    {
        if (_standingsPanel == null)
            return;

        var orderedStandings = snapshot.Standings.ToList();
        var maxScore = Math.Max(1, orderedStandings.Max(standing => Math.Max(0, standing.Score)));
        var maxCities = Math.Max(1, snapshot.Cities.Count);
        var maxUnits = Math.Max(1, orderedStandings.Max(standing => standing.UnitCount));
        var maxGeneralHealth = TacticalUnit.DefaultHealth(UnitKind.General);

        _standingsPanel.Children.Clear();
        _standingsPanel.Children.Add(Text("StandingsTitle", "Standings", 15, Color.White));
        _standingsPanel.Children.Add(CreateStandingHeader());
        foreach (var standing in orderedStandings)
            _standingsPanel.Children.Add(CreateStandingRow(standing, maxScore, maxCities, maxUnits, maxGeneralHealth));
    }

    private UIElement CreateStandingHeader()
    {
        var row = new StackPanel
        {
            Name = "StandingHeaderRow",
            Orientation = Orientation.Horizontal,
            MinimumHeight = 18,
            MaximumHeight = 18,
            Margin = new Thickness(0, 0, 0, 1)
        };

        row.Children.Add(new StackPanel
        {
            Name = "StandingHeaderDotSpacer",
            MinimumWidth = 20,
            MaximumWidth = 20
        });
        row.Children.Add(CreateStandingMetricHeader("Score", "Score"));
        row.Children.Add(CreateStandingMetricHeader("Cities", "Cities"));
        row.Children.Add(CreateStandingMetricHeader("Units", "Units"));
        row.Children.Add(CreateStandingMetricHeader("General", "General"));

        return row;
    }

    private UIElement CreateStandingMetricHeader(string metricName, string label)
    {
        var group = new StackPanel
        {
            Name = $"Standing{metricName}Header",
            Orientation = Orientation.Vertical,
            MinimumWidth = 70,
            MaximumWidth = 70,
            Margin = new Thickness(0, 0, 2, 0)
        };

        group.Children.Add(CompactText($"Standing{metricName}HeaderLabel", label, 8, Color.LightGray));
        return group;
    }

    private UIElement CreateStandingRow(
        StandingSnapshot standing,
        double maxScore,
        double maxCities,
        double maxUnits,
        double maxGeneralHealth)
    {
        var playerColor = PlayerColor(standing.PlayerId);
        var textColor = standing.IsEliminated ? new Color(132, 136, 142, 255) : playerColor;
        var fillColor = standing.IsEliminated ? new Color(72, 76, 84, 210) : playerColor;

        var row = new StackPanel
        {
            Name = $"StandingRow_{standing.PlayerId}",
            Orientation = Orientation.Horizontal,
            MinimumHeight = 28,
            MaximumHeight = 30,
            Margin = new Thickness(0, 2, 0, 2)
        };

        var dot = CompactText($"StandingPlayerDot_{standing.PlayerId}", "●", 12, playerColor);
        dot.MinimumWidth = 20;
        dot.MaximumWidth = 20;
        row.Children.Add(dot);

        row.Children.Add(CreateStandingMetric("Score", standing.PlayerId, standing.Score, maxScore, fillColor, textColor));
        row.Children.Add(CreateStandingMetric("Cities", standing.PlayerId, standing.CityCount, maxCities, fillColor, textColor));
        row.Children.Add(CreateStandingMetric("Units", standing.PlayerId, standing.UnitCount, maxUnits, fillColor, textColor));
        row.Children.Add(CreateStandingMetric("General", standing.PlayerId, standing.GeneralHealth, maxGeneralHealth, fillColor, textColor));

        return row;
    }

    private UIElement CreateStandingMetric(
        string metricName,
        int playerId,
        double value,
        double maxValue,
        Color fillColor,
        Color textColor)
    {
        const float trackWidth = 64;
        const float trackHeight = 7;

        var group = new StackPanel
        {
            Name = $"Standing{metricName}Group_{playerId}",
            Orientation = Orientation.Vertical,
            MinimumWidth = 70,
            MaximumWidth = 70,
            Margin = new Thickness(0, 0, 2, 0)
        };

        group.Children.Add(CompactText($"Standing{metricName}Value_{playerId}", $"{value:F0}", 8, textColor));

        var track = new StackPanel
        {
            Name = $"Standing{metricName}Track_{playerId}",
            Orientation = Orientation.Horizontal,
            BackgroundColor = new Color(30, 34, 42, 220),
            MinimumWidth = trackWidth,
            MaximumWidth = trackWidth,
            MinimumHeight = trackHeight,
            MaximumHeight = trackHeight
        };

        var fillWidth = Math.Clamp((float)(Math.Max(0, value) / Math.Max(1, maxValue) * trackWidth), 1, trackWidth);
        var fill = new StackPanel
        {
            Name = $"Standing{metricName}Bar_{playerId}",
            BackgroundColor = fillColor,
            MinimumWidth = fillWidth,
            MaximumWidth = fillWidth,
            MinimumHeight = trackHeight,
            MaximumHeight = trackHeight
        };
        track.Children.Add(fill);
        group.Children.Add(track);

        return group;
    }

    private void RefreshMap(MatchSnapshot snapshot)
    {
        if (_mapCanvas == null)
            return;

        EnsureMapHitTargets();
    }

    private void EnsureMapHitTargets()
    {
        if (_mapCanvas == null || _mapHitTargetsReady)
            return;

        _mapCanvas.Children.Clear();
        foreach (var city in _simulation.Cities)
        {
            var rel = ToMapRelative(city.Position);
            var button = new Button
            {
                Name = $"MapCity_{city.Id}",
                Content = new TextBlock
                {
                    Name = $"MapCityHitLabel_{city.Id}",
                    Text = string.Empty,
                    Font = _font,
                    TextColor = Color.Transparent
                },
                Padding = new Thickness(0, 0, 0, 0)
            };
            button.Click += (_, _) => SelectTargetCity(city.Id);
            button.SetCanvasRelativePosition(new Vector3(rel.X, rel.Y - 0.023f, 0));
            button.SetCanvasRelativeSize(new Vector3(0.075f, 0.075f, 0));
            button.SetCanvasPinOrigin(new Vector3(0.5f, 0.5f, 0));
            _mapCanvas.Children.Add(button);
        }

        _mapHitTargetsReady = true;
    }

    private void AddTerrainPatches()
    {
        foreach (var patch in _simulation.Terrain)
        {
            var width = (float)(patch.Width / MapRange);
            var height = (float)(patch.Height / MapRange);
            var element = new StackPanel
            {
                Name = $"TerrainPatch_{patch.Id}_{patch.Kind}",
                BackgroundColor = TerrainColor(patch.Kind),
                MinimumWidth = Math.Max(1f, width * MapCanvasWidth),
                MaximumWidth = Math.Max(1f, width * MapCanvasWidth),
                MinimumHeight = Math.Max(1f, height * MapCanvasHeight),
                MaximumHeight = Math.Max(1f, height * MapCanvasHeight)
            };

            var center = ToMapRelative(patch.Center);
            element.SetCanvasRelativePosition(new Vector3(center.X, center.Y, 0));
            element.SetCanvasRelativeSize(new Vector3(width, height, 0));
            element.SetCanvasPinOrigin(new Vector3(0.5f, 0.5f, 0));
            _mapCanvas!.Children.Add(element);
        }
    }

    private void AddMapLegend()
    {
        var legend = Text("MapLegend", "Terrain: green land | blue water | gray hills | black roads", 12, Color.White);
        legend.SetCanvasRelativePosition(new Vector3(0.015f, 0.02f, 0));
        legend.SetCanvasRelativeSize(new Vector3(0.62f, 0.05f, 0));
        _mapCanvas!.Children.Add(legend);
    }

    private void AddTerritoryBoundaries(MatchSnapshot snapshot)
    {
        var index = 0;
        foreach (var segment in snapshot.TerritoryBoundaries)
            AddTerritoryBoundary(segment, index++, BoundaryColor(segment.OwnerId), TerritoryBoundaryThickness, "TerritoryBoundary");
    }

    private void AddTerritoryBoundary(
        TerritoryBoundarySegment segment,
        int index,
        Color color,
        float thickness,
        string namePrefix)
    {
        var isVertical = segment.FromX == segment.ToX;
        var startX = segment.FromX / (float)_simulation.Grid.Width;
        var startY = segment.FromY / (float)_simulation.Grid.Height;
        var endX = segment.ToX / (float)_simulation.Grid.Width;
        var endY = segment.ToY / (float)_simulation.Grid.Height;
        var centerX = (startX + endX) / 2f;
        var centerY = (startY + endY) / 2f;
        var width = isVertical ? thickness : Math.Max(0.001f, Math.Abs(endX - startX));
        var height = isVertical ? Math.Max(0.001f, Math.Abs(endY - startY)) : thickness;

        var boundary = new StackPanel
        {
            Name = $"{namePrefix}_{index}_{segment.OwnerId}_{segment.NeighborOwnerId}",
            BackgroundColor = color,
            MinimumWidth = Math.Max(1f, width * MapCanvasWidth),
            MaximumWidth = Math.Max(1f, width * MapCanvasWidth),
            MinimumHeight = Math.Max(1f, height * MapCanvasHeight),
            MaximumHeight = Math.Max(1f, height * MapCanvasHeight)
        };

        boundary.SetCanvasRelativePosition(new Vector3(centerX, centerY, 0));
        boundary.SetCanvasRelativeSize(new Vector3(width, height, 0));
        boundary.SetCanvasPinOrigin(new Vector3(0.5f, 0.5f, 0));
        _mapCanvas!.Children.Add(boundary);
    }

    private void AddEdges()
    {
        var seen = new HashSet<string>();
        foreach (var city in _simulation.Cities)
        {
            foreach (var neighborId in city.Neighbors)
            {
                var key = city.Id < neighborId ? $"{city.Id}-{neighborId}" : $"{neighborId}-{city.Id}";
                if (!seen.Add(key))
                    continue;

                var neighbor = _simulation.Cities[neighborId];
                var mid = new MapPoint((city.Position.X + neighbor.Position.X) / 2, (city.Position.Y + neighbor.Position.Y) / 2);
                var rel = ToMapRelative(mid);
                var edge = Text($"MapEdge_{key}", "━━━━", 13, Color.Black);
                edge.SetCanvasRelativePosition(new Vector3(rel.X, rel.Y, 0));
                edge.SetCanvasRelativeSize(new Vector3(0.08f, 0.035f, 0));
                edge.SetCanvasPinOrigin(new Vector3(0.5f, 0.5f, 0));
                _mapCanvas!.Children.Add(edge);
            }
        }
    }

    private void AddCityMarkers(MatchSnapshot snapshot)
    {
        foreach (var city in _simulation.Cities)
        {
            var citySnapshot = snapshot.Cities.First(c => c.Id == city.Id);
            var isSelected = snapshot.HasHumanPlayer
                ? city.Id == snapshot.HumanTargetCityId
                : city.Id == _selectedTargetCityId;
            var rel = ToMapRelative(city.Position);
            var fill = new TextBlock
            {
                Name = $"MapCityFill_{city.Id}",
                Text = "■",
                Font = _font,
                TextSize = isSelected
                    ? 34
                    : citySnapshot.TotalUnits > 0 ? 31 : 29,
                TextColor = PlayerColor(citySnapshot.OwnerId),
                Margin = new Thickness(0, 0, 0, 0)
            };

            var button = new Button
            {
                Name = $"MapCity_{city.Id}",
                Content = fill,
                Padding = new Thickness(0, 0, 0, 0)
            };
            button.Click += (_, _) => SelectTargetCity(city.Id);
            button.SetCanvasRelativePosition(new Vector3(rel.X, rel.Y - 0.023f, 0));
            button.SetCanvasRelativeSize(new Vector3(0.075f, 0.075f, 0));
            button.SetCanvasPinOrigin(new Vector3(0.5f, 0.5f, 0));
            _mapCanvas!.Children.Add(button);
        }
    }

    private void AddUnitMarkers(MatchSnapshot snapshot)
    {
        foreach (var unit in _simulation.Units.Where(unit => unit.IsAlive).OrderBy(unit => unit.Id))
        {
            var (name, glyph, size) = unit.Kind switch
            {
                UnitKind.Commander => ($"CommanderMarker_{unit.PlayerId}", "○", 20f),
                UnitKind.General => ($"GeneralMarker_{unit.PlayerId}", "◉", 19f),
                UnitKind.Tank => ($"UnitMarker_{unit.Id}", "●", 17f),
                _ => ($"UnitMarker_{unit.Id}", "●", 14f)
            };

            var renderPosition = ResolveUnitRenderPosition(unit);
            if (IsEngaged(unit))
                AddCircleMarker($"EngagedMarker_{unit.Id}", "○", renderPosition, 0, -0.018f, size + 7, Color.LightYellow);

            AddCircleMarker(name, glyph, renderPosition, 0, -0.018f, size, PlayerColor(unit.PlayerId));
        }
    }

    internal IReadOnlyList<UnitVisualStateResponse> CreateUnitVisualStates()
        => _simulation.Units
            .Where(unit => unit.IsAlive && !_simulation.Players[unit.PlayerId].IsEliminated)
            .OrderBy(unit => unit.Id)
            .Select(unit =>
            {
                var progress = ResolveUnitVisualProgress(unit);
                var visual = ResolveUnitRenderPosition(unit);
                return new UnitVisualStateResponse(
                    unit.Id,
                    unit.PlayerId,
                    unit.Kind.ToString(),
                    unit.Cell.X,
                    unit.Cell.Y,
                    unit.VisualFromCell.X,
                    unit.VisualFromCell.Y,
                    unit.VisualToCell.X,
                    unit.VisualToCell.Y,
                    Math.Round(unit.VisualFromPosition.X, 4),
                    Math.Round(unit.VisualFromPosition.Y, 4),
                    Math.Round(unit.VisualToPosition.X, 4),
                    Math.Round(unit.VisualToPosition.Y, 4),
                    Math.Round(visual.X, 4),
                    Math.Round(visual.Y, 4),
                    Math.Round(progress, 4),
                    unit.VisualMoveTick == _simulation.Tick && unit.HasVisualMovement,
                    unit.IsUsingSmoothedSegment);
            })
            .ToList();

    private MapPoint ResolveUnitRenderPosition(TacticalUnit unit)
        => unit.VisualMoveTick == _simulation.Tick && unit.HasVisualMovement
            ? Interpolate(unit.VisualFromPosition, unit.VisualToPosition, ResolveUnitVisualProgress(unit))
            : unit.CurrentPosition;

    private double ResolveUnitVisualProgress(TacticalUnit unit)
        => unit.VisualMoveTick == _simulation.Tick && unit.HasVisualMovement
            ? _visualTickProgress
            : 1.0;

    private bool IsEngaged(TacticalUnit unit)
        => _simulation.Units.Any(other =>
            other.IsAlive &&
            other.PlayerId != unit.PlayerId &&
            other.Cell != unit.Cell &&
            other.Cell.ChebyshevDistanceTo(unit.Cell) == 1);

    private void AddCircleMarker(string name, string glyph, MapPoint point, float offsetX, float offsetY, float size, Color color)
    {
        var rel = ToMapRelative(point);
        var marker = Text(name, glyph, size, color);
        marker.SetCanvasRelativePosition(new Vector3(rel.X + offsetX, rel.Y + offsetY, 0));
        marker.SetCanvasRelativeSize(new Vector3(0.04f, 0.04f, 0));
        marker.SetCanvasPinOrigin(new Vector3(0.5f, 0.5f, 0));
        _mapCanvas!.Children.Add(marker);
    }

    private static (float X, float Y) MarkerOffset(int markerIndex)
    {
        var offsets = new[]
        {
            (-0.025f, 0.026f),
            (0.000f, 0.030f),
            (0.025f, 0.026f),
            (-0.020f, 0.050f),
            (0.020f, 0.050f)
        };
        return offsets[markerIndex % offsets.Length];
    }

    private static Color TerrainColor(TerrainKind kind)
        => kind switch
        {
            TerrainKind.Water => new Color(42, 154, 232, 255),
            TerrainKind.Road => new Color(20, 20, 24, 255),
            TerrainKind.Hill => new Color(118, 121, 118, 255),
            TerrainKind.Rock => new Color(150, 153, 149, 255),
            TerrainKind.Forest => new Color(45, 128, 53, 255),
            _ => new Color(157, 190, 60, 255)
        };

    private static Color PlayerColor(int playerId)
        => playerId switch
        {
            GameConstants.NeutralPlayerId => Color.Yellow,
            0 => new Color(68, 42, 232, 255),
            1 => Color.Red,
            2 => Color.Orange,
            3 => Color.DeepSkyBlue,
            4 => Color.Magenta,
            5 => Color.Lime,
            6 => Color.Cyan,
            7 => Color.White,
            _ => Color.LightPink
        };

    private static Color BoundaryColor(int playerId)
    {
        var color = PlayerColor(playerId);
        return new Color(color.R, color.G, color.B, playerId == GameConstants.NeutralPlayerId ? (byte)190 : byte.MaxValue);
    }

    private const float TerritoryBoundaryThickness = 0.004f;
    private const float MapMin = -9.2f;
    private const float MapMax = 9.2f;
    private const float MapRange = MapMax - MapMin;

    private static (float X, float Y) ToMapRelative(MapPoint point)
        => (
            Math.Clamp((float)((point.X - MapMin) / MapRange), 0.02f, 0.98f),
            Math.Clamp((float)((point.Y - MapMin) / MapRange), 0.02f, 0.98f)
        );

    private static MapPoint Interpolate(MapPoint from, MapPoint to, double progress)
    {
        var clamped = Math.Clamp(progress, 0, 1);
        return new MapPoint(
            from.X + (to.X - from.X) * clamped,
            from.Y + (to.Y - from.Y) * clamped);
    }

    private void ShowMenu()
    {
        lock (_stateLock)
        {
            _simulation = GameSimulation.Create(new GameSettings(ParseInt(_seedInput?.Text, 1337), ParseInt(_aiCountInput?.Text, 4)));
            _lastSnapshot = null;
            _mapHitTargetsReady = false;
            _menuPanel?.SetVisible(true);
            _hudPanel?.SetVisible(false);
            _settingsPanel?.SetVisible(false);
            _endPanel?.SetVisible(false);
        }
    }

    private void ShowGame()
    {
        lock (_stateLock)
        {
            _menuPanel?.SetVisible(false);
            _hudPanel?.SetVisible(true);
            _settingsPanel?.SetVisible(false);
        }
    }

    private void ShowSettings()
    {
        lock (_stateLock)
        {
            _menuPanel?.SetVisible(false);
            _hudPanel?.SetVisible(false);
            _settingsPanel?.SetVisible(true);
            _endPanel?.SetVisible(false);
        }
    }

    private TextBlock Text(string name, string text, float size, Color color)
        => new()
        {
            Name = name,
            Text = text,
            Font = _font,
            TextSize = size,
            TextColor = color,
            Margin = new Thickness(0, 3, 0, 3)
        };

    private TextBlock CompactText(string name, string text, float size, Color color)
        => new()
        {
            Name = name,
            Text = text,
            Font = _font,
            TextSize = size,
            TextColor = color,
            Margin = new Thickness(0, 0, 0, 0)
        };

    private UIElement CreateHumanToggleRow()
    {
        _humanToggleText = new TextBlock { Text = "Yes", Font = _font, TextColor = Color.White };
        _humanPlayerToggle = new ToggleButton
        {
            Name = "HumanPlayerCheckBox",
            Content = _humanToggleText,
            State = ToggleState.Checked,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 4, 0, 4)
        };
        _humanPlayerToggle.Click += (_, _) => RefreshHumanToggleText();

        return LabeledRow("Human", _humanPlayerToggle);
    }

    private UIElement CreateHumanRoleRow()
    {
        _humanRoleText = new TextBlock { Text = "General", Font = _font, TextColor = Color.White };
        _humanRoleToggle = new ToggleButton
        {
            Name = "HumanRoleButton",
            Content = _humanRoleText,
            State = ToggleState.UnChecked,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 4, 0, 4)
        };
        _humanRoleToggle.Click += (_, _) => CycleHumanRole();

        return LabeledRow("Role", _humanRoleToggle);
    }

    private EditText Edit(string name, string text)
        => new()
        {
            Name = name,
            Text = text,
            Font = _font,
            TextSize = 16,
            MinimumWidth = 180,
            Margin = new Thickness(0, 4, 0, 4)
        };

    private UIElement LabeledRow(string label, UIElement control)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 4)
        };

        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            Font = _font,
            TextSize = 16,
            TextColor = Color.White,
            MinimumWidth = 110
        });
        row.Children.Add(control);
        return row;
    }

    private Button Button(string name, string label, Action action)
    {
        var button = new Button
        {
            Name = name,
            Content = new TextBlock { Text = label, Font = _font, TextColor = Color.White },
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 4, 6, 0)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static UIElement Spacer(float height)
        => new StackPanel
        {
            MinimumHeight = height,
            MaximumHeight = height
        };

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

    private static double NormalizeSimulationSpeed(double speed)
        => Math.Clamp(speed, MinimumSimulationSpeed, FastSimulationSpeed);

    private static string FormatSimulationSpeed(double speed)
        => $"{speed:0.##}x";

    private GameMode ResolveModeFromMenu()
        => _humanPlayerToggle?.State == ToggleState.UnChecked ? GameMode.AiOnly : GameMode.HumanVsAi;

    private HumanControlMode ResolveHumanControlModeFromMenu()
        => _selectedHumanControlMode;

    private void SetHumanToggle(bool hasHuman)
    {
        if (_humanPlayerToggle != null)
            _humanPlayerToggle.State = hasHuman ? ToggleState.Checked : ToggleState.UnChecked;

        RefreshHumanToggleText();
    }

    private void RefreshHumanToggleText()
    {
        if (_humanToggleText != null)
            _humanToggleText.Text = _humanPlayerToggle?.State == ToggleState.UnChecked ? "No" : "Yes";
    }

    private void SetHumanRoleToggle(HumanControlMode mode)
    {
        _selectedHumanControlMode = mode;
        if (_humanRoleToggle != null)
            _humanRoleToggle.State = mode == HumanControlMode.General ? ToggleState.UnChecked : ToggleState.Checked;

        RefreshHumanRoleText();
    }

    private void RefreshHumanRoleText()
    {
        if (_humanRoleText != null)
            _humanRoleText.Text = HumanRoleLabel(_selectedHumanControlMode);
    }

    private void CycleHumanRole()
    {
        var modes = new[]
        {
            HumanControlMode.General,
            HumanControlMode.Commander,
            HumanControlMode.GeneralAndCommander,
            HumanControlMode.DotChaos
        };
        var currentIndex = Array.IndexOf(modes, _selectedHumanControlMode);
        SetHumanRoleToggle(modes[(currentIndex + 1 + modes.Length) % modes.Length]);
        if (_simulation.HasHumanPlayer)
            _simulation.ApplyHumanCommand(new HumanCommand(ControlMode: _selectedHumanControlMode));
    }

    private static string HumanRoleLabel(HumanControlMode mode)
        => mode switch
        {
            HumanControlMode.GeneralAndCommander => "General+Commander",
            HumanControlMode.DotChaos => "Dot Chaos",
            _ => mode.ToString()
        };

    private static string OwnerLabel(int ownerId, bool hasHumanPlayer)
        => ownerId switch
        {
            GameConstants.NeutralPlayerId => "Neutral",
            GameConstants.HumanPlayerId when hasHumanPlayer => "Human",
            _ => $"AI {ownerId}"
        };

    private static string FormatAiStateSummary(MatchSnapshot snapshot)
    {
        var states = snapshot.Players
            .Where(player => player.Kind == PlayerKind.Ai.ToString() && !player.IsEliminated)
            .OrderBy(player => player.Id)
            .Select(player => $"{player.Id}:{ShortArchetype(player.AiArchetype)}/{ShortDirective(player.Directive)}");

        return $"AI states {string.Join(" ", states)}";
    }

    private static string ShortArchetype(string archetype)
        => archetype switch
        {
            "opportunist" => "opp",
            _ => archetype
        };

    private static string ShortDirective(string directive)
        => string.IsNullOrWhiteSpace(directive) ? "?" : directive[..1];

    private static string ResolvePipeName()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--pipe", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return "Brinell.Stride.Automation";
    }
}

internal static class UiElementExtensions
{
    public static void SetVisible(this UIElement element, bool visible)
        => element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
}
