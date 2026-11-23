using System.Text;

namespace Hanbit.Domain.Hangul;

/// <summary>
/// Phiên âm Latin theo Revised Romanization of Korean (국어의 로마자 표기법, 2000) — chữ thường, không gạch nối.
/// RR ghi theo CÁCH ĐỌC: áp dụng biến âm giữa các âm tiết liền nhau trong một từ:
/// nối âm (음악 eumak), mũi hóa (국민 gungmin), ㄹ (신라 silla, 종로 jongno, 독립 dongnip),
/// bật hơi với ㅎ (좋고 joko, 잡혀 japyeo), vòm hóa (같이 gachi). Âm căng KHÔNG ghi (학교 hakgyo) — đúng RR.
/// <para>
/// Giới hạn (cần biết ranh giới hình vị/từ ghép, thuật toán theo mặt chữ không xác định được):
/// chèn ㄴ (솜이불 somnibul), ㄴ+ㄹ đọc ㄴㄴ trong từ ghép (신문로 Sinmunno — ở đây ra "sillo" kiểu 신라),
/// ngoại lệ danh từ giữ "h" sau ㄱㄷㅂ (묵호 Mukho — ở đây "muko"), 밟- đọc [밥].
/// Ký tự không phải âm tiết Hangul (khoảng trắng, dấu câu, chữ cái rời ㄱ…) giữ nguyên.
/// </para>
/// </summary>
public static class RomanizationService
{
    private const char None = '\0';

    private enum FinalSound
    {
        None,
        K,
        T,
        P,
        N,
        L,
        M,
        Ng,
    }

    public static string Romanize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var output = new StringBuilder(text.Length * 3);
        var run = new List<Jamo>();
        foreach (var c in text)
        {
            if (HangulSyllable.IsSyllable(c))
            {
                var syllable = HangulSyllable.Decompose(c);
                run.Add(new Jamo(syllable.InitialJamo, syllable.MedialJamo, syllable.FinalJamo));
                continue;
            }

            AppendRun(output, run);
            output.Append(c);
        }

