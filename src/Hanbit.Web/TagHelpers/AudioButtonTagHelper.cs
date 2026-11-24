using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Hanbit.Web.TagHelpers;

/// <summary>
/// Nút nghe phát âm (DESIGN.md §5 AudioButton): <c>&lt;audio-button text="안녕하세요" /&gt;</c>.
/// <c>slow="true"</c>: thêm nút nghe chậm 0.7×. <c>audio="/audio/x.mp3"</c>: MP3 dự phòng khi thiết bị không có giọng ko-KR.
/// <c>size="sm|md"</c> (md mặc định, ≥ 44px cho cảm ứng). Logic phát ở <c>wwwroot/js/audio-button.js</c> + <c>speech.js</c>.
/// </summary>
[HtmlTargetElement("audio-button", TagStructure = TagStructure.WithoutEndTag)]
public sealed class AudioButtonTagHelper(HtmlEncoder htmlEncoder) : TagHelper
{
    // Chuỗi class đầy đủ (không ghép động) để Tailwind quét được.
    private const string ButtonBase =
        "inline-flex shrink-0 items-center justify-center rounded-full border border-border bg-surface text-ink " +
        "transition duration-150 ease-out hover:border-secondary hover:text-secondary";

    private static readonly Dictionary<string, string> SizeClasses = new(StringComparer.Ordinal)
    {
        ["sm"] = "size-9",
        ["md"] = "size-11",
    };

    private const string SpeakerIcon =
        "<svg aria-hidden=\"true\" focusable=\"false\" class=\"size-5\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" " +
        "stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">" +
        "<path d=\"M11 5 6 9H3v6h3l5 4z\" /><path d=\"M15.5 8.5a5 5 0 0 1 0 7\" /><path d=\"M18.5 5.5a9 9 0 0 1 0 13\" /></svg>";

    /// <summary>Chữ tiếng Hàn được đọc.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>URL file MP3 dự phòng (tùy chọn).</summary>
    public string? Audio { get; set; }

    public bool Slow { get; set; }

    public string Size { get; set; } = "md";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            throw new InvalidOperationException("<audio-button> cần thuộc tính text (chữ tiếng Hàn cần đọc).");
        }

        if (!SizeClasses.TryGetValue(Size, out var sizeClasses))
        {
            throw new InvalidOperationException(
                $"<audio-button> size không hợp lệ: '{Size}'. Dùng: {string.Join(", ", SizeClasses.Keys)}.");
        }

        var args = JsonSerializer.Serialize(new { text = Text, audio = Audio });

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "inline-flex items-center gap-1");
        output.Attributes.SetAttribute("x-data", $"audioButton({args})");
        output.Attributes.SetAttribute("data-audio-button", string.Empty);

        output.Content.SetHtmlContent(
            Button("normal", "play(false)", $"Nghe: {Text}", SpeakerIcon, sizeClasses) +
            (Slow ? Button("slow", "play(true)", $"Nghe chậm: {Text}", "<span aria-hidden=\"true\" class=\"text-xs font-semibold\">0.7×</span>", sizeClasses) : string.Empty));
    }

    private string Button(string mode, string onClick, string label, string innerHtml, string sizeClasses) =>
        $"<button type=\"button\" class=\"{ButtonBase} {sizeClasses}\" x-on:click=\"{onClick}\" " +
        $"aria-label=\"{htmlEncoder.Encode(label)}\" data-mode=\"{mode}\" " +
        $"x-bind:class=\"{{ 'border-secondary text-secondary animate-pulse': playing === '{mode}' }}\" " +
        $"x-bind:aria-busy=\"(playing === '{mode}').toString()\">{innerHtml}</button>";
}
