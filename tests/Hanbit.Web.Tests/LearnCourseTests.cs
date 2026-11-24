using System.Net;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T4.1: /learn/{course} + View Component CourseMap.</summary>
public partial class LearnCourseTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("data-lesson=\"([a-z0-9-]+)\"\\s+data-state=\"([a-z]+)\"")]
    private static partial Regex LessonStateRegex();

    private async Task<string> GetCourseHtmlAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/learn/beginner");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task CoursePage_ListsLessonsInOrder_FirstOpenOthersLocked()
    {
        var html = await GetCourseHtmlAsync();

        var lessons = LessonStateRegex().Matches(html).Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
        lessons.ShouldBe(
        [
            ("chao-hoi", "open"),
            ("gioi-thieu", "locked"),
            ("nghe-nghiep", "locked"),
            ("gia-dinh", "locked"),
            ("thu-trong-tuan", "locked"),
        ]);
    }

    [Fact]
    public async Task CoursePage_EveryLessonIsALink_WithStats()
    {
        var html = await GetCourseHtmlAsync();

        html.ShouldContain("href=\"/learn/beginner/chao-hoi\"");
        html.ShouldContain("href=\"/learn/beginner/thu-trong-tuan\"");
        html.ShouldContain("14 từ · 1 ngữ pháp · 7 bài tập");
        html.ShouldContain("/js/course-map.js");
        html.ShouldContain("x-data=\"courseMap(");
    }

    [Fact]
    public async Task CoursePage_MarksLessonsNavItemCurrent()
    {
        var html = await GetCourseHtmlAsync();

        html.Split("href=\"/learn/beginner\" aria-current=\"page\"").Length.ShouldBe(3);
    }

    [Theory]
    [InlineData("/learn/khong-co")]
    [InlineData("/learn/..%2F..%2Fetc")]
    public async Task UnknownCourse_Returns404(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
