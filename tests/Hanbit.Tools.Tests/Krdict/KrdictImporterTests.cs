using System.Text.Json;

using Hanbit.ContentImporter.Krdict;
using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content.Validation;

using Shouldly;

namespace Hanbit.Tools.Tests.Krdict;

public sealed class KrdictImporterTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("hanbit-importer-");

    public void Dispose() => _temp.Delete(recursive: true);

    private static string Page(int total, params (string Code, string Word, string? Grade, string? TransWord)[] items)
    {
        var xmlItems = string.Concat(items.Select(i => $"""
            <item><target_code>{i.Code}</target_code><word>{i.Word}</word><sup_no>0</sup_no>
              {(i.Grade is null ? "" : $"<word_grade>{i.Grade}</word_grade>")}<pos>명사</pos>
              <link>https://krdict.korean.go.kr/kor/dicSearch/SearchView?ParaWordNo={i.Code}</link>
              <sense><sense_order>1</sense_order><definition>정의</definition>
                {(i.TransWord is null ? "" : $"<translation><trans_word><![CDATA[{i.TransWord}]]></trans_word></translation>")}
              </sense></item>
            """));
        return $"<channel><total>{total}</total><start>1</start><num>100</num>{xmlItems}</channel>";
    }

    /// <summary>Nguồn giả: trả XML theo (âm tiết, trang); ghi lại các lần gọi.</summary>
    private sealed class FakeSource(Dictionary<(char, int), string> pages) : IKrdictSearchSource
    {
        public List<(char Syllable, string Level, int Page)> Calls { get; } = [];

        public Task<string> GetStartsWithPageAsync(char syllable, string level, int page, CancellationToken cancellationToken)
        {
            lock (Calls)
            {
                Calls.Add((syllable, level, page));
            }

            return Task.FromResult(pages.TryGetValue((syllable, page), out var xml) ? xml : Page(0));
        }
    }

    [Fact]
    public async Task ImportAsync_RealFixture_ImportsAllBeginnerWords()
    {
        var source = new FakeSource(new() { [('학', 1)] = KrdictXmlParserTests.Fixture("search-hak-level1.xml") });

        var (items, report) = await new KrdictImporter(source).ImportAsync(VocabGrade.Beginner, ['학'], 1, CancellationToken.None);

        items.Count.ShouldBe(7);
        items.ShouldContain(v => v.Ko == "학교" && v.Vi == "trường học" && v.Hanja == "學校");
        items.ShouldAllBe(v => v.Grade == VocabGrade.Beginner && !v.Reviewed && v.Id.StartsWith("kd-"));
        report.ImportedCount.ShouldBe(7);
        report.SkippedWithoutVietnamese.ShouldBeEmpty();
        source.Calls.ShouldHaveSingleItem().Level.ShouldBe("level1");
    }

    [Fact]
    public async Task ImportAsync_TotalAbovePageSize_FetchesNextPages()
    {
        var source = new FakeSource(new()
        {
            [('가', 1)] = Page(150, ("1", "가게", "초급", "cửa hàng")),
            [('가', 2)] = Page(150, ("2", "가격", "초급", "giá cả")),
        });

        var (items, report) = await new KrdictImporter(source).ImportAsync(VocabGrade.Beginner, ['가'], 1, CancellationToken.None);

        items.Select(v => v.Id).ShouldBe(["kd-1", "kd-2"]);
        report.PagesFetched.ShouldBe(2);
    }

    [Fact]
    public async Task ImportAsync_DuplicateAcrossSyllables_KeptOnce_AndOtherGradesDropped()
    {
        var source = new FakeSource(new()
        {
            [('가', 1)] = Page(2, ("1", "가게", "초급", "cửa hàng"), ("9", "가결", "고급", "thông qua")),
            [('나', 1)] = Page(1, ("1", "가게", "초급", "cửa hàng")),
        });

        var (items, _) = await new KrdictImporter(source).ImportAsync(VocabGrade.Beginner, ['가', '나'], 2, CancellationToken.None);

        items.ShouldHaveSingleItem().Id.ShouldBe("kd-1");
    }

    [Fact]
    public async Task ImportAsync_WordWithoutVietnamese_IsSkippedAndReported()
    {
        var source = new FakeSource(new() { [('가', 1)] = Page(2, ("1", "가게", "초급", "cửa hàng"), ("2", "가로", "초급", null)) });

        var (items, report) = await new KrdictImporter(source).ImportAsync(VocabGrade.Beginner, ['가'], 1, CancellationToken.None);

        items.ShouldHaveSingleItem();
        report.SkippedWithoutVietnamese.ShouldBe(["가로 (kd-2)"]);
    }

    [Fact]
    public async Task ImportAsync_SortsByKoreanWordThenCode()
    {
        var source = new FakeSource(new() { [('가', 1)] = Page(3, ("30", "나무", "초급", "cây"), ("20", "가게", "초급", "x"), ("10", "가게", "초급", "y")) });

        var (items, _) = await new KrdictImporter(source).ImportAsync(VocabGrade.Beginner, ['가'], 1, CancellationToken.None);

        items.Select(v => v.Id).ShouldBe(["kd-10", "kd-20", "kd-30"]);
    }

    [Fact]
    public async Task WriteAsync_Output_IsValidContentForValidator()
    {
        var source = new FakeSource(new() { [('학', 1)] = KrdictXmlParserTests.Fixture("search-hak-level1.xml") });
        var (items, _) = await new KrdictImporter(source).ImportAsync(VocabGrade.Beginner, ['학'], 1, CancellationToken.None);
        var contentRoot = Path.Combine(_temp.FullName, "content");
        File.Copy(Path.Combine(FindRepoRoot(), "content", "hangul.json"), Path.Combine(Directory.CreateDirectory(contentRoot).FullName, "hangul.json"));

        await KrdictImporter.WriteAsync(Path.Combine(contentRoot, "vocab", "krdict-beginner.json"), items, CancellationToken.None);

        var validation = await new ContentValidationService().ValidateAsync(contentRoot, CancellationToken.None);
        validation.Issues.ShouldBeEmpty(string.Join(Environment.NewLine, validation.Issues));
        var json = await File.ReadAllTextAsync(Path.Combine(contentRoot, "vocab", "krdict-beginner.json"));
        json.ShouldContain("\"ko\": \"학교\"");
        json.ShouldNotContain("null");
        JsonSerializer.Deserialize<List<VocabItem>>(json, ContentJson.Options).ShouldNotBeNull().Count.ShouldBe(7);
    }

    [Theory]
    [InlineData("vocab-reviews.json")]
    [InlineData("VOCAB-REVIEWS.JSON")]
    public async Task WriteAsync_ToVocabReviewsFile_IsRefusedAndFileUntouched(string fileName)
    {
        var path = Path.Combine(_temp.FullName, fileName);
        await File.WriteAllTextAsync(path, "[ /* bản duyệt của người dùng */ ]");

        await Should.ThrowAsync<InvalidOperationException>(() => KrdictImporter.WriteAsync(path, [], CancellationToken.None));

        (await File.ReadAllTextAsync(path)).ShouldBe("[ /* bản duyệt của người dùng */ ]");
    }

    [Fact]
    public void AllSyllables_CoversWholeHangulBlock()
    {
        var syllables = KrdictImporter.AllSyllables().ToList();

        syllables.Count.ShouldBe(11172);
        syllables[0].ShouldBe('가');
        syllables[^1].ShouldBe('힣');
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Hanbit.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Không tìm thấy Hanbit.sln.");
    }
}
