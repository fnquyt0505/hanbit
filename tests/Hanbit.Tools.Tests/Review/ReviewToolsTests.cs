using System.Text;

using Hanbit.ContentImporter.Krdict;
using Hanbit.ContentImporter.Review;
using Hanbit.Domain.Content;

using Shouldly;

namespace Hanbit.Tools.Tests.Review;

public sealed class ReviewToolsTests : IDisposable
{
    private static readonly SourceRef Source = new("한국어기초사전", "1", "https://krdict.korean.go.kr/kor/dicSearch/SearchView?ParaWordNo=1", "CC BY-SA 2.0 KR");
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("hanbit-review-");

    public void Dispose() => _temp.Delete(recursive: true);

    private static VocabItem Vocab(string id, string ko, string vi) =>
        new(id, ko, vi, PartOfSpeech.Noun, VocabGrade.Beginner, [], false, Source with { Id = id[3..] }, DefinitionVi: $"định nghĩa {vi}");

    private static KrdictEntry Lmf(string code, params string[] topics) =>
        new(code, "x", 0, null, null, "초급", "명사", "", [], "단어", topics);

    private static readonly IReadOnlyList<VocabItem> Vocabulary =
    [
        Vocab("kd-1", "안녕하다", "bình an"),
        Vocab("kd-2", "감사", "sự cảm ơn"),
        Vocab("kd-3", "가족", "gia đình"),
        Vocab("kd-4", "물", "nước"),
    ];

    private static readonly IReadOnlyList<KrdictEntry> Entries =
    [
        Lmf("1", "인사하기"),
        Lmf("2", "감사하기", "인사하기"),
        Lmf("3", "소개하기(가족 소개)"),
        Lmf("4"),
    ];

    // ───────── CSV ─────────

    [Fact]
    public void Csv_WriteThenParse_RoundTripsQuotesCommasAndNewlines()
    {
        var csv = Csv.Write([["a", "b,c", "d\"e"], ["dòng\nmới", "", "한국어"]]);

        Csv.Parse(csv).ShouldBe([["a", "b,c", "d\"e"], ["dòng\nmới", "", "한국어"]]);
    }

    [Theory]
    [InlineData(';')]
    [InlineData('\t')]
    public void Csv_Parse_DetectsDelimiterFromHeader(char delimiter)
    {
        var text = $"﻿id{delimiter}duyet{delimiter}ghi_chu\r\nkd-1{delimiter}x{delimiter}\"a, b\"\r\n";

        Csv.Parse(text).ShouldBe([["id", "duyet", "ghi_chu"], ["kd-1", "x", "a, b"]]);
    }

    [Fact]
    public void Csv_Parse_SkipsBlankLines()
    {
        Csv.Parse("id,duyet\r\n\r\nkd-1,x\r\n,\r\n").Count.ShouldBe(2);
    }

    // ───────── Export ─────────

    [Fact]
    public void Select_ByTopics_KeepsTopicOrderAndDeduplicates()
    {
        var rows = ReviewExporter.Select(Vocabulary, [], Entries, ["인사하기", "감사하기", "소개하기(가족 소개)"]);

        rows.Select(r => (r.Topic, r.Item.Id)).ShouldBe([("인사하기", "kd-2"), ("인사하기", "kd-1"), ("소개하기(가족 소개)", "kd-3")]);
    }

    [Fact]
    public void Select_AlreadyReviewed_IsExcluded()
    {
        var rows = ReviewExporter.Select(Vocabulary, [new VocabReview("kd-1", true)], Entries, ["인사하기"]);

        rows.ShouldHaveSingleItem().Item.Id.ShouldBe("kd-2");
    }

    [Fact]
    public void Select_WithLimit_TakesFirstRows()
    {
        ReviewExporter.Select(Vocabulary, [], Entries, ["인사하기", "소개하기(가족 소개)"], limit: 2).Count.ShouldBe(2);
    }

    [Fact]
    public void ToCsv_Rows_HaveHeaderRomanizationAndEmptyReviewerColumns()
    {
        var csv = ReviewExporter.ToCsv(ReviewExporter.Select(Vocabulary, [], Entries, ["소개하기(가족 소개)"]));

        var rows = Csv.Parse(csv);
        rows[0].ShouldBe(ReviewCsvColumns.All.ToList());
        var row = rows[1];
        row[ReviewCsvColumns.All.ToList().IndexOf(ReviewCsvColumns.Korean)].ShouldBe("가족");
        row[ReviewCsvColumns.All.ToList().IndexOf(ReviewCsvColumns.Romanization)].ShouldBe("gajok");
        row[ReviewCsvColumns.All.ToList().IndexOf(ReviewCsvColumns.PartOfSpeech)].ShouldBe("danh từ");
        row[ReviewCsvColumns.All.ToList().IndexOf(ReviewCsvColumns.Decision)].ShouldBeEmpty();
    }

