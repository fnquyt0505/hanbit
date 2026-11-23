using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Hanbit.Web.TagHelpers;

/// <summary>
/// Chữ tiếng Hàn: <c>&lt;ko-text&gt;한빛&lt;/ko-text&gt;</c> → <c>&lt;span lang="ko" class="font-ko …"&gt;</c>.
/// Trong câu: lớn hơn chữ Việt 1 bậc (DESIGN.md §3). <c>display="true"</c>: Hangul hiển thị lớn.
/// <c>romanization="…"</c>: thêm phiên âm Latin nhỏ, nghiêng, màu phụ.
/// </summary>
[HtmlTargetElement("ko-text")]
public sealed class KoTextTagHelper(HtmlEncoder htmlEncoder) : TagHelper
{
    private const string InlineClasses = "font-ko text-[1.125em]";
    private const string DisplayClasses = "font-hangul text-hangul-display";
    private const string RomanizationClasses = "ms-1 text-sm italic text-ink-muted";

    public bool Display { get; set; }

    public string? Romanization { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("lang", "ko");

        foreach (var cssClass in (Display ? DisplayClasses : InlineClasses).Split(' '))
        {
            output.AddClass(cssClass, htmlEncoder);
        }

        if (!string.IsNullOrWhiteSpace(Romanization))
        {
            var romanization = new TagBuilder("span");
            romanization.AddCssClass(RomanizationClasses);
            romanization.Attributes["lang"] = "ko-Latn";
            romanization.InnerHtml.Append(Romanization);
            output.PostElement.AppendHtml(romanization);
        }
    }
}
