using System.Xml.Linq;

namespace WorkRoles.Core.Tests;

public class BuildContractTests
{
    private const string ExactOptInCondition =
        "$([System.String]::Equals('$(DeployToRimWorld)', 'true', System.StringComparison.Ordinal))";

    [Test]
    public async Task RimWorldDeploymentRequiresAnExactOptIn()
    {
        XElement project = Project();
        XElement target = DeployTarget(project);

        await Assert.That(target.Attribute("AfterTargets")?.Value).IsEqualTo("Build");
        await Assert.That(target.Attribute("Condition")?.Value)
            .IsEqualTo(ExactOptInCondition);
        await Assert.That((project.Attribute("TreatAsLocalProperty")?.Value ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Contains("WorkRolesDeployDestination");
    }

    [Test]
    [Arguments("True")]
    [Arguments("TRUE")]
    public async Task RimWorldDeploymentRejectsNonExactOptInCasing(string optIn)
    {
        await Assert.That(DeployTarget(Project()).Attribute("Condition")?.Value)
            .IsEqualTo(ExactOptInCondition);
        await Assert.That(string.Equals(optIn, "true", StringComparison.Ordinal))
            .IsFalse();
    }

    [Test]
    public async Task RimWorldDeploymentValidatesAndUsesOneScopedDestination()
    {
        XElement target = DeployTarget(Project());
        string destination = target.Elements("PropertyGroup")
            .Elements("WorkRolesDeployDestination")
            .Select(element => element.Value)
            .SingleOrDefault();
        string[] errorConditions = target.Elements("Error")
            .Select(error => error.Attribute("Condition")?.Value ?? "")
            .ToArray();
        XElement deployedFiles = target.Elements("ItemGroup")
            .Elements("DeployedFiles").Single();
        XElement modFiles = target.Elements("ItemGroup")
            .Elements("ModFiles").Single();
        XElement staleFiles = target.Elements("ItemGroup")
            .Elements("StaleDeployedFiles").Single();
        XElement delete = target.Elements("Delete").Single();
        XElement copy = target.Elements("Copy").Single();

        await Assert.That(destination).IsEqualTo("$(RimWorldMods)\\WorkRoles");
        await Assert.That(errorConditions)
            .Contains("'$(RimWorldMods)' == ''");
        await Assert.That(errorConditions)
            .Contains("'$(WorkRolesDeployDestination)' == ''");
        await Assert.That(deployedFiles.Attribute("Include")?.Value)
            .IsEqualTo("$(WorkRolesDeployDestination)\\**\\*.*");
        await Assert.That(deployedFiles.Attribute("Exclude")?.Value)
            .IsEqualTo("$(WorkRolesDeployDestination)\\About\\PublishedFileId.txt");
        await Assert.That(modFiles.Attribute("Exclude")?.Value)
            .IsEqualTo("$(MSBuildThisFileDirectory)..\\..\\mod\\**\\*.pdb;$(MSBuildThisFileDirectory)..\\..\\mod\\About\\PublishedFileId.txt");
        await Assert.That(staleFiles.Attribute("Exclude")?.Value)
            .IsEqualTo("@(ModFiles->'$(WorkRolesDeployDestination)\\%(RecursiveDir)%(Filename)%(Extension)')");
        await Assert.That(delete.Attribute("Files")?.Value)
            .IsEqualTo("@(StaleDeployedFiles)");
        await Assert.That(copy.Attribute("SourceFiles")?.Value)
            .IsEqualTo("@(ModFiles)");
        await Assert.That(copy.Attribute("DestinationFiles")?.Value)
            .IsEqualTo("@(ModFiles->'$(WorkRolesDeployDestination)\\%(RecursiveDir)%(Filename)%(Extension)')");
    }

    [Test]
    public async Task ReadmeDocumentsDeploymentAsOptIn()
    {
        string readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));

        await Assert.That(readme)
            .Contains("dotnet build WorkRoles.slnx -p:DeployToRimWorld=true");
        await Assert.That(readme)
            .Contains("Building does not deploy the mod by default.");
        await Assert.That(readme.Contains(
                "The build deploys the `mod/` folder to your RimWorld `Mods` directory automatically",
                StringComparison.Ordinal))
            .IsFalse();
    }

    private static XElement Project()
    {
        string projectPath = Path.Combine(
            RepoRoot(), "src", "WorkRoles", "WorkRoles.csproj");
        return XElement.Load(projectPath);
    }

    private static XElement DeployTarget(XElement project)
    {
        return project.Elements("Target")
            .Single(target => target.Attribute("Name")?.Value == "DeployToRimWorld");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
