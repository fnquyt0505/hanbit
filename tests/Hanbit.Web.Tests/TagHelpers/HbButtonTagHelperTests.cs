using System.Text.Encodings.Web;

using Hanbit.Web.TagHelpers;

using Shouldly;

namespace Hanbit.Web.Tests.TagHelpers;

public class HbButtonTagHelperTests
{
    private static HbButtonTagHelper Create() => new(HtmlEncoder.Default);

    [Fact]
    public void Process_Default_RendersPrimaryMdButton()
    {
        var html = TagHelperRenderer.Render(Create(), "hb-button", "Bắt đầu");

        html.ShouldStartWith("<button");
        html.ShouldContain("type=\"button\"");
        html.ShouldContain("bg-primary");
        html.ShouldContain("text-bg");
        html.ShouldContain("h-11");
        html.ShouldContain(">Bắt đầu</button>");
    }

    [Theory]
    [InlineData("primary", "bg-primary")]
    [InlineData("secondary", "bg-secondary")]
    [InlineData("outline", "border-border")]
    [InlineData("ghost", "bg-transparent")]
    public void Process_Variant_AddsVariantClasses(string variant, string expectedClass)
    {
        var tagHelper = Create();
        tagHelper.Variant = variant;

        var html = TagHelperRenderer.Render(tagHelper, "hb-button", "Nút");

        html.ShouldContain(expectedClass);
    }

    [Fact]
    public void Process_SizeLg_MeetsMinimumTouchHeight()
    {
        var tagHelper = Create();
        tagHelper.Size = "lg";

        var html = TagHelperRenderer.Render(tagHelper, "hb-button", "Kiểm tra");

        html.ShouldContain("h-12"); // 48px — DESIGN.md §5
    }

    [Fact]
    public void Process_WithHref_RendersAnchorWithoutType()
    {
        var tagHelper = Create();
        tagHelper.Href = "/learn/beginner";

        var html = TagHelperRenderer.Render(tagHelper, "hb-button", "Học ngay");

        html.ShouldStartWith("<a");
        html.ShouldContain("href=\"/learn/beginner\"");
        html.ShouldNotContain("type=");
    }

    [Fact]
    public void Process_DisabledAnchor_RemovesHrefAndMarksAriaDisabled()
    {
        var tagHelper = Create();
        tagHelper.Href = "/learn/beginner";
        tagHelper.Disabled = true;

        var html = TagHelperRenderer.Render(tagHelper, "hb-button", "Học ngay");

        html.ShouldNotContain("href=");
        html.ShouldContain("aria-disabled=\"true\"");
    }

    [Fact]
    public void Process_DisabledButton_AddsDisabledAttribute()
    {
        var tagHelper = Create();
        tagHelper.Disabled = true;

        var html = TagHelperRenderer.Render(tagHelper, "hb-button", "Đang tải");

        html.ShouldContain("disabled=\"disabled\"");
    }

    [Fact]
    public void Process_Block_IsFullWidthOnMobileOnly()
    {
        var tagHelper = Create();
        tagHelper.Block = true;

        var html = TagHelperRenderer.Render(tagHelper, "hb-button", "Kiểm tra");

        html.ShouldContain("w-full");
        html.ShouldContain("sm:w-auto");
    }

    [Fact]
    public void Process_ExistingClassAndAttributes_ArePreserved()
    {
        var html = TagHelperRenderer.Render(Create(), "hb-button", "Nút",
            ("class", "mt-4"), ("x-on:click", "open = true"));

        html.ShouldContain("mt-4");
        html.ShouldContain("bg-primary");
        html.ShouldContain("x-on:click=\"open = true\"");
    }

    [Theory]
    [InlineData("danger", "md")]
    [InlineData("primary", "xl")]
    public void Process_UnknownVariantOrSize_Throws(string variant, string size)
    {
        var tagHelper = Create();
        tagHelper.Variant = variant;
        tagHelper.Size = size;

        Should.Throw<InvalidOperationException>(() => TagHelperRenderer.Render(tagHelper, "hb-button", "Nút"));
    }
}
