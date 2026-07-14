using System;
using System.Linq;
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
        private readonly bool showCancel;         // group mode: explicit Cancel beside OK
        private string name;
        private bool focusedField;

        public override Vector2 InitialSize => new Vector2(360f, sourceLabel == null ? 160f : 186f);

        /// Role-rename constructor: prefilled with the current name.
        public Dialog_RenameRole(Role role)
        {
            onConfirm = n => RoleCommands.RenameRole(role.id, n);
            title = "WR_RenameRoleTitle".Translate();
            name = role.label;
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

        private bool NameTaken
        {
            get
            {
                var store = RoleStore.Current;
                return store != null && store.roles.Any(r =>
                    string.Equals(r.label, name.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        private bool NameValid => !name.Trim().NullOrEmpty() && !(requireUniqueName && NameTaken);

        public override void OnAcceptKeyPressed()
        {
            if (!NameValid) return; // keep the dialog open on Enter with an unusable name
            Apply();
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
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(0f, y, inRect.width, 22f), "WR_CopySource".Translate(sourceLabel));
                GUI.color = Color.white;
                y += 26f;
            }
            GUI.SetNextControlName("WR_RenameField");
            name = Widgets.TextField(new Rect(0f, y, inRect.width, 30f), name);
            y += 32f;
            if (!focusedField)
            {
                Verse.UI.FocusControl("WR_RenameField", this);
                focusedField = true;
            }

            if (requireUniqueName && !name.Trim().NullOrEmpty() && NameTaken)
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
            if (Widgets.ButtonText(okRect, "WR_OK".Translate(), active: NameValid) && NameValid)
            {
                Apply();
                Close();
            }
        }

        private void Apply()
        {
            if (NameValid)
                onConfirm(name.Trim());
        }
    }
}
