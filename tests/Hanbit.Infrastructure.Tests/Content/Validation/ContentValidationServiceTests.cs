using Hanbit.Infrastructure.Content.Validation;

using Shouldly;

namespace Hanbit.Infrastructure.Tests.Content.Validation;

public sealed class ContentValidationServiceTests : IDisposable
{
    private const string Hangul = """
        [ { "id": "c-giyeok", "char": "ㄱ", "type": "consonant", "romanization": "g/k", "name_ko": "기역",
            "vi_hint": "x", "examples": [ { "ko": "가방", "romanization": "gabang", "vi": "cái cặp" } ] } ]
        """;

    private const string Vocab = """
        [
          { "id": "kd-1", "ko": "물", "vi": "nước", "pos": "noun", "grade": "beginner", "examples": [], "reviewed": true,
            "source": { "name": "한국어기초사전", "id": "1", "url": "https://krdict.korean.go.kr/", "license": "CC BY-SA 2.0 KR" } },
          { "id": "kd-2", "ko": "불", "vi": "lửa", "pos": "noun", "grade": "beginner", "examples": [], "reviewed": false,
            "source": { "name": "한국어기초사전", "id": "2", "url": "https://krdict.korean.go.kr/", "license": "CC BY-SA 2.0 KR" } }
        ]
        """;

    private const string Grammar = """
        [ { "id": "g-yo", "pattern": "-요", "meaning_vi": "Đuôi lịch sự", "explanation_vi": "x",
            "examples": [ { "ko": "물이에요.", "vi": "Là nước." } ], "grade": "beginner" } ]
        """;

    private const string Course = """
        { "slug": "beginner", "title": "Sơ cấp", "level": "beginner", "description": "x",
          "lessons": [ { "slug": "01-nuoc", "title": "Nước", "order": 1 } ] }
        """;

    private const string Lesson = """
        { "slug": "01-nuoc", "title": "Nước", "order": 1, "vocab_ids": ["kd-1"], "grammar_ids": ["g-yo"],
          "exercises": [ { "type": "multiple_choice", "id": "e1", "prompt": "물?", "choices": ["nước", "lửa"], "answer_index": 0 } ] }
        """;

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("hanbit-content-");

    public ContentValidationServiceTests()
    {
        Write("hangul.json", Hangul);
        Write("vocab/krdict-beginner.json", Vocab);
        Write("grammar/beginner.json", Grammar);
        Write("courses/beginner/course.json", Course);
        Write("courses/beginner/lessons/01-nuoc.json", Lesson);
    }

    public void Dispose() => _root.Delete(recursive: true);

