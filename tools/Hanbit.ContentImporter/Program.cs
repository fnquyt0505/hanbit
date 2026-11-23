// Hanbit.ContentImporter — nhập từ vựng 한국어기초사전 (krdict Open API) → content/vocab/krdict-<grade>.json
//
//   dotnet run --project tools/Hanbit.ContentImporter -- import --grade beginner --out content/vocab/krdict-beginner.json
//       [--cache data-raw/krdict-cache] [--concurrency 1] [--delay-ms 300] [--syllables 가나학] [--content-root content]
//   dotnet run --project tools/Hanbit.ContentImporter -- probe search "q=...&translated=y" [maxChars]
//
// API key: dotnet user-secrets set "Krdict:ApiKey" "..." --project tools/Hanbit.ContentImporter
// (hoặc biến môi trường Krdict__ApiKey). Key không bao giờ được in ra.
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

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var apiKey = config["Krdict:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Chưa có Krdict:ApiKey. Chạy: dotnet user-secrets set \"Krdict:ApiKey\" \"<key>\" --project tools/Hanbit.ContentImporter");
    return 2;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("Hanbit.ContentImporter/1.0");

try
{
    return args switch
    {
        ["import", .. var rest] => await ImportAsync(ParseOptions(rest)),
        ["probe", var endpoint, var query, .. var rest] => await ProbeAsync(endpoint, query, rest),
        _ => Usage(),
    };
}
catch (KrdictApiException ex) when (ex.IsDailyLimitExceeded)
{
    Console.Error.WriteLine("✖ Hết lượt gọi API trong ngày (50.000). Các trang đã tải nằm trong cache — chạy lại vào ngày mai để tiếp tục.");
    return 1;
}
catch (KrdictApiException ex)
{
    Console.Error.WriteLine($"✖ {ex.Message}");
    return 1;
}

async Task<int> ImportAsync(Dictionary<string, string> options)
{
    if (!options.TryGetValue("grade", out var gradeText) || !options.TryGetValue("out", out var output))
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
    var cacheDir = options.GetValueOrDefault("cache", Path.Combine("data-raw", "krdict-cache"));
    var concurrency = int.Parse(options.GetValueOrDefault("concurrency", "1"), CultureInfo.InvariantCulture);
    var delayMs = int.Parse(options.GetValueOrDefault("delay-ms", "300"), CultureInfo.InvariantCulture);
    var syllables = options.TryGetValue("syllables", out var only) ? only.ToList() : KrdictImporter.AllSyllables().ToList();

    Console.WriteLine($"Nhập từ vựng {gradeText} từ 한국어기초사전: {syllables.Count} âm tiết, cache '{cacheDir}', song song {concurrency}.");
    var source = new KrdictHttpSearchSource(http, apiKey, cacheDir, TimeSpan.FromMilliseconds(delayMs), log: Console.WriteLine);
    var importer = new KrdictImporter(source, Console.WriteLine);
    var (items, report) = await importer.ImportAsync(grade, syllables, concurrency, cancellation.Token);
    await KrdictImporter.WriteAsync(output, items, cancellation.Token);

    Console.WriteLine();
    Console.WriteLine($"✔ Đã ghi {output}");
    Console.WriteLine($"  Âm tiết quét: {report.SyllablesQueried} · trang API/cache: {report.PagesFetched}");
    Console.WriteLine($"  Mục từ tìm thấy: {report.EntriesFound} · nhập: {report.ImportedCount} · bỏ qua (không có nghĩa tiếng Việt): {report.SkippedWithoutVietnamese.Count}");
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
    var maxChars = rest.Length > 0 ? int.Parse(rest[0], CultureInfo.InvariantCulture) : 4000;
    using var response = await http.GetAsync(new Uri($"https://krdict.korean.go.kr/api/{endpoint}?key={Uri.EscapeDataString(apiKey)}&{query}"));
    var body = (await response.Content.ReadAsStringAsync()).Replace(apiKey, "***", StringComparison.Ordinal);
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
    Console.Error.WriteLine("Dùng: import --grade beginner --out content/vocab/krdict-beginner.json [--cache dir] [--concurrency 1] [--delay-ms 300] [--syllables 가나]");
    Console.Error.WriteLine("      probe search|view \"q=...\" [maxChars]");
    return 2;
}
