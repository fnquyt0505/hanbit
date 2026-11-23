using System.Text.RegularExpressions;

using FluentValidation;

namespace Hanbit.Infrastructure.Content.Validation;

/// <summary>Quy tắc dùng chung cho validator nội dung, thông báo tiếng Việt.</summary>
internal static partial class RuleExtensions
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    public static IRuleBuilderOptions<T, string> Required<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("bắt buộc, không được để trống.");

    /// <summary>Trường tùy chọn: được bỏ trống (null) nhưng nếu có thì không được là chuỗi rỗng.</summary>
    public static IRuleBuilderOptions<T, string?> NotBlankWhenPresent<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithMessage("nếu có thì không được là chuỗi rỗng (bỏ trường đi nếu không dùng).");

    public static IRuleBuilderOptions<T, string> Slug<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => SlugRegex().IsMatch(value))
            .WithMessage("chỉ gồm chữ thường a–z, số và dấu '-' (VD: 01-chao-hoi).");

    public static IRuleBuilderOptions<T, string> ContainsHangul<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(HasHangul).WithMessage("phải chứa chữ Hangul.");

    public static IRuleBuilderOptions<T, IReadOnlyList<TItem>> Distinct<T, TItem>(this IRuleBuilder<T, IReadOnlyList<TItem>> rule) =>
        rule.Must(items => items.Distinct().Count() == items.Count).WithMessage("có phần tử bị trùng lặp.");

    public static IRuleBuilderOptions<T, IReadOnlyList<TItem>> MinCount<T, TItem>(this IRuleBuilder<T, IReadOnlyList<TItem>> rule, int min) =>
        rule.Must(items => items.Count >= min).WithMessage($"cần ít nhất {min} phần tử.");

    public static bool HasHangul(string? value) =>
        value is not null && value.Any(c => c is (>= '가' and <= '힣') or (>= 'ㄱ' and <= 'ㅣ'));
}
