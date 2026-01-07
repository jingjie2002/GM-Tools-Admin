<template>
  <div class="dashboard-container">
    <div class="dashboard-header">
      <h1 class="dashboard-title">GM 管理控制台</h1>
      <div class="user-info">
        <span class="username">{{ username }}</span>
        <el-button type="danger" size="small" @click="handleLogout">退出登录</el-button>
      </div>
    </div>

    <div class="dashboard-content">
      <!-- 统计卡片 -->
      <el-row :gutter="20" v-loading="loading">
        <el-col :span="6">
          <div class="stat-card">
            <div class="stat-icon player-icon">
              <el-icon size="32"><User /></el-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ formatNumber(stats.onlineCount) }}</span>
              <span class="stat-label">在线玩家</span>
            </div>
          </div>
        </el-col>

        <el-col :span="6">
          <div class="stat-card" @click="goToAudits" style="cursor: pointer;">
            <div class="stat-icon pending-icon">
              <el-icon size="32"><Clock /></el-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.pendingCount }}</span>
              <span class="stat-label">待审批</span>
            </div>
          </div>
        </el-col>

        <el-col :span="6">
          <div class="stat-card">
            <div class="stat-icon gold-icon">
              <el-icon size="32"><Coin /></el-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ formatGold(stats.totalGoldIssued) }}</span>
              <span class="stat-label">今日发放</span>
            </div>
          </div>
        </el-col>

        <el-col :span="6">
          <div class="stat-card">
            <div class="stat-icon ban-icon">
              <el-icon size="32"><Warning /></el-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.bannedCount }}</span>
              <span class="stat-label">封禁玩家</span>
            </div>
          </div>
        </el-col>
      </el-row>

      <!-- 快捷操作 -->
      <div class="quick-actions">
        <h2>快捷操作</h2>
        <el-row :gutter="20">
          <el-col :span="8">
            <el-card shadow="hover" class="action-card" @click="goToPlayers">
              <el-icon size="48" color="#6366f1"><Present /></el-icon>
              <h3>发放奖励</h3>
              <p>给玩家发放金币、道具等奖励</p>
            </el-card>
          </el-col>
          <el-col :span="8">
            <el-card shadow="hover" class="action-card" @click="goToAudits">
              <el-icon size="48" color="#f59e0b"><DocumentChecked /></el-icon>
              <h3>审批管理</h3>
              <p>审核大额发放申请</p>
            </el-card>
          </el-col>
          <el-col :span="8">
            <el-card shadow="hover" class="action-card" @click="goToPlayers">
              <el-icon size="48" color="#ef4444"><UserFilled /></el-icon>
              <h3>玩家管理</h3>
              <p>查询、封禁、解封玩家</p>
            </el-card>
          </el-col>
        </el-row>
      </div>

      <!-- 管理员排行 -->
      <div class="top-admins" v-if="stats.topAdmins.length > 0">
        <h2>今日发奖排行</h2>
        <el-table :data="stats.topAdmins" style="width: 100%" :header-cell-style="{ background: 'rgba(255,255,255,0.05)', color: '#fff' }">
          <el-table-column prop="adminName" label="管理员" />
          <el-table-column prop="totalAmount" label="发放总额">
            <template #default="{ row }">
              <span style="color: #10b981;">{{ formatGold(row.totalAmount) }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="operationCount" label="操作次数" />
        </el-table>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElNotification } from 'element-plus'
import { 
  User, Clock, Coin, Warning, Present, DocumentChecked, UserFilled 
} from '@element-plus/icons-vue'
import { getStatsSummary } from '@/api/gm'
import type { StatsResponse } from '@/api/gm'
import { initSignalR, disconnectSignalR, signalrEvents, SignalREvents } from '@/utils/signalr'

const router = useRouter()
const username = ref(localStorage.getItem('username') || 'Admin')
const loading = ref(false)
const signalRConnected = ref(false)

const stats = ref<StatsResponse>({
  onlineCount: 0,
  totalGoldIssued: 0,
  pendingCount: 0,
  bannedCount: 0,
  topAdmins: []
})

const formatNumber = (num: number): string => {
  return num.toLocaleString('zh-CN')
}

const formatGold = (amount: number): string => {
  if (amount >= 1000000) {
    return `¥${(amount / 1000000).toFixed(1)}M`
  } else if (amount >= 1000) {
    return `¥${(amount / 1000).toFixed(1)}K`
  }
  return `¥${amount}`
}

