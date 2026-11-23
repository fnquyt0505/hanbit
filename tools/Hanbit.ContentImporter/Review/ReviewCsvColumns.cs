namespace Hanbit.ContentImporter.Review;

/// <summary>Cột của file CSV duyệt nghĩa (tiêu đề không dấu để Excel/Sheets ở mọi vùng đọc ổn định).</summary>
public static class ReviewCsvColumns
{
    public const string Id = "id";
    public const string Topic = "chu_de";
    public const string Korean = "tieng_han";
    public const string Romanization = "phien_am";
    public const string PartOfSpeech = "tu_loai";
    public const string Hanja = "han_tu";
    public const string KrdictMeaning = "nghia_krdict";
    public const string KrdictDefinition = "dinh_nghia_krdict";

    /// <summary>Người duyệt điền: nghĩa thay thế (để trống = giữ nghĩa krdict).</summary>
    public const string NewMeaning = "nghia_moi";

    /// <summary>Người duyệt điền: định nghĩa thay thế (để trống = giữ nguyên).</summary>
    public const string NewDefinition = "dinh_nghia_moi";

    /// <summary>Người duyệt điền: x = duyệt (cho vào bài học), loai = không dùng, trống = chưa quyết.</summary>
    public const string Decision = "duyet";

    /// <summary>Người duyệt điền: ghi chú nội bộ (tùy chọn).</summary>
    public const string Note = "ghi_chu";

    public const string Link = "link_krdict";

    public static readonly IReadOnlyList<string> All =
        [Id, Topic, Korean, Romanization, PartOfSpeech, Hanja, KrdictMeaning, KrdictDefinition, NewMeaning, NewDefinition, Decision, Note, Link];
}
