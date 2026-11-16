using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class ColonistSkillGridLayoutTests
{
    [Test]
    public async Task MinimumCompactWidthIsExactlyWhatThreeColumnsOccupy()
    {
        // Twelve skills in a panel exactly as wide as the minimum keep the
        // three columns' full extent: the blocks beside the grid may use
        // only room beyond this width.
        float minimum = ColonistSkillGridLayout.MinimumCompactWidth(200f);
        ColonistSkillGridLayout grid =
            ColonistSkillGridLayout.CalculateCompact(12, 200f, minimum);

        await Assert.That(grid.ColumnCount).IsEqualTo(3);
        await Assert.That(grid.CellLeft(11) + 200f).IsLessThanOrEqualTo(minimum);
    }

    // A thirteenth skill previously grew the panel and stole a table row. The
    // displayed cell bounds are the observable contract of this pure layout.
    [Test]
    [Arguments(13)]
    [Arguments(18)]
    public async Task AddedSkillsFitBesideVanillaSkillsWithoutGrowingThePanel(int skillCount)
    {
        ColonistSkillGridLayout vanilla = ColonistSkillGridLayout.Calculate(12, 200f);
        ColonistSkillGridLayout expanded = ColonistSkillGridLayout.Calculate(skillCount, 200f);

        await Assert.That(expanded.ContentHeight).IsEqualTo(vanilla.ContentHeight);
        for (int i = 0; i < skillCount; i++)
        {
            await Assert.That(expanded.CellTop(i) + ColonistSkillGridLayout.CellHeight
                <= vanilla.ContentHeight).IsTrue();
            await Assert.That(expanded.CellLeft(i) + expanded.ColumnWidth
                < expanded.RecommendationLeft).IsTrue();
            for (int previous = 0; previous < i; previous++)
                await Assert.That(expanded.CellLeft(i) + expanded.ColumnWidth
                        <= expanded.CellLeft(previous)
                    || expanded.CellLeft(previous) + expanded.ColumnWidth
                        <= expanded.CellLeft(i)
                    || expanded.CellTop(i) + ColonistSkillGridLayout.CellHeight
                        <= expanded.CellTop(previous)
                    || expanded.CellTop(previous) + ColonistSkillGridLayout.CellHeight
                        <= expanded.CellTop(i)).IsTrue();
        }
    }

    [Test]
    public async Task VanillaSkillsRetainTheirExistingRowsAndRecommendationSpace()
    {
        ColonistSkillGridLayout layout = ColonistSkillGridLayout.Calculate(12, 200f);

        await Assert.That(layout.ContentHeight).IsEqualTo(120f);
        await Assert.That(layout.CellLeft(0)).IsEqualTo(0f);
        await Assert.That(layout.CellLeft(1)).IsEqualTo(226f);
        await Assert.That(layout.CellLeft(10)).IsEqualTo(0f);
        await Assert.That(layout.CellLeft(11)).IsEqualTo(226f);
        await Assert.That(layout.CellTop(10)).IsEqualTo(100f);
        await Assert.That(layout.CellTop(11)).IsEqualTo(100f);
        await Assert.That(layout.SeparatorLeft(0)).IsEqualTo(212f);
        await Assert.That(layout.SeparatorLeft(1)).IsEqualTo(438f);
        await Assert.That(layout.RecommendationLeft).IsEqualTo(452f);
    }

    // Recommendations off: three columns of four, each led by a separator,
    // and only as tall as the rows.
    [Test]
    public async Task CompactGridShowsVanillaSkillsInThreeColumnsOfFour()
    {
        ColonistSkillGridLayout layout = ColonistSkillGridLayout.CalculateCompact(12, 200f, 1000f);

        await Assert.That(layout.ColumnCount).IsEqualTo(3);
        await Assert.That(layout.RowCount).IsEqualTo(4);
        await Assert.That(layout.ContentHeight).IsEqualTo(80f);
        await Assert.That(layout.SeparatorLeft(0)).IsEqualTo(0f);
        await Assert.That(layout.CellLeft(0)).IsEqualTo(14f);
        await Assert.That(layout.SeparatorLeft(1)).IsEqualTo(226f);
        await Assert.That(layout.CellLeft(1)).IsEqualTo(240f);
        await Assert.That(layout.CellLeft(3)).IsEqualTo(14f);
        await Assert.That(layout.CellTop(11)).IsEqualTo(60f);
        await Assert.That(layout.CellLeft(11) + layout.ColumnWidth)
            .IsEqualTo(layout.RecommendationLeft);
    }

    // Sixteen skills: a fourth column when it fits, otherwise more rows.
    [Test]
    public async Task CompactGridAddsColumnsBeforeRows()
    {
        ColonistSkillGridLayout wide = ColonistSkillGridLayout.CalculateCompact(16, 200f, 892f);
        ColonistSkillGridLayout narrow = ColonistSkillGridLayout.CalculateCompact(16, 200f, 891f);

        await Assert.That(wide.ColumnCount).IsEqualTo(4);
        await Assert.That(wide.RowCount).IsEqualTo(4);
        await Assert.That(wide.RecommendationLeft).IsEqualTo(892f);
        await Assert.That(narrow.ColumnCount).IsEqualTo(3);
        await Assert.That(narrow.RowCount).IsEqualTo(6);
        await Assert.That(narrow.ContentHeight).IsEqualTo(120f);
    }

    [Test]
    public async Task ManySkillsGrowDownwardAfterTheThirdColumnAndKeepMeasuredWidths()
    {
        ColonistSkillGridLayout layout = ColonistSkillGridLayout.Calculate(19, 248f);

        await Assert.That(layout.ColumnCount).IsEqualTo(3);
        await Assert.That(layout.CellLeft(2)).IsEqualTo(548f);
        await Assert.That(layout.CellLeft(18)).IsEqualTo(0f);
        await Assert.That(layout.CellTop(18)).IsEqualTo(120f);
        await Assert.That(layout.ContentHeight).IsEqualTo(140f);
        await Assert.That(layout.RecommendationLeft).IsEqualTo(822f);
        await Assert.That(layout.CellLeft(2) + layout.ColumnWidth
            < layout.SeparatorLeft(2)).IsTrue();
        await Assert.That(layout.SeparatorLeft(2) + 2f
            < layout.RecommendationLeft).IsTrue();
    }
}
