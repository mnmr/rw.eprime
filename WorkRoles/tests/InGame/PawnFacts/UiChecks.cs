using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core;

namespace WorkRolesPawnFacts;

public sealed partial class Probe
{
    private Pawn takeo, retained, unrelated;
    private Role filterRole;
    private ListSelection<Pawn> selection;
    private Ideo oldIdeo, newIdeo;
    private object ideologySkills, ideologySections;
    private int ideologyFactRevision;
    private string collapsedKey;
    private object Sections() => Call(roster, "Sections", store);
    private object Section(Pawn pawn)
    {
        object sections = Sections();
        for (int i = 0; i < (int)Get(sections, "Count"); i++)
        {
            object section = Call(sections, "SectionAt", i);
            if ((bool)Call(section, "Contains", pawn)) return section;
        }
        return null;
    }
    private bool Assigned(Pawn pawn, int id) => store.pawnSets.TryGetValue(pawn, out var set) && set.assignments.Any(a => a.roleId == id);
    private void SetOnly(Pawn pawn, Role role) => RoleCommands.PasteRoleSet(pawn,
        new List<RoleAssignment> { new RoleAssignment { roleId = role.id } });
    private void Next(int next) { phase = next; frames = 0; }
    private void UiChecks()
    {
        if (phase == 0)
        {
            Require(ModsConfig.IdeologyActive, "WR15 native Ideology is active");
            var pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned;
            takeo = pawns.Single(p => p.LabelShort == "Takeo");
            retained = pawns.Single(p => p.LabelShort == "Hysott");
            unrelated = pawns.Single(p => p.LabelShort == "Flow");
            RoleCommands.CreateRole("Pawn facts filter Construction");
            filterRole = store.roles.Single(r => r.label == "Pawn facts filter Construction");
            RoleCommands.AddEntry(filterRole.id, new JobEntry(JobEntryKind.WorkType, "Construction"));
            SetOnly(takeo, filterRole); SetOnly(retained, filterRole); SetOnly(unrelated, filterRole);
            WorkRolesMod.Settings.groupBy = "ideo";
            WorkRolesGameComponent.RequestSettingsWrite();
            Next(1); return;
        }
        if (phase == 1)
        {
            if (!OpenColonists()) return;
            selection = (ListSelection<Pawn>)Get(view, "selection");
            Set(roster, "RoleFilterId", filterRole.id);
            selection.Click(takeo); selection.Toggle(retained);
            Next(2); return;
        }
        if (phase == 2)
        {
            Require(Section(takeo) != null && Section(retained) != null && selection.Count == 2,
                "WR14 native filtered rows retain two selected colonists");
            RoleCommands.RemoveRoleFromPawn(unrelated, filterRole.id);
            Next(3); return;
        }
        if (phase == 3)
        {
            Require(Section(unrelated) == null && selection.Contains(takeo) && selection.Contains(retained) && selection.Count == 2,
                "WR14 removing unrelated filtered membership preserves visible multi-selection while paused");
            collapsedKey = (string)Get(Section(takeo), "Key");
            if (!(bool)Call(roster, "IsCollapsed", collapsedKey)) Call(roster, "ToggleCollapsed", collapsedKey);
            Next(4); return;
        }
        if (phase == 4)
        {
            Require((bool)Call(roster, "IsCollapsed", collapsedKey)
                && selection.Contains(takeo) && selection.Contains(retained) && selection.Count == 2,
                "WR14 collapsed groups and repeated native window repaints preserve selection");
            Call(roster, "ToggleCollapsed", collapsedKey);
            Capture("wr14-before"); Next(5); return;
        }
        if (phase == 5)
        {
            if (!Captured()) return;
            RoleCommands.RemoveRoleFromPawn(takeo, filterRole.id);
            Next(6); return;
        }
        if (phase == 6)
        {
            Require(Section(takeo) == null && !selection.Contains(takeo),
                "WR14 removing selected Takeo's filter role immediately unselects the hidden pawn while paused");
            // Role filters also include equivalent job coverage, so the first
            // visible fallback need not be the other explicit role holder.
            Pawn fallback = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Single(p => selection.Contains(p));
            Require(fallback != takeo && Section(fallback) != null,
                "WR14 fallback selection is a currently visible pawn " + fallback.LabelShort);
            RoleCommands.CreateRole("Pawn facts palette action");
            Role hauler = store.roles.Single(r => r.label == "Pawn facts palette action");
            RoleCommands.AddEntry(hauler.id, new JobEntry(JobEntryKind.WorkType, "Hauling"));
            Require(!Assigned(takeo, hauler.id) && !Assigned(fallback, hauler.id), "WR14 palette action starts unassigned");
            var assign = AccessTools.Method(view.GetType(), "AssignToSelection");
            assign.Invoke(view, new object[] { hauler.id, Enum.ToObject(assign.GetParameters()[1].ParameterType, 0) });
            Require(!Assigned(takeo, hauler.id) && Assigned(fallback, hauler.id),
                "WR14 real AssignToSelection affects visible fallback and never hidden Takeo");
            Capture("wr14-after"); Next(7); return;
        }
        if (phase == 7)
        {
            if (!Captured()) return;
            Set(roster, "RoleFilterId", -1);
            Set(roster, "Search", takeo.LabelShortCap.ToString());
            oldIdeo = takeo.Ideo;
            newIdeo = Find.IdeoManager.IdeosListForReading.First(i => i != oldIdeo && i.name != oldIdeo.name);
            Next(8); return;
        }
        if (phase == 8)
        {
            Require((string)Get(Section(takeo), "Key") == "ideo|" + oldIdeo.name,
                "WR15 Takeo name search shows native original ideology group " + oldIdeo.name);
            ideologySections = Sections();
            Require(ReferenceEquals(ideologySections, Sections()), "WR15 warm roster sections retain snapshot identity");
            object stats = Get(view, "statsState");
            ideologySkills = SkillIdentity(stats, takeo);
            ideologyFactRevision = Revision("FactRevisions", takeo);
            Capture("wr15-before"); Next(9); return;
        }
        if (phase == 9)
        {
            if (!Captured()) return;
            takeo.ideo.SetIdeo(newIdeo);
            Next(10); return;
        }
        if (phase == 10)
        {
            Require(ReferenceEquals(takeo.Ideo, newIdeo) && (string)Get(Section(takeo), "Key") == "ideo|" + newIdeo.name,
                "WR15 native SetIdeo immediately moves visible Takeo into current group " + newIdeo.name);
            Require(!ReferenceEquals(ideologySections, Sections()) && ReferenceEquals(Sections(), Sections()),
                "WR15 changed grouping republishes once and warm reads retain identity");
            Require(Revision("FactRevisions", takeo) == ideologyFactRevision
                && ReferenceEquals(ideologySkills, SkillIdentity(Get(view, "statsState"), takeo)),
                "WR15 presentation-only conversion preserves factual revision and shared skill capture");
            Require(Find.TickManager.Paused, "WR14 WR15 all structural changes became visible while paused");
            Capture("wr15-after"); Next(11); return;
        }
        if (phase == 11 && Captured()) Finish("SUCCESS ui WR14 WR15");
    }
}
