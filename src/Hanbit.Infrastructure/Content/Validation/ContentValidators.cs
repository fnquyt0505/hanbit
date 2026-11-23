using FluentValidation;

using Hanbit.Domain.Content;

namespace Hanbit.Infrastructure.Content.Validation;

// Validator FluentValidation cho từng loại nội dung (kiểm tra trong phạm vi 1 đối tượng).
// Kiểm tra chéo giữa các file (id tồn tại, reviewed, mục lục khóa học) nằm ở ContentValidationService.

public sealed class ExampleValidator : AbstractValidator<Example>
{
    public ExampleValidator()
    {
        RuleFor(x => x.Ko).Required().ContainsHangul();
        RuleFor(x => x.Vi).Required();
    }
}

public sealed class HangulExampleValidator : AbstractValidator<HangulExample>
{
    public HangulExampleValidator()
    {
        RuleFor(x => x.Ko).Required().ContainsHangul();
        RuleFor(x => x.Romanization).Required();
        RuleFor(x => x.Vi).Required();
    }
}

public sealed class HangulLetterValidator : AbstractValidator<HangulLetter>
{
    public HangulLetterValidator()
    {
        RuleFor(x => x.Id).Required().Slug();
        RuleFor(x => x.Char)
            .Must(c => c.Length == 1 && c[0] is >= 'ㄱ' and <= 'ㅣ')
            .WithMessage("phải là đúng 1 chữ cái Hangul (ㄱ…ㅣ).");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Romanization).Required();
        RuleFor(x => x.ViHint).Required();
        RuleFor(x => x.NameKo).NotBlankWhenPresent()
            .Must(RuleExtensions.HasHangul).When(x => x.NameKo is not null).WithMessage("phải chứa chữ Hangul.");
        RuleFor(x => x.Audio).NotBlankWhenPresent();
        RuleFor(x => x.Examples).MinCount(1);
        RuleForEach(x => x.Examples).SetValidator(new HangulExampleValidator());
    }
}

public sealed class SourceRefValidator : AbstractValidator<SourceRef>
{
    public SourceRefValidator()
    {
        RuleFor(x => x.Name).Required();
        RuleFor(x => x.Id).Required();
        RuleFor(x => x.License).Required();
        RuleFor(x => x.Url)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            .WithMessage("phải là URL tuyệt đối http(s).");
    }
}

public sealed class VocabItemValidator : AbstractValidator<VocabItem>
{
    public VocabItemValidator()
    {
        RuleFor(x => x.Id).Required().Slug();
        RuleFor(x => x.Ko).Required().ContainsHangul();
        RuleFor(x => x.Vi).Required();
        RuleFor(x => x.Pos).IsInEnum();
        RuleFor(x => x.Grade).IsInEnum();
        RuleForEach(x => x.Examples).SetValidator(new ExampleValidator());
        RuleFor(x => x.Source).SetValidator(new SourceRefValidator());
        RuleFor(x => x.Pronunciation).NotBlankWhenPresent();
        RuleFor(x => x.Romanization).NotBlankWhenPresent();
        RuleFor(x => x.DefinitionVi).NotBlankWhenPresent();
        RuleFor(x => x.Hanja).NotBlankWhenPresent();
        RuleFor(x => x.Audio).NotBlankWhenPresent();
    }
}

public sealed class GrammarPointValidator : AbstractValidator<GrammarPoint>
{
    public GrammarPointValidator()
    {
        RuleFor(x => x.Id).Required().Slug();
        RuleFor(x => x.Pattern).Required().ContainsHangul();
        RuleFor(x => x.MeaningVi).Required();
        RuleFor(x => x.ExplanationVi).Required();
        RuleFor(x => x.Grade).IsInEnum();
        RuleFor(x => x.Examples).MinCount(1);
        RuleForEach(x => x.Examples).SetValidator(new ExampleValidator());
    }
}

public sealed class LessonSummaryValidator : AbstractValidator<LessonSummary>
{
    public LessonSummaryValidator()
    {
        RuleFor(x => x.Slug).Required().Slug();
        RuleFor(x => x.Title).Required();
        RuleFor(x => x.Order).GreaterThan(0).WithMessage("phải lớn hơn 0.");
    }
}

public sealed class CourseValidator : AbstractValidator<Course>
{
    public CourseValidator()
    {
        RuleFor(x => x.Slug).Required().Slug();
        RuleFor(x => x.Title).Required();
        RuleFor(x => x.Description).Required();
        RuleFor(x => x.Level).IsInEnum();
        RuleFor(x => x.Lessons).MinCount(1);
        RuleFor(x => x.Lessons)
            .Must(lessons => lessons.Select(l => l.Slug).Distinct().Count() == lessons.Count)
            .WithMessage("có slug bài học bị trùng.");
        RuleFor(x => x.Lessons)
            .Must(lessons => lessons.Select(l => l.Order).Distinct().Count() == lessons.Count)
            .WithMessage("có order bài học bị trùng.");
        RuleForEach(x => x.Lessons).SetValidator(new LessonSummaryValidator());
    }
}

