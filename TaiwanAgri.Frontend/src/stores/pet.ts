// src/stores/pet.ts
// 職責：管理模組 3（毛小孩守護地圖）的全局狀態
// 四個查詢各自獨立：收容動物地圖／官方遺失啟事／合法寵物業／自建遺失啟事 CRUD

import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'
import { petApi } from '@/api/pet'
import { useLatestRequest } from '@/composables/useLatestRequest'
import type {
  ShelterAnimalResponseDto,
  GetShelterAnimalsParams,
  OfficialLostPetPostResponseDto,
  GetOfficialLostPetPostsParams,
  LegalSpecificPetResponseDto,
  GetLegalSpecificPetsParams,
  LostPetPostResponseDto,
  GetLostPetPostsParams,
  CreateLostPetPostRequest,
  UpdateLostPetPostRequest,
  PagedResult,
} from '@/api/pet'

export const usePetStore = defineStore('pet', () => {
  // ─── 收容動物地圖 ─────────────────────────────────────────────────────
  // 不分頁，MarkerCluster 需要篩選後的完整清單

  const shelterAnimals = ref<ShelterAnimalResponseDto[]>([])
  const isLoadingShelterAnimals = ref(false)
  const shelterAnimalsError = ref<string | null>(null)
  const shelterAnimalsRequest = useLatestRequest()

  async function fetchShelterAnimals(params: GetShelterAnimalsParams) {
    const mySeq = shelterAnimalsRequest.next()
    isLoadingShelterAnimals.value = true
    shelterAnimalsError.value = null
    try {
      const result = await petApi.getShelterAnimals(params)
      if (!shelterAnimalsRequest.isLatest(mySeq)) return
      shelterAnimals.value = result
    } catch (e) {
      if (!shelterAnimalsRequest.isLatest(mySeq)) return
      shelterAnimalsError.value = '載入收容動物資料失敗，請稍後再試'
      console.error(e)
    } finally {
      if (shelterAnimalsRequest.isLatest(mySeq)) {
        isLoadingShelterAnimals.value = false
      }
    }
  }

  // ─── 官方遺失啟事（唯讀表格） ─────────────────────────────────────────

  const officialLostPetPostsPage = ref<PagedResult<OfficialLostPetPostResponseDto> | null>(null)
  const isLoadingOfficialLostPetPosts = ref(false)
  const officialLostPetPostsError = ref<string | null>(null)
  const officialLostPetPostsRequest = useLatestRequest()

  async function fetchOfficialLostPetPosts(params: GetOfficialLostPetPostsParams) {
    const mySeq = officialLostPetPostsRequest.next()
    isLoadingOfficialLostPetPosts.value = true
    officialLostPetPostsError.value = null
    try {
      const result = await petApi.getOfficialLostPetPosts(params)
      if (!officialLostPetPostsRequest.isLatest(mySeq)) return
      officialLostPetPostsPage.value = result
    } catch (e) {
      if (!officialLostPetPostsRequest.isLatest(mySeq)) return
      officialLostPetPostsError.value = '載入官方遺失啟事失敗，請稍後再試'
      console.error(e)
    } finally {
      if (officialLostPetPostsRequest.isLatest(mySeq)) {
        isLoadingOfficialLostPetPosts.value = false
      }
    }
  }

  // ─── 合法寵物業查詢（唯讀表格） ───────────────────────────────────────

  const legalSpecificPetsPage = ref<PagedResult<LegalSpecificPetResponseDto> | null>(null)
  const isLoadingLegalSpecificPets = ref(false)
  const legalSpecificPetsError = ref<string | null>(null)
  const legalSpecificPetsRequest = useLatestRequest()

  async function fetchLegalSpecificPets(params: GetLegalSpecificPetsParams) {
    const mySeq = legalSpecificPetsRequest.next()
    isLoadingLegalSpecificPets.value = true
    legalSpecificPetsError.value = null
    try {
      const result = await petApi.getLegalSpecificPets(params)
      if (!legalSpecificPetsRequest.isLatest(mySeq)) return
      legalSpecificPetsPage.value = result
    } catch (e) {
      if (!legalSpecificPetsRequest.isLatest(mySeq)) return
      legalSpecificPetsError.value = '載入合法寵物業資料失敗，請稍後再試'
      console.error(e)
    } finally {
      if (legalSpecificPetsRequest.isLatest(mySeq)) {
        isLoadingLegalSpecificPets.value = false
      }
    }
  }

  // ─── 自建遺失啟事（CRUD） ─────────────────────────────────────────────

  const lostPetPostsPage = ref<PagedResult<LostPetPostResponseDto> | null>(null)
  const isLoadingLostPetPosts = ref(false)
  const lostPetPostsError = ref<string | null>(null)
  const lostPetPostsRequest = useLatestRequest()

  // 新增／編輯／刪除共用一組狀態：三個動作不會同時發生，不需要各自分開
  const isSavingLostPetPost = ref(false)
  const saveLostPetPostError = ref<string | null>(null)

  async function fetchLostPetPosts(params: GetLostPetPostsParams) {
    const mySeq = lostPetPostsRequest.next()
    isLoadingLostPetPosts.value = true
    lostPetPostsError.value = null
    try {
      const result = await petApi.getLostPetPosts(params)
      if (!lostPetPostsRequest.isLatest(mySeq)) return
      lostPetPostsPage.value = result
    } catch (e) {
      if (!lostPetPostsRequest.isLatest(mySeq)) return
      lostPetPostsError.value = '載入遺失啟事失敗，請稍後再試'
      console.error(e)
    } finally {
      if (lostPetPostsRequest.isLatest(mySeq)) {
        isLoadingLostPetPosts.value = false
      }
    }
  }

  /** 新增遺失啟事；成功後回傳新建的項目，讓表單元件決定接下來要導頁還是留在原地 */
  async function createLostPetPost(request: CreateLostPetPostRequest): Promise<LostPetPostResponseDto | null> {
    isSavingLostPetPost.value = true
    saveLostPetPostError.value = null
    try {
      return await petApi.createLostPetPost(request)
    } catch (e) {
      saveLostPetPostError.value = axios.isAxiosError(e) && e.response?.status === 400
        ? '電話與 Email 至少填一項，才能讓拾獲者聯絡到你'
        : '新增失敗，請稍後再試'
      console.error(e)
      return null
    } finally {
      isSavingLostPetPost.value = false
    }
  }

  /** 編輯遺失啟事；回傳是否成功，成功後由呼叫端決定要不要重新整理清單 */
  async function updateLostPetPost(id: number, request: UpdateLostPetPostRequest): Promise<boolean> {
    isSavingLostPetPost.value = true
    saveLostPetPostError.value = null
    try {
      await petApi.updateLostPetPost(id, request)
      return true
    } catch (e) {
      saveLostPetPostError.value = axios.isAxiosError(e) && e.response?.status === 400
        ? '電話與 Email 至少填一項，才能讓拾獲者聯絡到你'
        : '更新失敗，請稍後再試（可能不是你的貼文）'
      console.error(e)
      return false
    } finally {
      isSavingLostPetPost.value = false
    }
  }

  /** 刪除遺失啟事；回傳是否成功 */
  async function deleteLostPetPost(id: number): Promise<boolean> {
    isSavingLostPetPost.value = true
    saveLostPetPostError.value = null
    try {
      await petApi.deleteLostPetPost(id)
      return true
    } catch (e) {
      saveLostPetPostError.value = '刪除失敗，請稍後再試（可能不是你的貼文）'
      console.error(e)
      return false
    } finally {
      isSavingLostPetPost.value = false
    }
  }

  return {
    // 收容動物地圖
    shelterAnimals,
    isLoadingShelterAnimals,
    shelterAnimalsError,
    fetchShelterAnimals,
    // 官方遺失啟事
    officialLostPetPostsPage,
    isLoadingOfficialLostPetPosts,
    officialLostPetPostsError,
    fetchOfficialLostPetPosts,
    // 合法寵物業
    legalSpecificPetsPage,
    isLoadingLegalSpecificPets,
    legalSpecificPetsError,
    fetchLegalSpecificPets,
    // 自建遺失啟事 CRUD
    lostPetPostsPage,
    isLoadingLostPetPosts,
    lostPetPostsError,
    isSavingLostPetPost,
    saveLostPetPostError,
    fetchLostPetPosts,
    createLostPetPost,
    updateLostPetPost,
    deleteLostPetPost,
  }
})
