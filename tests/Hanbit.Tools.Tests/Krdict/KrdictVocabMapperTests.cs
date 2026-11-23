using Hanbit.ContentImporter.Krdict;
using Hanbit.Domain.Content;

using Shouldly;

namespace Hanbit.Tools.Tests.Krdict;

public class KrdictVocabMapperTests
{
    private static KrdictEntry Entry(params KrdictSense[] senses) =>
        new("62450", "가격", 2, "價格", "가격", "초급", "명사", "https://krdict.korean.go.kr/kor/dicSearch/SearchView?ParaWordNo=62450", senses);

    [Fact]
    public void Map_Entry_ProducesVocabItemWithSourceRef()
    {
        var item = KrdictVocabMapper.Map(Entry(new KrdictSense(1, "물건의 가치", "giá cả, giá", "Cái thể hiện giá trị."))).ShouldNotBeNull();

        item.Id.ShouldBe("kd-62450");
        item.Ko.ShouldBe("가격");
        item.Vi.ShouldBe("giá cả, giá");
        item.DefinitionVi.ShouldBe("Cái thể hiện giá trị.");
        item.Pos.ShouldBe(PartOfSpeech.Noun);
        item.Grade.ShouldBe(VocabGrade.Beginner);
        item.Hanja.ShouldBe("價格");
        item.Reviewed.ShouldBeFalse();
        item.Examples.ShouldBeEmpty();
        item.Source.ShouldBe(new SourceRef("한국어기초사전", "62450", "https://krdict.korean.go.kr/kor/dicSearch/SearchView?ParaWordNo=62450", "CC BY-SA 2.0 KR"));
    }

    [Fact]
    public void Map_SeveralSenses_JoinsDistinctMeaningsInOrderUpToThree()
    {
        var item = KrdictVocabMapper.Map(Entry(
            new KrdictSense(2, "b", "b", null),
            new KrdictSense(1, "a", "a", "định nghĩa 1"),
            new KrdictSense(3, "a", "a", null),
            new KrdictSense(4, "c", "c", null),
            new KrdictSense(5, "d", "d", null))).ShouldNotBeNull();

        item.Vi.ShouldBe("a; b; c");
        item.DefinitionVi.ShouldBe("định nghĩa 1");
    }

    [Fact]
    public void Map_NoVietnameseTranslation_ReturnsNull()
    {
        KrdictVocabMapper.Map(Entry(new KrdictSense(1, "정의", null, null), new KrdictSense(2, "정의", "  ", null))).ShouldBeNull();
    }

    [Fact]
    public void Map_PlaceholderMeaning_IsDropped()
    {
        // Dữ liệu thật (물): sense 3 không có từ tương đương
        var item = KrdictVocabMapper.Map(Entry(
            new KrdictSense(1, "x", "nước", null),
            new KrdictSense(2, "x", "nước triều, thủy triều", null),
            new KrdictSense(3, "x", "(Không có từ tương ứng)", null))).ShouldNotBeNull();

        item.Vi.ShouldBe("nước; nước triều, thủy triều");
    }

    [Fact]
    public void Map_OnlyPlaceholder_FallsBackToDefinition()
    {
        // Dữ liệu thật (지각하다)
        var item = KrdictVocabMapper.Map(Entry(
            new KrdictSense(1, "x", "(Không có từ tương ứng)", "Đi làm hoặc đi học trễ hơn giờ đã định."))).ShouldNotBeNull();

        item.Vi.ShouldBe("Đi làm hoặc đi học trễ hơn giờ đã định");
        item.DefinitionVi.ShouldBe("Đi làm hoặc đi học trễ hơn giờ đã định.");
    }

    [Fact]
    public void Map_PartsRepeatedAcrossSenses_AreDeduplicated()
    {
        // Dữ liệu thật (불편하다): "khó chịu" lặp ở sense 3; sense 3 bị bỏ hẳn vì không còn phần mới
        var item = KrdictVocabMapper.Map(Entry(
            new KrdictSense(1, "x", "bất tiện", null),
            new KrdictSense(2, "x", "khó chịu, bực bội, bứt rứt", null),
            new KrdictSense(3, "x", "khó chịu", null),
            new KrdictSense(4, "x", "bực bội, phiền", null))).ShouldNotBeNull();

        item.Vi.ShouldBe("bất tiện; khó chịu, bực bội, bứt rứt; phiền");
    }

