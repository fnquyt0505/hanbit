namespace Hanbit.Domain.Content;

/// <summary>Tên từ loại tiếng Việt (hiển thị trong bài học, CSV duyệt nghĩa).</summary>
public static class PartOfSpeechNames
{
    public static string Vietnamese(PartOfSpeech pos) => pos switch
    {
        PartOfSpeech.Noun => "danh từ",
        PartOfSpeech.DependentNoun => "danh từ phụ thuộc",
        PartOfSpeech.Pronoun => "đại từ",
        PartOfSpeech.Numeral => "số từ",
        PartOfSpeech.Verb => "động từ",
        PartOfSpeech.AuxiliaryVerb => "trợ động từ",
        PartOfSpeech.Adjective => "tính từ",
        PartOfSpeech.AuxiliaryAdjective => "trợ tính từ",
        PartOfSpeech.Determiner => "định từ",
        PartOfSpeech.Adverb => "phó từ",
        PartOfSpeech.Interjection => "thán từ",
        PartOfSpeech.Particle => "trợ từ",
        PartOfSpeech.Ending => "vĩ tố",
        PartOfSpeech.Affix => "phụ tố",
        _ => "không xác định",
    };
}
