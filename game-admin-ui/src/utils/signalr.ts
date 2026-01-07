import * as signalR from '@microsoft/signalr'

// ==================== SignalR 客户端配置 ====================

const HUB_URL = 'http://localhost:5201/hubs/gm'

// 简单事件总线
type EventCallback = (...args: unknown[]) => void
const eventBus = new Map<string, Set<EventCallback>>()

export const signalrEvents = {
  on(event: string, callback: EventCallback) {
    if (!eventBus.has(event)) {
      eventBus.set(event, new Set())
    }
    eventBus.get(event)!.add(callback)
  },
  off(event: string, callback: EventCallback) {
    eventBus.get(event)?.delete(callback)
  },
  emit(event: string, ...args: unknown[]) {
    eventBus.get(event)?.forEach(cb => cb(...args))
  }
}

// 事件名称常量
export const SignalREvents = {
  STATS_UPDATED: 'StatsUpdated',
  NEW_PENDING_AUDIT: 'NewPendingAudit',
  PLAYER_STATUS_CHANGED: 'PlayerStatusChanged',
  BATCH_JOB_FINISHED: 'BatchJobFinished',
  CONNECTION_STATE_CHANGED: 'ConnectionStateChanged'
} as const

let connection: signalR.HubConnection | null = null
const MAX_RECONNECT_ATTEMPTS = 5

/**
 * 获取 SignalR 连接实例
 */
export function getConnection(): signalR.HubConnection | null {
  return connection
}

/**
 * 初始化 SignalR 连接
 */
export async function initSignalR(): Promise<void> {
  const token = localStorage.getItem('token')
  
  if (!token) {
    console.warn('[SignalR] No token found, skipping connection')
    return
  }

  // 如果已连接，先断开
  if (connection) {
    await connection.stop()
  }

  // 创建连接 - Token 通过 QueryString 传递
  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${HUB_URL}?access_token=${encodeURIComponent(token)}`)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext: signalR.RetryContext): number | null => {
        if (retryContext.previousRetryCount >= MAX_RECONNECT_ATTEMPTS) {
          return null // 停止重连
        }
        return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000)
      }
    })
    .configureLogging(signalR.LogLevel.Information)
    .build()

  // 监听服务端推送事件
  connection.on('StatsUpdated', () => {
    console.log('[SignalR] Received: StatsUpdated')
    signalrEvents.emit(SignalREvents.STATS_UPDATED)
  })

  connection.on('NewPendingAudit', (data: unknown) => {
    console.log('[SignalR] Received: NewPendingAudit', data)
    signalrEvents.emit(SignalREvents.NEW_PENDING_AUDIT, data)
  })

  connection.on('PlayerStatusChanged', (data: unknown) => {
    console.log('[SignalR] Received: PlayerStatusChanged', data)
    signalrEvents.emit(SignalREvents.PLAYER_STATUS_CHANGED, data)
  })

  connection.on('BatchJobFinished', (data: unknown) => {
    console.log('[SignalR] Received: BatchJobFinished', data)
    signalrEvents.emit(SignalREvents.BATCH_JOB_FINISHED, data)
  })

  // 连接状态变化
  connection.onclose((error: Error | undefined) => {
    console.warn('[SignalR] Connection closed', error)
    signalrEvents.emit(SignalREvents.CONNECTION_STATE_CHANGED, 'disconnected')
  })

  connection.onreconnecting((error: Error | undefined) => {
    console.log('[SignalR] Reconnecting...', error)
    signalrEvents.emit(SignalREvents.CONNECTION_STATE_CHANGED, 'reconnecting')
  })

  connection.onreconnected((connectionId: string | undefined) => {
    console.log('[SignalR] Reconnected:', connectionId)
    signalrEvents.emit(SignalREvents.CONNECTION_STATE_CHANGED, 'connected')
  })

  // 启动连接
  try {
    await connection.start()
    console.log('[SignalR] Connected successfully')
    signalrEvents.emit(SignalREvents.CONNECTION_STATE_CHANGED, 'connected')
  } catch (err) {
    console.error('[SignalR] Connection failed:', err)
    signalrEvents.emit(SignalREvents.CONNECTION_STATE_CHANGED, 'failed')
  }
}

/**
 * 断开 SignalR 连接
 */
export async function disconnectSignalR(): Promise<void> {
  if (connection) {
    await connection.stop()
    connection = null
    console.log('[SignalR] Disconnected')
  }
}

/**
 * 获取连接状态
 */
export function getConnectionState(): string {
  if (!connection) return 'disconnected'
  return signalR.HubConnectionState[connection.state]
}
