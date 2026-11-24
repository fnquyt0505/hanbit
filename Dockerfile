# Hanbit — image chạy trên Render (Web Service, runtime Docker). Build: docker build -t hanbit .

# ---------- build ----------
# SDK khớp global.json (10.0.401). Tailwind CLI (linux) được Tailwind.targets tải + kiểm SHA-256 lúc build.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src

# Restore trước (cache layer theo csproj)
COPY global.json Directory.Build.props ./
COPY src/Hanbit.Domain/Hanbit.Domain.csproj src/Hanbit.Domain/
COPY src/Hanbit.Application/Hanbit.Application.csproj src/Hanbit.Application/
COPY src/Hanbit.Infrastructure/Hanbit.Infrastructure.csproj src/Hanbit.Infrastructure/
COPY src/Hanbit.Web/Hanbit.Web.csproj src/Hanbit.Web/Tailwind.targets src/Hanbit.Web/
# -r linux-x64: cần cho ReadyToRun (biên dịch sẵn mã máy → khởi động nhanh hơn sau khi Render đánh thức — T7.6)
RUN dotnet restore src/Hanbit.Web/Hanbit.Web.csproj -r linux-x64

COPY src/ src/
RUN dotnet publish src/Hanbit.Web/Hanbit.Web.csproj -c Release -o /app/publish --no-restore \
    -r linux-x64 --self-contained false -p:PublishReadyToRun=true

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=10000 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_NOLOGO=true

COPY --from=build /app/publish .
# Dữ liệu học (ARCHITECTURE §10) — appsettings.json: Content:RootPath = "content" → /app/content
COPY content/ ./content/

EXPOSE 10000
# User không phải root có sẵn trong image aspnet
USER $APP_UID
ENTRYPOINT ["dotnet", "Hanbit.Web.dll"]
