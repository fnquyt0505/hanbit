using Hanbit.ContentImporter.Krdict;

using Shouldly;

namespace Hanbit.Tools.Tests.Krdict;

public class KrdictXmlParserTests
{
    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void ParseSearch_RealPage_ReadsChannelAndItems()
    {
        var page = KrdictXmlParser.ParseSearch(Fixture("search-hak-level1.xml"));

        page.Total.ShouldBe(7);
        page.Start.ShouldBe(1);
        page.Items.Count.ShouldBe(7);
        page.Items.Select(i => i.Word).ShouldContain("학교");
    }

    [Fact]
    public void ParseSearch_RealPage_ReadsOriginGradePosAndVietnameseTranslation()
    {
        var school = KrdictXmlParser.ParseSearch(Fixture("search-hak-level1.xml")).Items.First(i => i.Word == "학교");

        school.Origin.ShouldBe("學校");
        school.WordGrade.ShouldBe("초급");
        school.Pos.ShouldBe("명사");
        school.Link.ShouldStartWith("https://krdict.korean.go.kr/");
        var sense = school.Senses.ShouldHaveSingleItem();
        sense.Order.ShouldBe(1);
        sense.TransWord.ShouldBe("trường học");
        sense.TransDefinition.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseSearch_ErrorResponse_ThrowsWithCode()
    {
        var ex = Should.Throw<KrdictApiException>(() => KrdictXmlParser.ParseSearch(Fixture("error-daily-limit.xml")));

        ex.Code.ShouldBe("010");
        ex.IsDailyLimitExceeded.ShouldBeTrue();
    }

    [Fact]
    public void ParseSearch_EmptyResult_HasNoItems()
    {
        const string xml = "<channel><total>0</total><start>1</start><num>100</num></channel>";

        KrdictXmlParser.ParseSearch(xml).Items.ShouldBeEmpty();
    }

    [Fact]
    public void ParseSearch_ItemWithoutOptionalElements_UsesNulls()
    {
        const string xml = """
            <channel><total>1</total><start>1</start><num>10</num>
              <item><target_code>1</target_code><word>가</word><pos>조사</pos><link>https://krdict.korean.go.kr/x</link>
                <sense><sense_order>1</sense_order><definition>x</definition></sense>
              </item>
            </channel>
            """;

        var item = KrdictXmlParser.ParseSearch(xml).Items.ShouldHaveSingleItem();

        item.Origin.ShouldBeNull();
        item.Pronunciation.ShouldBeNull();
        item.SupNo.ShouldBe(0);
        item.Senses.ShouldHaveSingleItem().TransWord.ShouldBeNull();
    }

    [Fact]
    public void ParseSearch_UnexpectedRoot_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => KrdictXmlParser.ParseSearch("<html></html>"));
    }
}
