// src/api/authClient.ts
// 職責：帶 JWT token 的 axios instance，供需要驗證的 API 使用。
// 共用設定（timeout、401 統一處理）在 httpBase。

import { createHttpClient } from './httpBase'

const authClient = createHttpClient({ withAuth: true })

export default authClient
