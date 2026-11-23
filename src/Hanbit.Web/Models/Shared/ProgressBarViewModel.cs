namespace Hanbit.Web.Models.Shared;

/// <summary>Model cho partial <c>_ProgressBar</c>.</summary>
public sealed record ProgressBarViewModel(int Value, int Max, string Label)
{
    public int Percent => Max <= 0 ? 0 : (int)Math.Round(Math.Clamp(Value, 0, Max) * 100.0 / Max);
}