        AppendRun(output, run);
        return output.ToString();
    }

    private static void AppendRun(StringBuilder output, List<Jamo> run)
    {
        if (run.Count == 0)
        {
            return;
        }

        var syllables = run.ToArray();
        run.Clear();

        for (var i = 0; i < syllables.Length - 1; i++)
        {
            ApplyBoundary(ref syllables[i], ref syllables[i + 1]);
        }

        for (var i = 0; i < syllables.Length; i++)
        {
            var previousFinalIsL = i > 0 && Classify(syllables[i - 1].Final) == FinalSound.L;
            output.Append(InitialRoman(syllables[i].Initial, previousFinalIsL));
            output.Append(VowelRoman(syllables[i].Medial));
            output.Append(FinalRoman(Classify(syllables[i].Final)));
        }
    }

    /// <summary>Biến âm giữa phụ âm cuối của âm tiết trước và phụ âm đầu của âm tiết sau.</summary>
    private static void ApplyBoundary(ref Jamo current, ref Jamo next)
    {
        var final = current.Final;
        if (final == None)
        {
            return;
        }

        // 1. Nối âm: phụ âm cuối + ㅇ (câm) → chuyển sang âm tiết sau.
        if (next.Initial == 'ㅇ')
        {
            if (final == 'ㅇ')
            {
                return;
            }

            (current.Final, next.Initial) = final switch
            {
                'ㅎ' => (None, 'ㅇ'),       // 좋아 joa
                'ㄶ' => (None, 'ㄴ'),       // 않아 ana
                'ㅀ' => (None, 'ㄹ'),       // 싫어 sireo
                'ㄳ' => ('ㄱ', 'ㅅ'),
                'ㄵ' => ('ㄴ', 'ㅈ'),       // 앉아 anja
                'ㄺ' => ('ㄹ', 'ㄱ'),       // 읽어 ilgeo
                'ㄻ' => ('ㄹ', 'ㅁ'),       // 젊어 jeolmeo
                'ㄼ' => ('ㄹ', 'ㅂ'),
                'ㄽ' => ('ㄹ', 'ㅅ'),
                'ㄾ' => ('ㄹ', 'ㅌ'),
                'ㄿ' => ('ㄹ', 'ㅍ'),
                'ㅄ' => ('ㅂ', 'ㅅ'),       // 없어 eopseo
                _ => (None, final),         // 음악 eumak
            };

            // Vòm hóa: ㄷ/ㅌ + 이 → 지/치 (굳이 guji, 같이 gachi)
            if (next.Medial == 'ㅣ')
            {
                next.Initial = next.Initial switch
                {
                    'ㄷ' => 'ㅈ',
                    'ㅌ' => 'ㅊ',
                    _ => next.Initial,
                };
            }

            return;
        }

        // 2. ㅎ cuối (ㅎ, ㄶ, ㅀ) + ㄱㄷㅈ → bật hơi (좋고 joko, 많다 manta); + ㄴ → ㄴ (놓는 nonneun).
        if (final is 'ㅎ' or 'ㄶ' or 'ㅀ')
        {
            var rest = final switch
            {
                'ㄶ' => 'ㄴ',
                'ㅀ' => 'ㄹ',
                _ => None,
            };

            if (next.Initial is 'ㄱ' or 'ㄷ' or 'ㅈ')
            {
                current.Final = rest;
                next.Initial = Aspirate(next.Initial);
                return;
            }

            if (final == 'ㅎ' && next.Initial == 'ㄴ')
            {
                current.Final = 'ㄴ';
                return;
            }

            current.Final = rest == None ? 'ㄷ' : rest;
            final = current.Final;
        }

        // 3. Phụ âm cuối ㄱㄷㅂㅈ(ㅅ) + ㅎ → bật hơi (축하 chuka, 잡혀 japyeo, 깨끗하다 kkaekkeutada).
        if (next.Initial == 'ㅎ')
        {
            (char Keep, char Aspirated)? merged = final switch
            {
                'ㄱ' or 'ㄲ' => (None, 'ㅋ'),
                'ㄷ' or 'ㅅ' or 'ㅆ' => (None, 'ㅌ'),
                'ㅂ' => (None, 'ㅍ'),
                'ㅈ' => (None, 'ㅊ'),
                'ㄺ' => ('ㄹ', 'ㅋ'),
                'ㄼ' => ('ㄹ', 'ㅍ'),
                'ㄵ' => ('ㄴ', 'ㅊ'),
                _ => null,
            };

            if (merged is { } m)
            {
                current.Final = m.Keep;
                next.Initial = m.Aspirated;
            }

            return; // ㄴㄹㅁㅇ + ㅎ: giữ nguyên (전화 jeonhwa, 결혼 gyeolhon)
        }

        var sound = Classify(final);

        // ㄺ trước ㄱ đọc [ㄹ] (읽고 ilgo)
        if (final == 'ㄺ' && next.Initial == 'ㄱ')
        {
            current.Final = 'ㄹ';
            return;
        }

        // 4. Phụ âm đầu ㄹ sau phụ âm cuối
        if (next.Initial == 'ㄹ')
        {
            switch (sound)
            {
                case FinalSound.L:
                    return;                 // 물론 mullon (ㄹ đọc l — xử lý khi ghép chữ)
                case FinalSound.N:
                    current.Final = 'ㄹ';   // 신라 silla
                    return;
                default:
                    next.Initial = 'ㄴ';    // 종로 jongno, 심리 simni, 독립 dongnip (→ mũi hóa ở bước 5)
                    break;
            }
        }

        // 5. ㄹ cuối + ㄴ → ㄹㄹ (설날 seollal); ㄱㄷㅂ (âm đại diện) + ㄴㅁ → mũi hóa (국민 gungmin, 합니다 hamnida).
        if (next.Initial == 'ㄴ' && sound == FinalSound.L)
        {
            next.Initial = 'ㄹ';
            return;
        }

        if (next.Initial is 'ㄴ' or 'ㅁ')
        {
            current.Final = sound switch
            {
                FinalSound.K => 'ㅇ',
                FinalSound.T => 'ㄴ',
                FinalSound.P => 'ㅁ',
                _ => current.Final,
            };
        }
    }

    private static char Aspirate(char initial) => initial switch
    {
        'ㄱ' => 'ㅋ',
        'ㄷ' => 'ㅌ',
        'ㅈ' => 'ㅊ',
        _ => initial,
    };

    /// <summary>7 âm đại diện của phụ âm cuối.</summary>
    private static FinalSound Classify(char final) => final switch
    {
        None => FinalSound.None,
        'ㄱ' or 'ㄲ' or 'ㅋ' or 'ㄳ' or 'ㄺ' => FinalSound.K,
        'ㄷ' or 'ㅅ' or 'ㅆ' or 'ㅈ' or 'ㅊ' or 'ㅌ' or 'ㅎ' => FinalSound.T,
        'ㅂ' or 'ㅍ' or 'ㅄ' or 'ㄿ' => FinalSound.P,
        'ㄴ' or 'ㄵ' or 'ㄶ' => FinalSound.N,
        'ㄹ' or 'ㄼ' or 'ㄽ' or 'ㄾ' or 'ㅀ' => FinalSound.L,
        'ㅁ' or 'ㄻ' => FinalSound.M,
        'ㅇ' => FinalSound.Ng,
        _ => throw new ArgumentOutOfRangeException(nameof(final), final, "Phụ âm cuối không hợp lệ."),
    };

    private static string InitialRoman(char initial, bool previousFinalIsL) => initial switch
    {
        'ㄱ' => "g",
        'ㄲ' => "kk",
        'ㄴ' => "n",
        'ㄷ' => "d",
        'ㄸ' => "tt",
        'ㄹ' => previousFinalIsL ? "l" : "r",
        'ㅁ' => "m",
        'ㅂ' => "b",
        'ㅃ' => "pp",
        'ㅅ' => "s",
        'ㅆ' => "ss",
        'ㅇ' => string.Empty,
        'ㅈ' => "j",
        'ㅉ' => "jj",
        'ㅊ' => "ch",
        'ㅋ' => "k",
        'ㅌ' => "t",
        'ㅍ' => "p",
        'ㅎ' => "h",
        _ => throw new ArgumentOutOfRangeException(nameof(initial), initial, "Phụ âm đầu không hợp lệ."),
    };

    private static string VowelRoman(char medial) => medial switch
    {
        'ㅏ' => "a",
        'ㅐ' => "ae",
        'ㅑ' => "ya",
        'ㅒ' => "yae",
        'ㅓ' => "eo",
        'ㅔ' => "e",
        'ㅕ' => "yeo",
        'ㅖ' => "ye",
        'ㅗ' => "o",
        'ㅘ' => "wa",
        'ㅙ' => "wae",
        'ㅚ' => "oe",
        'ㅛ' => "yo",
        'ㅜ' => "u",
        'ㅝ' => "wo",
        'ㅞ' => "we",
        'ㅟ' => "wi",
        'ㅠ' => "yu",
        'ㅡ' => "eu",
        'ㅢ' => "ui",
        'ㅣ' => "i",
        _ => throw new ArgumentOutOfRangeException(nameof(medial), medial, "Nguyên âm không hợp lệ."),
    };

    private static string FinalRoman(FinalSound sound) => sound switch
    {
        FinalSound.None => string.Empty,
        FinalSound.K => "k",
        FinalSound.T => "t",
        FinalSound.P => "p",
        FinalSound.N => "n",
        FinalSound.L => "l",
        FinalSound.M => "m",
        FinalSound.Ng => "ng",
        _ => throw new ArgumentOutOfRangeException(nameof(sound), sound, null),
    };

    private struct Jamo(char initial, char medial, char final)
    {
        public char Initial = initial;
        public char Medial = medial;
        public char Final = final;
    }
}
