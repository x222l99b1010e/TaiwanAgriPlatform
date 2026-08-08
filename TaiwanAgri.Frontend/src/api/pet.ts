// src/api/pet.ts
// 職責：封裝所有對後端 /api/pet/* 的 HTTP 呼叫
// 模組 3（毛小孩守護地圖）：收容動物地圖／官方遺失啟事／合法寵物業／自建遺失啟事 CRUD

import axios from 'axios'
import authClient from './authClient'

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

// ─── 共用型別 ───────────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

// ─── 收容動物地圖：型別 ─────────────────────────────────────────────────────
// 對應後端 ShelterAnimalResponseDto。enum 欄位（kind/sex/bodyType/age/sterilization/
// bacterin）後端已在 Service 層 .ToString()，一律回傳字串，型別上直接當 string 處理，
// 不要預期數字（專案未全域註冊 JsonStringEnumConverter，這是既有慣例）

export type AnimalKind = 'Dog' | 'Cat' | 'Other'

export interface ShelterAnimalResponseDto {
  id: number
  animalSubId: string
  shelterName: string
  shelterAddress: string
  county: string
  latitude: number | null   // Shelter.Latitude 是 decimal?，未知收容所留 null，渲染層要自己處理
  longitude: number | null
  kind: string
  sex: string
  bodyType: string
  age: string
  sterilization: string
  bacterin: string
  variety: string
  colour: string
  foundPlace: string
  remark: string
  openDate: string | null   // DateOnly? 序列化後是 "yyyy-MM-dd" 或 null
  createdTime: string       // DateOnly，一定有值
  albumFile: string
}

export interface GetShelterAnimalsParams {
  county?: string
  kind?: AnimalKind
}

// ─── 官方遺失啟事：型別（唯讀表格，無座標欄位） ─────────────────────────────

export interface OfficialLostPetPostResponseDto {
  id: number
  keyNo: string
  chipNum: string
  petName: string
  category: string
  sex: string
  variety: string
  coat: string
  exterior: string
  feature: string
  lostTime: string
  lostPlace: string
  feederName: string
  phoneNum: string
  eMail: string
  pictureUrl: string
}

export type OfficialLostPetPostSortByValue = 'LostTime' | 'Category' | 'Sex'

export interface GetOfficialLostPetPostsParams {
  category?: AnimalKind
  sex?: 'Male' | 'Female' | 'Other' | 'Unknown'
  sortBy?: OfficialLostPetPostSortByValue
  sortDescending?: boolean
  page: number
  pageSize: number
}

// ─── 合法寵物業查詢：型別（唯讀表格，無座標欄位） ───────────────────────────

export type LegalPetAnimalType = 'Dog' | 'Cat' | 'Both' | 'Other'
export type LegalPetRankGrade = 'Excellent' | 'GradeA' | 'GradeB' | 'GradeC' | 'Unknown'
export type LegalPetStateFlag = 'Operating' | 'Closed' | 'Suspended' | 'Revoked' | 'Unknown'
export type LegalSpecificPetSortByValue = 'Name' | 'PermitValidDate' | 'RankGrade'

export interface LegalSpecificPetResponseDto {
  id: number
  externalId: string
  county: string
  businessItems: string
  animalType: string
  name: string
  address: string
  permitNumber: string
  permitValidDate: string | null
  ownerName: string
  responsibleStaffName: string
  rankYear: string
  rankGrade: string
  rankText: string
  stateFlag: string
}

export interface GetLegalSpecificPetsParams {
  county?: string
  animalType?: LegalPetAnimalType
  rankGrade?: LegalPetRankGrade
  stateFlag?: LegalPetStateFlag
  /** 比對 businessItems（如 "ABC"）是否包含這個代碼字元；A=繁殖 B=買賣 C=寄養 */
  businessItem?: string
  sortBy?: LegalSpecificPetSortByValue
  sortDescending?: boolean
  page: number
  pageSize: number
}

// ─── 自建遺失啟事（CRUD）：型別 ─────────────────────────────────────────────

export type LostPetPostStatusValue = 'Searching' | 'Found' | 'Withdrawn'

export interface LostPetPostResponseDto {
  id: number
  title: string
  description: string
  county: string
  phone: string
  email: string
  photoUrl: string
  latitude: number | null   // 使用者可以不點地圖，座標可為 null；地圖標記時要濾掉沒座標的
  longitude: number | null
  status: LostPetPostStatusValue
  createdAt: string
  updatedAt: string
  /** 目前登入者是否為本篇作者；未登入一律 false。前端只依此顯示編輯／刪除按鈕，不做任何比對 */
  isOwner: boolean
}

export type LostPetPostSortByValue = 'CreatedAt' | 'UpdatedAt'

