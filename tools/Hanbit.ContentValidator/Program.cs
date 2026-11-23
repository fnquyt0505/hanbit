// Kiểm tra dữ liệu nội dung: dotnet run --project tools/Hanbit.ContentValidator -- ./content
// Exit code: 0 = hợp lệ, 1 = có lỗi (CI dùng để chặn dữ liệu hỏng).
using System.Text;

using Hanbit.Infrastructure.Content.Validation;

Console.OutputEncoding = Encoding.UTF8;

var contentRoot = args.Length > 0 ? args[0] : "content";

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var result = await new ContentValidationService().ValidateAsync(contentRoot, cancellation.Token);

if (result.IsValid)
{
    Console.WriteLine($"✔ Nội dung hợp lệ ({result.FileCount} file JSON trong '{contentRoot}').");
    return 0;
}

foreach (var issue in result.Issues)
{
    Console.Error.WriteLine($"✖ {issue}");
}

Console.Error.WriteLine($"{result.Issues.Count} lỗi trong {result.FileCount} file JSON.");
return 1;