public sealed class LessonValidator : AbstractValidator<Lesson>
{
    public LessonValidator()
    {
        RuleFor(x => x.Slug).Required().Slug();
        RuleFor(x => x.Title).Required();
        RuleFor(x => x.Order).GreaterThan(0).WithMessage("phải lớn hơn 0.");
        RuleFor(x => x.VocabIds).Distinct();
        RuleForEach(x => x.VocabIds).Required();
        RuleFor(x => x.GrammarIds).Distinct();
        RuleForEach(x => x.GrammarIds).Required();
        RuleFor(x => x.Exercises)
            .Must(exercises => exercises.Select(e => e.Id).Distinct().Count() == exercises.Count)
            .WithMessage("có id bài tập bị trùng.");
        RuleForEach(x => x.Exercises).SetInheritanceValidator(v =>
        {
            v.Add(new MultipleChoiceExerciseValidator());
            v.Add(new ListenChooseExerciseValidator());
            v.Add(new MatchPairsExerciseValidator());
            v.Add(new ReorderExerciseValidator());
            v.Add(new TypeAnswerExerciseValidator());
        });
    }
}

/// <summary>Quy tắc chung mọi loại bài tập.</summary>
public abstract class ExerciseValidator<T> : AbstractValidator<T>
    where T : Exercise
{
    protected ExerciseValidator()
    {
        RuleFor(x => x.Id).Required().Slug();
        RuleFor(x => x.ExplanationVi).NotBlankWhenPresent();
    }
}

public sealed class MultipleChoiceExerciseValidator : ExerciseValidator<MultipleChoiceExercise>
{
    public MultipleChoiceExerciseValidator()
    {
        RuleFor(x => x.Prompt).Required();
        RuleFor(x => x.Choices).MinCount(2).Distinct();
        RuleForEach(x => x.Choices).Required();
        RuleFor(x => x.AnswerIndex)
            .Must((exercise, index) => index >= 0 && index < exercise.Choices.Count)
            .WithMessage(x => $"phải nằm trong khoảng 0…{x.Choices.Count - 1} (số lựa chọn: {x.Choices.Count}).");
    }
}

public sealed class ListenChooseExerciseValidator : ExerciseValidator<ListenChooseExercise>
{
    public ListenChooseExerciseValidator()
    {
        RuleFor(x => x.AudioKo).Required().ContainsHangul();
        RuleFor(x => x.Choices).MinCount(2).Distinct();
        RuleForEach(x => x.Choices).Required();
        RuleFor(x => x.AnswerIndex)
            .Must((exercise, index) => index >= 0 && index < exercise.Choices.Count)
            .WithMessage(x => $"phải nằm trong khoảng 0…{x.Choices.Count - 1} (số lựa chọn: {x.Choices.Count}).");
    }
}

public sealed class MatchPairsExerciseValidator : ExerciseValidator<MatchPairsExercise>
{
    public MatchPairsExerciseValidator()
    {
        RuleFor(x => x.Prompt).Required();
        RuleFor(x => x.Pairs).MinCount(2);
        RuleFor(x => x.Pairs)
            .Must(pairs => pairs.Select(p => p.Left).Distinct().Count() == pairs.Count)
            .WithMessage("vế trái (left) bị trùng.");
        RuleFor(x => x.Pairs)
            .Must(pairs => pairs.Select(p => p.Right).Distinct().Count() == pairs.Count)
            .WithMessage("vế phải (right) bị trùng.");
        RuleForEach(x => x.Pairs).ChildRules(pair =>
        {
            pair.RuleFor(p => p.Left).Required();
            pair.RuleFor(p => p.Right).Required();
        });
    }
}

public sealed class ReorderExerciseValidator : ExerciseValidator<ReorderExercise>
{
    public ReorderExerciseValidator()
    {
        RuleFor(x => x.Prompt).Required();
        RuleFor(x => x.Tokens).MinCount(2);
        RuleForEach(x => x.Tokens).Required();
    }
}

public sealed class TypeAnswerExerciseValidator : ExerciseValidator<TypeAnswerExercise>
{
    public TypeAnswerExerciseValidator()
    {
        RuleFor(x => x.Prompt).Required();
        RuleFor(x => x.AcceptedAnswers).MinCount(1).Distinct();
        RuleForEach(x => x.AcceptedAnswers).Required();
    }
}
