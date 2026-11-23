using System.Text.Json;

using Hanbit.ContentImporter.Krdict;
using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content.Validation;

using Shouldly;

namespace Hanbit.Tools.Tests.Krdict;

public sealed class KrdictImporterTests : IDisposable
{
    private static readonly string Sample = Path.Combine(AppContext.BaseDirectory, "Fixtures", "krdict-lmf-sample.json");
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("hanbit-importer-");

    public void Dispose() => _temp.Delete(recursive: true);

    [Fact]
    public async Task ReadAsync_Sample_ReadsAllEntries()
    {
        var entries = await KrdictLmfReader.ReadAsync(Sample, CancellationToken.None);

        entries.Count.ShouldBe(7);
    }

    [Fact]
    public async Task ReadAsync_Entry_MapsLmfFeatures()
    {
        var youth = (await KrdictLmfReader.ReadAsync(Sample, CancellationToken.None)).Single(e => e.TargetCode == "72119");

        youth.Word.ShouldBe("청년");
        youth.WordGrade.ShouldBe("초급");
        youth.LexicalUnit.ShouldBe("단어");
        youth.Pos.ShouldBe("명사");
        youth.Origin.ShouldBe("靑年");
        youth.Pronunciation.ShouldNotBeNullOrWhiteSpace();
        youth.Link.ShouldBe("https://krdict.korean.go.kr/kor/dicSearch/SearchView?ParaWordNo=72119");
        var sense = youth.Senses.ShouldHaveSingleItem();
        sense.Order.ShouldBe(1);
        sense.Definition.ShouldNotBeNullOrWhiteSpace();
        sense.TransWord.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReadAsync_LemmaArrayWithVariants_UsesWrittenForm()
    {
        var chocolate = (await KrdictLmfReader.ReadAsync(Sample, CancellationToken.None)).Single(e => e.TargetCode == "72128");

        chocolate.Word.ShouldBe("초콜릿");
        chocolate.Pronunciation.ShouldBeNull(); // không có WordForm
    }

    [Fact]
    public async Task ReadAsync_WordFormArray_JoinsPronunciations()
    {
        var entry = (await KrdictLmfReader.ReadAsync(Sample, CancellationToken.None)).Single(e => e.TargetCode == "91894");

        entry.Word.ShouldBe("청소되다");
        entry.Pronunciation.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReadAsync_NotLmf_ThrowsFormatException()
    {
        var path = Path.Combine(_temp.FullName, "khac.json");
        await File.WriteAllTextAsync(path, """{ "foo": [] }""");

        await Should.ThrowAsync<FormatException>(() => KrdictLmfReader.ReadAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_Sample_KeepsOnlyBeginnerWords()
    {
        var (items, report) = await KrdictImporter.ImportAsync(VocabGrade.Beginner, [Sample], CancellationToken.None);

        items.Select(v => v.Ko).ShouldBe(["첫", "청년", "청소되다", "초콜릿"]);
        items.ShouldAllBe(v => v.Grade == VocabGrade.Beginner && !v.Reviewed && v.Id.StartsWith("kd-") && v.Vi.Length > 0);
        report.FilesRead.ShouldBe(1);
        report.EntriesScanned.ShouldBe(7);
        report.MatchingGrade.ShouldBe(5); // 4 단어 + 1 구
        report.ExcludedNonWordUnits.ShouldBe(new Dictionary<string, int> { ["구"] = 1 });
        report.ImportedCount.ShouldBe(4);
        report.SkippedWithoutVietnamese.ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportAsync_DuplicateTargetCode_ImportedOnce()
    {
        var (items, _) = await KrdictImporter.ImportAsync(VocabGrade.Beginner, [Sample, Sample], CancellationToken.None);

        items.Count(v => v.Id == "kd-77610").ShouldBe(1);
        items.Count.ShouldBe(4);
    }

    [Fact]
    public async Task ImportAsync_IntermediateGrade_SelectsOtherLevel()
    {
        var (items, _) = await KrdictImporter.ImportAsync(VocabGrade.Intermediate, [Sample], CancellationToken.None);

        items.ShouldHaveSingleItem().Ko.ShouldBe("첫가을");
    }

    [Fact]
    public async Task WriteAsync_Output_IsValidContentForValidator()
    {
        var (items, _) = await KrdictImporter.ImportAsync(VocabGrade.Beginner, [Sample], CancellationToken.None);
        var contentRoot = Directory.CreateDirectory(Path.Combine(_temp.FullName, "content")).FullName;
        File.Copy(Path.Combine(FindRepoRoot(), "content", "hangul.json"), Path.Combine(contentRoot, "hangul.json"));

        await KrdictImporter.WriteAsync(Path.Combine(contentRoot, "vocab", "krdict-beginner.json"), items, CancellationToken.None);

        var validation = await new ContentValidationService().ValidateAsync(contentRoot, CancellationToken.None);
        validation.Issues.ShouldBeEmpty(string.Join(Environment.NewLine, validation.Issues));
        var json = await File.ReadAllTextAsync(Path.Combine(contentRoot, "vocab", "krdict-beginner.json"));
        json.ShouldContain("\"ko\": \"청년\"");
        json.ShouldNotContain("null");
        JsonSerializer.Deserialize<List<VocabItem>>(json, ContentJson.Options).ShouldNotBeNull().Count.ShouldBe(4);
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
    public void ResolveInputFiles_Directory_ReturnsSortedJsonFiles()
    {
        File.WriteAllText(Path.Combine(_temp.FullName, "2_x.json"), "{}");
        File.WriteAllText(Path.Combine(_temp.FullName, "1_x.json"), "{}");
        File.WriteAllText(Path.Combine(_temp.FullName, "ghi-chu.txt"), "");

        KrdictImporter.ResolveInputFiles(_temp.FullName).Select(Path.GetFileName).ShouldBe(["1_x.json", "2_x.json"]);
    }

    [Fact]
    public void ResolveInputFiles_Missing_Throws()
    {
        Should.Throw<FileNotFoundException>(() => KrdictImporter.ResolveInputFiles(Path.Combine(_temp.FullName, "khong-co")));
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
