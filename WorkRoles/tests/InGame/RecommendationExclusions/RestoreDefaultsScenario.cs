using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.UI;

namespace WorkRolesExclusionVerification
{
    public static partial class Probe
    {
        private static Dialog_RestorePreview restoreDialog;
        private static Role[] defaultRoles;
        private static string assignmentsBeforeRestore;

        private static string AssignmentSignature() => string.Join(";", store.pawnSets
            .OrderBy(pair => pair.Key.thingIDNumber)
            .Select(pair => pair.Key.thingIDNumber + ":" + string.Join(",", pair.Value.assignments
                .Select(a => a.roleId + "/" + a.state + "/" + a.pinned))));

        private static void BeginRestoreDefaults()
        {
            string[] names = { "WS_Rescuer", "WS_Firefighter", "WS_Patient", "WS_Bedrest", "WS_Laborer" };
            defaultRoles = names.Select(name => store.roles.Single(r => r.templateDefName == name)).ToArray();
            Assert(defaultRoles.All(r => r.excludedFromRecommendations), "default exclusions start present");
            Assert(!Seeding.ComputeRestoreItems().Any(i => i.defaultExclusions), "complete default list offers no redundant restore item");
            foreach (Role value in defaultRoles) RoleCommands.SetRoleRecommendationExcluded(value.id, false);
            role = store.roles.Single(r => r.templateDefName == "WS_Hunter");
            RoleCommands.SetRoleRecommendationExcluded(role.id, true);
            assignmentsBeforeRestore = AssignmentSignature();
            RoleCommands.RestoreSelected(new RestoreSelection());
            Assert(defaultRoles.All(r => !r.excludedFromRecommendations), "unselected restoration leaves exclusions alone");

            var items = Seeding.ComputeRestoreItems();
            Assert(items.Count(i => i.defaultExclusions) == 1 && items.Single(i => i.defaultExclusions).UndoesUserChange,
                "missing defaults offer one clearly marked restore choice");
            restoreDialog = new Dialog_RestorePreview(items);
            object rows = Read(restoreDialog, "rows");
            Call(restoreDialog, "EnsureRowsCurrent");
            Assert(ReferenceEquals(rows, Read(restoreDialog, "rows")), "unchanged restore preview reuses its rows");
            RoleCommands.SetRoleRecommendationExcluded(role.id, false);
            Call(restoreDialog, "EnsureRowsCurrent");
            Assert(ReferenceEquals(rows, Read(restoreDialog, "rows")), "unrelated exclusion edits retain equal restore rows");
            RoleCommands.SetRoleRecommendationExcluded(role.id, true);
            foreach (Role value in defaultRoles) RoleCommands.SetRoleRecommendationExcluded(value.id, true);
            Call(restoreDialog, "EnsureRowsCurrent");
            Assert(!RestoreRows().Cast<object>().Any(r => ((Seeding.RestoreItem)Read(r, "item")).defaultExclusions),
                "restore choice disappears immediately while paused once defaults are present");
            foreach (Role value in defaultRoles) RoleCommands.SetRoleRecommendationExcluded(value.id, false);
            Call(restoreDialog, "EnsureRowsCurrent");
            Assert(DefaultRestoreRow() != null, "restore choice reappears immediately while paused after removals");

            var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
            window = (MainTabWindow_WorkRoles)button.TabWindow;
            Find.MainTabsRoot.SetCurrentTab(button);
            SelectTab("Recommendations");
            AccessTools.Field(typeof(Dialog_RestorePreview), "scroll").SetValue(restoreDialog,
                new Vector2(0f, RestoreRows().IndexOf(DefaultRestoreRow()) * 26f));
            Find.WindowStack.Add(restoreDialog);
            stage = 20; settle = 0;
        }

        private static IList RestoreRows() => (IList)Read(restoreDialog, "rows");
        private static object DefaultRestoreRow() => RestoreRows().Cast<object>()
            .SingleOrDefault(r => ((Seeding.RestoreItem)Read(r, "item")).defaultExclusions);

        private static void RestoreRow(Dialog_RestorePreview __instance, float rowW)
        {
            if (!ReferenceEquals(__instance, restoreDialog)) return;
            int index = RestoreRows().IndexOf(DefaultRestoreRow());
            if (index >= 0) Point("restore-defaults", new Vector2(rowW - 12f, index * 26f + 12f));
        }

        private static void UpdateRestoreDefaults()
        {
            if (stage == 20)
            {
                stage = 21; Mark("defaults-preview");
            }
            else if (stage == 21 && (int)Read(restoreDialog, "includedCount") == 0)
            {
                RoleCommands.SetRoleRecommendationExcluded(role.id, false);
                Call(restoreDialog, "EnsureRowsCurrent");
                Assert(!(bool)Read(DefaultRestoreRow(), "included"), "unchecked restore choice survives a paused refresh");
                RoleCommands.SetRoleRecommendationExcluded(role.id, true);
                stage = 22; Mark("defaults-deselected");
            }
            else if (stage == 22 && (int)Read(restoreDialog, "includedCount") == 1)
            {
                Assert((bool)Read(DefaultRestoreRow(), "included"), "UI selects only the default exclusions restoration");
                stage = 23; Mark("defaults-selected");
            }
            else if (stage == 23 && Find.WindowStack.WindowOfType<Dialog_RestorePreview>() == null)
            {
                Assert(defaultRoles.All(r => r.excludedFromRecommendations), "Restore Defaults Apply restores all five existing roles to Excluded roles");
                Assert(role.excludedFromRecommendations, "restoring defaults keeps the player's unrelated exclusion");
                Assert(AssignmentSignature() == assignmentsBeforeRestore, "restoring exclusions leaves actual pawn assignments unchanged");
                int revision = UiVersion.Current;
                RoleCommands.RestoreSelected(new RestoreSelection { defaultExclusions = true });
                Assert(UiVersion.Current == revision, "repeating the restoration is a revision-preserving no-op");
                foreach (Role value in defaultRoles) RoleCommands.SetRoleRecommendationExcluded(value.id, false);
                RoundTripDefaultExclusions(defaultRoles);
                RoleCommands.RestoreSelected(new RestoreSelection { defaultExclusions = true });
                stage = 24; settle = 0;
            }
            else if (stage == 24)
            {
                AssertRoleOptions(true);
                Assert(!Seeding.ComputeRestoreItems().Any(i => i.defaultExclusions), "completed restore is no longer offered");
                stage = 99; Mark("SUCCESS");
                Log.Message("[ExclusionProbe] SUCCESS");
                Patches.UnpatchAll(Patches.Id);
            }
        }
    }
}
