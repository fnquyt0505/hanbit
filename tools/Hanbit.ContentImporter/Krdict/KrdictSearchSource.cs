using System.Net;

namespace Hanbit.ContentImporter.Krdict;

/// <summary>Nguồn XML trang tìm kiếm (thật: HTTP + cache; test: fixture).</summary>
public interface IKrdictSearchSource
{
    /// <summary>XML trang <paramref name="page"/> các từ cấp <paramref name="level"/> bắt đầu bằng âm tiết <paramref name="syllable"/>.</summary>
    Task<string> GetStartsWithPageAsync(char syllable, string level, int page, CancellationToken cancellationToken);
}

/// <summary>
/// Gọi https://krdict.korean.go.kr/api/search; lưu XML thô vào thư mục cache (data-raw/, không commit)
/// → chạy lại không gọi lại API (tiếp tục khi bị ngắt / hết lượt trong ngày). Không bao giờ ghi key vào log/cache.
/// </summary>
public sealed class KrdictHttpSearchSource(
    HttpClient http,
    string apiKey,
    string cacheDirectory,
    TimeSpan? delayBetweenRequests = null,
    IReadOnlyList<TimeSpan>? retryDelays = null,
    Action<string>? log = null) : IKrdictSearchSource
{
    public const int PageSize = 100;

    /// <summary>
    /// Chờ trước mỗi lần thử lại (lỗi mạng/timeout/429/5xx). Server krdict từng ngừng phản hồi khi gọi dồn dập
    /// 4 kết nối song song (2026-11-24) → mặc định gọi tuần tự, nghỉ giữa các request, chờ lâu dần khi lỗi.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> DefaultRetryDelays =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120)];

    private readonly TimeSpan _delayBetweenRequests = delayBetweenRequests ?? TimeSpan.FromMilliseconds(300);
    private readonly IReadOnlyList<TimeSpan> _retryDelays = retryDelays ?? DefaultRetryDelays;

    public async Task<string> GetStartsWithPageAsync(char syllable, string level, int page, CancellationToken cancellationToken)
    {
        var cacheFile = Path.Combine(cacheDirectory, level, $"{(int)syllable:X4}-{page}.xml");
        if (File.Exists(cacheFile))
        {
            return await File.ReadAllTextAsync(cacheFile, cancellationToken);
        }

        var query = $"key={Uri.EscapeDataString(apiKey)}&q={Uri.EscapeDataString(syllable.ToString())}" +
            $"&advanced=y&method=start&level={level}&translated=y&trans_lang=7&num={PageSize}&start={page}";
        var xml = await GetWithRetryAsync(new Uri($"https://krdict.korean.go.kr/api/search?{query}"), cancellationToken);

        // Chỉ cache trang hợp lệ (trang lỗi như hết lượt sẽ được gọi lại lần chạy sau).
        if (!xml.Contains("<error>", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
            await File.WriteAllTextAsync(cacheFile, xml, cancellationToken);
        }

        return xml;
    }

    private async Task<string> GetWithRetryAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (_delayBetweenRequests > TimeSpan.Zero)
            {
                await Task.Delay(_delayBetweenRequests, cancellationToken);
            }

            var canRetry = attempt < _retryDelays.Count;
            try
            {
                using var response = await http.GetAsync(uri, cancellationToken);
                if (response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError && canRetry)
                {
                    await WaitBeforeRetryAsync(attempt, $"HTTP {(int)response.StatusCode}", cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (HttpRequestException ex) when (canRetry)
            {
                // Lỗi mạng tạm thời (đã gặp "No such host" thoáng qua, timeout kết nối) → thử lại.
                await WaitBeforeRetryAsync(attempt, ex.Message, cancellationToken);
            }
            catch (TaskCanceledException ex) when (canRetry && !cancellationToken.IsCancellationRequested)
            {
                // HttpClient.Timeout (không phải người dùng hủy) → thử lại.
                await WaitBeforeRetryAsync(attempt, $"timeout ({ex.Message})", cancellationToken);
            }
        }
    }

    private async Task WaitBeforeRetryAsync(int attempt, string reason, CancellationToken cancellationToken)
    {
        var delay = _retryDelays[attempt];
        log?.Invoke($"  ! Lỗi tạm thời ({reason}) — thử lại lần {attempt + 1}/{_retryDelays.Count} sau {delay.TotalSeconds:0} giây");
        await Task.Delay(delay, cancellationToken);
    }
}
