using System.Collections.Generic;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// A named color for role definitions: RoleDef.colorRef points at one by
    /// defName. Shipped entries use Tailwind names ("slate-700") — the same
    /// vocabulary as the editor's swatches and the export format — and other
    /// mods can add their own or re-hex ours by patch.
    public class PaletteDef : Def
    {
        /// "#rrggbb"; parsed into color at load.
        public string hex;
        public Color color = Color.white;

        public override void PostLoad()
        {
            base.PostLoad();
            if (!hex.NullOrEmpty() && ColorRgb.TryParseHex(hex, out var parsed))
                color = new Color(parsed.R, parsed.G, parsed.B);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors())
                yield return error;
            if (!hex.NullOrEmpty() && !ColorRgb.TryParseHex(hex, out _))
                yield return $"unparseable hex '{hex}' (want \"#rrggbb\")";
        }
    }
}
