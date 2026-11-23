using Hanbit.Domain.Hangul;

using Shouldly;

namespace Hanbit.Domain.Tests.Hangul;

/// <summary>
/// Kết quả mong đợi theo Revised Romanization (국어의 로마자 표기법) — phần lớn lấy từ ví dụ chính thức
/// trong quy tắc (백암 baegam, 종로 jongno, 왕십리 wangsimni, 독립 dongnip, 좋고 joko, 같이 gachi…).
/// </summary>
public class RomanizationServiceTests
{
    [Theory]
    // Cơ bản, không biến âm
    [InlineData("사랑", "sarang")]
    [InlineData("김치", "gimchi")]
    [InlineData("서울", "seoul")]
    [InlineData("부산", "busan")]
    [InlineData("울산", "ulsan")]
    [InlineData("구미", "gumi")]
    [InlineData("영동", "yeongdong")]
    [InlineData("칠곡", "chilgok")]
    [InlineData("라면", "ramyeon")]
    [InlineData("짜다", "jjada")]
    [InlineData("꽃", "kkot")]
    [InlineData("의사", "uisa")]
    [InlineData("외국", "oeguk")]
    [InlineData("웨이터", "weiteo")]
    [InlineData("강아지", "gangaji")]
    // Âm căng không ghi
    [InlineData("학교", "hakgyo")]
    [InlineData("낙동강", "nakdonggang")]
    [InlineData("압구정", "apgujeong")]
    [InlineData("옥천", "okcheon")]
    [InlineData("합덕", "hapdeok")]
    [InlineData("호법", "hobeop")]
    // Âm cuối đại diện, phụ âm cuối kép
    [InlineData("닭", "dak")]
    [InlineData("여덟", "yeodeol")]
    [InlineData("읽고", "ilgo")]
    // Nối âm
    [InlineData("한국어", "hangugeo")]
    [InlineData("음악", "eumak")]
    [InlineData("백암", "baegam")]
    [InlineData("맛있다", "masitda")]
    [InlineData("읽어", "ilgeo")]
    [InlineData("없어", "eopseo")]
    [InlineData("앉아", "anja")]
    [InlineData("좋아", "joa")]
    [InlineData("싫어", "sireo")]
    // Mũi hóa
    [InlineData("감사합니다", "gamsahamnida")]
    [InlineData("백마", "baengma")]
    [InlineData("국민", "gungmin")]
    [InlineData("있는", "inneun")]
    [InlineData("읽는", "ingneun")]
    // ㄹ
    [InlineData("신라", "silla")]
    [InlineData("별내", "byeollae")]
    [InlineData("설날", "seollal")]
    [InlineData("물론", "mullon")]
    [InlineData("종로", "jongno")]
    [InlineData("왕십리", "wangsimni")]
    [InlineData("독립", "dongnip")]
    [InlineData("협력", "hyeomnyeok")]
    // Bật hơi với ㅎ
    [InlineData("좋고", "joko")]
    [InlineData("놓다", "nota")]
    [InlineData("낳지", "nachi")]
    [InlineData("많다", "manta")]
    [InlineData("잡혀", "japyeo")]
    [InlineData("축하", "chuka")]
    [InlineData("놓는", "nonneun")]
    [InlineData("전화", "jeonhwa")]
    [InlineData("안녕하세요", "annyeonghaseyo")]
    // Vòm hóa
    [InlineData("같이", "gachi")]
    [InlineData("굳이", "guji")]
    [InlineData("해돋이", "haedoji")]
    public void Romanize_Word_FollowsRevisedRomanization(string korean, string expected)
    {
        RomanizationService.Romanize(korean).ShouldBe(expected);
    }

    [Fact]
    public void Romanize_Sentence_KeepsSpacesAndPunctuation_AndAppliesRulesPerWord()
    {
        RomanizationService.Romanize("물을 마셔요.").ShouldBe("mureul masyeoyo.");
    }

    [Fact]
    public void Romanize_SoundChanges_DoNotCrossSpaces()
    {
        // "한국 어" (2 từ) khác "한국어": không nối âm qua khoảng trắng
        RomanizationService.Romanize("한국 어").ShouldBe("hanguk eo");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc 123")]
    [InlineData("ㄱ ㅏ")]
    public void Romanize_NonSyllableText_IsUnchanged(string text)
    {
        RomanizationService.Romanize(text).ShouldBe(text);
    }

    [Fact]
    public void Romanize_EverySyllable_ProducesLowercaseLatin()
    {
        for (var c = HangulSyllable.First; c <= HangulSyllable.Last; c++)
        {
            var roman = RomanizationService.Romanize(c.ToString());
            roman.ShouldNotBeEmpty();
            roman.ShouldAllBe(ch => ch >= 'a' && ch <= 'z');
        }
    }

    [Theory]
    [InlineData('가', 0, 0, 0)]
    [InlineData('힣', 18, 20, 27)]
    [InlineData('학', 18, 0, 1)]
    public void Decompose_Syllable_ReturnsUnicodeIndexes(char syllable, int initial, int medial, int final)
    {
        var decomposed = HangulSyllable.Decompose(syllable);

        decomposed.ShouldBe(new HangulSyllable(initial, medial, final));
        decomposed.Compose().ShouldBe(syllable);
    }

    [Fact]
    public void Decompose_NonSyllable_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => HangulSyllable.Decompose('a'));
    }
}
