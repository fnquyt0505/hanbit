using Microsoft.AspNetCore.Html;

namespace Hanbit.Web.Models.Shared;

/// <summary>Model cho partial <c>_Card</c>. <paramref name="Body"/> là Razor template: <c>@&lt;p&gt;…&lt;/p&gt;</c>.</summary>
public sealed record CardViewModel(string? Title, Func<object?, IHtmlContent> Body);
