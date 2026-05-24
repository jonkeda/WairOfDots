namespace WairOfDots.UITests.Pages;

public sealed class MainMenuPage(IStrideTestContext context) : PageObjectBase<MainMenuPage>(context)
{
    public override string AutomationId => "MainMenuPanel";

    public TextBlock<MainMenuPage> Title => TextBlock("MenuTitle");
    public EditText<MainMenuPage> SeedInput => EditText("SeedInput");
    public EditText<MainMenuPage> AiCountInput => EditText("AiCountInput");
    public Button<MainMenuPage> StartGameButton => Button("StartGameButton");
    public Button<MainMenuPage> SettingsButton => Button("SettingsButton");

    public MainMenuPage Start(int seed = 1337, int aiPlayers = 4)
    {
        SeedInput.SetText(seed.ToString());
        AiCountInput.SetText(aiPlayers.ToString());
        StartGameButton.Click();
        return this;
    }
}
