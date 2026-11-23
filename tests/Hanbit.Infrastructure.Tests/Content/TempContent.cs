namespace Hanbit.Infrastructure.Tests.Content;

/// <summary>Thư mục content/ tạm cho test; xóa khi Dispose.</summary>
internal sealed class TempContent : IDisposable
{
    public const string Hangul = """
        [ { "id": "c-giyeok", "char": "ㄱ", "type": "consonant", "romanization": "g/k", "name_ko": "기역",
            "vi_hint": "x", "examples": [ { "ko": "가방", "romanization": "gabang", "vi": "cái cặp" } ] } ]
        """;

    public const string Vocab = """
        [
          { "id": "kd-1", "ko": "물", "vi": "nước", "pos": "noun", "grade": "beginner", "examples": [], "reviewed": true,
            "source": { "name": "한국어기초사전", "id": "1", "url": "https://krdict.korean.go.kr/", "license": "CC BY-SA 2.0 KR" } },
          { "id": "kd-2", "ko": "불", "vi": "lửa", "pos": "noun", "grade": "beginner", "examples": [], "reviewed": false,
            "source": { "name": "한국어기초사전", "id": "2", "url": "https://krdict.korean.go.kr/", "license": "CC BY-SA 2.0 KR" } }
        ]
        """;

    public const string Grammar = """
        [ { "id": "g-yo", "pattern": "-요", "meaning_vi": "Đuôi lịch sự", "explanation_vi": "x",
            "examples": [ { "ko": "물이에요.", "vi": "Là nước." } ], "grade": "beginner" } ]
        """;

    public const string Course = """
        { "slug": "beginner", "title": "Sơ cấp", "level": "beginner", "description": "x",
          "lessons": [ { "slug": "01-nuoc", "title": "Nước", "order": 1 } ] }
        """;

    public const string Lesson = """
        { "slug": "01-nuoc", "title": "Nước", "order": 1, "vocab_ids": ["kd-1"], "grammar_ids": ["g-yo"],
          "exercises": [ { "type": "multiple_choice", "id": "e1", "prompt": "물?", "choices": ["nước", "lửa"], "answer_index": 0 } ] }
        """;

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("hanbit-content-");

    /// <summary>Tạo bộ nội dung hợp lệ tối thiểu (5 file).</summary>
    public TempContent()
    {
        Write("hangul.json", Hangul);
        Write("vocab/krdict-beginner.json", Vocab);
        Write("grammar/beginner.json", Grammar);
        Write("courses/beginner/course.json", Course);
        Write("courses/beginner/lessons/01-nuoc.json", Lesson);
    }

    public string Root => _root.FullName;

    public string Name => _root.Name;

    public void Write(string relativePath, string json)
    {
        var path = Path.Combine(_root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    public void Delete(string relativePath) => File.Delete(Path.Combine(_root.FullName, relativePath));

    public void Dispose() => _root.Delete(recursive: true);
}
