using RimWorld.Automation.Core;

public class RunIdentityTests
{
    // This identity gate protects process/input/resource ownership before a
    // game exists; UI behavior cannot prove rejection of unsafe launch paths.
    [Test]
    public async Task EachRunAcceptsOnlyItsOwnProfileAndToken()
    {
        const string a = "0123456789abcdef0123456789abcdef";
        const string b = "fedcba9876543210fedcba9876543210";
        string profile = RunIdentity.RunsRoot + "\\" + a;
        await Assert.That(RunIdentity.Matches(profile, "rimworld-shared-" + a)).IsTrue();
        await Assert.That(RunIdentity.Matches(profile.Replace('\\', '/'), "rimworld-shared-" + a)).IsTrue();
        foreach (string wrong in new[] { RunIdentity.RunsRoot, profile + "\\child", profile + "-extra",
            RunIdentity.RunsRoot + "\\..\\" + a, @"C:\Users\player\" + a })
            await Assert.That(RunIdentity.Matches(wrong, "rimworld-shared-" + a)).IsFalse();
        await Assert.That(RunIdentity.Matches(profile, "rimworld-shared-" + b)).IsFalse();
        await Assert.That(RunIdentity.Matches(profile, "rimworld-shared-" + a + "extra")).IsFalse();
    }

    // Extra launch arguments must never redirect the profile, log, token or
    // display the host validated above; only Pickle runner flags may pass.
    [Test]
    public async Task ExtraGameArgumentsAreLimitedToTestRunnerFlags()
    {
        foreach (string allowed in new[] { "-pickle-run", "-pickle-run=spike.feature,!@wip",
            @"-pickle-report-dir=D:\Code\RimWorld\AutomationProfiles\Shared\Runs\x\PickleReports", "-pickle-no-http" })
            await Assert.That(RunIdentity.IsAllowedGameArgument(allowed)).IsTrue();
        foreach (string rejected in new[] { "", "-savedatafolder=C:\\x", "-logFile", "-screen-width", "-automationtoken=x",
            "-pickle-run=a b", "-pickle-run=\"x\"", "-Pickle-run", "-pickle-", "pickle-run", "-pickle-run -savedatafolder=x" })
            await Assert.That(RunIdentity.IsAllowedGameArgument(rejected)).IsFalse();
    }
}
