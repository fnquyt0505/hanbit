using Hanbit.Domain.Content;

using Shouldly;

namespace Hanbit.Domain.Tests.Content;

public class VocabReviewMergerTests
{
    private static readonly SourceRef Source = new("한국어기초사전", "1", "https://krdict.korean.go.kr/", "CC BY-SA 2.0 KR");

    private static VocabItem Item(string id, string vi = "nghĩa krdict") =>
        new(id, "물", vi, PartOfSpeech.Noun, VocabGrade.Beginner, [], Reviewed: false, Source, DefinitionVi: "định nghĩa krdict");

    [Fact]
    public void Apply_ReviewWithoutOverrides_OnlyMarksReviewed()
    {
        var merged = VocabReviewMerger.Apply([Item("kd-1")], [new VocabReview("kd-1", Reviewed: true)]).ShouldHaveSingleItem();

        merged.Reviewed.ShouldBeTrue();
        merged.Vi.ShouldBe("nghĩa krdict");
        merged.DefinitionVi.ShouldBe("định nghĩa krdict");
    }

    [Fact]
    public void Apply_ReviewWithOverrides_ReplacesVietnameseFields()
    {
        var merged = VocabReviewMerger.Apply(
            [Item("kd-1")],
            [new VocabReview("kd-1", Reviewed: true, Vi: "nước", DefinitionVi: "chất lỏng trong suốt")]).ShouldHaveSingleItem();

        merged.Vi.ShouldBe("nước");
        merged.DefinitionVi.ShouldBe("chất lỏng trong suốt");
        merged.Ko.ShouldBe("물");
        merged.Source.ShouldBe(Source);
    }

    [Fact]
    public void Apply_ReviewMarkedNotReviewed_KeepsItemOutOfLessons()
    {
        var merged = VocabReviewMerger.Apply(
            [Item("kd-1") with { Reviewed = true }],
            [new VocabReview("kd-1", Reviewed: false, Note: "nghĩa sai, cần sửa")]).ShouldHaveSingleItem();

        merged.Reviewed.ShouldBeFalse();
    }

    [Fact]
    public void Apply_ItemsWithoutReview_AreUnchanged()
    {
        var item = Item("kd-2");

        VocabReviewMerger.Apply([item], [new VocabReview("kd-1", true)]).ShouldHaveSingleItem().ShouldBeSameAs(item);
    }

    [Fact]
    public void Apply_ReviewForUnknownId_IsIgnored()
    {
        VocabReviewMerger.Apply([Item("kd-1")], [new VocabReview("kd-999", true)]).ShouldHaveSingleItem().Reviewed.ShouldBeFalse();
    }

    [Fact]
    public void Apply_PreservesItemOrder()
    {
        var merged = VocabReviewMerger.Apply([Item("kd-3"), Item("kd-1"), Item("kd-2")], [new VocabReview("kd-1", true)]);

        merged.Select(v => v.Id).ShouldBe(["kd-3", "kd-1", "kd-2"]);
    }
}
