// src/api/auth.ts
// 職責：封裝所有對後端 /api/auth/* 的 HTTP 呼叫

import axios from 'axios'

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

// ─── DTO 型別 ───────────────────────────────────────────

export interface LoginRequestDto {
  email: string
  password: string
}

export interface RegisterRequestDto {
  email: string
  password: string
  displayName?: string
  userType?: string
}

export interface AuthResponseDto {
  token: string
  email: string
  displayName?: string
  role: string
}

// ─── API 呼叫函式 ────────────────────────────────────────

export const authApi = {
  login(data: LoginRequestDto): Promise<AuthResponseDto> {
    return apiClient
      .post<AuthResponseDto>('/api/auth/login', data)
      .then(res => res.data)
  },

  register(data: RegisterRequestDto): Promise<AuthResponseDto> {
    return apiClient
      .post<AuthResponseDto>('/api/auth/register', data)
      .then(res => res.data)
  },
}