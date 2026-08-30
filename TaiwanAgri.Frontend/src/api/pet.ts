// src/api/pet.ts
// 職責：封裝所有對後端 /api/pet/* 的 HTTP 呼叫
// 模組 3（毛小孩守護地圖）：收容動物地圖／官方遺失啟事／合法寵物業／自建遺失啟事 CRUD

import apiClient from './apiClient'
import type { PagedResult } from './pagination'
import authClient from './authClient'

// ─── 共用型別 ───────────────────────────────────────────────────────────────

// 分頁契約型別集中於 pagination.ts；re-export 以維持既有 import 路徑
export type { PagedResult }

// ─── 收容動物地圖：型別 ─────────────────────────────────────────────────────
// 對應後端 ShelterAnimalResponseDto。enum 欄位（kind/sex/bodyType/age/sterilization/
// bacterin）後端已在 Service 層 .ToString()，一律回傳字串，型別上直接當 string 處理，
// 不要預期數字（專案未全域註冊 JsonStringEnumConverter，這是既有慣例）

export type AnimalKind = 'Dog' | 'Cat' | 'Other'
export type AnimalSex = 'Male' | 'Female' | 'Other' | 'Unknown'

export interface ShelterAnimalResponseDto {
  id: number
  animalSubId: string
  // 收容所人工維護的真實 PK（37 筆種子資料），收容所詳情頁 /pet/shelter-map/:shelterId
  // 用這個欄位組連結——不掛週次分支新增，地圖端點原本沒有回傳這個欄位（只回傳名稱/地址等展示用資訊）
  shelterPkId: number
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

/**
 * 對應後端 ShelterAnimalSummaryDto——收容動物地圖聚合端點用，一間收容所一筆摘要，
 * 取代原本逐隻動物的不分頁清單。結果集本身只有約 30 筆，不會被截斷，也不需要分頁。
 */
export interface ShelterAnimalSummaryDto {
  shelterPkId: number
  shelterName: string
  shelterAddress: string
  county: string
  latitude: number | null
  longitude: number | null
  totalCount: number
  dogCount: number
  catCount: number
  otherCount: number
}

export type ShelterAnimalSortByValue = 'CreatedTime' | 'AnimalSubId'

/** 收容所詳情頁用：單一收容所的分頁查詢（不掛週次分支新增，見 shelters/{shelterId}/animals） */
export interface GetShelterAnimalsByShelterParams {
  kind?: AnimalKind
  sex?: AnimalSex
  sortBy?: ShelterAnimalSortByValue
  sortDescending?: boolean
  page: number
  pageSize: number
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
  /** 個人管理頁用（不掛週次分支新增）：true 時只回傳目前登入者自己的貼文，
   *  必須用 authClient 帶著 token 呼叫才有意義，未登入時後端回 401 */
  onlyMine?: boolean
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
   * GET /api/pet/shelters/summary — 收容動物地圖用，一間收容所一筆聚合摘要。
   *
   * 取代原本逐隻動物的不分頁清單（曾有 3000 筆防禦上限與 X-Result-Truncated 截斷標頭那整套
   * 機制，資料源頭問題解決後一併移除）：結果集本身只有約 30 筆，不會被截斷，也不需要分頁。
   */
  getShelterAnimalSummary(params: GetShelterAnimalsParams): Promise<ShelterAnimalSummaryDto[]> {
    return apiClient
      .get<ShelterAnimalSummaryDto[]>('/api/pet/shelters/summary', { params })
      .then(res => res.data)
  },

  /**
   * GET /api/pet/shelters/{shelterId}/animals — 收容所詳情頁用，分頁列出該所全部在養動物。
   * 與 getShelterAnimals（地圖用、不分頁、跨收容所）刻意分開：這支端點是「查一間」，
   * 真正做 Skip/Take 分頁，不會像 popup 那樣被 POPUP_ANIMAL_LIMIT 這種前端常數截斷。
   */
  getShelterAnimalsByShelter(
    shelterId: number,
    params: GetShelterAnimalsByShelterParams
  ): Promise<PagedResult<ShelterAnimalResponseDto>> {
    return apiClient
      .get<PagedResult<ShelterAnimalResponseDto>>(`/api/pet/shelters/${shelterId}/animals`, { params })
      .then(res => res.data)
  },

  /** GET /api/pet/shelter-animals/{id} — 動物詳情頁用，單筆查詢 */
  getShelterAnimalById(id: number): Promise<ShelterAnimalResponseDto> {
    return apiClient
      .get<ShelterAnimalResponseDto>(`/api/pet/shelter-animals/${id}`)
      .then(res => res.data)
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