    // ───────── Import ─────────

    private static string ReviewedCsv(params string[] lines) =>
        "id,tieng_han,nghia_moi,dinh_nghia_moi,duyet,ghi_chu\r\n" + string.Join("\r\n", lines);

    [Fact]
    public void Merge_Decisions_ProduceReviews()
    {
        var result = ReviewImporter.Merge(ReviewedCsv(
            "kd-1,안녕하다,,,x,",
            "kd-2,감사,\"lời cảm ơn, sự cảm ơn\",,X,sửa nghĩa",
            "kd-3,가족,,,loại,trùng nghĩa",
            "kd-4,물,,,,"), Vocabulary, []);

        result.Errors.ShouldBeEmpty();
        result.Approved.ShouldBe(2);
        result.Rejected.ShouldBe(1);
        result.Undecided.ShouldBe(1);
        result.MeaningChanged.ShouldBe(1);
        result.Reviews.ShouldBe(
        [
            new VocabReview("kd-1", true),
            new VocabReview("kd-2", true, Vi: "lời cảm ơn, sự cảm ơn", Note: "sửa nghĩa"),
            new VocabReview("kd-3", false, Note: "trùng nghĩa"),
        ]);
    }

    [Fact]
    public void Merge_KeepsExistingReviewsAndOverwritesSameId()
    {
        IReadOnlyList<VocabReview> existing = [new VocabReview("kd-4", true, Vi: "nước uống"), new VocabReview("kd-1", false)];

        var result = ReviewImporter.Merge(ReviewedCsv("kd-1,안녕하다,,,x,"), Vocabulary, existing);

        result.Reviews.ShouldBe([new VocabReview("kd-1", true), new VocabReview("kd-4", true, Vi: "nước uống")]);
    }

    [Fact]
    public void Merge_UnknownIdOrDecision_ReturnsErrorsAndNoReviews()
    {
        var result = ReviewImporter.Merge(ReviewedCsv("kd-999,?,,,x,", "kd-1,안녕하다,,,có lẽ,"), Vocabulary, [new VocabReview("kd-4", true)]);

        result.Errors.Count.ShouldBe(2);
        result.Errors[0].ShouldContain("kd-999");
        result.Errors[1].ShouldContain("có lẽ");
        result.Reviews.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_DuplicateId_ReturnsError()
    {
        ReviewImporter.Merge(ReviewedCsv("kd-1,a,,,x,", "kd-1,a,,,x,"), Vocabulary, []).Errors.ShouldHaveSingleItem().ShouldContain("nhiều lần");
    }

    [Fact]
    public void Merge_MissingRequiredColumns_ReturnsError()
    {
        ReviewImporter.Merge("tieng_han,nghia_moi\r\n가족,x", Vocabulary, []).Errors.ShouldHaveSingleItem().ShouldContain("duyet");
    }

    [Fact]
    public void Merge_ExcelSemicolonExport_IsAccepted()
    {
        var result = ReviewImporter.Merge("﻿id;duyet;nghia_moi\r\nkd-3;x;gia đình, nhà\r\n", Vocabulary, []);

        result.Reviews.ShouldHaveSingleItem().ShouldBe(new VocabReview("kd-3", true, Vi: "gia đình, nhà"));
    }

    [Fact]
    public void ReadCsvText_NonUtf8File_ThrowsHelpfulError()
    {
        var path = Path.Combine(_temp.FullName, "excel.csv");
        File.WriteAllBytes(path, [0x69, 0x64, 0x2C, 0xE0, 0xE1]); // byte không hợp lệ UTF-8 (kiểu Windows-1258)

        Should.Throw<InvalidDataException>(() => ReviewImporter.ReadCsvText(path)).Message.ShouldContain("CSV UTF-8");
    }

    [Fact]
    public void ReadCsvText_Utf8WithBom_IsRead()
    {
        var path = Path.Combine(_temp.FullName, "sheet.csv");
        File.WriteAllText(path, "id,duyet\r\nkd-1,x", Csv.Utf8WithBom);

        ReviewImporter.Merge(ReviewImporter.ReadCsvText(path), Vocabulary, []).Approved.ShouldBe(1);
    }
}
