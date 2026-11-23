using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hanbit.Infrastructure.Tests.Content;

public sealed class JsonContentRepositoryTests : IDisposable
{
    private readonly TempContent _content = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose()
    {
        _cache.Dispose();
        _content.Dispose();
    }

    private JsonContentRepository CreateRepository(TimeSpan? cacheDuration = null) =>
        new(_cache, Options.Create(new ContentOptions { RootPath = _content.Root, CacheDuration = cacheDuration }));

    [Fact]
    public async Task GetHangulAsync_ReadsLetters()
    {
        var letters = await CreateRepository().GetHangulAsync(CancellationToken.None);

        letters.ShouldHaveSingleItem().Char.ShouldBe("ㄱ");
    }

    [Fact]
    public async Task GetHangulAsync_MissingFile_Throws()
    {
        _content.Delete("hangul.json");

        await Should.ThrowAsync<FileNotFoundException>(() => CreateRepository().GetHangulAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCourseAsync_ExistingSlug_ReturnsCourse()
    {
        var course = await CreateRepository().GetCourseAsync("beginner", CancellationToken.None);

        course.ShouldNotBeNull().Lessons.ShouldHaveSingleItem().Slug.ShouldBe("01-nuoc");
    }

    [Fact]
    public async Task GetCourseAsync_UnknownSlug_ReturnsNull()
    {
        (await CreateRepository().GetCourseAsync("advanced", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task GetCoursesAsync_ReturnsAllCourses()
    {
        _content.Write("courses/intermediate/course.json", TempContent.Course.Replace("\"beginner\"", "\"intermediate\"", StringComparison.Ordinal));

        var courses = await CreateRepository().GetCoursesAsync(CancellationToken.None);

        courses.Select(c => c.Slug).ShouldBe(["beginner", "intermediate"]);
    }

    [Fact]
    public async Task GetLessonAsync_ExistingSlugs_ReturnsLessonWithExercises()
    {
        var lesson = await CreateRepository().GetLessonAsync("beginner", "01-nuoc", CancellationToken.None);

        lesson.ShouldNotBeNull().Exercises.ShouldHaveSingleItem().ShouldBeOfType<MultipleChoiceExercise>();
    }

    [Fact]
    public async Task GetLessonAsync_UnknownLesson_ReturnsNull()
    {
        (await CreateRepository().GetLessonAsync("beginner", "99-khong-co", CancellationToken.None)).ShouldBeNull();
    }

    [Theory]
    [InlineData("..", "hangul")]
    [InlineData("beginner", "../course")]
    [InlineData("beginner/lessons", "01-nuoc")]
    [InlineData("Beginner", "01-nuoc")]
    [InlineData("", "01-nuoc")]
    public async Task GetLessonAsync_InvalidSlug_ReturnsNullWithoutReadingOutsideContent(string course, string lesson)
    {
        (await CreateRepository().GetLessonAsync(course, lesson, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task GetCourseAsync_PathTraversalSlug_ReturnsNull()
    {
        (await CreateRepository().GetCourseAsync("../..", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task GetVocabularyAsync_ReturnsRequestedIdsInOrderSkippingUnknown()
    {
        var items = await CreateRepository().GetVocabularyAsync(["kd-2", "kd-999", "kd-1"], CancellationToken.None);

        items.Select(v => v.Id).ShouldBe(["kd-2", "kd-1"]);
    }

    [Fact]
    public async Task GetVocabularyAsync_ReadsAllVocabFiles()
    {
        _content.Write("vocab/krdict-extra.json", TempContent.Vocab
            .Replace("kd-1", "kd-3", StringComparison.Ordinal)
            .Replace("kd-2", "kd-4", StringComparison.Ordinal));

        var items = await CreateRepository().GetVocabularyAsync(["kd-1", "kd-4"], CancellationToken.None);

        items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetVocabularyAsync_WithReviews_MergesReviewedAndVietnamese()
    {
        _content.Write("vocab-reviews.json", """
            [ { "id": "kd-2", "reviewed": true, "vi": "lửa, ngọn lửa", "note": "đã sửa nghĩa" } ]
            """);

        var item = (await CreateRepository().GetVocabularyAsync(["kd-2"], CancellationToken.None)).ShouldHaveSingleItem();

        item.Reviewed.ShouldBeTrue();
        item.Vi.ShouldBe("lửa, ngọn lửa");
    }

    [Fact]
    public async Task GetGrammarAsync_ReturnsRequestedIds()
    {
        var grammar = await CreateRepository().GetGrammarAsync(["g-yo", "g-khong-co"], CancellationToken.None);

        grammar.ShouldHaveSingleItem().Pattern.ShouldBe("-요");
    }

    [Fact]
    public async Task GetHangulAsync_SecondCall_ServedFromCache()
    {
        var repository = CreateRepository();
        var first = await repository.GetHangulAsync(CancellationToken.None);
        _content.Delete("hangul.json");

        var second = await repository.GetHangulAsync(CancellationToken.None);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task GetCourseAsync_CacheExpired_ReloadsFromDisk()
    {
        var repository = CreateRepository(cacheDuration: TimeSpan.FromMilliseconds(50));
        (await repository.GetCourseAsync("beginner", CancellationToken.None)).ShouldNotBeNull().Title.ShouldBe("Sơ cấp");
        _content.Write("courses/beginner/course.json", TempContent.Course.Replace("Sơ cấp", "Sơ cấp 1", StringComparison.Ordinal));

        await Task.Delay(200);

        (await repository.GetCourseAsync("beginner", CancellationToken.None)).ShouldNotBeNull().Title.ShouldBe("Sơ cấp 1");
    }
}
