using System.Collections.Generic;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// A shipped holder scale. Seeded once per save as an ordinary editable
    /// scale named from its invariant defName; players may rename, edit or delete it and the
    /// change sticks (only the code-seeded Never scale is immutable).
    public class ScaleDef : Def
    {
        /// Comma rows, one value per colony-size band (short rows extend
        /// flat). Absent max = uncapped everywhere.
        public string min;
        public string train;
        public string max;

        public HolderScale ToScale()
        {
            var scale = new HolderScale
            {
                Name = SeededDefIdentity.ScaleName(this),
                Min = HolderScaleCodec.DecodeRow(min, 0),
                Train = HolderScaleCodec.DecodeRow(train, 0),
            };
            if (!max.NullOrEmpty())
                scale.Max = HolderScaleCodec.DecodeRow(max, RoleHolderRange.Uncapped);
            scale.Normalize();
            return scale;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors())
                yield return error;
            if (label.NullOrEmpty())
                yield return "missing label (the scale name)";
            if (min.NullOrEmpty())
                yield return "missing min row";
        }
    }
}
