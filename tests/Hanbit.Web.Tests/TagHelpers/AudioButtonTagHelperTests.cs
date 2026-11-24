using System.Text.Encodings.Web;
using System.Text.Unicode;

using Hanbit.Web.TagHelpers;

using Shouldly;

namespace Hanbit.Web.Tests.TagHelpers;

public class AudioButtonTagHelperTests
{
    private static AudioButtonTagHelper Create(string text = "안녕하세요") =>
        new(HtmlEncoder.Create(UnicodeRanges.All)) { Text = text };

    [Fact]
    public void Process_Default_RendersAlpineComponentWithOneButton()
    {
        var html = TagHelperRenderer.Render(Create(), "audio-button", string.Empty);

        html.ShouldStartWith("<span");
        html.ShouldContain("x-data=\"audioButton(");
        html.ShouldContain("data-audio-button");
        html.Split("<button").Length.ShouldBe(2);
        html.ShouldContain("type=\"button\"");
        html.ShouldContain("x-on:click=\"play(false)\"");
        html.ShouldContain("aria-label=\"Nghe: 안녕하세요\"");
        html.ShouldContain("size-11");
    }

    [Fact]
    public void Process_Slow_AddsSlowButton()
    {
        var tagHelper = Create();
        tagHelper.Slow = true;

        var html = TagHelperRenderer.Render(tagHelper, "audio-button", string.Empty);

        html.Split("<button").Length.ShouldBe(3);
        html.ShouldContain("x-on:click=\"play(true)\"");
        html.ShouldContain("aria-label=\"Nghe chậm: 안녕하세요\"");
        html.ShouldContain("0.7×");
    }

    [Fact]
    public void Process_PassesTextAndAudioAsJsonArgument()
    {
        var tagHelper = Create("감사합니다");
        tagHelper.Audio = "/audio/gamsa.mp3";

        var html = TagHelperRenderer.Render(tagHelper, "audio-button", string.Empty);

        // JSON trong thuộc tính được mã hóa HTML: " → &quot;
        html.ShouldContain("&quot;audio&quot;:&quot;/audio/gamsa.mp3&quot;");
        html.ShouldContain("&quot;text&quot;:");
    }

    [Fact]
    public void Process_TextWithMarkup_IsEncodedInLabelAndArgs()
    {
        var html = TagHelperRenderer.Render(Create("<b>\"x\"</b>"), "audio-button", string.Empty);

        html.ShouldNotContain("<b>");
    }

    [Fact]
    public void Process_SmallSize_UsesSmallClasses()
    {
        var tagHelper = Create();
        tagHelper.Size = "sm";

        var html = TagHelperRenderer.Render(tagHelper, "audio-button", string.Empty);

        html.ShouldContain("size-9");
    }

    [Fact]
    public void Process_EmptyText_Throws()
    {
        Should.Throw<InvalidOperationException>(() => TagHelperRenderer.Render(Create(" "), "audio-button", string.Empty));
    }

    [Fact]
    public void Process_InvalidSize_Throws()
    {
        var tagHelper = Create();
        tagHelper.Size = "xl";

        Should.Throw<InvalidOperationException>(() => TagHelperRenderer.Render(tagHelper, "audio-button", string.Empty))
            .Message.ShouldContain("xl");
    }
}
