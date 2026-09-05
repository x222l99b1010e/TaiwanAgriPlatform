// src/stores/authStore.ts
// 職責：管理登入狀態，儲存 token 和使用者資訊

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/api/auth'
import { TOKEN_STORAGE_KEY, USER_STORAGE_KEY } from '@/api/httpBase'
import type { LoginRequestDto, RegisterRequestDto, AuthResponseDto } from '@/api/auth'

export const useAuthStore = defineStore('auth', () => {
  // ─── 狀態 ────────────────────────────────────────────

  // token 存在 localStorage，讓瀏覽器重新整理後仍然保持登入
  const token = ref<string | null>(localStorage.getItem(TOKEN_STORAGE_KEY))
  const user = ref<Omit<AuthResponseDto, 'token'> | null>(
    JSON.parse(localStorage.getItem(USER_STORAGE_KEY) ?? 'null')
  )

  // ─── computed ─────────────────────────────────────────

  const isLoggedIn = computed(() => !!token.value)
  const displayName = computed(() => user.value?.displayName ?? user.value?.email ?? '')
  const role = computed(() => user.value?.role ?? 'Guest')

  // ─── 動作 ─────────────────────────────────────────────

  function saveAuth(res: AuthResponseDto) {
    token.value = res.token
    user.value = { email: res.email, displayName: res.displayName, role: res.role }
    // 存到 localStorage，重整後還在
    localStorage.setItem(TOKEN_STORAGE_KEY, res.token)
    localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user.value))
  }

  async function login(data: LoginRequestDto) {
    const res = await authApi.login(data)
    saveAuth(res)
  }

  async function register(data: RegisterRequestDto) {
    const res = await authApi.register(data)
    saveAuth(res)
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem(TOKEN_STORAGE_KEY)
    localStorage.removeItem(USER_STORAGE_KEY)
  }

  return {
    token,
    user,
    isLoggedIn,
    displayName,
    role,
    login,
    register,
    logout,
  }
})