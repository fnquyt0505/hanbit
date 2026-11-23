// Hanbit.ContentImporter — nhập từ vựng 한국어기초사전 → content/vocab/krdict-<grade>.json
//
//   dotnet run --project tools/Hanbit.ContentImporter -- import --grade beginner --input data-raw --out content/vocab/krdict-beginner.json [--content-root content]
//       --input: file JSON "사전 전체 내려받기" (LMF) hoặc thư mục chứa các file đó (data-raw/, không commit).
//   dotnet run --project tools/Hanbit.ContentImporter -- probe search "q=...&translated=y&trans_lang=7" [maxChars]
//       Tra 1 truy vấn Open API (cần key: dotnet user-secrets set "Krdict:ApiKey" "..." --project tools/Hanbit.ContentImporter).
//       Không dùng API để import hàng loạt: server chặn theo tần suất (PROGRESS E2).
using System.Globalization;
using System.Text;

using Hanbit.ContentImporter.Krdict;
using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content.Validation;

using Microsoft.Extensions.Configuration;

Console.OutputEncoding = Encoding.UTF8;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return args switch
{
    ["import", .. var rest] => await ImportAsync(ParseOptions(rest)),
    ["probe", var endpoint, var query, .. var rest] => await ProbeAsync(endpoint, query, rest),
    _ => Usage(),
};

async Task<int> ImportAsync(Dictionary<string, string> options)
{
    if (!options.TryGetValue("grade", out var gradeText) || !options.TryGetValue("input", out var input) || !options.TryGetValue("out", out var output))
    {
        return Usage();
    }

    var grade = gradeText switch
    {
        "beginner" => VocabGrade.Beginner,
        "intermediate" => VocabGrade.Intermediate,
        "advanced" => VocabGrade.Advanced,
        _ => throw new ArgumentException($"--grade không hợp lệ: {gradeText} (beginner|intermediate|advanced)"),
    };

    var files = KrdictImporter.ResolveInputFiles(input);
    Console.WriteLine($"Nhập từ vựng {gradeText} từ 한국어기초사전: {files.Count} file trong '{input}'.");
    var (items, report) = await KrdictImporter.ImportAsync(grade, files, cancellation.Token);
    await KrdictImporter.WriteAsync(output, items, cancellation.Token);

    Console.WriteLine($"✔ Đã ghi {output}");
    Console.WriteLine($"  File đọc: {report.FilesRead} · mục quét: {report.EntriesScanned} · đúng cấp {gradeText}: {report.MatchingGrade}");
    Console.WriteLine("  Loại trừ (không phải 단어): " + (report.ExcludedNonWordUnits.Count == 0 ? "0" : string.Join(", ", report.ExcludedNonWordUnits.Select(u => $"{u.Key} {u.Value}"))));
    Console.WriteLine($"  Đã nhập: {report.ImportedCount} từ · bỏ qua (không có nghĩa tiếng Việt): {report.SkippedWithoutVietnamese.Count}");
    Console.WriteLine("  Theo từ loại: " + string.Join(", ", report.CountByPos.Select(p => $"{p.Key} {p.Value}")));
    foreach (var skipped in report.SkippedWithoutVietnamese.Take(20))
    {
        Console.WriteLine($"    – bỏ qua: {skipped}");
    }

    var contentRoot = options.GetValueOrDefault("content-root", "content");
    if (Directory.Exists(contentRoot))
    {
        var validation = await new ContentValidationService().ValidateAsync(contentRoot, cancellation.Token);
        Console.WriteLine(validation.IsValid
            ? $"✔ Validator: '{contentRoot}' hợp lệ."
            : $"✖ Validator: {validation.Issues.Count} lỗi — chạy tools/Hanbit.ContentValidator để xem chi tiết.");
        return validation.IsValid ? 0 : 1;
    }

    return 0;
}

async Task<int> ProbeAsync(string endpoint, string query, string[] rest)
{
    var apiKey = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build()["Krdict:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("Chưa có Krdict:ApiKey. Chạy: dotnet user-secrets set \"Krdict:ApiKey\" \"<key>\" --project tools/Hanbit.ContentImporter");
        return 2;
    }

    var maxChars = rest.Length > 0 ? int.Parse(rest[0], CultureInfo.InvariantCulture) : 4000;
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    using var response = await http.GetAsync(new Uri($"https://krdict.korean.go.kr/api/{endpoint}?key={Uri.EscapeDataString(apiKey)}&{query}"), cancellation.Token);
    var body = (await response.Content.ReadAsStringAsync(cancellation.Token)).Replace(apiKey, "***", StringComparison.Ordinal);
    Console.WriteLine($"HTTP {(int)response.StatusCode} {response.Content.Headers.ContentType}");
    Console.WriteLine(body.Length > maxChars ? body[..maxChars] + $"\n…(cắt, tổng {body.Length} ký tự)" : body);
    return 0;
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length - 1; i += 2)
    {
        options[args[i].TrimStart('-')] = args[i + 1];
    }

    return options;
}

static int Usage()
{
    Console.Error.WriteLine("Dùng: import --grade beginner --input data-raw --out content/vocab/krdict-beginner.json [--content-root content]");
    Console.Error.WriteLine("      probe search|view \"q=...\" [maxChars]");
    return 2;
}
