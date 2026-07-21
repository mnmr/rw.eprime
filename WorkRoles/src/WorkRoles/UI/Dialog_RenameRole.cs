using System;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public class Dialog_RenameRole : Window
    {
        private readonly Action<string> onConfirm;
        private readonly string title;
        private readonly string sourceLabel;      // copy mode: the original role's name
        private readonly bool requireUniqueName;  // copy mode: OK only for new names
        private readonly Role exceptRole;
        private readonly RoleGroup exceptGroup;
        private readonly bool showCancel;         // group mode: explicit Cancel beside OK
        private string name;
        private string validatedName;
        private string trimmedName = "";
        private int validationRevision = int.MinValue;
        private bool nameTaken;
        private bool nameValid;
        private bool focusedField;

        public override Vector2 InitialSize => new Vector2(360f, sourceLabel == null ? 160f : 186f);

        /// Role-rename constructor: prefilled with the current name.
        public Dialog_RenameRole(Role role)
        {
            onConfirm = n => RoleCommands.RenameRole(role.id, n);
            exceptRole = role;
            requireUniqueName = true;
            title = "WR_RenameRoleTitle".Translate();
            name = role.label;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        /// Group-rename constructor: prefilled with the current name and validates
        /// against groups while excluding the group being renamed.
        public Dialog_RenameRole(RoleGroup group)
        {
            onConfirm = n => RoleCommands.RenameGroup(group.id, n);
            exceptGroup = group;
            requireUniqueName = true;
            showCancel = true;
            title = "WR_RenameGroupTitle".Translate();
            name = group.label;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        /// New/copy constructor: the input starts empty and only a name no existing
        /// role carries can be accepted; sourceLabel (copy mode) shows the original
        /// role's name above the input, or is null (new mode).
        public Dialog_RenameRole(string title, string sourceLabel, Action<string> onConfirm)
        {
            this.onConfirm = onConfirm;
            this.title = title;
            this.sourceLabel = sourceLabel;
            requireUniqueName = true;
            name = "";
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        /// Generic name prompt (new/rename group): optionally prefilled, any
        /// non-empty name is acceptable (for a new group, an existing group's
        /// name simply joins it), explicit Cancel beside OK.
        public Dialog_RenameRole(string title, Action<string> onConfirm, string initialName = "")
        {
            this.onConfirm = onConfirm;
            this.title = title;
            showCancel = true;
            name = initialName ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        private bool IsNameTaken(string candidate)
        {
            var store = RoleStore.Current;
            if (store == null) return false;
            if (exceptGroup != null)
                return !WorkRoles.Core.GroupNameRules.IsAvailable(
                    candidate, store.groups, group => group.label, exceptGroup);
            return !WorkRoles.Core.CatalogNameRules.IsAvailable(
                candidate, store.roles, role => role.label, exceptRole);
        }

        private void EnsureValidation(bool force = false)
        {
            int revision = UiVersion.Current;
            if (!force && validationRevision == revision
                && string.Equals(validatedName, name, StringComparison.Ordinal)) return;

            validatedName = name;
            validationRevision = revision;
            trimmedName = (name ?? "").Trim();
            bool hasName = !trimmedName.NullOrEmpty();
            nameTaken = hasName && requireUniqueName && IsNameTaken(trimmedName);
            nameValid = hasName && !nameTaken;
        }

        private bool TryApply()
        {
            // Commands may have changed the catalog since the last GUI pass.
            EnsureValidation(force: true);
            if (!nameValid) return false;
            onConfirm(trimmedName);
            return true;
        }

        public override void OnAcceptKeyPressed()
        {
            if (!TryApply()) return; // keep the dialog open on Enter with an unusable name
            base.OnAcceptKeyPressed();
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            if (!title.NullOrEmpty())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, y, inRect.width, 30f), title);
                Text.Font = GameFont.Small;
                y += 34f;
            }
            if (sourceLabel != null)
            {
                GUI.color = WrStyle.DimText;
                Widgets.Label(new Rect(0f, y, inRect.width, 22f), "WR_CopySource".Translate(sourceLabel));
                GUI.color = Color.white;
                y += 26f;
            }
            GUI.SetNextControlName("WR_RenameField");
            name = Widgets.TextField(new Rect(0f, y, inRect.width, 30f), name);
            // Chips, list rows and dialogs all size to the name; cap it well
            // above the longest seeded label.
            const int MaxNameLength = 30;
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            EnsureValidation();
            y += 32f;
            if (!focusedField)
            {
                Verse.UI.FocusControl("WR_RenameField", this);
                focusedField = true;
            }

            if (nameTaken)
            {
                GUI.color = new Color(0.9f, 0.4f, 0.4f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, y, inRect.width, 20f), "WR_NameTaken".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            var okRect = showCancel
                ? new Rect(inRect.width - 120f, inRect.height - 35f, 120f, 30f)
                : new Rect(inRect.width / 2f - 60f, inRect.height - 35f, 120f, 30f);
            if (showCancel
                && Widgets.ButtonText(new Rect(0f, inRect.height - 35f, 120f, 30f), "WR_Cancel".Translate()))
                Close();
            if (Widgets.ButtonText(okRect, "WR_OK".Translate(), active: nameValid) && TryApply())
            {
                Close();
            }
        }
    }
}
