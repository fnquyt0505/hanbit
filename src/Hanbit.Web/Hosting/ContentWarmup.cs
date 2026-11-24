using System.Diagnostics;

using Hanbit.Application.Abstractions;

namespace Hanbit.Web.Hosting;

/// <summary>
/// T7.6: nạp sẵn nội dung vào cache ngay khi khởi động (Hangul, khóa học, bài, từ vựng, ngữ pháp) — request đầu tiên sau khi
/// Render đánh thức instance không phải đọc + phân tích tệp từ vựng 1,3 MB. Chạy nền, không chặn việc mở cổng.
/// Lỗi chỉ ghi log — /health (ContentHealthCheck) báo tình trạng nội dung.
/// </summary>
public sealed class ContentWarmup(IContentRepository contentRepository, ILogger<ContentWarmup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await contentRepository.GetHangulAsync(stoppingToken);
            var lessons = 0;
            foreach (var course in await contentRepository.GetCoursesAsync(stoppingToken))
            {
                foreach (var summary in course.Lessons)
                {
                    var lesson = await contentRepository.GetLessonAsync(course.Slug, summary.Slug, stoppingToken);
                    if (lesson is null)
                    {
                        continue;
                    }

                    await contentRepository.GetVocabularyAsync(lesson.VocabIds, stoppingToken);
                    await contentRepository.GetGrammarAsync(lesson.GrammarIds, stoppingToken);
                    lessons++;
                }
            }

            logger.LogInformation("Đã nạp sẵn nội dung ({Lessons} bài) trong {Elapsed} ms.", lessons, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // đang tắt
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nạp sẵn nội dung thất bại sau {Elapsed} ms — nội dung sẽ được đọc khi có request.", stopwatch.ElapsedMilliseconds);
        }
    }
}
