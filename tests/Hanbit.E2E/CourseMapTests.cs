using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T4.1: CourseMap — bài khóa không mở được (toast), bài mở dẫn tới trang bài học.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class CourseMapTests(WebServerFixture server)
{
    [Fact]
    public async Task ClickLockedLesson_ShowsToast_StaysOnCourse()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");

        var locked = page.Locator("[data-lesson='gioi-thieu']");
        (await locked.GetAttributeAsync("aria-disabled")).ShouldBe("true");
        await locked.ClickAsync(new() { Force = true }); // Playwright chờ phần tử aria-disabled "enabled" → bấm cưỡng bức như người dùng

        await page.GetByTestId("toast").WaitForAsync();
        new Uri(page.Url).AbsolutePath.ShouldBe("/learn/beginner");
    }

    [Fact]
    public async Task OpenLesson_IsNotDisabled_AndLinksToLesson()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");

        var open = page.Locator("[data-lesson='chao-hoi']");
        (await open.GetAttributeAsync("aria-disabled")).ShouldBe("false");
        (await open.GetAttributeAsync("href")).ShouldBe("/learn/beginner/chao-hoi");
    }

    [Fact]
    public async Task LessonStates_ComputedFromCompletedSet()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");

        var states = await page.EvaluateAsync<string>("""
            async () => {
              const m = await import('/js/course-map.js');
              return JSON.stringify(m.lessonStates(['c/a', 'c/b', 'c/c', 'c/d'], new Set(['c/a', 'c/c'])));
            }
            """);

        states.ShouldBe("{\"c/a\":\"completed\",\"c/b\":\"open\",\"c/c\":\"completed\",\"c/d\":\"locked\"}");
    }

    [Fact]
    public async Task Mobile_CourseMap_HasNoHorizontalScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 700 });
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
