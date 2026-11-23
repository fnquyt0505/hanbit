using System.Text.Json;
using System.Text.RegularExpressions;

using FluentValidation;
using FluentValidation.Results;

using Hanbit.Domain.Content;

namespace Hanbit.Infrastructure.Content.Validation;

/// <summary>
/// Kiểm tra toàn bộ thư mục content/ (cấu trúc theo ARCHITECTURE §3):
/// 1) mỗi file đọc được bằng <see cref="ContentJson.Options"/>; 2) validator FluentValidation từng đối tượng;
/// 3) kiểm tra chéo: id không trùng, bài học chỉ dùng từ vựng đã reviewed + ngữ pháp tồn tại,
/// mục lục khóa học khớp file bài học, không có file JSON lạ.
/// </summary>
public sealed partial class ContentValidationService
{
    private const string HangulFile = ContentPaths.Hangul;
    private const string VocabReviewsFile = ContentPaths.VocabReviews;

    private static readonly HangulLetterValidator HangulLetterValidator = new();
    private static readonly VocabItemValidator VocabItemValidator = new();
    private static readonly VocabReviewValidator VocabReviewValidator = new();
    private static readonly GrammarPointValidator GrammarPointValidator = new();
    private static readonly CourseValidator CourseValidator = new();
    private static readonly LessonValidator LessonValidator = new();

    [GeneratedRegex("[A-Za-z][A-Za-z0-9]*")]
    private static partial Regex IdentifierRegex();

