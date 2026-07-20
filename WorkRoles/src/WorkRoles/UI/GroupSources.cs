using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// One grouping the colonist table offers. Partition == null means the
    /// flat list; classify-backed sources section A-Z, membership-backed
    /// sources (Colony Groups) keep their own group order.
    internal sealed class GroupSourceDef
    {
        internal string Key;
        internal string Label;
        internal System.Func<List<Pawn>, List<GroupSection<Pawn>>> Partition;
    }

    internal static class GroupSources
    {
        private static List<GroupSourceDef> all;

        internal static List<GroupSourceDef> All() => all ??= Build();

        internal static void InvalidateLanguageCaches()
        {
            all = null;
        }

        private static GroupSourceDef Classified(string key, string label,
            System.Func<Pawn, (string key, string title)> classify) =>
            new GroupSourceDef
            {
                Key = key,
                Label = label,
                Partition = pawns => GroupEngine.Partition(pawns, classify),
            };

        private static List<GroupSourceDef> Build()
        {
            var list = new List<GroupSourceDef>
            {
                new GroupSourceDef { Key = "none", Label = "WR_GroupNone".Translate() },
                Classified("faction", "WR_GroupFaction".Translate(), pawn =>
                {
                    var faction = pawn.HomeFaction ?? pawn.Faction;
                    string name = faction?.Name ?? pawn.kindDef.race.LabelCap.ToString();
                    return ("faction|" + name, name);
                }),
                Classified("race", "WR_GroupRace".Translate(), pawn =>
                {
                    string name = pawn.kindDef.race.LabelCap.ToString();
                    return ("race|" + name, name);
                }),
                Classified("gender", "WR_GroupGender".Translate(), pawn =>
                {
                    string name = pawn.gender.GetLabel().CapitalizeFirst();
                    return ("gender|" + pawn.gender, name);
                }),
            };
            if (ModsConfig.BiotechActive)
                list.Add(Classified("xenotype", "WR_GroupXenotype".Translate(), pawn =>
                {
                    string name = pawn.genes?.XenotypeLabelCap.ToString();
                    if (name.NullOrEmpty()) name = "?";
                    return ("xenotype|" + name, name);
                }));
            if (ModsConfig.IdeologyActive)
            {
                list.Add(Classified("ideo", "WR_GroupIdeo".Translate(), pawn =>
                {
                    string name = pawn.Ideo?.name ?? "?";
                    return ("ideo|" + name, name);
                }));
                list.Add(Classified("slaves", "WR_GroupSlaves".Translate(), pawn => pawn.IsSlave
                    ? ("slaves|1", "WR_GroupTitleSlaves".Translate().ToString())
                    : ("slaves|0", "WR_GroupTitleColonists".Translate().ToString())));
            }
            if (ColonyGroupsDataSource.Available)
                list.Add(new GroupSourceDef
                {
                    Key = "colonygroups",
                    Label = "WR_GroupColonyGroups".Translate(),
                    Partition = pawns => GroupEngine.PartitionByMembership(
                        pawns, ColonyGroupsDataSource.Groups(), "WR_GroupTitleUngrouped".Translate()),
                });
            return list;
        }
    }
}
