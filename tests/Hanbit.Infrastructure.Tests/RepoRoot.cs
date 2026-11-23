namespace Hanbit.Infrastructure.Tests;

/// <summary>Tìm thư mục gốc repo (chứa Hanbit.sln) để test đọc dữ liệu thật trong content/.</summary>
internal static class RepoRoot
{
    public static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Hanbit.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục chứa Hanbit.sln.");
    }
}