    [Fact]
    public void Map_SemicolonInsideMeaning_IsDeduplicatedToo()
    {
        // Dữ liệu thật (접시): sense 2 "jeopsi; đĩa" lặp "đĩa" của sense 1
        var item = KrdictVocabMapper.Map(Entry(
            new KrdictSense(1, "x", "đĩa", null),
            new KrdictSense(2, "x", "jeopsi; đĩa", null))).ShouldNotBeNull();

        item.Vi.ShouldBe("đĩa; jeopsi");
    }

    [Fact]
    public void Map_HtmlEntities_AreDecoded()
    {
        var item = KrdictVocabMapper.Map(Entry(
            new KrdictSense(1, "x", "hậu tố", "Hậu tố thêm nghĩa &apos;thực hiện hành động đó&apos; &amp; tạo thành động từ."))).ShouldNotBeNull();

        item.DefinitionVi.ShouldBe("Hậu tố thêm nghĩa 'thực hiện hành động đó' & tạo thành động từ.");
    }

    [Fact]
    public void Map_MessyWhitespace_IsCollapsed()
    {
        var item = KrdictVocabMapper.Map(Entry(new KrdictSense(1, "x", "cửa hàng, cửa hiệu,  cửa tiệm ,\n quầy", null))).ShouldNotBeNull();

        item.Vi.ShouldBe("cửa hàng, cửa hiệu, cửa tiệm, quầy");
    }

    [Fact]
    public void Map_DecomposedText_IsNormalizedToNfc()
    {
        var decomposed = "nước".Normalize(System.Text.NormalizationForm.FormD);

        var item = KrdictVocabMapper.Map(Entry(new KrdictSense(1, "x", decomposed, null))).ShouldNotBeNull();

        item.Vi.ShouldBe("nước".Normalize(System.Text.NormalizationForm.FormC));
        item.Vi.IsNormalized(System.Text.NormalizationForm.FormC).ShouldBeTrue();
    }

    [Theory]
    [InlineData("bus")]   // nguyên ngữ ngoại lai, không phải Hán tự
    [InlineData(null)]
    public void Map_OriginWithoutHanCharacters_HasNoHanja(string? origin)
    {
        var entry = Entry(new KrdictSense(1, "x", "xe buýt", null)) with { Origin = origin };

        KrdictVocabMapper.Map(entry).ShouldNotBeNull().Hanja.ShouldBeNull();
    }

    [Theory]
    [InlineData("명사", PartOfSpeech.Noun)]
    [InlineData("의존 명사", PartOfSpeech.DependentNoun)]
    [InlineData("대명사", PartOfSpeech.Pronoun)]
    [InlineData("수사", PartOfSpeech.Numeral)]
    [InlineData("동사", PartOfSpeech.Verb)]
    [InlineData("보조 동사", PartOfSpeech.AuxiliaryVerb)]
    [InlineData("형용사", PartOfSpeech.Adjective)]
    [InlineData("보조 형용사", PartOfSpeech.AuxiliaryAdjective)]
    [InlineData("관형사", PartOfSpeech.Determiner)]
    [InlineData("부사", PartOfSpeech.Adverb)]
    [InlineData("감탄사", PartOfSpeech.Interjection)]
    [InlineData("조사", PartOfSpeech.Particle)]
    [InlineData("어미", PartOfSpeech.Ending)]
    [InlineData("접사", PartOfSpeech.Affix)]
    [InlineData("품사 없음", PartOfSpeech.None)]
    [InlineData("không rõ", PartOfSpeech.None)]
    public void MapPos_KoreanPartOfSpeech_MapsToEnum(string pos, PartOfSpeech expected)
    {
        KrdictVocabMapper.MapPos(pos).ShouldBe(expected);
    }

    [Theory]
    [InlineData("초급", VocabGrade.Beginner)]
    [InlineData("중급", VocabGrade.Intermediate)]
    [InlineData("고급", VocabGrade.Advanced)]
    [InlineData(null, VocabGrade.None)]
    public void MapGrade_KoreanGrade_MapsToEnum(string? grade, VocabGrade expected)
    {
        KrdictVocabMapper.MapGrade(grade).ShouldBe(expected);
    }
}
