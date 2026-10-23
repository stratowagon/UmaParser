using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class ResultsFileHeaderTests
{
    [Fact]
    public void HorseAct_timestamps_become_a_month_day_and_hour_minute()
    {
        string expected = "10/19" + Environment.NewLine + "09:49";
        Assert.Equal(expected, ResultsFileHeader.FormatCompact("TT-20261019_094946_386.json"));
        Assert.Equal(expected, ResultsFileHeader.FormatCompact("races/tt-20261019_094946.json"));
        Assert.Equal(expected, ResultsFileHeader.FormatCompact("TT-20261019_0949"));
    }

    [Fact]
    public void Other_names_wrap_on_a_separator_and_short_names_stay_one_line()
    {
        Assert.Equal("short", ResultsFileHeader.FormatCompact("short.json"));
        Assert.Equal("", ResultsFileHeader.FormatCompact(""));
        Assert.Equal(
            "not-a-times…" + Environment.NewLine + "file-name",
            ResultsFileHeader.FormatCompact("not-a-timestamp-file-name.json"));
    }
}
