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
RUN dotnet restore src/Hanbit.Web/Hanbit.Web.csproj

COPY src/ src/
RUN dotnet publish src/Hanbit.Web/Hanbit.Web.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=10000 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_NOLOGO=true

COPY --from=build /app/publish .
# TODO(T1.x): COPY content/ ./content/ khi có dữ liệu học (ARCHITECTURE §10)

EXPOSE 10000
# User không phải root có sẵn trong image aspnet
USER $APP_UID
ENTRYPOINT ["dotnet", "Hanbit.Web.dll"]
