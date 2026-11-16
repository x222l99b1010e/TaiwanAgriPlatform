import { defineStore } from 'pinia'
import { ref } from 'vue'
import { profileApi } from '../api/profile'
import type { CropItem, FarmProfileResponse } from '../api/profile'
import { badRequestMessage } from '@/api/apiError'

export const useProfileStore = defineStore('profile', () => {
  // 狀態
  const farmProfile = ref<FarmProfileResponse | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  // 載入失敗跟儲存失敗分開記：載入失敗時畫面上的表單是空的，不是使用者的設定，
  // 頁面要靠這一個旗標擋住儲存（後端是整份取代，這時存下去會把既有設定蓋成空白）
  const loadError = ref<string | null>(null)
  const errorMessage = ref<string | null>(null)
  const successMessage = ref<string | null>(null)

  // 取得農場設定；成功回 true
  async function fetchFarmProfile(): Promise<boolean> {
    isLoading.value = true
    loadError.value = null
    try {
      farmProfile.value = await profileApi.getFarmProfile()
      return true
    } catch {
      loadError.value = '載入農場設定失敗'
      return false
    } finally {
      isLoading.value = false
    }
  }

  // 儲存農場設定
  async function saveFarmProfile(
    farmCity: string | null,
    farmType: string | null,
    crops: CropItem[]
  ) {
    isSaving.value = true
    errorMessage.value = null
    successMessage.value = null
    try {
      await profileApi.upsertFarmProfile({ farmCity, farmType, crops })
      successMessage.value = '儲存成功'
      // 存完重抓一次，store 裡的就是資料庫的版本。重抓失敗不影響「已經存好」這個結果，
      // 也不能把它當成載入失敗（那會把表單整個收起來），所以另外接住
      try {
        farmProfile.value = await profileApi.getFarmProfile()
      } catch {
        // 下次進這一頁會重新載入
      }
    } catch (e) {
      // 400 帶著後端的原因（例如作物超過上限），照實顯示
      errorMessage.value = badRequestMessage(e) ?? '儲存失敗，請稍後再試'
    } finally {
      isSaving.value = false
    }
  }

  return {
    farmProfile,
    isLoading,
    isSaving,
    loadError,
    errorMessage,
    successMessage,
    fetchFarmProfile,
    saveFarmProfile
  }
})