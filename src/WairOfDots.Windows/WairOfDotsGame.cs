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
    private const double FixedTickSeconds = 1.0 / 8.0;

    private GameSimulation _simulation = GameSimulation.Create(new GameSettings());
    private readonly object _stateLock = new();
    private double _tickAccumulator;
    private double _visualTickProgress = 1.0;
    private UIElement? _mainUi;
    private UIComponent? _uiComponent;
    private SpriteFont? _font;
    private StackPanel? _menuPanel;
    private StackPanel? _hudPanel;
    private Canvas? _mapCanvas;
    private StackPanel? _settingsPanel;
    private StackPanel? _endPanel;
    private StackPanel? _humanCommandPanel;
    private StackPanel? _spectatorControlsPanel;
    private StackPanel? _standingsPanel;
    private StackPanel? _cityListPanel;
    private TextBlock? _statusText;
    private TextBlock? _scoreText;
    private TextBlock? _targetText;
    private TextBlock? _lastEventText;
    private TextBlock? _telemetryText;
    private TextBlock? _endText;
    private TextBlock? _pauseButtonText;
    private TextBlock? _humanToggleText;
    private TextBlock? _speedText;
    private EditText? _seedInput;
    private EditText? _aiCountInput;
    private ToggleButton? _humanPlayerToggle;
    private int _selectedTargetCityId;
    private int _simulationSpeed = 1;

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
                _tickAccumulator += gameTime.Elapsed.TotalSeconds;
                while (_tickAccumulator >= FixedTickSeconds)
                {
                    for (var i = 0; i < _simulationSpeed; i++)
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

    public void StartMatch(int? seed = null, int? aiPlayers = null, GameMode? mode = null)
    {
        lock (_stateLock)
        {
            var parsedSeed = seed ?? ParseInt(_seedInput?.Text, 1337);
            var parsedAiPlayers = aiPlayers ?? ParseInt(_aiCountInput?.Text, 4);
            var parsedMode = mode ?? ResolveModeFromMenu();
            SetHumanToggle(parsedMode == GameMode.HumanVsAi);
            _simulation = GameSimulation.Create(new GameSettings(
                Seed: parsedSeed,
                AiPlayers: parsedAiPlayers,
                Mode: parsedMode));
            _selectedTargetCityId = 0;
            if (_simulation.HasHumanPlayer)
                _simulation.ApplyHumanCommand(new HumanCommand(TargetCityId: _selectedTargetCityId));

            _simulation.Start();
            _tickAccumulator = 0;
            _visualTickProgress = 1.0;
            _simulationSpeed = 1;
            ShowGame();
            RefreshUi();
        }
    }

    public void RestartMatch()
    {
        lock (_stateLock)
        {
            StartMatch(_simulation.Settings.Seed, _simulation.Settings.AiPlayers, _simulation.Settings.Mode);
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

    public void SetSimulationSpeed(int speed)
    {
        lock (_stateLock)
        {
            _simulationSpeed = Math.Clamp(speed, 1, 8);
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
            MinimumWidth = 890,
            MinimumHeight = 660,
            MaximumWidth = 890,
            MaximumHeight = 660,
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
        speedRow.Children.Add(Button("SpeedSlowButton", "Slower", () => SetSimulationSpeed(Math.Max(1, _simulationSpeed / 2))));
        speedRow.Children.Add(Button("SpeedNormalButton", "Normal", () => SetSimulationSpeed(1)));
        speedRow.Children.Add(Button("SpeedFastButton", "Faster", () => SetSimulationSpeed(Math.Min(8, _simulationSpeed * 2))));
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

        _cityListPanel = new StackPanel
        {
            Name = "CityListPanel",
            Orientation = Orientation.Vertical
        };
        commandPanel.Children.Add(_cityListPanel);

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

        if (_statusText != null)
            _statusText.Text = $"Tick {snapshot.Tick} | {snapshot.Phase} | {snapshot.GameMode} | Seed {_simulation.Settings.Seed} | AI {_simulation.Settings.AiPlayers}";

        if (_scoreText != null)
        {
            if (snapshot.HasHumanPlayer)
            {
                var human = snapshot.Players.First(p => p.Id == GameConstants.HumanPlayerId);
                _scoreText.Text = $"Human score {human.Score:F0} | Resources {human.Resources:F1} | General {human.GeneralHealth:F0}";
            }
            else
            {
                var leader = snapshot.Standings.OrderByDescending(player => player.Score).ThenBy(player => player.PlayerId).First();
                _scoreText.Text = $"Leader {leader.Name} | Score {leader.Score:F0} | Cities {leader.CityCount} | Units {leader.UnitCount}";
            }
        }

        if (_targetText != null)
        {
            var selectedCityId = snapshot.HasHumanPlayer ? snapshot.HumanTargetCityId : _selectedTargetCityId;
            var target = snapshot.Cities.FirstOrDefault(c => c.Id == selectedCityId);
            _targetText.Text = snapshot.HasHumanPlayer
                ? $"Directive {snapshot.HumanDirective} | Target {target?.Name ?? "None"} | Infantry {snapshot.HumanLightPreference:P0}"
                : $"Spectating {target?.Name ?? "None"} | Owner {OwnerLabel(target?.OwnerId ?? GameConstants.NeutralPlayerId, snapshot.HasHumanPlayer)} | Units {target?.TotalUnits ?? 0}";
        }

        if (_lastEventText != null)
            _lastEventText.Text = snapshot.LastEvent;

        if (_telemetryText != null)
        {
            var latest = _simulation.Telemetry.LastOrDefault();
            _telemetryText.Text = latest == null ? "AI telemetry pending" : $"AI telemetry {latest.GenomeId}: {latest.Action}";
        }

        if (_pauseButtonText != null)
            _pauseButtonText.Text = _simulation.Phase == MatchPhase.Paused ? "Resume" : "Pause";

        if (_speedText != null)
            _speedText.Text = $"Spectator speed {_simulationSpeed}x";

        _humanCommandPanel?.SetVisible(snapshot.HasHumanPlayer);
        _spectatorControlsPanel?.SetVisible(!snapshot.HasHumanPlayer);
        _standingsPanel?.SetVisible(!snapshot.HasHumanPlayer);

        RefreshStandings(snapshot);
        RefreshCityButtons(snapshot);
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

        _standingsPanel.Children.Clear();
        _standingsPanel.Children.Add(Text("StandingsTitle", "Standings", 15, Color.White));
        foreach (var standing in snapshot.Standings.OrderByDescending(player => player.Score).ThenBy(player => player.PlayerId))
        {
            var status = standing.IsEliminated ? "out" : "in";
            _standingsPanel.Children.Add(Text(
                $"Standing_{standing.PlayerId}",
                $"{standing.Name} | {standing.Score:F0} | C {standing.CityCount} U {standing.UnitCount} | G {standing.GeneralHealth:F0} | {status}",
                11,
                PlayerColor(standing.PlayerId)));
        }
    }

    private void RefreshCityButtons(MatchSnapshot snapshot)
    {
        if (_cityListPanel == null)
            return;

        _cityListPanel.Children.Clear();
        foreach (var city in snapshot.Cities.OrderBy(city => city.Id))
        {
            var owner = OwnerLabel(city.OwnerId, snapshot.HasHumanPlayer);
            var label = snapshot.HasHumanPlayer
                ? $"{city.Id}: {city.Name} | {owner} | H {city.HumanUnits} E {city.EnemyUnits}"
                : $"{city.Id}: {city.Name} | {owner} | U {city.TotalUnits}";
            _cityListPanel.Children.Add(Button($"CityButton_{city.Id}", label, () => SelectTargetCity(city.Id)));
        }
    }

    private void RefreshMap(MatchSnapshot snapshot)
    {
        if (_mapCanvas == null)
            return;

        _mapCanvas.Children.Clear();
        AddTerrainPatches();
        AddMapLegend();
        AddCityMarkers(snapshot);
        AddUnitMarkers(snapshot);
    }

    private void AddTerrainPatches()
    {
        foreach (var patch in _simulation.Terrain)
        {
            var element = new StackPanel
            {
                Name = $"TerrainPatch_{patch.Id}_{patch.Kind}",
                BackgroundColor = TerrainColor(patch.Kind)
            };

            var center = ToMapRelative(patch.Center);
            element.SetCanvasRelativePosition(new Vector3(center.X, center.Y, 0));
            element.SetCanvasRelativeSize(new Vector3((float)(patch.Width / MapRange), (float)(patch.Height / MapRange), 0));
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
            var rel = ToMapRelative(city.Position);
            var button = new Button
            {
                Name = $"MapCity_{city.Id}",
                Content = new TextBlock
                {
                    Text = "◆",
                    Font = _font,
                    TextSize = citySnapshot.TotalUnits > 0
                        ? 34
                        : citySnapshot.OwnerId == GameConstants.NeutralPlayerId ? 26 : 30,
                    TextColor = PlayerColor(citySnapshot.OwnerId)
                },
                Padding = new Thickness(0, 0, 0, 0)
            };
            button.Click += (_, _) => SelectTargetCity(city.Id);
            button.SetCanvasRelativePosition(new Vector3(rel.X, rel.Y, 0));
            button.SetCanvasRelativeSize(new Vector3(0.04f, 0.055f, 0));
            button.SetCanvasPinOrigin(new Vector3(0.5f, 0.5f, 0));
            _mapCanvas!.Children.Add(button);

            var label = Text($"MapCityLabel_{city.Id}", city.Id.ToString(), 11, Color.White);
            label.SetCanvasRelativePosition(new Vector3(rel.X + 0.018f, rel.Y + 0.028f, 0));
            label.SetCanvasRelativeSize(new Vector3(0.035f, 0.03f, 0));
            _mapCanvas.Children.Add(label);
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
                    unit.VisualMoveTick == _simulation.Tick && unit.HasVisualMovement);
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

    private GameMode ResolveModeFromMenu()
        => _humanPlayerToggle?.State == ToggleState.UnChecked ? GameMode.AiOnly : GameMode.HumanVsAi;

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

    private static string OwnerLabel(int ownerId, bool hasHumanPlayer)
        => ownerId switch
        {
            GameConstants.NeutralPlayerId => "Neutral",
            GameConstants.HumanPlayerId when hasHumanPlayer => "Human",
            _ => $"AI {ownerId}"
        };

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
