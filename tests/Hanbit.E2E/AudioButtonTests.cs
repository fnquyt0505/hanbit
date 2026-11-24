using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>
/// T3.1: &lt;audio-button&gt; gọi Web Speech API đúng text/lang/tốc độ. Trình duyệt headless không có giọng thật
/// → thay window.speechSynthesis bằng bản giả ghi lại các câu được đọc. Nghe thật: kiểm thủ công (PROGRESS).
/// </summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class AudioButtonTests(WebServerFixture server)
{
    private const string FakeSpeechScript = """
        (() => {
          const voices = window.__fakeVoices ?? [{ name: 'Fake Korean', lang: 'ko-KR', localService: true }];
          window.__spoken = [];
          const synth = {
            getVoices: () => window.__noKoreanVoice ? [{ name: 'Fake English', lang: 'en-US', localService: true }] : voices,
            addEventListener() {}, removeEventListener() {},
            cancel() {},
            speak(u) {
              if (u.voice?.name === window.__failVoice) { // giả lập giọng online lỗi mạng
                setTimeout(() => u.onerror?.({ error: 'network' }), 10);
                return;
              }
              window.__spoken.push({ text: u.text, lang: u.lang, rate: u.rate, voice: u.voice?.name });
              setTimeout(() => u.onend?.(), 50);
            },
          };
          Object.defineProperty(window, 'speechSynthesis', { value: synth, configurable: true });
          // Utterance thật chỉ nhận voice là SpeechSynthesisVoice thật → thay bằng lớp giả.
          window.SpeechSynthesisUtterance = class { constructor(text) { this.text = text; } };
        })();
        """;

    private const string WindowsVoices = """
        window.__fakeVoices = [
          { name: 'Microsoft Heami - Korean (Korean)', lang: 'ko-KR', localService: true },
          { name: 'Microsoft SunHi Online (Natural) - Korean (Korean)', lang: 'ko-KR', localService: false },
          { name: 'Microsoft Aria Online (Natural) - English', lang: 'en-US', localService: false },
        ];
        """;

    private async Task<IPage> OpenStyleguideAsync(bool noKoreanVoice = false, string? setupScript = null)
    {
        var page = await server.NewPageAsync();
        if (noKoreanVoice)
        {
            await page.AddInitScriptAsync("window.__noKoreanVoice = true;");
        }

        if (setupScript is not null)
        {
            await page.AddInitScriptAsync(setupScript);
        }

        await page.AddInitScriptAsync(FakeSpeechScript);
        await page.GotoAsync($"{server.BaseUrl}/styleguide");
        return page;
    }

    private static Task<string> SpokenJsonAsync(IPage page) =>
        page.EvaluateAsync<string>("() => JSON.stringify(window.__spoken)");

    [Fact]
    public async Task Click_AudioButton_SpeaksKoreanAtNormalRate()
    {
        var page = await OpenStyleguideAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Nghe: 안녕하세요" }).ClickAsync();
        await page.WaitForFunctionAsync("() => window.__spoken.length === 1");

        var spoken = await SpokenJsonAsync(page);
        spoken.ShouldContain("\"text\":\"안녕하세요\"");
        spoken.ShouldContain("\"lang\":\"ko-KR\"");
        spoken.ShouldContain("\"rate\":1");
        spoken.ShouldContain("\"voice\":\"Fake Korean\"");
    }

    [Fact]
    public async Task PrefersNaturalOnlineVoice_OverOldLocalVoice()
    {
        var page = await OpenStyleguideAsync(setupScript: WindowsVoices);

        await page.GetByRole(AriaRole.Button, new() { Name = "Nghe: 안녕하세요" }).ClickAsync();
        await page.WaitForFunctionAsync("() => window.__spoken.length === 1");

        (await SpokenJsonAsync(page)).ShouldContain("SunHi Online (Natural)");
    }

    [Fact]
    public async Task OnlineVoiceFails_FallsBackToLocalVoice()
    {
        var page = await OpenStyleguideAsync(
            setupScript: WindowsVoices + "window.__failVoice = 'Microsoft SunHi Online (Natural) - Korean (Korean)';");

        await page.GetByRole(AriaRole.Button, new() { Name = "Nghe: 안녕하세요" }).ClickAsync();
        await page.WaitForFunctionAsync("() => window.__spoken.length === 1");

        (await SpokenJsonAsync(page)).ShouldContain("Microsoft Heami");
        (await page.GetByTestId("toast").CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Click_SlowButton_SpeaksAtSlowRate()
    {
        var page = await OpenStyleguideAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Nghe chậm: 안녕하세요" }).ClickAsync();
        await page.WaitForFunctionAsync("() => window.__spoken.length === 1");

        (await SpokenJsonAsync(page)).ShouldContain("\"rate\":0.7");
    }

    [Fact]
    public async Task NoKoreanVoice_ShowsHelpfulToast_AndStatusSaysMissing()
    {
        var page = await OpenStyleguideAsync(noKoreanVoice: true);

        await page.GetByRole(AriaRole.Button, new() { Name = "Nghe: 감사합니다" }).ClickAsync();

        var toast = page.GetByTestId("toast");
        await toast.WaitForAsync();
        (await toast.InnerTextAsync()).ShouldContain("giọng đọc tiếng Hàn");
        (await toast.GetAttributeAsync("data-kind")).ShouldBe("danger");
        (await SpokenJsonAsync(page)).ShouldBe("[]");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=speech-status]').textContent === 'chưa có'");
    }

    [Fact]
    public async Task SpeechStatus_WithKoreanVoice_SaysAvailable()
    {
        var page = await OpenStyleguideAsync();

        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=speech-status]').textContent === 'có'");
    }
}
