using System.Text.Encodings.Web;
using System.Text.Unicode;

using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Hanbit.Web.Tests.TagHelpers;

/// <summary>Chạy một Tag Helper với nội dung con và trả về HTML đã render.</summary>
internal static class TagHelperRenderer
{
    public static string Render(TagHelper tagHelper, string tagName, string childContent = "", params (string Name, string Value)[] attributes)
    {
        var attributeList = new TagHelperAttributeList();
        foreach (var (name, value) in attributes)
        {
            attributeList.Add(name, value);
        }

        var context = new TagHelperContext(tagName, new TagHelperAttributeList(attributeList), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(tagName, attributeList, (_, _) =>
        {
            var content = new DefaultTagHelperContent();
            content.SetContent(childContent);
            return Task.FromResult<TagHelperContent>(content);
        });
        output.Content.SetContent(childContent);

        tagHelper.Process(context, output);

        // Giữ nguyên chữ Việt/Hàn trong chuỗi kết quả (HtmlEncoder.Default mã hóa mọi ký tự ngoài ASCII) để dễ so sánh.
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Create(UnicodeRanges.All));
        return writer.ToString();
    }
}
