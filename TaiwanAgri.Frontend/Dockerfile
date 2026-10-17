# 前端映像：Node 建置 → nginx 送出建置結果，並把 /api 與 /health 轉給後端容器。
# 建置內容要從 repo 根目錄送（compose 的 context: .）：建置時的圖示子集化外掛會讀後端的種子檔
# （TaiwanAgri.Core/Infrastructure/DbInitializer.cs 與 docs/sql），只送前端資料夾會少這兩處

FROM node:22-slim AS build
WORKDIR /src/TaiwanAgri.Frontend

# 先只複製套件清單安裝依賴：沒動套件時這一層取用快取
COPY TaiwanAgri.Frontend/package.json TaiwanAgri.Frontend/package-lock.json ./
RUN npm ci

COPY TaiwanAgri.Frontend/ ./
COPY TaiwanAgri.Core/Infrastructure/DbInitializer.cs /src/TaiwanAgri.Core/Infrastructure/
COPY docs/sql/ /src/docs/sql/
# 不設 VITE_API_BASE_URL：API 走相對路徑 /api，跟頁面同一個來源，由下面的 nginx 轉給後端
RUN npm run build

# 非 root 版本的 nginx，聽 8080
FROM nginxinc/nginx-unprivileged:stable-alpine AS frontend
COPY TaiwanAgri.Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/TaiwanAgri.Frontend/dist /usr/share/nginx/html
EXPOSE 8080
