using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class CountFormatTests
{
    [Test]
    public async Task Zero_ExactInteger()
    {
        await Assert.That(CountFormat.Compact(0)).IsEqualTo("0");
    }

    [Test]
    public async Task TenThousand_ExactInteger()
    {
        // 10000 is at-or-below the threshold → exact
        await Assert.That(CountFormat.Compact(10000)).IsEqualTo("10000");
    }

    [Test]
    public async Task TenThousandOne_TenK()
    {
        // 10001 > 10000 → 10001/1000 = 10.001 → rounds to 10.0 → trimmed → "10k"
        await Assert.That(CountFormat.Compact(10001)).IsEqualTo("10k");
    }

    [Test]
    public async Task TwelveSevenEightSix_TwelvePointEightK()
    {
        // 12786/1000 = 12.786 → one decimal AwayFromZero = 12.8 → "12.8k"
        await Assert.That(CountFormat.Compact(12786)).IsEqualTo("12.8k");
    }

    [Test]
    public async Task FifteenThousand_FifteenK()
    {
        // 15000/1000 = 15.0 → trimmed → "15k"
        await Assert.That(CountFormat.Compact(15000)).IsEqualTo("15k");
    }

    [Test]
    public async Task NinetyNineThousand_KeepsOneDecimal()
    {
        // 99940/1000 = 99.94 → below the 100 cutoff → one decimal → "99.9k"
        await Assert.That(CountFormat.Compact(99940)).IsEqualTo("99.9k");
    }

    [Test]
    public async Task HundredThousandPlus_IntegerK()
    {
        // >= 100k drops the decimal so counters stay three-digits-plus-suffix
        await Assert.That(CountFormat.Compact(114000)).IsEqualTo("114k");
        await Assert.That(CountFormat.Compact(114499)).IsEqualTo("114k");
        await Assert.That(CountFormat.Compact(114500)).IsEqualTo("115k");
        // The "1000k" sliver is promoted to the M tier instead.
        await Assert.That(CountFormat.Compact(999449)).IsEqualTo("999k");
        await Assert.That(CountFormat.Compact(999500)).IsEqualTo("1M");
        await Assert.That(CountFormat.Compact(999949)).IsEqualTo("1M");
    }

    [Test]
    public async Task OneMillion_OneM()
    {
        // 1000000/1000000 = 1.0 → trimmed → "1M"
        await Assert.That(CountFormat.Compact(1_000_000)).IsEqualTo("1M");
    }

    [Test]
    public async Task OneTwoThreeFourFiveSixSeven_OnePointTwoM()
    {
        // 1234567/1000000 = 1.234567 → one decimal AwayFromZero = 1.2 → "1.2M"
        await Assert.That(CountFormat.Compact(1_234_567)).IsEqualTo("1.2M");
    }
}
