using Microsoft.AspNetCore.Html;

namespace Hanbit.Web.Models.Shared;

/// <summary>
/// Model cho partial <c>_Dialog</c> (thẻ &lt;dialog&gt; gốc: khóa focus, Esc để đóng).
/// Mở bằng <c>x-on:click="$dispatch('hb-dialog-open', 'id-cua-dialog')"</c>.
/// </summary>
public sealed record DialogViewModel(string Id, string Title, Func<object?, IHtmlContent> Body, string CloseLabel = "Đóng");
