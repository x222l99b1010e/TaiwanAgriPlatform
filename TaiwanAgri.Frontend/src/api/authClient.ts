// src/api/authClient.ts
// 職責：建立帶有 JWT token 的 axios instance，供需要驗證的 API 使用

import axios from 'axios'

const authClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

// 攔截器：每次請求發出前，自動從 localStorage 取 token 並加到 header
authClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export default authClient