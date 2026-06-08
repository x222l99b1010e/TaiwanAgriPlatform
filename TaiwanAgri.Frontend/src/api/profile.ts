import authClient from './authClient'
// authClient 是帶 JWT Authorization header 的 axios instance
// 和 notificationApi 一樣，profile 需要登入才能用

export interface CropItem {
  cropCode: string
  cropName: string
}

export interface FarmProfileResponse {
  farmCity: string | null
  farmType: string | null
  createdAt: string
  updatedAt: string
  crops: CropItem[]
}

export interface UpsertFarmProfileRequest {
  farmCity: string | null
  farmType: string | null
  crops: CropItem[]
}

export const profileApi = {
  getFarmProfile: async (): Promise<FarmProfileResponse | null> => {
    const res = await authClient.get('/api/profile/farm')
    return res.data
    // 後端回 200 + null 時，res.data 就是 null
    // 代表使用者還沒有設定過農場資料
  },

  upsertFarmProfile: async (request: UpsertFarmProfileRequest): Promise<void> => {
    await authClient.put('/api/profile/farm', request)
    // 後端回 204 NoContent，沒有 response body
  }
}