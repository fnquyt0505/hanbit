using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content;
using Hanbit.Infrastructure.Content.Validation;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hanbit.Infrastructure.Tests.Content;

/// <summary>Kiểm tra dữ liệu thật của khóa beginner (T1.8), đọc qua <see cref="JsonContentRepository"/>.</summary>
public sealed class BeginnerCourseDataTests : IDisposable
{
    private const string CourseSlug = "beginner";

    private static readonly string ContentRoot = Path.Combine(RepoRoot.Find(), "content");

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    private JsonContentRepository CreateRepository() =>
        new(_cache, Options.Create(new ContentOptions { RootPath = ContentRoot }));

    [Fact]
    public async Task Content_PassesValidator()
    {
        var result = await new ContentValidationService().ValidateAsync(ContentRoot);

        result.Issues.ShouldBeEmpty(string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public async Task BeginnerCourse_HasFirstFiveLessonsInOrder()
    {
        var course = await CreateRepository().GetCourseAsync(CourseSlug, CancellationToken.None);

        course.ShouldNotBeNull();
        course.Level.ShouldBe(VocabGrade.Beginner);
        course.Lessons.Select(l => l.Order).ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task BeginnerLessons_UseOnlyReviewedVocab_AndExistingGrammar()
    {
        var repository = CreateRepository();
        var course = await repository.GetCourseAsync(CourseSlug, CancellationToken.None);
        course.ShouldNotBeNull();

        foreach (var summary in course.Lessons)
        {
            var lesson = await repository.GetLessonAsync(CourseSlug, summary.Slug, CancellationToken.None);
            lesson.ShouldNotBeNull(summary.Slug);

            var vocab = await repository.GetVocabularyAsync(lesson.VocabIds, CancellationToken.None);
            vocab.Count.ShouldBe(lesson.VocabIds.Count, summary.Slug);
            vocab.ShouldAllBe(v => v.Reviewed, summary.Slug);

            var grammar = await repository.GetGrammarAsync(lesson.GrammarIds, CancellationToken.None);
            grammar.Count.ShouldBe(lesson.GrammarIds.Count, summary.Slug);
            lesson.GrammarIds.ShouldNotBeEmpty(summary.Slug);
        }
    }

    [Theory]
    [InlineData("kd-44506", "tôi (khiêm nhường); em, con, cháu")]
    [InlineData("kd-29702", "lui tới; ghé qua; đi làm, đi học")]
    [InlineData("kd-25949", "cảnh sát")]
    public async Task ReviewedMeaningOverrides_AreMergedIntoVocabulary(string id, string expectedVi)
    {
        var vocab = await CreateRepository().GetVocabularyAsync([id], CancellationToken.None);

        var item = vocab.ShouldHaveSingleItem();
        item.Reviewed.ShouldBeTrue();
        item.Vi.ShouldBe(expectedVi);
    }

    [Fact]
    public async Task BeginnerLessons_EachHasAllFiveExerciseTypes()
    {
        var repository = CreateRepository();
        var course = await repository.GetCourseAsync(CourseSlug, CancellationToken.None);
        course.ShouldNotBeNull();

        foreach (var summary in course.Lessons)
        {
            var lesson = await repository.GetLessonAsync(CourseSlug, summary.Slug, CancellationToken.None);
            lesson.ShouldNotBeNull(summary.Slug);
            lesson.Exercises.Select(e => e.GetType()).Distinct().Count().ShouldBe(5, summary.Slug);
        }
    }
}
