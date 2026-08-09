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
  GetShelterAnimalsByShelterParams,
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
  // 後端以 X-Result-Truncated 標頭告知本次結果是否觸及上限而被截斷；
  // 前端不複製一份上限常數，避免兩邊各改各的導致提示失效或誤報
  const shelterAnimalsTruncated = ref(false)
  const shelterAnimalsRequest = useLatestRequest()

  async function fetchShelterAnimals(params: GetShelterAnimalsParams) {
    const mySeq = shelterAnimalsRequest.next()
    isLoadingShelterAnimals.value = true
    shelterAnimalsError.value = null
    try {
      const result = await petApi.getShelterAnimals(params)
      if (!shelterAnimalsRequest.isLatest(mySeq)) return
      shelterAnimals.value = result.items
      shelterAnimalsTruncated.value = result.truncated
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

  // ─── 動物詳情頁（單筆） ───────────────────────────────────────────────
  // 不掛週次分支新增：owner 實機測試後指出收容所詳情頁還缺「單一動物」這一層，
  // 補齊「地圖→收容所→動物」三層下鑽的最後一層

  const shelterAnimalDetail = ref<ShelterAnimalResponseDto | null>(null)
  const isLoadingShelterAnimalDetail = ref(false)
  const shelterAnimalDetailError = ref<string | null>(null)
  const shelterAnimalDetailRequest = useLatestRequest()

  async function fetchShelterAnimalById(id: number) {
    const mySeq = shelterAnimalDetailRequest.next()
    isLoadingShelterAnimalDetail.value = true
    shelterAnimalDetailError.value = null
    shelterAnimalDetail.value = null
    try {
      const result = await petApi.getShelterAnimalById(id)
      if (!shelterAnimalDetailRequest.isLatest(mySeq)) return
      shelterAnimalDetail.value = result
    } catch (e) {
      if (!shelterAnimalDetailRequest.isLatest(mySeq)) return
      shelterAnimalDetailError.value = axios.isAxiosError(e) && e.response?.status === 404
        ? '找不到這隻動物的資料，可能已經離開收容所（例如已被領養）'
        : '載入動物資料失敗，請稍後再試'
      console.error(e)
    } finally {
      if (shelterAnimalDetailRequest.isLatest(mySeq)) {
        isLoadingShelterAnimalDetail.value = false
      }
    }
  }

  // ─── 收容所詳情頁（單一收容所的分頁動物清單） ───────────────────────────
  // 不掛週次分支新增：popup 摘要「查看全部→」連到獨立頁，這裡是那頁的資料來源，
  // 跟上面「地圖用、不分頁、跨收容所」的 shelterAnimals 是兩組獨立狀態，不共用

  const shelterAnimalsByShelterPage = ref<PagedResult<ShelterAnimalResponseDto> | null>(null)
  const isLoadingShelterAnimalsByShelter = ref(false)
  const shelterAnimalsByShelterError = ref<string | null>(null)
  const shelterAnimalsByShelterRequest = useLatestRequest()

  async function fetchShelterAnimalsByShelter(shelterId: number, params: GetShelterAnimalsByShelterParams) {
    const mySeq = shelterAnimalsByShelterRequest.next()
    isLoadingShelterAnimalsByShelter.value = true
    shelterAnimalsByShelterError.value = null
    try {
      const result = await petApi.getShelterAnimalsByShelter(shelterId, params)
      if (!shelterAnimalsByShelterRequest.isLatest(mySeq)) return
      shelterAnimalsByShelterPage.value = result
    } catch (e) {
      if (!shelterAnimalsByShelterRequest.isLatest(mySeq)) return
      shelterAnimalsByShelterError.value = '載入收容所動物清單失敗，請稍後再試'
      console.error(e)
    } finally {
      if (shelterAnimalsByShelterRequest.isLatest(mySeq)) {
        isLoadingShelterAnimalsByShelter.value = false
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

  // ─── 遺失啟事詳情頁（單筆） ───────────────────────────────────────────
  // 不掛週次分支新增：後端 GET /{id} 與前端 getLostPetPostById 原本零使用，這裡接起來

  const lostPetPostDetail = ref<LostPetPostResponseDto | null>(null)
  const isLoadingLostPetPostDetail = ref(false)
  const lostPetPostDetailError = ref<string | null>(null)
  const lostPetPostDetailRequest = useLatestRequest()

  async function fetchLostPetPostById(id: number) {
    const mySeq = lostPetPostDetailRequest.next()
    isLoadingLostPetPostDetail.value = true
    lostPetPostDetailError.value = null
    lostPetPostDetail.value = null // 切換到不同 id 時先清空，避免短暫顯示上一筆的舊內容
    try {
      const result = await petApi.getLostPetPostById(id)
      if (!lostPetPostDetailRequest.isLatest(mySeq)) return
      lostPetPostDetail.value = result
    } catch (e) {
      if (!lostPetPostDetailRequest.isLatest(mySeq)) return
      // 404（貼文不存在或已被刪除）跟其他錯誤分開講，訊息才不會誤導使用者去重試一個不會成功的請求
      lostPetPostDetailError.value = axios.isAxiosError(e) && e.response?.status === 404
        ? '找不到這篇協尋啟事，可能已被刪除'
        : '載入協尋啟事失敗，請稍後再試'
      console.error(e)
    } finally {
      if (lostPetPostDetailRequest.isLatest(mySeq)) {
        isLoadingLostPetPostDetail.value = false
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
      // 後端對「查無此貼文」與「不是本人」一律回 404（刻意不洩漏存在與否），前端無法區分這兩者；
      // 但使用者能走到這一步，代表當初是從 isOwner 為 true 才渲染出來的編輯按鈕進來的，
      // 實務上 404 幾乎都是「這篇剛剛被刪掉了」，訊息要照這個情境寫才不會誤導
      const status = axios.isAxiosError(e) ? e.response?.status : undefined
      saveLostPetPostError.value =
        status === 400 ? '電話與 Email 至少填一項，才能讓拾獲者聯絡到你'
        : status === 404 ? '這篇貼文已不存在（可能已被刪除），請重新整理清單'
        : '更新失敗，請稍後再試'
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
    shelterAnimalsTruncated,
    fetchShelterAnimals,
    // 動物詳情頁
    shelterAnimalDetail,
    isLoadingShelterAnimalDetail,
    shelterAnimalDetailError,
    fetchShelterAnimalById,
    // 收容所詳情頁
    shelterAnimalsByShelterPage,
    isLoadingShelterAnimalsByShelter,
    shelterAnimalsByShelterError,
    fetchShelterAnimalsByShelter,
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
    // 遺失啟事詳情頁
    lostPetPostDetail,
    isLoadingLostPetPostDetail,
    lostPetPostDetailError,
    fetchLostPetPostById,
    createLostPetPost,
    updateLostPetPost,
    deleteLostPetPost,
  }
})
