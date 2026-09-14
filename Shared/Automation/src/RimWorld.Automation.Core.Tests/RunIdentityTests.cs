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
}
