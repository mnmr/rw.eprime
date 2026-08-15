using System;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public class Dialog_RenameRole : Window
    {
        private sealed class RenameChromeSnapshot
        {
            internal RenameChromeSnapshot(
                string title,
                string copySource,
                string nameTaken,
                string cancel,
                string ok)
            {
                Title = title;
                CopySource = copySource;
                NameTaken = nameTaken;
                Cancel = cancel;
                Ok = ok;
            }

            internal string Title { get; }
            internal string CopySource { get; }
            internal string NameTaken { get; }
            internal string Cancel { get; }
            internal string Ok { get; }

            internal bool ContentEquals(RenameChromeSnapshot other) =>
                other != null
                && Title == other.Title
                && CopySource == other.CopySource
                && NameTaken == other.NameTaken
                && Cancel == other.Cancel
                && Ok == other.Ok;
        }

        private readonly Action<string> onConfirm;
        private readonly string fixedTitle;
        private readonly string titleKey;
        private readonly string sourceLabel;      // copy mode: the original role's name
        private readonly bool requireUniqueName;  // copy mode: OK only for new names
        private readonly int exceptRoleId = -1;
        private readonly int exceptGroupId = -1;
        private readonly bool showCancel;         // group mode: explicit Cancel beside OK
        private string name;
        private string validatedName;
        private string trimmedName = "";
        private int validationRevision = int.MinValue;
        private bool nameTaken;
        private bool nameValid;
        private bool focusedField;

        // Owner: this rename/create dialog instance.
        // Key: dialog identity and LanguageChangeCoordinator.Revision.
        // Value: immutable detached title, source caption, validation status,
        // and button-label chrome.
        // Dependencies: fixedTitle or titleKey, sourceLabel, showCancel, and
        // LanguageChangeCoordinator.Revision.
        // Refresh: immediately on the first draw after a language revision.
        // Equality: an equal localized rebuild preserves snapshot identity.
        // Teardown: closing the dialog releases the instance-owned snapshot.
        private RenameChromeSnapshot chromeSnapshot;
        private int chromeLanguageRevision = -1;

        public override Vector2 InitialSize => new Vector2(360f, sourceLabel == null ? 160f : 186f);

        /// Role-rename constructor: prefilled with the current name.
        public Dialog_RenameRole(Role role)
            : this(role.id, role.label, renameRole: true)
        {
        }

        internal static Dialog_RenameRole ForRole(int roleId, string roleLabel)
            => new Dialog_RenameRole(roleId, roleLabel, renameRole: true);

        private Dialog_RenameRole(int roleId, string roleLabel, bool renameRole)
        {
            onConfirm = n => RoleCommands.RenameRole(roleId, n);
            exceptRoleId = roleId;
            requireUniqueName = true;
            titleKey = "WR_RenameRoleTitle";
            name = roleLabel;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        /// Group-rename constructor: prefilled with the current name and validates
        /// against groups while excluding the group being renamed.
        public Dialog_RenameRole(RoleGroup group)
            : this(group.id, group.label)
        {
        }

        internal Dialog_RenameRole(int groupId, string groupLabel)
        {
            onConfirm = n => RoleCommands.RenameGroup(groupId, n);
            exceptGroupId = groupId;
            requireUniqueName = true;
            showCancel = true;
            titleKey = "WR_RenameGroupTitle";
            name = groupLabel;
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
            fixedTitle = title;
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
            fixedTitle = title;
            showCancel = true;
            name = initialName ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        private void EnsureChrome()
        {
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (chromeSnapshot != null
                && chromeLanguageRevision == languageRevision)
                return;

            string resolvedTitle = titleKey == null
                ? fixedTitle
                : titleKey.Translate().ToString();
            var rebuilt = new RenameChromeSnapshot(
                resolvedTitle,
                sourceLabel == null
                    ? null
                    : "WR_CopySource".Translate(sourceLabel).ToString(),
                "WR_NameTaken".Translate().ToString(),
                showCancel ? "WR_Cancel".Translate().ToString() : null,
                "WR_OK".Translate().ToString());
            if (chromeSnapshot == null || !chromeSnapshot.ContentEquals(rebuilt))
                chromeSnapshot = rebuilt;
            chromeLanguageRevision = languageRevision;
        }

        private bool IsNameTaken(string candidate)
        {
            var store = RoleStore.Current;
            if (store == null) return false;
            if (exceptGroupId >= 0)
                return !WorkRoles.Core.GroupNameRules.IsAvailable(
                    candidate, store.groups, group => group.label,
                    store.GroupById(exceptGroupId));
            return !WorkRoles.Core.CatalogNameRules.IsAvailable(
                candidate, store.roles, role => role.label,
                exceptRoleId < 0 ? null : store.RoleById(exceptRoleId));
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
            using var guiState = new GuiStateScope(capture: true);
            EnsureChrome();
            RenameChromeSnapshot chrome = chromeSnapshot;
            GameFont oldFont = Text.Font;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                float y = 0f;
                if (!chrome.Title.NullOrEmpty())
                {
                    Text.Font = GameFont.Medium;
                    Widgets.Label(new Rect(0f, y, inRect.width, 30f), chrome.Title);
                    Text.Font = GameFont.Small;
                    y += 34f;
                }
                if (chrome.CopySource != null)
                {
                    GUI.color = WrStyle.DimText;
                    Widgets.Label(new Rect(0f, y, inRect.width, 22f),
                        chrome.CopySource);
                    GUI.color = oldColor;
                    y += 26f;
                }
                GUI.SetNextControlName("WR_RenameField");
                name = Widgets.TextField(new Rect(0f, y, inRect.width, 30f), name);
                // Chips, list rows and dialogs all size to the name; cap it well
                // above the longest seeded label.
                const int MaxNameLength = 30;
                if (name.Length > MaxNameLength)
                    name = name.Substring(0, MaxNameLength);
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
                    Widgets.Label(new Rect(0f, y, inRect.width, 20f),
                        chrome.NameTaken);
                    Text.Font = GameFont.Small;
                    GUI.color = oldColor;
                }

                var okRect = showCancel
                    ? new Rect(inRect.width - 120f, inRect.height - 35f, 120f, 30f)
                    : new Rect(inRect.width / 2f - 60f, inRect.height - 35f,
                        120f, 30f);
                if (showCancel
                    && Widgets.ButtonText(new Rect(0f, inRect.height - 35f,
                        120f, 30f), chrome.Cancel))
                    Close();
                if (Widgets.ButtonText(okRect, chrome.Ok, active: nameValid)
                    && TryApply())
                    Close();
            }
            finally
            {
                Text.Font = oldFont;
                GUI.color = oldColor;
            }
        }
    }
}