export interface GetLostPetPostsParams {
  status?: LostPetPostStatusValue
  county?: string
  sortBy?: LostPetPostSortByValue
  sortDescending?: boolean
  page: number
  pageSize: number
}

export interface CreateLostPetPostRequest {
  title: string
  description: string
  county?: string
  phone?: string
  email?: string
  photoUrl?: string
  latitude?: number | null
  longitude?: number | null
}

export interface UpdateLostPetPostRequest extends CreateLostPetPostRequest {
  status: LostPetPostStatusValue
}

// ─── API 呼叫函式 ───────────────────────────────────────────────────────────

export const petApi = {
  /**
   * GET /api/pet/shelter-animals — 刻意不分頁，地圖需要篩選後的完整清單。
   *
   * 端點有防禦性上限，結果可能被截斷。**是否截斷由後端用 `X-Result-Truncated` 標頭直接告知**，
   * 前端不自行拿筆數去比對一份複製的上限常數——上限值只存在後端一處，日後調整不必同步改前端。
   * ⚠ 跨網域部署時該標頭需要後端 CORS 的 `WithExposedHeaders` 放行（已設定）。
   */
  getShelterAnimals(
    params: GetShelterAnimalsParams
  ): Promise<{ items: ShelterAnimalResponseDto[]; truncated: boolean }> {
    return apiClient
      .get<ShelterAnimalResponseDto[]>('/api/pet/shelter-animals', { params })
      .then(res => ({
        items: res.data,
        // 標頭缺席時（舊版後端／代理層剝掉）一律當作沒有截斷，寧可不提示也不要誤報
        truncated: String(res.headers['x-result-truncated']).toLowerCase() === 'true',
      }))
  },

  /** GET /api/pet/official-lost-posts */
  getOfficialLostPetPosts(
    params: GetOfficialLostPetPostsParams
  ): Promise<PagedResult<OfficialLostPetPostResponseDto>> {
    return apiClient
      .get<PagedResult<OfficialLostPetPostResponseDto>>('/api/pet/official-lost-posts', { params })
      .then(res => res.data)
  },

  /** GET /api/pet/legal-specific-pets */
  getLegalSpecificPets(
    params: GetLegalSpecificPetsParams
  ): Promise<PagedResult<LegalSpecificPetResponseDto>> {
    return apiClient
      .get<PagedResult<LegalSpecificPetResponseDto>>('/api/pet/legal-specific-pets', { params })
      .then(res => res.data)
  },

  /**
   * GET /api/pet/lost-pet-posts
   * 刻意用 authClient 而非 apiClient：端點本身未登入也能查（唯讀），但登入時回應會多帶
   * isOwner 這個因人而異的欄位。authClient 的攔截器只在「有 token」時才加 Authorization
   * header，沒有 token 時行為跟 apiClient 完全相同——訪客查詢不受影響，登入時才會讓
   * 後端多算出正確的 isOwner。這是既有四個模組沒遇過的「公開可讀、登入後內容更豐富」情境。
   */
  getLostPetPosts(params: GetLostPetPostsParams): Promise<PagedResult<LostPetPostResponseDto>> {
    return authClient
      .get<PagedResult<LostPetPostResponseDto>>('/api/pet/lost-pet-posts', { params })
      .then(res => res.data)
  },

  /** GET /api/pet/lost-pet-posts/{id} — 同上，用 authClient 讓 isOwner 判斷正確 */
  getLostPetPostById(id: number): Promise<LostPetPostResponseDto> {
    return authClient
      .get<LostPetPostResponseDto>(`/api/pet/lost-pet-posts/${id}`)
      .then(res => res.data)
  },

  /** POST /api/pet/lost-pet-posts — 需登入；Phone/Email 至少填一項，否則後端 400 */
  createLostPetPost(request: CreateLostPetPostRequest): Promise<LostPetPostResponseDto> {
    return authClient
      .post<LostPetPostResponseDto>('/api/pet/lost-pet-posts', request)
      .then(res => res.data)
  },

  /** PUT /api/pet/lost-pet-posts/{id} — 需登入，且只能改自己的貼文（非本人一律 404） */
  updateLostPetPost(id: number, request: UpdateLostPetPostRequest): Promise<void> {
    return authClient
      .put(`/api/pet/lost-pet-posts/${id}`, request)
      .then(() => undefined)
  },

  /** DELETE /api/pet/lost-pet-posts/{id} — 需登入，且只能刪自己的貼文（非本人一律 404） */
  deleteLostPetPost(id: number): Promise<void> {
    return authClient
      .delete(`/api/pet/lost-pet-posts/${id}`)
      .then(() => undefined)
  },
}
