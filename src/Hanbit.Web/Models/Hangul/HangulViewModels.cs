using Hanbit.Domain.Content;
using Hanbit.Domain.Hangul;

namespace Hanbit.Web.Models.Hangul;

/// <summary>Nhóm chữ cái trên trang /hangul — thứ tự học: nguyên âm cơ bản → phụ âm cơ bản → nguyên âm ghép → phụ âm kép.</summary>
public sealed record HangulGroupInfo(string Slug, HangulType Type, string Title, string Description)
{
    public static IReadOnlyList<HangulGroupInfo> All { get; } =
    [
        new("nguyen-am", HangulType.Vowel, "Nguyên âm cơ bản", "Học trước tiên — mỗi âm tiết đều có một nguyên âm."),
        new("phu-am", HangulType.Consonant, "Phụ âm cơ bản", "Ghép với nguyên âm để tạo âm tiết, VD ㄱ + ㅏ = 가."),
        new("nguyen-am-ghep", HangulType.CompoundVowel, "Nguyên âm ghép", "Ghép từ các nguyên âm cơ bản, VD ㅗ + ㅏ = ㅘ."),
        new("phu-am-kep", HangulType.DoubleConsonant, "Phụ âm kép", "Phụ âm căng, đọc mạnh và không bật hơi."),
    ];

    public static HangulGroupInfo? FindBySlug(string? slug) => All.FirstOrDefault(g => g.Slug == slug);
}

/// <summary>Model cho partial <c>_HangulTile</c>.</summary>
/// <param name="SpeakText">Chữ được đọc khi bấm: tên chữ (phụ âm, VD 기역) hoặc âm tiết với ㅇ câm (nguyên âm, VD 아).</param>
public sealed record HangulTileViewModel(string Id, string Char, string Romanization, string? NameKo, string SpeakText, string? Audio)
{
    public static HangulTileViewModel From(HangulLetter letter) => new(
        letter.Id,
        letter.Char,
        letter.Romanization,
        letter.NameKo,
        SpeakTextFor(letter),
        letter.Audio);

    public static string SpeakTextFor(HangulLetter letter) => letter.Type switch
    {
        HangulType.Vowel or HangulType.CompoundVowel => HangulSyllable.VowelSyllable(letter.Char[0]).ToString(),
        _ => letter.NameKo ?? letter.Char,
    };
}

public sealed record HangulGroupViewModel(HangulGroupInfo Info, IReadOnlyList<HangulTileViewModel> Tiles);

public sealed record HangulIndexViewModel(IReadOnlyList<HangulGroupViewModel> Groups)
{
    public int LetterCount => Groups.Sum(g => g.Tiles.Count);

    /// <summary>Âm tiết mặc định của bảng ghép âm (render sẵn ở server, JS cập nhật khi chọn).</summary>
    public ComposedSyllable DefaultComposition { get; } = HangulComposer.TryCompose("ㄱ", "ㅏ").Result!;

    public static HangulIndexViewModel Create(IReadOnlyList<HangulLetter> letters) => new(
        HangulGroupInfo.All
            .Select(info => new HangulGroupViewModel(
                info,
                letters.Where(l => l.Type == info.Type).Select(HangulTileViewModel.From).ToList()))
            .ToList());
}
