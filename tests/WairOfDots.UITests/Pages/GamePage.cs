namespace WairOfDots.UITests.Pages;

public sealed class GamePage(IStrideTestContext context) : PageObjectBase<GamePage>(context)
{
    public override string AutomationId => "HUD";

    public Panel<GamePage> MapCanvas => Panel("MapCanvas");
    public TextBlock<GamePage> GameTitle => TextBlock("GameTitle");
    public TextBlock<GamePage> Status => TextBlock("StatusDisplay");
    public TextBlock<GamePage> Score => TextBlock("ScoreDisplay");
    public TextBlock<GamePage> Target => TextBlock("TargetDisplay");
    public TextBlock<GamePage> LastEvent => TextBlock("LastEventDisplay");
    public TextBlock<GamePage> Telemetry => TextBlock("TelemetryDisplay");
    public TextBlock<GamePage> OverlayMode => TextBlock("OverlayModeDisplay");
    public TextBlock<GamePage> Speed => TextBlock("SpeedDisplay");
    public Panel<GamePage> HumanCommandPanel => Panel("HumanCommandPanel");
    public Panel<GamePage> SpectatorControlsPanel => Panel("SpectatorControlsPanel");
    public Panel<GamePage> StandingsPanel => Panel("StandingsPanel");
    public Panel<GamePage> OverlayToggleGroup => Panel("OverlayToggleGroup");
    public Button<GamePage> OverlayNormalButton => Button("OverlayNormalButton");
    public Button<GamePage> OverlayEconomyButton => Button("OverlayEconomyButton");
    public Button<GamePage> OverlayCommandButton => Button("OverlayCommandButton");
    public Button<GamePage> OverlayVisibilityButton => Button("OverlayVisibilityButton");
    public Button<GamePage> OverlayAiDebugButton => Button("OverlayAiDebugButton");
    public Button<GamePage> AttackButton => Button("AttackButton");
    public Button<GamePage> HoldButton => Button("HoldButton");
    public Button<GamePage> DefendButton => Button("DefendButton");
    public Button<GamePage> LightPreferenceButton => Button("LightPreferenceButton");
    public Button<GamePage> HeavyPreferenceButton => Button("HeavyPreferenceButton");
    public Button<GamePage> PauseButton => Button("PauseButton");
    public Button<GamePage> RestartButton => Button("RestartButton");
    public Button<GamePage> SpeedSlowButton => Button("SpeedSlowButton");
    public Button<GamePage> SpeedNormalButton => Button("SpeedNormalButton");
    public Button<GamePage> SpeedFastButton => Button("SpeedFastButton");
}
