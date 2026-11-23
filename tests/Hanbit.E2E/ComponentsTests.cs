using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class ComponentsTests(WebServerFixture server)
{
    private async Task<IPage> OpenStyleguideAsync()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/styleguide");
        return page;
    }

    [Fact]
    public async Task ClickOpenDialog_Dialog_OpensAndClosesWithEscape()
    {
        var page = await OpenStyleguideAsync();
        var dialog = page.Locator("dialog#demo-dialog");
        (await dialog.IsVisibleAsync()).ShouldBeFalse();

        await page.GetByTestId("open-dialog").ClickAsync();

        await dialog.WaitForAsync();
        (await dialog.EvaluateAsync<bool>("el => el.open")).ShouldBeTrue();
        await page.Keyboard.PressAsync("Escape");
        await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }

    [Fact]
    public async Task ClickCloseButton_Dialog_Closes()
    {
        var page = await OpenStyleguideAsync();
        await page.GetByTestId("open-dialog").ClickAsync();
        var dialog = page.Locator("dialog#demo-dialog");
        await dialog.WaitForAsync();

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Đóng" }).ClickAsync();

        await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }

    [Theory]
    [InlineData("toast-success", "success", "Tuyệt! Bạn đã trả lời đúng.")]
    [InlineData("toast-danger", "danger", "Gần đúng rồi! Thử lại nhé.")]
    public async Task ClickToastButton_Toast_ShowsMessageWithIcon(string buttonTestId, string kind, string message)
    {
        var page = await OpenStyleguideAsync();

        await page.GetByTestId(buttonTestId).ClickAsync();

        var toast = page.GetByTestId("toast");
        await toast.WaitForAsync();
        (await toast.GetAttributeAsync("data-kind")).ShouldBe(kind);
        (await toast.InnerTextAsync()).ShouldContain(message);
        (await toast.Locator("svg:visible").CountAsync()).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ClickDismiss_PersistentToast_IsRemoved()
    {
        var page = await OpenStyleguideAsync();
        await page.GetByTestId("toast-info").ClickAsync();
        var toast = page.GetByTestId("toast");
        await toast.WaitForAsync();

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Đóng thông báo" }).ClickAsync();

        (await toast.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Open_ProgressBar_ExposesAccessibleValue()
    {
        var page = await OpenStyleguideAsync();

        var bar = page.GetByRole(AriaRole.Progressbar, new PageGetByRoleOptions { Name = "Tiến độ bài học" });

        (await bar.GetAttributeAsync("aria-valuenow")).ShouldBe("3");
        (await bar.Locator("div").EvaluateAsync<string>("el => el.style.width")).ShouldBe("30%");
    }
}