    public async Task<ContentValidationResult> ValidateAsync(string contentRoot, CancellationToken cancellationToken = default)
    {
        var context = new ValidationContext(Path.GetFullPath(contentRoot));
        if (!Directory.Exists(context.Root))
        {
            context.Add(contentRoot, null, "không tìm thấy thư mục nội dung.");
            return context.ToResult(fileCount: 0);
        }

        var files = Directory.EnumerateFiles(context.Root, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(context.Root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (!files.Contains(HangulFile))
        {
            context.Add(context.Display(HangulFile), null, "thiếu file bảng chữ cái Hangul.");
        }

        foreach (var file in files)
        {
            await ValidateFileAsync(context, file, cancellationToken);
        }

        CheckCrossReferences(context);
        return context.ToResult(files.Count);
    }

    private static async Task ValidateFileAsync(ValidationContext context, string file, CancellationToken cancellationToken)
    {
        var segments = file.Split('/');
        switch (segments)
        {
            case [HangulFile]:
                var letters = await LoadAsync<List<HangulLetter>>(context, file, cancellationToken);
                if (letters is not null)
                {
                    ValidateItems(context, file, letters, HangulLetterValidator, l => l.Id);
                    ReportDuplicates(context, file, letters.Select(l => l.Id), "id");
                    ReportDuplicates(context, file, letters.Select(l => l.Char), "char");
                }

                break;

            case [VocabReviewsFile]:
                var reviews = await LoadAsync<List<VocabReview>>(context, file, cancellationToken);
                if (reviews is not null)
                {
                    ValidateItems(context, file, reviews, VocabReviewValidator, r => r.Id);
                    ReportDuplicates(context, file, reviews.Select(r => r.Id), "id");
                    context.Reviews.AddRange(reviews);
                    context.ReviewsFile = file;
                }
                else
                {
                    context.VocabIncomplete = true;
                }

                break;

            case ["vocab", _]:
                var vocab = await LoadAsync<List<VocabItem>>(context, file, cancellationToken);
                if (vocab is not null)
                {
                    ValidateItems(context, file, vocab, VocabItemValidator, v => v.Id);
                    context.Vocab.AddRange(vocab.Select(v => (file, v)));
                }
                else
                {
                    context.VocabIncomplete = true;
                }

                break;

            case ["grammar", _]:
                var grammar = await LoadAsync<List<GrammarPoint>>(context, file, cancellationToken);
                if (grammar is not null)
                {
                    ValidateItems(context, file, grammar, GrammarPointValidator, g => g.Id);
                    context.Grammar.AddRange(grammar.Select(g => (file, g)));
                }
                else
                {
                    context.GrammarIncomplete = true;
                }

                break;

            case ["courses", var courseDir, "course.json"]:
                var course = await LoadAsync<Course>(context, file, cancellationToken);
                if (course is not null)
                {
                    AddIssues(context, file, pathPrefix: null, CourseValidator.Validate(course));
                    context.Courses[courseDir] = (file, course);
                }

                break;

            case ["courses", var courseDir, "lessons", var lessonFile]:
                var lesson = await LoadAsync<Lesson>(context, file, cancellationToken);
                if (lesson is not null)
                {
                    AddIssues(context, file, pathPrefix: null, LessonValidator.Validate(lesson));
                    context.Lessons.Add((courseDir, Path.GetFileNameWithoutExtension(lessonFile), file, lesson));
                }

                break;

            default:
                context.Add(context.Display(file), null,
                    "file không thuộc cấu trúc content/ (hangul.json, vocab/, grammar/, courses/{khóa}/course.json, courses/{khóa}/lessons/) — xem ARCHITECTURE §3.");
                break;
        }
    }

    private static async Task<T?> LoadAsync<T>(ValidationContext context, string file, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            await using var stream = File.OpenRead(Path.Combine(context.Root, file));
            var value = await JsonSerializer.DeserializeAsync<T>(stream, ContentJson.Options, cancellationToken);
            if (value is null)
            {
                context.Add(context.Display(file), null, "nội dung là null.");
            }

            return value;
        }
        catch (JsonException ex)
        {
            var line = ex.LineNumber is { } n ? $" (dòng {n + 1})" : string.Empty;
            context.Add(context.Display(file), ToJsonPath(ex.Path), $"JSON không hợp lệ{line}: {ex.Message}");
            return null;
        }
    }

    private static void ValidateItems<T>(ValidationContext context, string file, IReadOnlyList<T> items, IValidator<T> validator, Func<T, string> id)
    {
        for (var i = 0; i < items.Count; i++)
        {
            AddIssues(context, file, $"[{i}]", validator.Validate(items[i]), $"(id {id(items[i])}) ");
        }
    }

    private static void AddIssues(ValidationContext context, string file, string? pathPrefix, ValidationResult result, string messagePrefix = "")
    {
        foreach (var error in result.Errors)
        {
            var path = ToJsonPath(error.PropertyName);
            var fullPath = pathPrefix is null ? path : string.IsNullOrEmpty(path) ? pathPrefix : $"{pathPrefix}.{path}";
            context.Add(context.Display(file), fullPath, messagePrefix + error.ErrorMessage);
        }
    }

    private static void ReportDuplicates(ValidationContext context, string file, IEnumerable<string> values, string field)
    {
        foreach (var duplicate in values.GroupBy(v => v, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            context.Add(context.Display(file), null, $"{field} '{duplicate.Key}' bị trùng {duplicate.Count()} lần.");
        }
    }

    private static void CheckCrossReferences(ValidationContext context)
    {
        // Id từ vựng / ngữ pháp không trùng giữa các file
        foreach (var duplicate in context.Vocab.GroupBy(v => v.Item.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var where = string.Join(", ", duplicate.Select(d => context.Display(d.File)).Distinct());
            context.Add(context.Display(duplicate.First().File), null, $"id từ vựng '{duplicate.Key}' bị trùng ({where}).");
        }

        foreach (var duplicate in context.Grammar.GroupBy(g => g.Item.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var where = string.Join(", ", duplicate.Select(d => context.Display(d.File)).Distinct());
            context.Add(context.Display(duplicate.First().File), null, $"id ngữ pháp '{duplicate.Key}' bị trùng ({where}).");
        }

        // Gộp vocab-reviews.json giống JsonContentRepository → "đã duyệt" tính trên dữ liệu sau gộp.
        var uniqueVocab = context.Vocab.GroupBy(v => v.Item.Id, StringComparer.Ordinal).Select(g => g.First().Item);
        var vocabById = VocabReviewMerger.Apply(uniqueVocab, context.Reviews).ToDictionary(v => v.Id, StringComparer.Ordinal);
        var grammarIds = context.Grammar.Select(g => g.Item.Id).ToHashSet(StringComparer.Ordinal);

        if (context.ReviewsFile is not null && !context.VocabIncomplete)
        {
            for (var i = 0; i < context.Reviews.Count; i++)
            {
                if (!vocabById.ContainsKey(context.Reviews[i].Id))
                {
                    context.Add(context.Display(context.ReviewsFile), $"[{i}].id",
                        $"không tìm thấy từ vựng '{context.Reviews[i].Id}' trong content/vocab/.");
                }
            }
        }

        foreach (var (courseDir, fileSlug, file, lesson) in context.Lessons)
        {
            var display = context.Display(file);
            if (lesson.Slug != fileSlug)
            {
                context.Add(display, "slug", $"'{lesson.Slug}' phải trùng tên file '{fileSlug}'.");
            }

            for (var i = 0; i < lesson.VocabIds.Count; i++)
            {
                var vocabId = lesson.VocabIds[i];
                if (!vocabById.TryGetValue(vocabId, out var item))
                {
                    // File từ vựng nào đó không đọc được → đã báo lỗi ở file đó, tránh báo dây chuyền "không tìm thấy".
                    if (context.VocabIncomplete)
                    {
                        continue;
                    }

                    context.Add(display, $"vocab_ids[{i}]", $"không tìm thấy từ vựng '{vocabId}' trong content/vocab/.");
                }
                else if (!item.Reviewed)
                {
                    context.Add(display, $"vocab_ids[{i}]", $"từ vựng '{vocabId}' ({item.Ko}) chưa được duyệt (reviewed = false) — bài học chỉ dùng từ đã duyệt.");
                }
            }

            for (var i = 0; i < lesson.GrammarIds.Count; i++)
            {
                if (!context.GrammarIncomplete && !grammarIds.Contains(lesson.GrammarIds[i]))
                {
                    context.Add(display, $"grammar_ids[{i}]", $"không tìm thấy ngữ pháp '{lesson.GrammarIds[i]}' trong content/grammar/.");
                }
            }

            if (!context.Courses.ContainsKey(courseDir))
            {
                context.Add(display, null, $"thư mục courses/{courseDir}/ thiếu course.json.");
            }
        }

        foreach (var (courseDir, (file, course)) in context.Courses)
        {
            var display = context.Display(file);
            if (course.Slug != courseDir)
            {
                context.Add(display, "slug", $"'{course.Slug}' phải trùng tên thư mục '{courseDir}'.");
            }

            var lessons = context.Lessons.Where(l => l.CourseDir == courseDir).ToDictionary(l => l.FileSlug, l => l.Lesson, StringComparer.Ordinal);
            for (var i = 0; i < course.Lessons.Count; i++)
            {
                var summary = course.Lessons[i];
                if (!lessons.TryGetValue(summary.Slug, out var lesson))
                {
                    context.Add(display, $"lessons[{i}]", $"không có file courses/{courseDir}/lessons/{summary.Slug}.json.");
                    continue;
                }

                if (lesson.Title != summary.Title)
                {
                    context.Add(display, $"lessons[{i}].title", $"'{summary.Title}' khác title trong file bài học ('{lesson.Title}').");
                }

                if (lesson.Order != summary.Order)
                {
                    context.Add(display, $"lessons[{i}].order", $"{summary.Order} khác order trong file bài học ({lesson.Order}).");
                }
            }

            var listed = course.Lessons.Select(l => l.Slug).ToHashSet(StringComparer.Ordinal);
            foreach (var orphan in lessons.Keys.Where(slug => !listed.Contains(slug)))
            {
                context.Add(display, "lessons", $"bài học '{orphan}' có file nhưng chưa có trong mục lục khóa học.");
            }
        }
    }

    /// <summary><c>Exercises[2].AnswerIndex</c> / <c>$.exercises[2].answer_index</c> → <c>exercises[2].answer_index</c>.</summary>
    private static string ToJsonPath(string? propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
        {
            return string.Empty;
        }

        var path = propertyPath.StartsWith("$.", StringComparison.Ordinal) ? propertyPath[2..] : propertyPath.TrimStart('$');
        return IdentifierRegex().Replace(path, m => JsonNamingPolicy.SnakeCaseLower.ConvertName(m.Value));
    }

    private sealed class ValidationContext(string root)
    {
        private readonly List<ContentIssue> _issues = [];
        private readonly string _rootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        public string Root { get; } = root;

        public List<(string File, VocabItem Item)> Vocab { get; } = [];

        public List<(string File, GrammarPoint Item)> Grammar { get; } = [];

        public List<VocabReview> Reviews { get; } = [];

        public string? ReviewsFile { get; set; }

        /// <summary>Có file từ vựng không đọc được → danh sách id chưa đầy đủ.</summary>
        public bool VocabIncomplete { get; set; }

        /// <summary>Có file ngữ pháp không đọc được → danh sách id chưa đầy đủ.</summary>
        public bool GrammarIncomplete { get; set; }

        public Dictionary<string, (string File, Course Course)> Courses { get; } = new(StringComparer.Ordinal);

        public List<(string CourseDir, string FileSlug, string File, Lesson Lesson)> Lessons { get; } = [];

        public string Display(string relativeFile) => $"{_rootName}/{relativeFile}";

        public void Add(string file, string? path, string message) =>
            _issues.Add(new ContentIssue(file, string.IsNullOrEmpty(path) ? null : path, message));

        public ContentValidationResult ToResult(int fileCount) => new(fileCount, _issues);
    }
}
