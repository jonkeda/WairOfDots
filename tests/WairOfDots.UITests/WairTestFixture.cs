namespace WairOfDots.UITests;

public sealed class WairTestFixture : StrideTestFixtureBase
{
    protected override string GetDefaultAppPath()
    {
        var root = FindRepoRoot();
        return Path.Combine(root, "src", "WairOfDots.Windows", "bin", "Debug", "net10.0-windows", "WairOfDots.Windows.exe");
    }

    protected override StrideTestContextOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.StartupTimeoutMs = 30000;
        options.ConnectionTimeoutMs = 15000;
        options.DefaultTimeoutMs = 7000;
        return options;
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "WairOfDots.slnx")))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;

            dir = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
