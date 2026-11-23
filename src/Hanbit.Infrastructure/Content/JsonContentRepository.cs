using System.Text.Json;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Content;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Hanbit.Infrastructure.Content;

/// <summary>
/// Đọc nội dung từ file JSON trong content/ (bằng <see cref="ContentJson.Options"/>) và cache bằng <see cref="IMemoryCache"/>.
/// Từ vựng = tất cả vocab/*.json gộp với vocab-reviews.json (<see cref="VocabReviewMerger"/>).
/// Slug nhận từ URL được kiểm tra trước khi ghép đường dẫn (chặn path traversal).
/// </summary>
public sealed class JsonContentRepository(IMemoryCache cache, IOptions<ContentOptions> options) : IContentRepository
{
    private const string CacheKeyPrefix = "hanbit:content:";

    private readonly string _root = Path.GetFullPath(options.Value.RootPath);
    private readonly TimeSpan? _cacheDuration = options.Value.CacheDuration;

    public Task<IReadOnlyList<HangulLetter>> GetHangulAsync(CancellationToken cancellationToken) =>
        GetCachedAsync<IReadOnlyList<HangulLetter>>("hangul", async ct =>
            await ReadRequiredAsync<List<HangulLetter>>(ContentPaths.Hangul, ct), cancellationToken);

    public Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken) =>
        GetCachedAsync<IReadOnlyList<Course>>("courses", async ct =>
        {
            var coursesDir = Path.Combine(_root, ContentPaths.CoursesDirectory);
            if (!Directory.Exists(coursesDir))
            {
                return [];
            }

            var courses = new List<Course>();
            foreach (var dir in Directory.EnumerateDirectories(coursesDir).Order(StringComparer.Ordinal))
            {
                var course = await GetCourseAsync(Path.GetFileName(dir), ct);
                if (course is not null)
                {
                    courses.Add(course);
                }
            }

            return courses;
        }, cancellationToken);

    public Task<Course?> GetCourseAsync(string courseSlug, CancellationToken cancellationToken)
    {
        if (!ContentPaths.IsValidSlug(courseSlug))
        {
            return Task.FromResult<Course?>(null);
        }

        return GetCachedAsync($"course:{courseSlug}", ct =>
            ReadOptionalAsync<Course>(Path.Combine(ContentPaths.CoursesDirectory, courseSlug, ContentPaths.CourseFile), ct),
            cancellationToken);
    }

    public Task<Lesson?> GetLessonAsync(string courseSlug, string lessonSlug, CancellationToken cancellationToken)
    {
        if (!ContentPaths.IsValidSlug(courseSlug) || !ContentPaths.IsValidSlug(lessonSlug))
        {
            return Task.FromResult<Lesson?>(null);
        }

        return GetCachedAsync($"lesson:{courseSlug}/{lessonSlug}", ct =>
            ReadOptionalAsync<Lesson>(
                Path.Combine(ContentPaths.CoursesDirectory, courseSlug, ContentPaths.LessonsDirectory, $"{lessonSlug}.json"), ct),
            cancellationToken);
    }

    public async Task<IReadOnlyList<VocabItem>> GetVocabularyAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var vocabulary = await GetCachedAsync<IReadOnlyDictionary<string, VocabItem>>("vocab", async ct =>
        {
            var items = new List<VocabItem>();
            foreach (var file in EnumerateJson(ContentPaths.VocabDirectory))
            {
                items.AddRange(await ReadRequiredAsync<List<VocabItem>>(file, ct));
            }

            var reviews = await ReadOptionalAsync<List<VocabReview>>(ContentPaths.VocabReviews, ct) ?? [];
            return VocabReviewMerger.Apply(items, reviews).ToDictionary(v => v.Id, StringComparer.Ordinal);
        }, cancellationToken);

        return Pick(ids, vocabulary);
    }

    public async Task<IReadOnlyList<GrammarPoint>> GetGrammarAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var grammar = await GetCachedAsync<IReadOnlyDictionary<string, GrammarPoint>>("grammar", async ct =>
        {
            var points = new List<GrammarPoint>();
            foreach (var file in EnumerateJson(ContentPaths.GrammarDirectory))
            {
                points.AddRange(await ReadRequiredAsync<List<GrammarPoint>>(file, ct));
            }

            return points.ToDictionary(g => g.Id, StringComparer.Ordinal);
        }, cancellationToken);

        return Pick(ids, grammar);
    }

    private static List<T> Pick<T>(IEnumerable<string> ids, IReadOnlyDictionary<string, T> source) =>
        ids.Select(id => source.TryGetValue(id, out var value) ? value : default)
            .OfType<T>()
            .ToList();

    private async Task<T> GetCachedAsync<T>(string key, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeyPrefix + key;
        if (cache.TryGetValue(cacheKey, out var cachedValue) && cachedValue is T cached)
        {
            return cached;
        }

        var value = await load(cancellationToken);
        using (var entry = cache.CreateEntry(cacheKey))
        {
            entry.Value = value;
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
        }

        return value;
    }

    private IEnumerable<string> EnumerateJson(string relativeDirectory)
    {
        var dir = Path.Combine(_root, relativeDirectory);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.json").Order(StringComparer.Ordinal).Select(f => Path.GetRelativePath(_root, f))
            : [];
    }

    private async Task<T> ReadRequiredAsync<T>(string relativePath, CancellationToken cancellationToken)
        where T : class =>
        await ReadOptionalAsync<T>(relativePath, cancellationToken)
        ?? throw new FileNotFoundException($"Thiếu file nội dung bắt buộc: {relativePath}", Path.Combine(_root, relativePath));

    private async Task<T?> ReadOptionalAsync<T>(string relativePath, CancellationToken cancellationToken)
        where T : class
    {
        var path = Path.Combine(_root, relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, ContentJson.Options, cancellationToken)
            ?? throw new JsonException($"File nội dung rỗng (null): {relativePath}");
    }
}
