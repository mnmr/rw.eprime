using static EPrimeReadouts.Core.Tests.ArchitectureTestSupport;

namespace EPrimeReadouts.Core.Tests;

public class CommandsArchitectureTests
{
    [Test]
    public async Task EveryCommandIsASyncMethodAndBumpsTheStore()
    {
        string source = Source("ReadoutCommands.cs");
        int methods = CountOf(source, "public static void ");
        await Assert.That(methods).IsGreaterThan(5);
        await Assert.That(CountOf(source, "[SyncMethod]")).IsEqualTo(methods);
        await Assert.That(CountOf(source, "store.Bump()")).IsEqualTo(methods);
    }

    [Test]
    public async Task OnlyCommandsAndStoreLifecycleMutateTheModel()
    {
        // UI files must route every mutation through ReadoutCommands.
        string[] uiFiles =
        {
            Path.Combine("UI", "ReadoutPanel.cs"),
            Path.Combine("UI", "Dialog_ReadoutConfig.cs"),
            Path.Combine("UI", "GroupListView.cs"),
            Path.Combine("UI", "ResourceTreeView.cs"),
            Path.Combine("UI", "EditorView.cs"),
            Path.Combine("UI", "PreviewView.cs"),
        };
        string[] mutators =
        {
            ".CreateGroup(", ".RenameGroup(", ".DeleteGroup(", ".ReorderGroup(",
            ".SetTiers(", ".SetThreshold(", ".ClearThreshold(", ".CleanupMissing(",
        };
        foreach (var file in uiFiles)
        {
            string path = Path.Combine(RepoRoot(), "src", "EPrimeReadouts", file);
            if (!File.Exists(path)) continue; // later tasks create these
            string source = File.ReadAllText(path);
            foreach (var mutator in mutators)
                await Assert.That(source.Replace("ReadoutCommands" + mutator, ""))
                    .DoesNotContain("Model" + mutator);
        }
    }

    [Test]
    public async Task MultiplayerRegistrationIsGuarded()
    {
        string source = Source("MultiplayerSupport.cs");
        await Assert.That(source).Contains("[StaticConstructorOnStartup]");
        await Assert.That(source).Contains("if (MP.enabled) MP.RegisterAll();");
    }
}
