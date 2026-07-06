using System;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public class Dialog_RenameRole : Window
    {
        private readonly Action<string> onConfirm;
        private readonly string title;
        private string name;

        public override Vector2 InitialSize => new Vector2(360f, 160f);

        /// Role-rename constructor (original).
        public Dialog_RenameRole(Role role)
        {
            onConfirm = n => RoleCommands.RenameRole(role.id, n);
            title = "Rename Role";
            name = role.label;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        /// Generic string-prompt constructor (e.g. for Copy-with-name).
        public Dialog_RenameRole(string title, string initial, Action<string> onConfirm)
        {
            this.onConfirm = onConfirm;
            this.title = title;
            name = initial;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
        }

        public override void OnAcceptKeyPressed()
        {
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
            name = Widgets.TextField(new Rect(0f, y, inRect.width, 30f), name);
            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 60f, inRect.height - 35f, 120f, 30f), "OK"))
            {
                Apply();
                Close();
            }
        }

        private void Apply()
        {
            if (!name.NullOrEmpty())
                onConfirm(name);
        }
    }
}
