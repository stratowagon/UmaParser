using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class UmaOptionTextTests
{
    [Fact]
    public void Skills_selector_text_is_the_uma_name()
    {
        var option = new SkillsUmaOption { TrainedCharaId = 1, Name = "Air Groove" };

        Assert.Equal("Air Groove", option.ToString());
    }

    [Fact]
    public void Tracks_selector_text_is_the_uma_name()
    {
        var option = new TracksUmaOption { TrainedCharaId = 2, Name = "El Condor Pasa" };

        Assert.Equal("El Condor Pasa", option.ToString());
    }
}
