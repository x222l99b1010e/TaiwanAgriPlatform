// src/api/apiClient.ts
// 職責：建立不帶 JWT 的共用 axios instance，供公開端點使用
// 需要登入、或「公開可讀但登入後回應更豐富」的端點一律改用 authClient
// （它的攔截器是「有 token 才加 header」，未登入時行為等同本 client）

import axios from 'axios'

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

export default apiClient
