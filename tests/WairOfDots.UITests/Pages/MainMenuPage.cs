namespace WairOfDots.UITests.Pages;

public sealed class MainMenuPage(IStrideTestContext context) : PageObjectBase<MainMenuPage>(context)
{
    public override string AutomationId => "MainMenuPanel";

    public TextBlock<MainMenuPage> Title => TextBlock("MenuTitle");
    public EditText<MainMenuPage> SeedInput => EditText("SeedInput");
    public EditText<MainMenuPage> AiCountInput => EditText("AiCountInput");
    public CheckBox<MainMenuPage> HumanPlayerCheckBox => CheckBox("HumanPlayerCheckBox");
    public CheckBox<MainMenuPage> HumanRoleButton => CheckBox("HumanRoleButton");
    public Button<MainMenuPage> StartGameButton => Button("StartGameButton");
    public Button<MainMenuPage> SettingsButton => Button("SettingsButton");

    public MainMenuPage Start(int seed = 1337, int aiPlayers = 4, bool human = true)
    {
        SeedInput.SetText(seed.ToString());
        AiCountInput.SetText(aiPlayers.ToString());
        HumanPlayerCheckBox.SetChecked(human);
        StartGameButton.Click();
        return this;
    }
}
