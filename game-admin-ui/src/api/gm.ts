import request from '@/utils/request'

// ==================== 类型定义 ====================

/** Dashboard 统计数据 */
export interface StatsResponse {
  onlineCount: number
  totalGoldIssued: number
  pendingCount: number
  bannedCount: number
  topAdmins: TopAdmin[]
}

export interface TopAdmin {
  adminId: string
  adminName: string
  totalAmount: number
  operationCount: number
}

/** 玩家数据 */
export interface Player {
  id: string
  nickname: string
  level: number
  gold: number
  isBanned: boolean
  createdAt: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface PlayerQuery {
  keyword?: string
  page?: number
  pageSize?: number
}

/** 发奖相关 */
export interface GiveItemRequest {
  playerId: string
  itemType: string
  amount: number
}

export interface GiveItemResponse {
  playerId: string
  itemType: string
  amount: number
  newBalance: number
  operatedAt: string
  status: 'Success' | 'Pending'
  message?: string
}

/** 审批相关 */
export interface PendingOperation {
  logId: string
  operatorId: string
  operatorName: string
  playerId: string
  playerNickname: string
  itemType: string
  amount: number
  createdAt: string
}

export interface AuditDecisionRequest {
  logId: string
  action: 'Approve' | 'Reject'
  reason?: string
}

export interface AuditDecisionResponse {
  logId: string
  playerId: string
  status: string
  message: string
  newBalance?: number
  processedAt?: string
}

// ==================== API 调用 ====================

/**
 * 获取 Dashboard 统计数据
 */
export const getStatsSummary = (): Promise<StatsResponse> => {
  return request.get('/Admin/stats/daily')
}

/**
 * 搜索玩家列表（分页）
 */
export const searchPlayers = (query: PlayerQuery = {}): Promise<PagedResult<Player>> => {
  return request.get('/Player', {
    params: {
      keyword: query.keyword || '',
      page: query.page || 1,
      pageSize: query.pageSize || 10
    }
  })
}

/**
 * 发放道具/金币给玩家
 */
export const giveItem = (data: GiveItemRequest): Promise<GiveItemResponse> => {
  return request.post('/Player/give-item', data)
}

/**
 * 获取待审批列表
 */
export const getPendingAudits = (): Promise<PendingOperation[]> => {
  return request.get('/Audit/pending')
}

/**
 * 审批决策（通过/拒绝）
 */
export const auditDecide = (data: AuditDecisionRequest): Promise<AuditDecisionResponse> => {
  return request.post('/Audit/decide', data)
}

/** 扣除金币请求 */
export interface DeductGoldRequest {
  playerId: string
  amount: number
  reason: string
}

/** 解封请求 */
export interface UnbanPlayerRequest {
  playerId: string
  reason: string
}

/**
 * 扣除金币 (金币回收)
 */
export const deductGold = (data: DeductGoldRequest): Promise<GiveItemResponse> => {
  return request.post('/Player/deduct-gold', data)
}

/**
 * 解封玩家
 */
export const unbanPlayer = (data: UnbanPlayerRequest): Promise<{ success: boolean, message: string }> => {
  return request.post('/Player/unban-player', data)
}

// ==================== 批量操作 ====================

/** 批量封禁请求 */
export interface BatchBanRequest {
  playerIds: string[]
  reason: string
  durationHours?: number
}

/** 批量操作响应 */
export interface BatchOperationResponse {
  message: string
  batchId: string
  estimatedSeconds: number
}

/**
 * 批量封禁玩家
 */
export const batchBan = (data: BatchBanRequest): Promise<BatchOperationResponse> => {
  return request.post('/Player/batch-ban', data)
}

// ==================== 导出功能 ====================

/**
 * 获取导出日志 URL（直接下载）
 */
export const getExportLogsUrl = (params?: { startDate?: string, endDate?: string, operationType?: string }): string => {
  const queryParams = new URLSearchParams()
  if (params?.startDate) queryParams.append('startDate', params.startDate)
  if (params?.endDate) queryParams.append('endDate', params.endDate)
  if (params?.operationType) queryParams.append('operationType', params.operationType)
  
  const queryString = queryParams.toString()
  return `/api/Audit/export${queryString ? `?${queryString}` : ''}`
}

// ==================== 二次验证辅助 ====================

/**
 * 带二次验证的扣除金币
 */
export const deductGoldWithAuth = (data: DeductGoldRequest, secondaryPassword: string): Promise<GiveItemResponse> => {
  return request.post('/Player/deduct-gold', data, {
    headers: {
      'X-Secondary-Password': secondaryPassword
    }
  })
}

/**
 * 带二次验证的发放奖励（大额）
 */
export const giveItemWithAuth = (data: GiveItemRequest, secondaryPassword: string): Promise<GiveItemResponse> => {
  return request.post('/Player/give-item', data, {
    headers: {
      'X-Secondary-Password': secondaryPassword
    }
  })
}
