import { defineStore } from 'pinia'
import { ref } from 'vue'
import { profileApi } from '../api/profile'
import type { CropItem, FarmProfileResponse } from '../api/profile'

export const useProfileStore = defineStore('profile', () => {
  // 狀態
  const farmProfile = ref<FarmProfileResponse | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const errorMessage = ref<string | null>(null)
  const successMessage = ref<string | null>(null)

  // 取得農場設定
  async function fetchFarmProfile() {
    isLoading.value = true
    errorMessage.value = null
    try {
      farmProfile.value = await profileApi.getFarmProfile()
    } catch (e) {
      errorMessage.value = '載入農場設定失敗'
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
      // 儲存完重新 fetch，確保畫面顯示的是資料庫裡的資料
      await fetchFarmProfile()
    } catch (e) {
      errorMessage.value = '儲存失敗，請稍後再試'
    } finally {
      isSaving.value = false
    }
  }

  return {
    farmProfile,
    isLoading,
    isSaving,
    errorMessage,
    successMessage,
    fetchFarmProfile,
    saveFarmProfile
  }
})