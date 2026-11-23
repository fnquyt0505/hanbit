var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Render (và Docker local) kết thúc TLS ở proxy rồi chuyển tiếp HTTP vào container:
// ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (Dockerfile) bật X-Forwarded-For/Proto để Request.IsHttps đúng.
// Không dùng UseHttpsRedirection: proxy của Render đã ép HTTPS, còn health check nội bộ đi bằng HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRouting();

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
