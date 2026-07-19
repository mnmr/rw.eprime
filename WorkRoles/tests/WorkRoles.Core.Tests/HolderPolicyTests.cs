using System.Xml.Linq;
using System.Xml;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class HolderPolicyTests
{
    [Test]
    public async Task ModeCycleIsAutoNeverCustom()
    {
        await Assert.That(RoleHolderPolicy.Next(RoleHolderMode.Auto)).IsEqualTo(RoleHolderMode.Never);
        await Assert.That(RoleHolderPolicy.Next(RoleHolderMode.Never)).IsEqualTo(RoleHolderMode.Custom);
        await Assert.That(RoleHolderPolicy.Next(RoleHolderMode.Custom)).IsEqualTo(RoleHolderMode.Auto);
    }

    [Test]
    public async Task ChangingEitherRangeEndMovesTheOtherWhenNecessary()
    {
        await Assert.That(RoleHolderPolicy.WithMin(2, 4, 6)).IsEqualTo((6, 6));
        await Assert.That(RoleHolderPolicy.WithMin(2, 4, 3)).IsEqualTo((3, 4));
        await Assert.That(RoleHolderPolicy.WithMax(2, 4, 1)).IsEqualTo((1, 1));
        await Assert.That(RoleHolderPolicy.WithMax(2, 4, 3)).IsEqualTo((2, 3));
    }

    [Test]
    public async Task TrainingWaiverIsClampedToTheConfiguredMinimum()
    {
        await Assert.That(RoleHolderPolicy.WithTraining(3, 2)).IsEqualTo(2);
        await Assert.That(RoleHolderPolicy.WithTraining(3, 5)).IsEqualTo(3);
        await Assert.That(RoleHolderPolicy.WithTraining(0, 1)).IsEqualTo(0);
        await Assert.That(RoleHolderPolicy.WithTraining(3, -1)).IsEqualTo(0);
    }

    [Test]
    public async Task RoleDefinitionMinimumReadsItsWaiverAttribute()
    {
        var xml = new XmlDocument();
        xml.LoadXml("<minHolders waivers=\"1\">2</minHolders>");
        var minimum = new RoleHolderMinimum();

        minimum.LoadDataFromXmlCustom(xml.DocumentElement!);

        await Assert.That(minimum.Count).IsEqualTo(2);
        await Assert.That(minimum.Waivers).IsEqualTo(1);
    }

    [Test]
    public async Task RoleDefinitionMinimumDefaultsToZeroWaivers()
    {
        var xml = new XmlDocument();
        xml.LoadXml("<minHolders>3</minHolders>");
        var minimum = new RoleHolderMinimum();

        minimum.LoadDataFromXmlCustom(xml.DocumentElement!);

        await Assert.That(minimum.Count).IsEqualTo(3);
        await Assert.That(minimum.Waivers).IsEqualTo(0);
    }

    [Test]
    public async Task RoleDefinitionMinimumRejectsNegativeWaivers()
    {
        var xml = new XmlDocument();
        xml.LoadXml("<minHolders waivers=\"-1\">2</minHolders>");
        var minimum = new RoleHolderMinimum();
        bool rejected = false;

        try
        {
            minimum.LoadDataFromXmlCustom(xml.DocumentElement!);
        }
        catch (FormatException)
        {
            rejected = true;
        }

        await Assert.That(rejected).IsTrue();
    }

    [Test]
    public async Task InitialCustomRangePreservesDefinitionWaivers()
    {
        await Assert.That(RoleHolderPolicy.InitialCustom(
                4, RoleHolderRange.Uncapped, 1))
            .IsEqualTo((4, RoleHolderRange.Uncapped, 1));
    }

    [Test]
    public async Task TrainingWaiverRoundTripsInTheCurrentFileFormat()
    {
        var doc = new RoleFileDocument
        {
            roles =
            {
                new FileRole
                {
                    label = "Doctor",
                    holderMode = RoleHolderMode.Custom,
                    minHolders = 3,
                    maxHolders = 5,
                    trainingWaivers = 2,
                    entries = { new JobEntry(JobEntryKind.WorkType, "Doctor") },
                },
            },
        };

        string xml = RoleFile.Build(doc);
        var holders = XElement.Parse(xml)
            .Element("Roles")!.Element("Role")!
            .Element("Options")!.Element("Holders")!;
        await Assert.That(XElement.Parse(xml).Attribute("version")!.Value).IsEqualTo("6");
        await Assert.That(holders.Attribute("train")!.Value).IsEqualTo("2");
        await Assert.That(RoleFile.Parse(xml).roles[0].trainingWaivers).IsEqualTo(2);
    }

    [Test]
    public async Task CustomRangeRoundTripsInCurrentFormat()
    {
        var doc = new RoleFileDocument
        {
            roles =
            {
                new FileRole
                {
                    label = "Doctor",
                    holderMode = RoleHolderMode.Custom,
                    minHolders = 2,
                    maxHolders = 5,
                    entries = { new JobEntry(JobEntryKind.WorkType, "Doctor") },
                },
            },
        };

        string xml = RoleFile.Build(doc);
        var holders = XElement.Parse(xml)
            .Element("Roles")!.Element("Role")!
            .Element("Options")!.Element("Holders")!;
        await Assert.That(XElement.Parse(xml).Attribute("version")!.Value).IsEqualTo("6");
        await Assert.That(holders.Attribute("mode")!.Value).IsEqualTo("custom");
        await Assert.That(holders.Attribute("min")!.Value).IsEqualTo("2");
        await Assert.That(holders.Attribute("max")!.Value).IsEqualTo("5");
        await Assert.That(holders.Attribute("inTraining") == null).IsTrue();

        var parsed = RoleFile.Parse(xml).roles[0];
        await Assert.That(parsed.holderMode).IsEqualTo(RoleHolderMode.Custom);
        await Assert.That(parsed.minHolders).IsEqualTo(2);
        await Assert.That(parsed.maxHolders).IsEqualTo(5);
    }

    [Test]
    public async Task NeverRoundTripsWithoutARange()
    {
        var doc = new RoleFileDocument
        {
            roles =
            {
                new FileRole
                {
                    label = "Art",
                    holderMode = RoleHolderMode.Never,
                    entries = { new JobEntry(JobEntryKind.WorkType, "Art") },
                },
            },
        };

        var parsed = RoleFile.Parse(RoleFile.Build(doc)).roles[0];
        await Assert.That(parsed.holderMode).IsEqualTo(RoleHolderMode.Never);
        await Assert.That(parsed.minHolders).IsEqualTo(0);
        await Assert.That(parsed.maxHolders).IsEqualTo(RoleHolderRange.Uncapped);
    }

    [Test]
    public async Task AutoPreservesAPreviouslyConfiguredCustomRange()
    {
        var doc = new RoleFileDocument
        {
            roles =
            {
                new FileRole
                {
                    label = "Cook",
                    holderMode = RoleHolderMode.Auto,
                    holderRangeSet = true,
                    minHolders = 0,
                    maxHolders = RoleHolderRange.Uncapped,
                    entries = { new JobEntry(JobEntryKind.WorkType, "Cooking") },
                },
            },
        };

        var parsed = RoleFile.Parse(RoleFile.Build(doc)).roles[0];
        await Assert.That(parsed.holderMode).IsEqualTo(RoleHolderMode.Auto);
        await Assert.That(parsed.holderRangeSet).IsTrue();
        await Assert.That(parsed.minHolders).IsEqualTo(0);
        await Assert.That(parsed.maxHolders).IsEqualTo(RoleHolderRange.Uncapped);
    }

    [Test]
    public async Task LegacyHolderValuesAreIgnoredWithoutErrors()
    {
        var parsed = RoleFile.Parse(
            "<WorkRoles version=\"4\"><Palette/><Roles>" +
            "<Role name=\"Doctor\"><Options><Holders min=\"2\" inTraining=\"1\"/></Options>" +
            "<Jobs><WorkType>Doctor</WorkType></Jobs></Role></Roles></WorkRoles>");

        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.roles[0].holderMode).IsEqualTo(RoleHolderMode.Auto);
        await Assert.That(parsed.roles[0].minHolders).IsEqualTo(0);
        await Assert.That(parsed.roles[0].maxHolders).IsEqualTo(RoleHolderRange.Uncapped);
    }
}
