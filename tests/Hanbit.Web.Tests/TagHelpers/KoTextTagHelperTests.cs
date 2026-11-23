using System.Text.Encodings.Web;

using Hanbit.Web.TagHelpers;

using Shouldly;

namespace Hanbit.Web.Tests.TagHelpers;

public class KoTextTagHelperTests
{
    private static KoTextTagHelper Create() => new(HtmlEncoder.Default);

    [Fact]
    public void Process_Default_RendersSpanWithKoreanLangAndFont()
    {
        var html = TagHelperRenderer.Render(Create(), "ko-text", "한빛");

        html.ShouldStartWith("<span");
        html.ShouldContain("lang=\"ko\"");
        html.ShouldContain("font-ko");
        html.ShouldContain(">한빛</span>");
    }

    [Fact]
    public void Process_Display_UsesHangulDisplayFont()
    {
        var tagHelper = Create();
        tagHelper.Display = true;

        var html = TagHelperRenderer.Render(tagHelper, "ko-text", "가");

        html.ShouldContain("font-hangul");
        html.ShouldContain("text-hangul-display");
        html.ShouldNotContain("font-ko");
    }

    [Fact]
    public void Process_WithRomanization_AppendsMutedLatinSpan()
    {
        var tagHelper = Create();
        tagHelper.Romanization = "Hanbit";

        var html = TagHelperRenderer.Render(tagHelper, "ko-text", "한빛");

        html.ShouldContain("</span><span");
        html.ShouldContain("lang=\"ko-Latn\"");
        html.ShouldContain("text-ink-muted");
        html.ShouldContain(">Hanbit</span>");
    }

    [Fact]
    public void Process_RomanizationWithMarkup_IsHtmlEncoded()
    {
        var tagHelper = Create();
        tagHelper.Romanization = "<b>x</b>";

        var html = TagHelperRenderer.Render(tagHelper, "ko-text", "한빛");

        html.ShouldNotContain("<b>");
        html.ShouldContain("&lt;b&gt;");
    }
}
