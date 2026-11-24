namespace Hanbit.Web.Models;

/// <summary>Model cho trang lỗi theo mã HTTP (<c>Views/Home/StatusCode.cshtml</c>).</summary>
public sealed record StatusCodeViewModel(int StatusCode)
{
    public bool IsNotFound => StatusCode == 404;

    public string Title => IsNotFound ? "Không tìm thấy trang" : "Đã xảy ra lỗi";

    public string Message => IsNotFound
        ? "Trang này chưa có hoặc đang được xây dựng. Bạn quay lại trang chủ hoặc chọn mục khác trong menu nhé."
        : "Rất tiếc, có lỗi khi xử lý yêu cầu của bạn. Vui lòng thử lại sau.";
}
