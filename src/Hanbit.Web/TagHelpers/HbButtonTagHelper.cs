using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Hanbit.Web.TagHelpers;

/// <summary>
/// Nút theo DESIGN.md §5: <c>&lt;hb-button variant="primary|secondary|outline|ghost" size="sm|md|lg"&gt;</c>.
/// Có <c>href</c> → render <c>&lt;a&gt;</c>, ngược lại <c>&lt;button type="button"&gt;</c>.
/// <c>block="true"</c>: full-width ở mobile (nút chính màn học). Thuộc tính khác (x-on:click…) giữ nguyên.
/// </summary>
[HtmlTargetElement("hb-button")]
public sealed class HbButtonTagHelper(HtmlEncoder htmlEncoder) : TagHelper
{
    // Chuỗi class đầy đủ (không ghép động) để Tailwind quét được.
    private const string BaseClasses =
        "inline-flex items-center justify-center gap-2 rounded-full font-semibold select-none " +
        "transition duration-150 ease-out hover:opacity-90 " +
        "disabled:cursor-not-allowed disabled:opacity-50 aria-disabled:pointer-events-none aria-disabled:opacity-50";

    private static readonly Dictionary<string, string> VariantClasses = new(StringComparer.Ordinal)
    {
        ["primary"] = "bg-primary text-bg",
        ["secondary"] = "bg-secondary text-bg",
        ["outline"] = "border border-border bg-surface text-ink",
        ["ghost"] = "bg-transparent text-ink",
    };

    private static readonly Dictionary<string, string> SizeClasses = new(StringComparer.Ordinal)
    {
        ["sm"] = "h-9 px-4 text-sm",
        ["md"] = "h-11 px-5 text-base",
        ["lg"] = "h-12 px-6 text-lg",
    };

    private const string BlockClasses = "w-full sm:w-auto";

    public string Variant { get; set; } = "primary";

    public string Size { get; set; } = "md";

    public string? Href { get; set; }

    /// <summary>Thuộc tính <c>type</c> của &lt;button&gt; (bỏ qua khi có <c>href</c>).</summary>
    public string Type { get; set; } = "button";

    public bool Block { get; set; }

    public bool Disabled { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!VariantClasses.TryGetValue(Variant, out var variantClasses))
        {
            throw new InvalidOperationException(
                $"<hb-button> variant không hợp lệ: '{Variant}'. Dùng: {string.Join(", ", VariantClasses.Keys)}.");
        }

        if (!SizeClasses.TryGetValue(Size, out var sizeClasses))
        {
            throw new InvalidOperationException(
                $"<hb-button> size không hợp lệ: '{Size}'. Dùng: {string.Join(", ", SizeClasses.Keys)}.");
        }

        output.TagMode = TagMode.StartTagAndEndTag;

        if (Href is not null)
        {
            output.TagName = "a";
            if (Disabled)
            {
                output.Attributes.SetAttribute("aria-disabled", "true");
                output.Attributes.SetAttribute("tabindex", "-1");
            }
            else
            {
                output.Attributes.SetAttribute("href", Href);
            }
        }
        else
        {
            output.TagName = "button";
            output.Attributes.SetAttribute("type", Type);
            if (Disabled)
            {
                output.Attributes.SetAttribute("disabled", "disabled");
            }
        }

        var classes = $"{BaseClasses} {variantClasses} {sizeClasses}" + (Block ? $" {BlockClasses}" : string.Empty);
        foreach (var cssClass in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            output.AddClass(cssClass, htmlEncoder);
        }
    }
}
