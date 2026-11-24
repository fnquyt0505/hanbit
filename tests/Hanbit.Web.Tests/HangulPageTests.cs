using System.Net;
using System.Text.RegularExpressions;

using Hanbit.Domain.Content;
using Hanbit.Web.Models.Hangul;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T3.2: /hangul — lưới 40 chữ theo 4 nhóm, bấm để nghe.</summary>
public partial class HangulPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("data-group=\"([a-z-]+)\"")]
    private static partial Regex GroupRegex();

    private async Task<string> GetHangulHtmlAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/hangul");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Hangul_ShowsFourGroupsInLearningOrder()
    {
        var html = await GetHangulHtmlAsync();

        GroupRegex().Matches(html).Select(m => m.Groups[1].Value)
            .ShouldBe(["nguyen-am", "phu-am", "nguyen-am-ghep", "phu-am-kep"]);
    }

    [Fact]
    public async Task Hangul_RendersFortyTilesWithIdsAndNewState()
    {
        var html = await GetHangulHtmlAsync();

        html.Split("data-testid=\"hangul-tile\"").Length.ShouldBe(41);
        html.Split("data-state=\"new\"").Length.ShouldBe(41);
        html.ShouldContain("data-letter-id=\"c-giyeok\"");
    }

    [Fact]
    public async Task Hangul_MarksHangulNavItemCurrent()
    {
        var html = await GetHangulHtmlAsync();

        html.Split("href=\"/hangul\" aria-current=\"page\"").Length.ShouldBe(3);
    }

    [Theory]
    [InlineData(HangulType.Consonant, "ㄱ", "기역", "기역")]
    [InlineData(HangulType.DoubleConsonant, "ㄲ", "쌍기역", "쌍기역")]
    [InlineData(HangulType.Vowel, "ㅏ", null, "아")]
    [InlineData(HangulType.CompoundVowel, "ㅘ", null, "와")]
    public void SpeakText_ConsonantUsesName_VowelUsesSilentIeung(HangulType type, string letterChar, string? nameKo, string expected)
    {
        var letter = new HangulLetter("x", letterChar, type, "r", "hint", [], NameKo: nameKo);

        HangulTileViewModel.SpeakTextFor(letter).ShouldBe(expected);
    }

    [Fact]
    public void GroupInfo_CoversEveryHangulTypeOnce()
    {
        HangulGroupInfo.All.Select(g => g.Type).ShouldBe(Enum.GetValues<HangulType>(), ignoreOrder: true);
        HangulGroupInfo.All.Select(g => g.Slug).ShouldBeUnique();
    }
}
