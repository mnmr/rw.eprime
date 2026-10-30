using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests.Persistence;

public class SetupFileTests
{
    [Test]
    [Arguments("<Roles><Role name=\" \"/></Roles>")]
    [Arguments("<Palette><Color name=\"Lost\">invalid</Color></Palette>")]
    [Arguments("<Roles><Role name=\"Worker\"><Options><Locations><Settlement/></Locations></Options></Role></Roles>")]
    [Arguments("<Roles><Role name=\"Worker\"><Options><Tuning><Training><Role min=\"0\" max=\"bad\">Worker</Role></Training></Tuning></Options></Role></Roles>")]
    public async Task MalformedPresentSectionsCannotSilentlyBecomeEmptyReplacements(string section)
    {
        await Assert.That(RoleFile.Parse("<WorkRoles version=\"14\">" + section + "</WorkRoles>").error != null).IsTrue();
    }

    [Test]
    public async Task ExplicitlyHiddenIconAndTemplateFallbackRemainDistinct()
    {
        var document = new RoleFileDocument();
        document.roles.Add(new FileRole { label = "Hidden", hasIcon = true, iconPath = "" });
        document.roles.Add(new FileRole { label = "Default", hasIcon = true, iconPath = null });
        var imported = RoleFile.Parse(RoleFile.Build(document));
        await Assert.That(imported.roles[0].iconPath).IsEqualTo("");
        await Assert.That(imported.roles[1].iconPath).IsNull();
    }

    [Test]
    public async Task BrokenCompositeReferenceRejectsTheSetupBeforeItCanReplaceRoles()
    {
        const string xml = "<WorkRoles version=\"14\"><Roles><Role name=\"Team\" composite=\"true\"><Members><Role roleId=\"missing\">Lost</Role></Members></Role></Roles></WorkRoles>";
        await Assert.That(RoleFile.Parse(xml).error != null).IsTrue();
    }

    // A shared setup must retain authored values even when this machine cannot
    // resolve its jobs, skills, icon or named settlement. The game bridge tests
    // separately prove that these preserved values reach the real store.
    [Test]
    public async Task PortableSetupRetainsAuthoredConfigurationAcrossAnUnavailableMod()
    {
        const string xml = """
            <WorkRoles version="14">
              <Palette><Color name="unused">#123456</Color></Palette>
              <Groups><Group fileId="g" name="Workshop"/></Groups>
              <Roles><Role fileId="r" name="Maker" groupId="g" group="Workshop">
                <Options><Icon>OptionalMod/Icons/Maker</Icon><Locations><Settlement name="Old home"/></Locations>
                  <Tuning><RequiredSkills>OptionalSkill</RequiredSkills></Tuning></Options>
                <Jobs><WorkGiver>OptionalCraft</WorkGiver></Jobs>
              </Role></Roles>
              <GameSettings><Option name="autoOptimize">true</Option><Option name="mechanicalWorkersEnabled">true</Option></GameSettings>
              <PlayerSettings><Option name="chipDisplay">2</Option><SkillColumns><Skill>Crafting</Skill><Skill>OptionalSkill</Skill></SkillColumns></PlayerSettings>
            </WorkRoles>
            """;
        var parsed = RoleFile.Parse(xml);
        await Assert.That(parsed.error).IsNull();
        var output = XElement.Parse(RoleFile.Build(parsed));
        await Assert.That(output.Element("Roles")!.Element("Role")!.Element("Options")!.Element("Icon")?.Value)
            .IsEqualTo("OptionalMod/Icons/Maker");
        await Assert.That(output.Element("GameSettings")?.Elements("Option").Select(e => e.Value).ToArray())
            .IsEquivalentTo(["true", "true"]);
        await Assert.That(output.Element("PlayerSettings")?.Element("SkillColumns")?.Elements("Skill").Select(e => e.Value).ToArray())
            .IsEquivalentTo(["Crafting", "OptionalSkill"]);
        await Assert.That(parsed.roles[0].entries[0].DefName).IsEqualTo("OptionalCraft");
        await Assert.That(parsed.roles[0].requiredSkills).IsEquivalentTo(["OptionalSkill"]);
        await Assert.That(parsed.palette[0].name).IsEqualTo("unused");
    }

    [Test]
    public async Task EmptySectionIsPreservedWithoutInventingOmittedSections()
    {
        var parsed = RoleFile.Parse("<WorkRoles version=\"14\"><Palette/><PlayerSettings><SkillColumns/></PlayerSettings></WorkRoles>");
        await Assert.That(parsed.error).IsNull();
        var output = XElement.Parse(RoleFile.Build(parsed));
        await Assert.That(output.Element("Palette") != null).IsTrue();
        await Assert.That(output.Element("Roles") == null).IsTrue();
        await Assert.That(output.Element("GameSettings") == null).IsTrue();
        await Assert.That(output.Element("PlayerSettings")?.Element("SkillColumns") != null).IsTrue();
    }

    [Test]
    public async Task UnresolvedNamedLocationRemainsAnInactivePortableRestriction()
    {
        string? token = ImportLocationResolver.FromMap("settlement:Old home", new Dictionary<string, string>());
        await Assert.That(token).IsEqualTo("unresolved:settlement:Old home");
    }

    [Test]
    public async Task NewWorldLocationMigrationPreservesPortableNames()
    {
        var migrated = LocationTokenMigration.Normalize(
            ["unresolved:settlement:Old,caravans"], null, new HashSet<string>());
        await Assert.That(migrated).IsEquivalentTo(["unresolved:settlement:Old,caravans"]);
        await Assert.That(LocationRules.Matches(migrated, default)).IsFalse();
    }
}