    private void Write(string relativePath, string json)
    {
        var path = Path.Combine(_root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private Task<ContentValidationResult> ValidateAsync() =>
        new ContentValidationService().ValidateAsync(_root.FullName, CancellationToken.None);

    private string DisplayPath(string relativePath) => $"{_root.Name}/{relativePath}";

    [Fact]
    public async Task ValidateAsync_ValidContent_HasNoIssues()
    {
        var result = await ValidateAsync();

        result.Issues.ShouldBeEmpty();
        result.FileCount.ShouldBe(5);
    }

    [Fact]
    public async Task ValidateAsync_RealContentFolder_IsValid()
    {
        var result = await new ContentValidationService().ValidateAsync(
            Path.Combine(RepoRoot.Find(), "content"));

        result.Issues.ShouldBeEmpty(string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public async Task ValidateAsync_InvalidJson_ReportsFileAndLine()
    {
        Write("grammar/beginner.json", "[\n  oops ]"); // lỗi cú pháp ở dòng 2

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.File.ShouldBe(DisplayPath("grammar/beginner.json"));
        issue.Message.ShouldContain("dòng 2");
    }

    [Fact]
    public async Task ValidateAsync_MissingRequiredField_ReportsFile()
    {
        Write("hangul.json", Hangul.Replace("\"vi_hint\": \"x\",", string.Empty, StringComparison.Ordinal));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.File.ShouldBe(DisplayPath("hangul.json"));
        issue.Message.ShouldContain("vi_hint");
    }

    [Fact]
    public async Task ValidateAsync_RuleViolation_ReportsSnakeCaseFieldPath()
    {
        Write("courses/beginner/lessons/01-nuoc.json", Lesson.Replace("\"answer_index\": 0", "\"answer_index\": 5", StringComparison.Ordinal));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.File.ShouldBe(DisplayPath("courses/beginner/lessons/01-nuoc.json"));
        issue.Path.ShouldBe("exercises[0].answer_index");
        issue.ToString().ShouldBe($"{DisplayPath("courses/beginner/lessons/01-nuoc.json")}: exercises[0].answer_index: {issue.Message}");
    }

    [Fact]
    public async Task ValidateAsync_ListItemViolation_ReportsIndexAndId()
    {
        Write("hangul.json", Hangul.Replace("\"romanization\": \"gabang\"", "\"romanization\": \"\"", StringComparison.Ordinal));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.Path.ShouldBe("[0].examples[0].romanization");
        issue.Message.ShouldStartWith("(id c-giyeok)");
    }

    [Fact]
    public async Task ValidateAsync_LessonUsesUnreviewedVocab_ReportsIssue()
    {
        Write("courses/beginner/lessons/01-nuoc.json", Lesson.Replace("[\"kd-1\"]", "[\"kd-1\", \"kd-2\"]", StringComparison.Ordinal));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.Path.ShouldBe("vocab_ids[1]");
        issue.Message.ShouldContain("chưa được duyệt");
    }

    [Fact]
    public async Task ValidateAsync_LessonReferencesMissingIds_ReportsEach()
    {
        Write("courses/beginner/lessons/01-nuoc.json",
            Lesson.Replace("[\"kd-1\"]", "[\"kd-999\"]", StringComparison.Ordinal)
                  .Replace("[\"g-yo\"]", "[\"g-khong-co\"]", StringComparison.Ordinal));

        var issues = (await ValidateAsync()).Issues;

        issues.Select(i => i.Path).ShouldBe(["vocab_ids[0]", "grammar_ids[0]"], ignoreOrder: true);
    }

    [Fact]
    public async Task ValidateAsync_CourseListsLessonWithoutFile_ReportsIssue()
    {
        Write("courses/beginner/course.json", Course.Replace(
            "\"order\": 1 } ]", "\"order\": 1 }, { \"slug\": \"02-lua\", \"title\": \"Lửa\", \"order\": 2 } ]", StringComparison.Ordinal));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.File.ShouldBe(DisplayPath("courses/beginner/course.json"));
        issue.Path.ShouldBe("lessons[1]");
    }

    [Fact]
    public async Task ValidateAsync_LessonFileNotInCourse_ReportsOrphan()
    {
        Write("courses/beginner/lessons/02-lua.json", Lesson
            .Replace("01-nuoc", "02-lua", StringComparison.Ordinal)
            .Replace("\"order\": 1", "\"order\": 2", StringComparison.Ordinal));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.Message.ShouldContain("02-lua");
    }

    [Fact]
    public async Task ValidateAsync_LessonTitleDiffersFromCourse_ReportsIssue()
    {
        Write("courses/beginner/lessons/01-nuoc.json", Lesson.Replace("\"title\": \"Nước\"", "\"title\": \"Nước uống\"", StringComparison.Ordinal));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.Path.ShouldBe("lessons[0].title");
    }

    [Fact]
    public async Task ValidateAsync_DuplicateVocabIdAcrossFiles_ReportsIssue()
    {
        Write("vocab/krdict-extra.json", Vocab);

        var issues = (await ValidateAsync()).Issues;

        issues.Count.ShouldBe(2); // kd-1 và kd-2
        issues.ShouldAllBe(i => i.Message.Contains("bị trùng"));
    }

    [Fact]
    public async Task ValidateAsync_UnknownFile_ReportsIssue()
    {
        Write("misc/notes.json", "{}");

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.File.ShouldBe(DisplayPath("misc/notes.json"));
    }

    [Fact]
    public async Task ValidateAsync_MissingHangulFile_ReportsIssue()
    {
        File.Delete(Path.Combine(_root.FullName, "hangul.json"));

        var issue = (await ValidateAsync()).Issues.ShouldHaveSingleItem();

        issue.File.ShouldBe(DisplayPath("hangul.json"));
    }

    [Fact]
    public async Task ValidateAsync_MissingFolder_ReportsIssue()
    {
        var result = await new ContentValidationService().ValidateAsync(
            Path.Combine(_root.FullName, "khong-ton-tai"));

        result.IsValid.ShouldBeFalse();
    }
}
