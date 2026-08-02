using System.Collections.Generic;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Opens the custom-swatch color picker and lands the pick through
    /// SwatchPickPlanner: slot define/clear plus every role recolor the plan
    /// demands, so a color the pick touches never leaves the palette. The
    /// callback runs outside OnGUI, never on the render path.
    internal static class SwatchPicking
    {
        /// An empty slot defines the slot and applies the color to the edited
        /// role (applyToEditedRole: true); right-click on a filled slot
        /// redefines the shared swatch and every role painted with it follows
        /// (mirrors palette import). A pick indistinguishable from another
        /// palette color reuses that color and empties the slot, so the
        /// palette never holds duplicates.
        internal static void Open(int roleId, Color initial, int slot,
            bool applyToEditedRole)
        {
            Find.WindowStack.Add(new Dialog_RoleColorPicker(initial, picked =>
            {
                WorkRolesGameComponent.RunOutsideOnGUI(() =>
                {
                    var store = RoleStore.Current;
                    if (store == null) return;
                    var custom = new List<Rgba>(store.customSwatches.Count);
                    for (int i = 0; i < store.customSwatches.Count; i++)
                        custom.Add(PaletteColors.ToRgba(store.customSwatches[i]));
                    var coloredRoles = new List<(int id, Rgba color)>();
                    for (int i = 0; i < store.roles.Count; i++)
                    {
                        Role role = store.roles[i];
                        if (role != null && role.hasCustomColor)
                            coloredRoles.Add((role.id, PaletteColors.ToRgba(role.color)));
                    }

                    SwatchPickPlan plan = SwatchPickPlanner.Plan(
                        PaletteColors.ToRgba(picked), slot,
                        PaletteColors.StandardRgba(), custom,
                        applyToEditedRole, roleId, coloredRoles);
                    Color applied = PaletteColors.ToColor(plan.Applied);
                    if (plan.ClearSlot) RoleCommands.ClearCustomSwatch(slot);
                    if (plan.SetSlot) RoleCommands.SetCustomSwatch(slot, applied);
                    for (int i = 0; i < plan.RecolorRoleIds.Count; i++)
                        RoleCommands.SetRoleColor(plan.RecolorRoleIds[i], applied);
                    if (plan.RecolorEditedRole)
                        RoleCommands.SetRoleColor(roleId, applied);
                });
            }));
        }
    }
}
