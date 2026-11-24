using Hanbit.Infrastructure;
using Hanbit.Infrastructure.Content;
using Hanbit.Web.HealthChecks;
using Hanbit.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));
builder.Services.AddHealthChecks()
    .AddCheck<ContentHealthCheck>("content");

// Nội dung học (content/): đường dẫn tương đối tính từ ContentRootPath
// (dev: src/Hanbit.Web → ../../content; Docker: /app → content).
builder.Services.Configure<ContentOptions>(builder.Configuration.GetSection(ContentOptions.SectionName));
builder.Services.PostConfigure<ContentOptions>(options =>
    options.RootPath = Path.GetFullPath(options.RootPath, builder.Environment.ContentRootPath));
builder.Services.AddInfrastructure();
builder.Services.AddScoped<Hanbit.Application.Lessons.LessonService>();
builder.Services.AddScoped<Hanbit.Application.Lessons.SubmitLessonHandler>();
builder.Services.AddScoped<Hanbit.Application.Progress.ProgressService>();
builder.Services.AddScoped<Hanbit.Application.Progress.ProgressImporter>();
builder.Services.AddScoped<Hanbit.Application.Reviews.ReviewService>();

// T7.3: rate limiting /api/*; HTML không mã hóa ký tự tiếng Việt/Hàn thành &#x…; (giảm dung lượng, dễ đọc).
builder.Services.AddHttpHardening(builder.Configuration);
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

var app = builder.Build();

// Render (và Docker local) kết thúc TLS ở proxy rồi chuyển tiếp HTTP vào container:
// ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (Dockerfile) bật X-Forwarded-For/Proto để Request.IsHttps đúng.
// Không dùng UseHttpsRedirection: proxy của Render đã ép HTTPS, còn health check nội bộ đi bằng HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Lỗi 4xx/5xx không có nội dung (VD 404 do không khớp route) → hiển thị trang lỗi trong layout,
// giữ nguyên mã lỗi. /api/* trả lỗi dạng API, không trả HTML.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/loi/{0}"));

app.UseSecurityHeaders();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
