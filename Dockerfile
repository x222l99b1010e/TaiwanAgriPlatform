# 後端三個映像共用這一份，用 --target 選要建哪一個：
#   web       API（TaiwanAgri.Web）
#   worker    農業部資料同步（TaiwanAgri.Worker），常駐
#   migrator  建表：依序套用七個 DbContext 的 migration，套完就結束
# 三者共用同一個 build 階段，方案只還原、編譯一次。前端的映像在 TaiwanAgri.Frontend/Dockerfile。

# ── 共用建置階段：SDK 映像裡還原與編譯，產物再搬進小的執行映像 ──────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先只複製專案檔做還原：之後只改程式碼、沒動套件時，這一層直接取用快取
COPY TaiwanAgri.Core/TaiwanAgri.Core.csproj TaiwanAgri.Core/
COPY TaiwanAgri.Modules.FoodSafety/TaiwanAgri.Modules.FoodSafety.csproj TaiwanAgri.Modules.FoodSafety/
COPY TaiwanAgri.Modules.Market/TaiwanAgri.Modules.Market.csproj TaiwanAgri.Modules.Market/
COPY TaiwanAgri.Modules.Pet/TaiwanAgri.Modules.Pet.csproj TaiwanAgri.Modules.Pet/
COPY TaiwanAgri.Modules.User/TaiwanAgri.Modules.User.csproj TaiwanAgri.Modules.User/
COPY TaiwanAgri.Modules.Weather/TaiwanAgri.Modules.Weather.csproj TaiwanAgri.Modules.Weather/
COPY TaiwanAgri.Web/TaiwanAgri.Web.csproj TaiwanAgri.Web/
COPY TaiwanAgri.Worker/TaiwanAgri.Worker.csproj TaiwanAgri.Worker/
RUN dotnet restore TaiwanAgri.Web/TaiwanAgri.Web.csproj \
 && dotnet restore TaiwanAgri.Worker/TaiwanAgri.Worker.csproj

# 只複製後端的專案資料夾：改前端或測試不會讓這一層之後的快取失效
COPY TaiwanAgri.Core/ TaiwanAgri.Core/
COPY TaiwanAgri.Modules.FoodSafety/ TaiwanAgri.Modules.FoodSafety/
COPY TaiwanAgri.Modules.Market/ TaiwanAgri.Modules.Market/
COPY TaiwanAgri.Modules.Pet/ TaiwanAgri.Modules.Pet/
COPY TaiwanAgri.Modules.User/ TaiwanAgri.Modules.User/
COPY TaiwanAgri.Modules.Weather/ TaiwanAgri.Modules.Weather/
COPY TaiwanAgri.Web/ TaiwanAgri.Web/
COPY TaiwanAgri.Worker/ TaiwanAgri.Worker/

# 在這裡編譯一次；下面三個階段都用 --no-build 直接拿這份結果，不各自重編
RUN dotnet build TaiwanAgri.Web/TaiwanAgri.Web.csproj -c Release --no-restore \
 && dotnet build TaiwanAgri.Worker/TaiwanAgri.Worker.csproj -c Release --no-restore

FROM build AS web-publish
RUN dotnet publish TaiwanAgri.Web/TaiwanAgri.Web.csproj -c Release -o /out --no-build

FROM build AS worker-publish
RUN dotnet publish TaiwanAgri.Worker/TaiwanAgri.Worker.csproj -c Release -o /out --no-build

# migration bundle：把一個 DbContext 的 migration 打包成一支可執行檔，執行時才連資料庫。
# 檔名前的序號就是套用順序（同 README 手動建表的順序），migrate.sh 照檔名排序執行。
# 七個都以 Web 當啟動專案：只有 Web 註冊了全部七個 DbContext
FROM build AS migrator-bundles
RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.9
# 打包時 EF 借用 Web 的啟動流程產生 DbContext，而啟動流程會先檢查連線字串與 JWT 設定、缺了就中止。
# 這裡是只為通過檢查的佔位值：打包不連資料庫，這個階段也不會進最終映像。
# 實際的值在執行建表容器時由 compose 的環境變數提供
ENV ConnectionStrings__DefaultConnection="Server=placeholder;Database=placeholder" \
    Jwt__SecretKey="placeholder-for-design-time-only-not-a-secret" \
    Jwt__Issuer="placeholder" \
    Jwt__Audience="placeholder" \
    Jwt__ExpiresInDays="1"
RUN /tools/dotnet-ef migrations bundle --configuration Release --no-build --startup-project TaiwanAgri.Web --project TaiwanAgri.Web --context ApplicationDbContext -o /bundles/01-ApplicationDbContext
RUN /tools/dotnet-ef migrations bundle --configuration Release --no-build --startup-project TaiwanAgri.Web --project TaiwanAgri.Modules.Weather --context WeatherDbContext -o /bundles/02-WeatherDbContext
RUN /tools/dotnet-ef migrations bundle --configuration Release --no-build --startup-project TaiwanAgri.Web --project TaiwanAgri.Modules.Market --context MarketDbContext -o /bundles/03-MarketDbContext
RUN /tools/dotnet-ef migrations bundle --configuration Release --no-build --startup-project TaiwanAgri.Web --project TaiwanAgri.Core --context CoreDbContext -o /bundles/04-CoreDbContext
RUN /tools/dotnet-ef migrations bundle --configuration Release --no-build --startup-project TaiwanAgri.Web --project TaiwanAgri.Modules.User --context UserDbContext -o /bundles/05-UserDbContext
RUN /tools/dotnet-ef migrations bundle --configuration Release --no-build --startup-project TaiwanAgri.Web --project TaiwanAgri.Modules.FoodSafety --context FoodSafetyDbContext -o /bundles/06-FoodSafetyDbContext
RUN /tools/dotnet-ef migrations bundle --configuration Release --no-build --startup-project TaiwanAgri.Web --project TaiwanAgri.Modules.Pet --context PetDbContext -o /bundles/07-PetDbContext

# ── web ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
WORKDIR /app
COPY --from=web-publish /out .
# 映像內建的非 root 帳號；容器內聽 8080（.NET 8 起的預設埠）
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaiwanAgri.Web.dll"]

# ── worker ──────────────────────────────────────────────────────────
# 執行映像用 aspnet 不用 runtime：發布產物的 runtimeconfig.json 列了 Microsoft.AspNetCore.App，
# 用 runtime 映像要到啟動時才會因為找不到框架而失敗
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
WORKDIR /app
COPY --from=worker-publish /out .
# Serilog 寫檔到 /app/logs；非 root 帳號對 /app 沒有寫入權，先建好資料夾交給它
RUN mkdir logs && chown "$APP_UID" logs
USER $APP_UID
ENTRYPOINT ["dotnet", "TaiwanAgri.Worker.dll"]

# ── migrator ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /migrations
COPY --from=migrator-bundles /bundles .
COPY --chmod=755 <<'EOF' migrate.sh
#!/bin/sh
# 任何一個失敗就停下並以非 0 結束，compose 因此不會啟動 web
set -e
for bundle in /migrations/[0-9]*; do
  echo "[migrate] $(basename "$bundle")"
  "$bundle"
done
echo "[migrate] 七個 DbContext 都已是最新"
EOF
USER $APP_UID
ENTRYPOINT ["./migrate.sh"]
