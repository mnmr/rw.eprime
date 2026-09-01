using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Implanner.Core
{
    /// <summary>
    /// Import/Export of implant plans in a human-readable/editable XML format
    /// using System.Xml.Linq.
    /// <para>
    /// Format:
    /// <code>
    /// &lt;ImplannerPlans&gt;
    ///   &lt;Plan Name="Full bionics" Extends="Essentials"&gt;
    ///     &lt;Implant Def="BionicArm" MayRequire="pkg.id"&gt;
    ///       &lt;Slot&gt;0&lt;/Slot&gt;
    ///       &lt;Slot&gt;1&lt;/Slot&gt;
    ///     &lt;/Implant&gt;
    ///   &lt;/Plan&gt;
    /// &lt;/ImplannerPlans&gt;
    /// </code>
    /// </para>
    /// <para>
    /// Save-local ids never travel. A base-plan link travels as the base
    /// plan's NAME in an <c>Extends</c> attribute, and only when the base
    /// plan is part of the exported payload; a link to a plan outside the
    /// payload is simply omitted.
    /// </para>
    /// <para>
    /// Temp-id contract: <see cref="TryImport"/> assigns TEMPORARY 1-based
    /// ids in payload order (plan i carries Id = i + 1) and resolves each
    /// <c>Extends</c> name onto those temporary ids (BasePlanId = the base's
    /// temp id, 0 when absent). These ids are placeholders only — the applier
    /// (<see cref="PlannerModel.ImportPlans"/>) remaps them onto real
    /// allocator ids and must never store them as-is; goals take their
    /// natural identity from the applied plan.
    /// </para>
    /// </summary>
    public static class PlansXml
    {
        // ── Export ────────────────────────────────────────────────────────────

        /// <summary>
        /// Pretty-printed export of the given plans in list order. Base links
        /// whose target plan is in the payload become <c>Extends</c>
        /// attributes (by name); other links are dropped. When
        /// <paramref name="packageIdOf"/> is provided (implant defName →
        /// owning packageId, null for base-game content), each restricted
        /// Implant element gets a <c>MayRequire</c> attribute.
        /// Throws <see cref="InvalidOperationException"/> on blank or
        /// duplicate plan names (the model enforces uniqueness, so the UI
        /// never hits this).
        /// </summary>
        public static string Export(
            IReadOnlyList<Plan> plans,
            Func<string, string?>? packageIdOf = null)
        {
            if (!TryCollectPlanNames(plans, out _, out string? nameError))
                throw new InvalidOperationException(nameError);

            // Id → exported name, for base-link rewriting.
            var idToName = new Dictionary<int, string>();
            if (plans != null)
                foreach (var plan in plans)
                    idToName[plan.Id] = plan.Name.Trim();

            var root = new XElement("ImplannerPlans");
            if (plans != null)
            {
                foreach (var plan in plans)
                {
                    var planEl = new XElement("Plan");
                    planEl.SetAttributeValue("Name", plan.Name.Trim());
                    if (plan.BasePlanId != 0 && plan.BasePlanId != plan.Id
                        && idToName.TryGetValue(plan.BasePlanId, out string baseName))
                        planEl.SetAttributeValue("Extends", baseName);

                    foreach (var goal in plan.Implants)
                    {
                        if (goal == null || string.IsNullOrEmpty(goal.ImplantDefName))
                            continue;
                        var implantEl = new XElement("Implant");
                        implantEl.SetAttributeValue("Def", goal.ImplantDefName);
                        string? packageId = packageIdOf?.Invoke(goal.ImplantDefName);
                        if (!string.IsNullOrEmpty(packageId))
                            implantEl.SetAttributeValue("MayRequire", packageId);
                        for (int i = 0; i < goal.SlotOrdinals.Count; i++)
                            implantEl.Add(new XElement("Slot",
                                goal.SlotOrdinals[i].ToString(CultureInfo.InvariantCulture)));
                        planEl.Add(implantEl);
                    }

                    root.Add(planEl);
                }
            }

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = true,
            };

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, settings))
                root.WriteTo(writer);
            return sb.ToString();
        }

        // ── Import ────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses the XML format. Returns false with a short error on
        /// malformed XML, a missing &lt;ImplannerPlans&gt; root, a blank or
        /// duplicate plan name (trimmed, ordinal-ignore-case), an unknown
        /// <c>Extends</c> target, an <c>Extends</c> chain that cycles
        /// (including a plan extending itself), or a negative/non-numeric
        /// slot ordinal; failure is atomic (<paramref name="plans"/> comes
        /// back empty).
        /// Unknown elements and attributes are ignored for forward
        /// compatibility. Slot ordinals are deduplicated and sorted; an
        /// Implant without valid slots is dropped; a Plan without implants is
        /// still valid.
        /// <para>
        /// When <paramref name="isModActive"/> is provided, Plan and Implant
        /// elements honor <c>MayRequire</c> (comma-separated packageIds, ALL
        /// must be active) and <c>MayRequireAnyOf</c> (ANY must be active)
        /// attributes; an element whose requirements are not met is skipped
        /// (an implant disappears from its plan, a plan disappears entirely,
        /// and an <c>Extends</c> naming a mod-skipped plan degrades to no
        /// base rather than failing). A null predicate keeps everything.
        /// </para>
        /// <para>
        /// Parsed plans follow the temp-id contract documented on this class:
        /// plan i gets Id = i + 1 and BasePlanId names the base's temp id
        /// (0 when absent). The applier remaps them to real allocator ids.
        /// </para>
        /// </summary>
        public static bool TryImport(
            string? xml,
            out List<Plan> plans,
            out string? error,
            Func<string, bool>? isModActive = null)
        {
            plans = new List<Plan>();
            error = null;

            if (string.IsNullOrEmpty(xml))
            {
                error = "XML string is null or empty.";
                return false;
            }

            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (Exception ex)
            {
                error = "Malformed XML: " + ex.Message;
                return false;
            }

            var root = doc.Root;
            if (root == null || root.Name.LocalName != "ImplannerPlans")
            {
                error = "Missing <ImplannerPlans> root element.";
                return false;
            }

            // First pass: build kept plans (temp ids) and record the names
            // of plans skipped for missing mods so Extends can degrade.
            var keptNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var skippedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extendsNames = new List<string?>();
            foreach (var planEl in root.Elements("Plan"))
            {
                string name = ((string)planEl.Attribute("Name") ?? "").Trim();
                if (!ModsPresent(planEl, isModActive))
                {
                    if (name.Length > 0) skippedNames.Add(name);
                    continue;
                }
                if (name.Length == 0)
                {
                    error = "Plan names must not be empty.";
                    plans.Clear();
                    return false;
                }
                if (keptNames.ContainsKey(name))
                {
                    error = "Duplicate plan name \"" + name + "\".";
                    plans.Clear();
                    return false;
                }

                int tempId = plans.Count + 1; // remapped on apply
                var goals = new List<ImplantGoal>();
                foreach (var implantEl in planEl.Elements("Implant"))
                {
                    if (!ModsPresent(implantEl, isModActive)) continue;
                    string def = ((string)implantEl.Attribute("Def") ?? "").Trim();
                    if (def.Length == 0) continue;

                    List<int>? ordinals = null;
                    foreach (var slotEl in implantEl.Elements("Slot"))
                    {
                        string text = slotEl.Value.Trim();
                        if (!int.TryParse(text, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int ordinal)
                            || ordinal < 0)
                        {
                            error = "Invalid slot ordinal \"" + text
                                + "\" in plan \"" + name + "\".";
                            plans.Clear();
                            return false;
                        }
                        ordinals ??= new List<int>();
                        if (!ordinals.Contains(ordinal)) ordinals.Add(ordinal);
                    }
                    if (ordinals == null) continue; // no valid slots → drop implant
                    ordinals.Sort();
                    goals.Add(new ImplantGoal(0, def, ordinals));
                }

                keptNames.Add(name, tempId);
                extendsNames.Add(((string)planEl.Attribute("Extends"))?.Trim());
                plans.Add(new Plan(tempId, name, 0, goals));
            }

            // Second pass: resolve Extends names onto temp ids. A target
            // skipped for mods degrades to no base; a name in neither set is
            // an error.
            for (int i = 0; i < plans.Count; i++)
            {
                string? extendsName = extendsNames[i];
                if (extendsName == null || extendsName.Length == 0) continue;
                if (keptNames.TryGetValue(extendsName, out int baseTempId))
                {
                    plans[i].BasePlanId = baseTempId;
                }
                else if (!skippedNames.Contains(extendsName))
                {
                    error = "Unknown base plan \"" + extendsName + "\".";
                    plans.Clear();
                    return false;
                }
            }

            // Third pass: the base chain must be acyclic. The model never
            // creates a cycle (base links are chosen at creation from
            // existing plans), so a payload that would import one — a plan
            // extending itself, or A→B→…→A — is invalid as a whole.
            for (int i = 0; i < plans.Count; i++)
            {
                if (!ChainCycles(plans, i)) continue;
                error = "Base plan cycle at \"" + plans[i].Name + "\".";
                plans.Clear();
                return false;
            }

            return true;
        }

        /// Walks the temp-id base chain from plan i (temp id = index + 1);
        /// true when it returns to i. Bounded by the plan count.
        static bool ChainCycles(List<Plan> plans, int i)
        {
            int baseId = plans[i].BasePlanId;
            for (int steps = 0; baseId != 0 && steps < plans.Count; steps++)
            {
                if (baseId == plans[i].Id) return true;
                baseId = plans[baseId - 1].BasePlanId;
            }
            return baseId == plans[i].Id;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the vanilla-style mod-requirement attributes on an
        /// element: <c>MayRequire</c> (comma-separated packageIds, ALL must
        /// be active) and <c>MayRequireAnyOf</c> (comma-separated packageIds,
        /// ANY must be active). A null predicate keeps everything.
        /// </summary>
        static bool ModsPresent(XElement el, Func<string, bool>? isModActive)
        {
            if (isModActive == null) return true;

            string all = (string)el.Attribute("MayRequire");
            if (!string.IsNullOrEmpty(all))
            {
                foreach (var id in all.Split(','))
                    if (!isModActive(id.Trim()))
                        return false;
            }

            string any = (string)el.Attribute("MayRequireAnyOf");
            if (!string.IsNullOrEmpty(any))
            {
                bool anyActive = false;
                foreach (var id in any.Split(','))
                {
                    if (isModActive(id.Trim()))
                    {
                        anyActive = true;
                        break;
                    }
                }
                if (!anyActive) return false;
            }

            return true;
        }

        static bool TryCollectPlanNames(
            IReadOnlyList<Plan>? plans,
            out HashSet<string> names,
            out string? error)
        {
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            error = null;
            if (plans == null) return true;

            for (int i = 0; i < plans.Count; i++)
            {
                string name = plans[i].Name?.Trim() ?? "";
                if (name.Length == 0)
                {
                    error = "Plan names must not be empty.";
                    return false;
                }
                if (names.Add(name)) continue;
                error = "Duplicate plan name \"" + name + "\".";
                return false;
            }
            return true;
        }
    }
}
