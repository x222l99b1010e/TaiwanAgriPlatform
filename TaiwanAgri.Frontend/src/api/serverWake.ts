// src/api/serverWake.ts
// 職責：有請求等太久時，判斷現在卡在哪一段，給畫面一句「伺服器喚醒中」之類的說明。
//
// 為什麼需要它：雲端後端閒置時會被平台收起來、資料庫也會自動暫停，閒置後第一個訪客
// 要等冷啟動加上資料庫恢復約 1.5 分鐘。這段期間請求不是失敗、只是還沒回來；
// 畫面只有「載入中」的話，使用者分不出是在等還是當掉了。
//
// 怎麼判斷卡在哪：有請求等超過 SLOW_AFTER_MS 時，另外問兩支健康檢查——
//   /health 回不來                     ＝伺服器還在啟動（平台會把請求留著等它就緒）；
//   /health 200、/health/ready 回不來  ＝伺服器好了，資料庫還在醒；
//   兩支都 200                         ＝伺服器與資料庫都醒著，只是這個查詢本身比較久。
// 健康檢查只在「有請求等太久」時才打：/health/ready 會碰資料庫，平常不該多打。
//
// ⚠ 這裡只負責說明，不判定失敗：請求最後逾時或回錯，照樣由各頁自己的錯誤畫面處理。
// ⚠ 健康檢查若被 CORS 擋下或連不上，一律當成「還沒好」，畫面停在「伺服器喚醒中」，不會說錯話。

import axios from 'axios'
import { reactive } from 'vue'

export type WakePhase = 'idle' | 'server' | 'database' | 'query'
export type ActiveWakePhase = Exclude<WakePhase, 'idle'>
export type ProbePath = '/health' | '/health/ready'
type ProbeResult = 'unknown' | 'ok' | 'failed'

/** 等多久才開始說明。太短會在正常的慢查詢上閃一下，太長則第一個訪客要先乾等 */
export const SLOW_AFTER_MS = 8_000
/** 健康檢查沒過時，隔多久再問一次 */
const PROBE_RETRY_MS = 5_000
/** 健康檢查本身的等待上限，跟一般請求一樣長：冷啟動期間 /health 也會被平台留著 */
const PROBE_TIMEOUT_MS = 120_000

/** 兩支健康檢查的結果 → 現在卡在哪一段 */
export function wakePhaseOf(health: ProbeResult, ready: ProbeResult): ActiveWakePhase {
  if (health !== 'ok') return 'server'
  if (ready !== 'ok') return 'database'
  return 'query'
}

export interface WakeMessage {
  title: string
  hint: string
}

/* 用 Record 而不是 switch：日後多一個階段時，少一個 key 會直接編譯失敗 */
const WAKE_MESSAGES: Record<ActiveWakePhase, WakeMessage> = {
  server: { title: '伺服器喚醒中', hint: '閒置一陣子後第一次開啟，約需 1–2 分鐘' },
  database: { title: '伺服器已啟動，正在喚醒資料庫', hint: '約需 30–60 秒' },
  query: { title: '資料較多，仍在查詢中', hint: '伺服器與資料庫都已就緒' },
}

export function describeWake(phase: ActiveWakePhase): WakeMessage {
  return WAKE_MESSAGES[phase]
}

export interface ServerWakeDeps {
  /** 打一支健康檢查，回 200 時 resolve true；其他結果 resolve false 或 reject 都可以 */
  probe: (path: ProbePath, signal: AbortSignal) => Promise<boolean>
  now?: () => number
  /** 有請求在等時開始定時呼叫 tick，回傳停止函式。測試傳一個不做事的版本、自己呼叫 tick */
  startTicking?: (tick: () => void) => () => void
  slowAfterMs?: number
  retryMs?: number
}

export function createServerWakeMonitor(deps: ServerWakeDeps) {
  const now = deps.now ?? Date.now
  const slowAfterMs = deps.slowAfterMs ?? SLOW_AFTER_MS
  const retryMs = deps.retryMs ?? PROBE_RETRY_MS
  const startTicking =
    deps.startTicking ??
    ((tick: () => void) => {
      const id = setInterval(tick, 1_000)
      return () => clearInterval(id)
    })

  const state = reactive({ phase: 'idle' as WakePhase, waitedSeconds: 0 })

  const pending = new Map<number, number>() // 請求編號 → 開始的時間
  let nextId = 0
  let stopTicking: (() => void) | null = null
  let health: ProbeResult = 'unknown'
  let ready: ProbeResult = 'unknown'
  let probing: AbortController | null = null
  let nextProbeAt = 0

  function oldestStart(): number | null {
    let oldest: number | null = null
    for (const startedAt of pending.values()) {
      if (oldest === null || startedAt < oldest) oldest = startedAt
    }
    return oldest
  }

  function reset() {
    probing?.abort()
    probing = null
    health = 'unknown'
    ready = 'unknown'
    nextProbeAt = 0
    state.phase = 'idle'
    state.waitedSeconds = 0
  }

  function settle(controller: AbortController, path: ProbePath, ok: boolean) {
    // 已經重置過（請求都回來了）或被新的健康檢查取代：這個結果不算數
    if (probing !== controller) return
    probing = null
    if (path === '/health') health = ok ? 'ok' : 'failed'
    else ready = ok ? 'ok' : 'failed'
    if (!ok) nextProbeAt = now() + retryMs
    if (state.phase !== 'idle') state.phase = wakePhaseOf(health, ready)
  }

  function launchProbe() {
    const path: ProbePath = health === 'ok' ? '/health/ready' : '/health'
    const controller = new AbortController()
    probing = controller
    deps.probe(path, controller.signal).then(
      ok => settle(controller, path, ok),
      () => settle(controller, path, false),
    )
  }

  function tick() {
    const oldest = oldestStart()
    if (oldest === null) return
    const t = now()
    const waited = t - oldest
    // 等最久的那個回來了、剩下的都還很新：先收起說明，健康檢查的結果留著給下一次用
    if (waited < slowAfterMs) {
      state.phase = 'idle'
      return
    }
    state.waitedSeconds = Math.floor(waited / 1_000)
    state.phase = wakePhaseOf(health, ready)
    if (!probing && ready !== 'ok' && t >= nextProbeAt) launchProbe()
  }

  /** 每個請求送出時呼叫一次；回傳的函式在請求結束（成功或失敗）時呼叫，重複呼叫無害 */
  function requestStarted(): () => void {
    const id = nextId++
    pending.set(id, now())
    if (!stopTicking) stopTicking = startTicking(tick)
    let ended = false
    return () => {
      if (ended) return
      ended = true
      pending.delete(id)
      if (pending.size === 0) {
        stopTicking?.()
        stopTicking = null
        reset()
      }
    }
  }

  return { state, requestStarted, tick }
}

/* 健康檢查用自己的 axios instance：不經過 httpBase 的攔截器，
   否則健康檢查本身也會被算成「在等的請求」，永遠等不完 */
const probeClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: PROBE_TIMEOUT_MS,
})

export const serverWake = createServerWakeMonitor({
  probe: async (path, signal) => {
    const response = await probeClient.get(path, { signal, validateStatus: () => true })
    return response.status === 200
  },
})
