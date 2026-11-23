// Hanbit.ContentImporter — nhập từ vựng 한국어기초사전 → content/vocab/krdict-<grade>.json
//
//   dotnet run --project tools/Hanbit.ContentImporter -- import --grade beginner --input data-raw --out content/vocab/krdict-beginner.json [--content-root content]
//       --input: file JSON "사전 전체 내려받기" (LMF) hoặc thư mục chứa các file đó (data-raw/, không commit).
//   dotnet run --project tools/Hanbit.ContentImporter -- probe search "q=...&translated=y&trans_lang=7" [maxChars]
//       Tra 1 truy vấn Open API (cần key: dotnet user-secrets set "Krdict:ApiKey" "..." --project tools/Hanbit.ContentImporter).
//       Không dùng API để import hàng loạt: server chặn theo tần suất (PROGRESS E2).
//   dotnet run --project tools/Hanbit.ContentImporter -- review-export --topics "인사하기|감사하기" --out data-raw/review/dot-01.csv
//       [--input data-raw] [--vocab content/vocab/krdict-beginner.json] [--reviews content/vocab-reviews.json] [--limit 150]
//       Xuất CSV các từ chưa duyệt thuộc các chủ đề (주제 및 상황 범주, ngăn bởi "|") để người dùng duyệt (T1.7).
//   dotnet run --project tools/Hanbit.ContentImporter -- review-import --csv data-raw/review/dot-01.csv
//       [--vocab content/vocab/krdict-beginner.json] [--reviews content/vocab-reviews.json] [--content-root content]
//       Gộp quyết định trong CSV vào vocab-reviews.json (giữ bản duyệt cũ), rồi chạy validator.
using System.Globalization;
using System.Text;
using System.Text.Json;

using Hanbit.ContentImporter.Krdict;
using Hanbit.ContentImporter.Review;
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
    ["review-export", .. var rest] => await ReviewExportAsync(ParseOptions(rest)),
    ["review-import", .. var rest] => await ReviewImportAsync(ParseOptions(rest)),
    ["probe", var endpoint, var query, .. var rest] => await ProbeAsync(endpoint, query, rest),
    _ => Usage(),
};

async Task<int> ReviewExportAsync(Dictionary<string, string> options)
{
    if (!options.TryGetValue("topics", out var topicsText) || !options.TryGetValue("out", out var output))
    {
        return Usage();
    }

    var topics = topicsText.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var vocabulary = await ReadJsonAsync<List<VocabItem>>(options.GetValueOrDefault("vocab", "content/vocab/krdict-beginner.json")) ?? [];
    var reviews = await ReadJsonAsync<List<VocabReview>>(options.GetValueOrDefault("reviews", "content/vocab-reviews.json")) ?? [];
    var lmfEntries = new List<KrdictEntry>();
    foreach (var file in KrdictImporter.ResolveInputFiles(options.GetValueOrDefault("input", "data-raw")))
    {
        lmfEntries.AddRange(await KrdictLmfReader.ReadAsync(file, cancellation.Token));
    }

    int? limit = options.TryGetValue("limit", out var limitText) ? int.Parse(limitText, CultureInfo.InvariantCulture) : null;
    var rows = ReviewExporter.Select(vocabulary, reviews, lmfEntries, topics, limit);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    await File.WriteAllTextAsync(output, ReviewExporter.ToCsv(rows), Csv.Utf8WithBom, cancellation.Token);

    Console.WriteLine($"✔ Đã xuất {rows.Count} từ chưa duyệt → {output}");
    foreach (var group in rows.GroupBy(r => r.Topic))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }

    return 0;
}

async Task<int> ReviewImportAsync(Dictionary<string, string> options)
{
    if (!options.TryGetValue("csv", out var csvPath))
    {
        return Usage();
    }

    var reviewsPath = options.GetValueOrDefault("reviews", "content/vocab-reviews.json");
    var vocabulary = await ReadJsonAsync<List<VocabItem>>(options.GetValueOrDefault("vocab", "content/vocab/krdict-beginner.json")) ?? [];
    var existing = await ReadJsonAsync<List<VocabReview>>(reviewsPath) ?? [];
    var result = ReviewImporter.Merge(ReviewImporter.ReadCsvText(csvPath), vocabulary, existing);

    if (result.Errors.Count > 0)
    {
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"✖ {error}");
        }

        Console.Error.WriteLine($"Không ghi {reviewsPath} ({result.Errors.Count} lỗi). Sửa CSV rồi chạy lại.");
        return 1;
    }

    var temp = reviewsPath + ".tmp";
    await using (var stream = File.Create(temp))
    {
        await JsonSerializer.SerializeAsync(stream, result.Reviews, ContentJson.Options, cancellation.Token);
    }

    File.Move(temp, reviewsPath, overwrite: true);
    Console.WriteLine($"✔ Đã cập nhật {reviewsPath}: duyệt {result.Approved} · loại {result.Rejected} · chưa quyết {result.Undecided} · sửa nghĩa {result.MeaningChanged} · tổng bản duyệt {result.Reviews.Count}");

    var contentRoot = options.GetValueOrDefault("content-root", "content");
    var validation = await new ContentValidationService().ValidateAsync(contentRoot, cancellation.Token);
    Console.WriteLine(validation.IsValid ? $"✔ Validator: '{contentRoot}' hợp lệ." : $"✖ Validator: {validation.Issues.Count} lỗi.");
    foreach (var issue in validation.Issues.Take(20))
    {
        Console.Error.WriteLine($"  ✖ {issue}");
    }

    return validation.IsValid ? 0 : 1;
}

async Task<T?> ReadJsonAsync<T>(string path)
    where T : class
{
    if (!File.Exists(path))
    {
        return null;
    }

    await using var stream = File.OpenRead(path);
    return await JsonSerializer.DeserializeAsync<T>(stream, ContentJson.Options, cancellation.Token);
}

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
    Console.Error.WriteLine("      review-export --topics \"인사하기|감사하기\" --out data-raw/review/dot-01.csv [--limit 150]");
    Console.Error.WriteLine("      review-import --csv data-raw/review/dot-01.csv");
    Console.Error.WriteLine("      probe search|view \"q=...\" [maxChars]");
    return 2;
}
