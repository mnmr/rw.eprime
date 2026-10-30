using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

/// Replays the detached input captured by the main-thread WorkEvidence probe.
/// Unlike SampleColony, this does not rebuild facts from today's role defs,
/// signal defaults or simplified job catalog. Expected outcomes come from the
/// pre-robot game capture, never from running the engine in this test.
internal sealed class CapturedColony
{
    internal readonly XElement Document;
    internal readonly ColonyView Colony;
    internal readonly RecommendationsTuningOptions Tuning;

    internal CapturedColony(string version)
    {
        using Stream stream = typeof(CapturedColony).Assembly.GetManifestResourceStream(
            $"WorkRoles.Core.Tests.SampleColony.Captures.Fisso-NAM-{version}.xml")
            ?? throw new InvalidDataException($"Missing recorded Fisso-NAM {version} capture.");
        Document = XElement.Load(stream);
        if (((string?)Document.Attribute("game"))?.StartsWith(version + ".", StringComparison.Ordinal) != true)
            throw new InvalidDataException($"Capture is not from RimWorld {version}.");
        if ((string?)Document.Attribute("cohortSource") != "ColonistRecommendationState.MapColonists"
            || (bool?)Document.Attribute("simulationCohortMatches") != true)
            throw new InvalidDataException("Capture must use the verified UI and AutoOptimizer cohort.");
        XElement provenance = Required(Document, "provenance");
        string[] mods = Required(provenance, "activeMods").Elements("mod").Select(e => e.Value).ToArray();
        if (!mods.Contains("mnmr.workroles", StringComparer.OrdinalIgnoreCase)
            || mods.Any(id => id.Equals("Haplo.Miscellaneous.Robots", StringComparison.OrdinalIgnoreCase)
                || id.Equals("Alaestor.MiscRobots.PlusPlus", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("This baseline requires WorkRoles without either Misc. Robots mod.");
        if (((string?)provenance.Attribute("sourceSaveSha256"))?.Length != 64)
            throw new InvalidDataException("Capture must identify its source save.");
        Colony = Read<ColonyView>(Required(Document, "colony"));
        var options = Required(Document, "tuningOptions").Elements("option").ToDictionary(
            e => Enum.Parse<RecommendationTuningOption>((string)e.Attribute("name")!),
            e => (int)e.Attribute("value")!);
        if (options.Count != RecommendationsTuningOptions.Descriptors.Count)
            throw new InvalidDataException("Capture must specify every tuning option; do not silently adopt new defaults.");
        Tuning = RecommendationsTuningOptions.FromValues(options);
        foreach (var option in options)
            if (Tuning.Get(option.Key) != option.Value)
                throw new InvalidDataException($"Captured tuning was normalized: {option.Key}.");
    }

    internal static XElement Required(XElement parent, string name) => parent.Element(name)
        ?? throw new InvalidDataException($"Missing {parent.Name}/{name} in captured input.");

    private static T Read<T>(XElement element) => (T)Read(element, typeof(T))!;

    // Test-only reflection mirrors the probe's detached public-data format.
    // Every writable field and constructor input is mandatory. Derived read-only
    // properties are ignored: their underlying captured facts are reconstructed
    // through the real constructor. New input fields must be reviewed explicitly.
    private static object? Read(XElement element, Type type)
    {
        if ((bool?)element.Attribute("null") == true) return null;
        if (type == typeof(string)) return element.Value;
        if (type.IsEnum) return Enum.Parse(type, element.Value);
        if (type.IsPrimitive || type == typeof(decimal))
            return Convert.ChangeType(element.Value, type, CultureInfo.InvariantCulture);
        if (type == typeof(PawnWorkEvidence))
            return PawnWorkEvidence.Create(
                element.Elements("Capability").Select(Read<WorkCapabilityFact>).ToArray(),
                element.Elements("Aversion").Select(Read<WorkAversionFact>).ToArray());
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            Type[] arguments = type.GetGenericArguments();
            if (definition == typeof(Dictionary<,>))
            {
                var dictionary = (IDictionary)Activator.CreateInstance(type)!;
                foreach (XElement entry in element.Elements("entry"))
                    dictionary.Add(Read(Required(entry, "key"), arguments[0])!,
                        Read(Required(entry, "value"), arguments[1]));
                return dictionary;
            }
            if (definition == typeof(List<>) || definition == typeof(IReadOnlyList<>)
                || definition == typeof(HashSet<>))
            {
                Type concrete = definition == typeof(IReadOnlyList<>)
                    ? typeof(List<>).MakeGenericType(arguments) : type;
                object sequence = Activator.CreateInstance(concrete)!;
                MethodInfo add = concrete.GetMethod("Add")!;
                foreach (XElement item in element.Elements("item"))
                    add.Invoke(sequence, [Read(item, arguments[0])]);
                return sequence;
            }
        }
        ConstructorInfo? constructor = type.GetConstructors()
            .SingleOrDefault(c => c.GetParameters().Length != 0);
        object result;
        if (constructor != null)
        {
            object?[] values = constructor.GetParameters().Select(parameter =>
            {
                XElement input = element.Elements().SingleOrDefault(e =>
                    string.Equals(e.Name.LocalName, parameter.Name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"Missing {type.Name}.{parameter.Name}.");
                return Read(input, parameter.ParameterType);
            }).ToArray();
            result = constructor.Invoke(values);
        }
        else result = Activator.CreateInstance(type)!;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            // Older captures recorded normal/disabled assignments only; the
            // new force-on distinction defaults to false for those fixtures.
            if (type == typeof(AssignmentView) && field.Name == nameof(AssignmentView.ForceOn)
                && element.Element(field.Name) == null) continue;
            // These post-capture inputs describe optional external supply. The
            // accepted saves predate it and explicitly have no such supply.
            if (type == typeof(ColonyView) && element.Element(field.Name) == null
                && (field.Name == nameof(ColonyView.IntegrateMechanicalWorkers)
                    || field.Name == nameof(ColonyView.MechanicalWorkers))) continue;
            // The accepted pawn cohort was captured before mechanical support
            // and contains humans only. Keep those frozen expected outputs.
            if (type == typeof(PawnView) && field.Name == nameof(PawnView.IsMechanical)
                && element.Element(field.Name) == null) continue;
            field.SetValue(result, Read(Required(element, field.Name), field.FieldType));
        }
        return result;
    }
}
