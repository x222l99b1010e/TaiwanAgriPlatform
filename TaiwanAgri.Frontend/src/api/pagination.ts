// src/api/pagination.ts
// 職責：後端分頁契約的共用型別，對應 TaiwanAgri.Core.Dtos.PagedResult<T>
//
// 原本 foodSafety.ts 與 pet.ts 各自宣告了一份完全相同的定義，weather.ts 接上分頁
// 契約時會變成第三份。集中在這裡，兩支既有模組改為 re-export，消費端（stores）
// 的 import 路徑不變。

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}
