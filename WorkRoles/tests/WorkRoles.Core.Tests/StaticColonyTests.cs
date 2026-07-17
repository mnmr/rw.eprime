using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Certifies the phase-1 inputs: the static colony is exactly what it claims
/// to be, and the flattening (spec -> engine pawn) loses and invents nothing.
/// Everything downstream may then trust these inputs.
public class StaticColonyTests
{
    [Test]
    public async Task TheColonyMatchesItsSpec()
    {
        await Assert.That(StaticColony.Pawns.Length).IsEqualTo(20);
        await Assert.That(StaticColony.Pawns.Count(p => p.HasGun)).IsEqualTo(16); // 80%
        foreach (var pawn in StaticColony.Pawns)
        {
            await Assert.That(pawn.Levels.Length).IsEqualTo(StaticColony.Skills.Length);
            foreach (int level in pawn.Levels)
                await Assert.That(level >= 0 && level <= 20).IsTrue()
                    .Because($"{pawn.Name}: level {level} outside 0..20");
            // Each pawn's levels sum to its drawn pool (30..100).
            int sum = pawn.Levels.Sum();
            await Assert.That(sum >= 30 && sum <= 100).IsTrue()
                .Because($"{pawn.Name}: level sum {sum} outside 30..100");
            await Assert.That(pawn.Attributes.Length).IsEqualTo(4);
            // No duplicate kind+skill; no contradictions on one skill (both
            // passion kinds, or liking AND hating it).
            await Assert.That(pawn.Attributes.Select(a => (a.Kind, a.Skill)).Distinct().Count())
                .IsEqualTo(4);
            var passionSkills = pawn.Attributes
                .Where(a => a.Kind == AttributeKind.BurningPassion || a.Kind == AttributeKind.Passion)
                .Select(a => a.Skill).ToList();
            await Assert.That(passionSkills.Distinct().Count()).IsEqualTo(passionSkills.Count);
            var attitudeSkills = pawn.Attributes
                .Where(a => a.Kind == AttributeKind.Aptitude || a.Kind == AttributeKind.Trait)
                .Select(a => a.Skill).ToList();
            await Assert.That(attitudeSkills.Distinct().Count()).IsEqualTo(attitudeSkills.Count);
        }
    }

    [Test]
    public async Task FlatteningReproducesTheSpec()
    {
        foreach (var spec in StaticColony.Pawns)
        {
            var pawn = StaticColony.ToPawnView(spec);

            for (int i = 0; i < StaticColony.Skills.Length; i++)
                await Assert.That(pawn.SkillLevels[StaticColony.Skills[i]]).IsEqualTo(spec.Levels[i])
                    .Because($"{spec.Name}: {StaticColony.Skills[i]} level");
            await Assert.That(pawn.HasRangedWeapon).IsEqualTo(spec.HasGun);
            await Assert.That(pawn.ShootingLevel).IsEqualTo(spec.Levels[0]);

            // Every attribute lands, nothing extra appears.
            foreach (var attribute in spec.Attributes)
                switch (attribute.Kind)
                {
                    case AttributeKind.Expertise:
                        await Assert.That(pawn.ExpertiseSkills.Contains(attribute.Skill)).IsTrue()
                            .Because($"{spec.Name}: expertise {attribute.Skill}");
                        break;
                    case AttributeKind.BurningPassion:
                        await Assert.That(pawn.PassionScores[attribute.Skill]).IsEqualTo(2)
                            .Because($"{spec.Name}: burning passion {attribute.Skill}");
                        break;
                    case AttributeKind.Passion:
                        await Assert.That(pawn.PassionScores[attribute.Skill]).IsEqualTo(1)
                            .Because($"{spec.Name}: passion {attribute.Skill}");
                        break;
                    case AttributeKind.Aptitude:
                        await Assert.That(pawn.Aptitudes[attribute.Skill] > 0).IsTrue()
                            .Because($"{spec.Name}: talent {attribute.Skill}");
                        break;
                    case AttributeKind.Trait:
                        await Assert.That(pawn.Aptitudes[attribute.Skill] < 0).IsTrue()
                            .Because($"{spec.Name}: apathy {attribute.Skill}");
                        break;
                }

            await Assert.That(pawn.ExpertiseSkills.Count)
                .IsEqualTo(spec.Attributes.Count(a => a.Kind == AttributeKind.Expertise));
            await Assert.That(pawn.PassionScores.Count).IsEqualTo(spec.Attributes.Count(a =>
                a.Kind == AttributeKind.BurningPassion || a.Kind == AttributeKind.Passion));
            await Assert.That(pawn.Aptitudes.Count).IsEqualTo(spec.Attributes.Count(a =>
                a.Kind == AttributeKind.Aptitude || a.Kind == AttributeKind.Trait));

            foreach (var workType in StaticColony.AllWorkTypes)
                await Assert.That(pawn.CapableWorkTypes.Contains(workType)).IsTrue()
                    .Because($"{spec.Name}: capable of {workType}");
        }
    }

    [Test]
    public async Task BestInColonyMatchesTheHandDerivedMaxima()
    {
        // Hand-derived from the generated table; refreshed on regeneration.
        // Shooting 15 = Osla/Quin, Melee 12 = Cass/Hark/Iris, Construction 14
        // = Osla, Mining 16 = Jorn, Cooking 13 = Edda/Rask, Plants 20 = Osla,
        // Animals 17 = Finn, Crafting 20 = Mira, Artistic 14 = Quin,
        // Medicine 14 = Jorn, Social 15 = Mira, Intellectual 20 = Mira.
        var expected = new Dictionary<string, int>
        {
            ["Shooting"] = 15, ["Melee"] = 12, ["Construction"] = 14,
            ["Mining"] = 16, ["Cooking"] = 13, ["Plants"] = 20,
            ["Animals"] = 17, ["Crafting"] = 20, ["Artistic"] = 14,
            ["Medicine"] = 14, ["Social"] = 15, ["Intellectual"] = 20,
        };
        var best = StaticColony.BestInColony(StaticColony.Pawns);
        await Assert.That(best.Count).IsEqualTo(expected.Count);
        foreach (var kv in expected)
            await Assert.That(best[kv.Key]).IsEqualTo(kv.Value).Because(kv.Key);
    }
}
