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
    public Button<GamePage> AttackButton => Button("AttackButton");
    public Button<GamePage> HoldButton => Button("HoldButton");
    public Button<GamePage> DefendButton => Button("DefendButton");
    public Button<GamePage> LightPreferenceButton => Button("LightPreferenceButton");
    public Button<GamePage> HeavyPreferenceButton => Button("HeavyPreferenceButton");
    public Button<GamePage> PauseButton => Button("PauseButton");
    public Button<GamePage> RestartButton => Button("RestartButton");

    public Button<GamePage> CityButton(int cityId) => Button($"CityButton_{cityId}");
    public Button<GamePage> MapCity(int cityId) => Button($"MapCity_{cityId}");
}