const fetchStats = async () => {
  loading.value = true
  try {
    const result = await getStatsSummary()
    // 防御性赋值：确保所有字段有默认值
    stats.value = {
      onlineCount: result?.onlineCount ?? 0,
      totalGoldIssued: result?.totalGoldIssued ?? 0,
      pendingCount: result?.pendingCount ?? 0,
      bannedCount: result?.bannedCount ?? 0,
      topAdmins: Array.isArray(result?.topAdmins) ? result.topAdmins : []
    }
  } catch (error) {
    console.error('Failed to fetch stats:', error)
    ElMessage.error('获取统计数据失败')
    // 保持初始状态不变
  } finally {
    loading.value = false
  }
}

// SignalR 事件处理
const onStatsUpdated = () => {
  console.log('[Dashboard] Received stats update, refreshing...')
  fetchStats()
}

const onNewPendingAudit = (data: any) => {
  ElNotification({
    title: '新待审批申请',
    message: `${data.operatorName} 申请发放 ${data.amount.toLocaleString()} 金币`,
    type: 'warning',
    duration: 5000
  })
  fetchStats()
}

const onConnectionStateChanged = (state: string) => {
  signalRConnected.value = state === 'connected'
  if (state === 'connected') {
    console.log('[Dashboard] SignalR connected')
  } else if (state === 'disconnected') {
    console.log('[Dashboard] SignalR disconnected')
  }
}

const goToPlayers = () => {
  router.push('/players')
}

const goToAudits = () => {
  router.push('/audits')
}

const handleLogout = async () => {
  await disconnectSignalR()
  localStorage.removeItem('token')
  localStorage.removeItem('roles')
  localStorage.removeItem('username')
  ElMessage.success('已退出登录')
  router.push('/login')
}

onMounted(async () => {
  fetchStats()
  
  // 初始化 SignalR 并订阅事件
  signalrEvents.on(SignalREvents.STATS_UPDATED, onStatsUpdated)
  signalrEvents.on(SignalREvents.NEW_PENDING_AUDIT, onNewPendingAudit)
  signalrEvents.on(SignalREvents.CONNECTION_STATE_CHANGED, onConnectionStateChanged)
  
  await initSignalR()
})

onUnmounted(() => {
  // 清理事件监听
  signalrEvents.off(SignalREvents.STATS_UPDATED, onStatsUpdated)
  signalrEvents.off(SignalREvents.NEW_PENDING_AUDIT, onNewPendingAudit)
  signalrEvents.off(SignalREvents.CONNECTION_STATE_CHANGED, onConnectionStateChanged)
})
</script>

<style scoped>
.dashboard-container {
  min-height: 100vh;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  padding: 24px;
}

.dashboard-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 32px;
  padding-bottom: 24px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.dashboard-title {
  font-size: 28px;
  font-weight: 700;
  color: #ffffff;
  margin: 0;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 16px;
}

.username {
  color: rgba(255, 255, 255, 0.8);
  font-size: 14px;
}

.stat-card {
  background: rgba(255, 255, 255, 0.05);
  backdrop-filter: blur(10px);
  border-radius: 16px;
  padding: 24px;
  display: flex;
  align-items: center;
  gap: 20px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  transition: all 0.3s ease;
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.3);
}

.stat-icon {
  width: 64px;
  height: 64px;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
}

.player-icon { background: linear-gradient(135deg, #6366f1, #8b5cf6); }
.pending-icon { background: linear-gradient(135deg, #f59e0b, #f97316); }
.gold-icon { background: linear-gradient(135deg, #10b981, #059669); }
.ban-icon { background: linear-gradient(135deg, #ef4444, #dc2626); }

.stat-info {
  display: flex;
  flex-direction: column;
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: #ffffff;
}

.stat-label {
  font-size: 14px;
  color: rgba(255, 255, 255, 0.5);
  margin-top: 4px;
}

.quick-actions {
  margin-top: 40px;
}

.quick-actions h2,
.top-admins h2 {
  font-size: 20px;
  color: #ffffff;
  margin-bottom: 20px;
}

.action-card {
  text-align: center;
  padding: 32px 20px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  cursor: pointer;
  transition: all 0.3s ease;
}

.action-card:hover {
  transform: translateY(-4px);
  border-color: rgba(99, 102, 241, 0.5);
}

.action-card h3 {
  color: #ffffff;
  margin: 16px 0 8px;
  font-size: 18px;
}

.action-card p {
  color: rgba(255, 255, 255, 0.5);
  font-size: 14px;
  margin: 0;
}

.action-card :deep(.el-card__body) {
  background: transparent;
}

.top-admins {
  margin-top: 40px;
}

.top-admins :deep(.el-table) {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 12px;
  --el-table-bg-color: transparent;
  --el-table-tr-bg-color: transparent;
  --el-table-text-color: rgba(255, 255, 255, 0.8);
  --el-table-border-color: rgba(255, 255, 255, 0.1);
}
</style>
