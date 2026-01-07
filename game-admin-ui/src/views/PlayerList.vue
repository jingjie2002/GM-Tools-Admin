<template>
  <div class="page-container">
    <div class="page-header">
      <h1>玩家管理</h1>
      <el-button @click="goBack" type="default" plain>返回 Dashboard</el-button>
    </div>

    <!-- 搜索区域 -->
    <div class="search-area">
      <el-input
        v-model="searchKeyword"
        placeholder="搜索玩家昵称"
        clearable
        style="width: 300px"
        @keyup.enter="handleSearch"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
      <el-button type="primary" @click="handleSearch">搜索</el-button>
    </div>

    <!-- 批量操作工具栏 -->
    <div class="batch-toolbar" v-if="multipleSelection.length > 0">
      <span class="selection-info">已选择 {{ multipleSelection.length }} 个玩家</span>
      <el-button 
        type="danger" 
        @click="handleBatchBan"
        :loading="batchBanning"
      >
        批量封禁
      </el-button>
      <el-button @click="clearSelection">取消选择</el-button>
    </div>

    <!-- 玩家列表 -->
    <el-table 
      ref="tableRef"
      :data="players" 
      v-loading="loading"
      style="width: 100%"
      :header-cell-style="{ background: 'rgba(255,255,255,0.05)', color: '#fff' }"
      @selection-change="handleSelectionChange"
    >
      <el-table-column type="selection" width="55" />
      <el-table-column prop="nickname" label="昵称" width="180" />
      <el-table-column prop="level" label="等级" width="100" />
      <el-table-column prop="gold" label="金币" width="150">
        <template #default="{ row }">
          <span style="color: #10b981;">{{ row.gold.toLocaleString() }}</span>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="120">
        <template #default="{ row }">
          <el-tag :type="row.isBanned ? 'danger' : 'success'">
            {{ row.isBanned ? '已封禁' : '正常' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="注册时间" width="180">
        <template #default="{ row }">
          {{ formatDate(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column label="操作" fixed="right" width="320">
        <template #default="{ row }">
          <!-- 解封按钮 -->
          <el-button 
            v-if="row.isBanned"
            type="success" 
            size="small" 
            @click="handleUnban(row)"
          >
            解封
          </el-button>
          
          <!-- 发放按钮 -->
          <el-button 
            v-if="!row.isBanned"
            type="primary" 
            size="small" 
            @click="openGiveDialog(row)"
          >
            发放
          </el-button>

          <!-- 扣除按钮 -->
          <el-button 
            v-if="!row.isBanned"
            type="danger" 
            size="small" 
            @click="openDeductDialog(row)"
          >
            扣除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 分页 -->
    <div class="pagination-area">
      <el-pagination
        v-model:current-page="currentPage"
        v-model:page-size="pageSize"
        :total="totalCount"
        :page-sizes="[10, 20, 50]"
        layout="total, sizes, prev, pager, next"
        @size-change="fetchPlayers"
        @current-change="fetchPlayers"
      />
    </div>

    <!-- 发放奖励对话框 -->
    <el-dialog
      v-model="giveDialogVisible"
      title="发放奖励"
      width="500px"
      :close-on-click-modal="false"
    >
      <el-form :model="giveForm" label-width="100px">
        <el-form-item label="玩家昵称">
          <el-input :model-value="selectedPlayer?.nickname" disabled />
        </el-form-item>
        <el-form-item label="当前金币">
          <el-input :model-value="selectedPlayer?.gold.toLocaleString()" disabled />
        </el-form-item>
        <el-form-item label="道具类型">
          <el-select v-model="giveForm.itemType" placeholder="请选择">
            <el-option label="金币" value="Gold" />
            <el-option label="钻石" value="Diamond" />
            <el-option label="礼包" value="GiftPack" />
          </el-select>
        </el-form-item>
        <el-form-item label="发放数量">
          <el-input-number 
            v-model="giveForm.amount" 
            :min="1" 
            :max="1000000"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="giveDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleGiveItem" :loading="submitting">
          确认发放
        </el-button>
      </template>
    </el-dialog>

    <!-- 扣除对话框 -->
    <el-dialog
      v-model="deductDialogVisible"
      title="扣除金币 (谨慎操作)"
      width="500px"
      :close-on-click-modal="false"
    >
      <el-alert
        title="此操作将直接扣除玩家余额，请确保原因填写正确。"
        type="warning"
        show-icon
        :closable="false"
        style="margin-bottom: 20px;"
      />
      <el-form :model="deductForm" label-width="100px">
        <el-form-item label="玩家昵称">
          <el-input :model-value="selectedPlayer?.nickname" disabled />
        </el-form-item>
        <el-form-item label="当前金币">
          <el-input :model-value="selectedPlayer?.gold.toLocaleString()" disabled />
        </el-form-item>
        <el-form-item label="扣除金额">
          <el-input-number 
            v-model="deductForm.amount" 
            :min="1" 
            :max="999999999"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="扣除原因">
          <el-input v-model="deductForm.reason" placeholder="请输入扣除原因（必填）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="deductDialogVisible = false">取消</el-button>
        <el-button type="danger" @click="handleDeductGold" :loading="submitting">
          确认扣除
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElNotification, ElMessageBox } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import { searchPlayers, giveItem, unbanPlayer, batchBan, deductGoldWithAuth } from '@/api/gm'
import type { Player, GiveItemRequest } from '@/api/gm'
import type { ElTable } from 'element-plus'
import { signalrEvents, SignalREvents } from '@/utils/signalr'
import type { PlayerStatusChangedData, BatchJobFinishedData } from '@/utils/signalr'

const router = useRouter()

// 状态
const loading = ref(false)
const submitting = ref(false)
const players = ref<Player[]>([])
const searchKeyword = ref('')
const currentPage = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)

// 多选
const tableRef = ref<InstanceType<typeof ElTable>>()
const multipleSelection = ref<Player[]>([])
const batchBanning = ref(false)

// 选中玩家 (单个操作)
const selectedPlayer = ref<Player | null>(null)

// 发放对话框
const giveDialogVisible = ref(false)
const giveForm = ref({
  itemType: 'Gold',
  amount: 100
})

// 扣除对话框
const deductDialogVisible = ref(false)
const deductForm = ref({
  amount: 100,
  reason: ''
})

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('zh-CN')
}

const fetchPlayers = async () => {
  loading.value = true
  try {
    const result = await searchPlayers({
      keyword: searchKeyword.value,
      page: currentPage.value,
      pageSize: pageSize.value
    })
    players.value = result.items
    totalCount.value = result.totalCount
  } catch (error) {
    console.error('Failed to fetch players:', error)
    ElMessage.error('获取玩家列表失败')
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  currentPage.value = 1
  fetchPlayers()
}

// ==================== 发放逻辑 ====================
const openGiveDialog = (player: Player) => {
  selectedPlayer.value = player
  giveForm.value = { itemType: 'Gold', amount: 100 }
  giveDialogVisible.value = true
}

const handleGiveItem = async () => {
  if (!selectedPlayer.value) return

  const APPROVAL_THRESHOLD = 5000

  submitting.value = true
  try {
    const request: GiveItemRequest = {
      playerId: selectedPlayer.value.id,
      itemType: giveForm.value.itemType,
      amount: giveForm.value.amount
    }

    const result = await giveItem(request)

    if (result.status === 'Pending') {
      ElNotification({
        title: '已触发大额审批',
        message: `金额 ${giveForm.value.amount} 超过 ${APPROVAL_THRESHOLD}，请前往审批中心处理`,
        type: 'warning',
        duration: 5000
      })
    } else {
      ElMessage.success(`发放成功！新余额: ${result.newBalance.toLocaleString()}`)
    }

    giveDialogVisible.value = false
    fetchPlayers()
  } catch (error) {
    console.error('Give item failed:', error)
  } finally {
    submitting.value = false
  }
}

// ==================== 扣除逻辑 ====================
const openDeductDialog = (player: Player) => {
  selectedPlayer.value = player
  deductForm.value = { amount: 100, reason: '' }
  deductDialogVisible.value = true
}

const handleDeductGold = async () => {
  if (!selectedPlayer.value) return
  if (!deductForm.value.reason.trim()) {
    ElMessage.warning('请输入扣除原因')
    return
  }

  // 二次验证：弹出密码输入框
  let password: string
  try {
    const { value } = await ElMessageBox.prompt(
      `确定要扣除玩家 ${selectedPlayer.value.nickname} 的 ${deductForm.value.amount} 金币吗？\n\n请输入您的登录密码进行二次验证：`,
      '高危操作 - 安全验证',
      {
        confirmButtonText: '确认扣除',
        cancelButtonText: '取消',
        inputType: 'password',
        inputPlaceholder: '请输入登录密码',
        inputPattern: /.+/,
        inputErrorMessage: '请输入密码',
        type: 'warning',
        confirmButtonClass: 'el-button--danger'
      }
    )
    password = value
  } catch {
    return // 用户取消
  }

  submitting.value = true
  try {
    const result = await deductGoldWithAuth({
      playerId: selectedPlayer.value.id,
      amount: deductForm.value.amount,
      reason: deductForm.value.reason
    }, password)

    ElMessage.success(`扣除成功！新余额: ${result.newBalance.toLocaleString()}`)
    
    deductDialogVisible.value = false
    fetchPlayers()
  } catch (error: any) {
    console.error('Deduct failed:', error)
    // 显示后端返回的错误信息
    const errMsg = error?.response?.data?.message || error?.message || '扣除失败，请重试'
    ElMessage.error(errMsg)
    // 不关闭对话框，让用户可以修正输入
  } finally {
    submitting.value = false
  }
}

// ==================== 解封逻辑 ====================
const handleUnban = async (player: Player) => {
  try {
    await ElMessageBox.confirm(
      `确定要解封玩家 ${player.nickname} 吗？解封后玩家将立即恢复登录。`,
      '解封确认',
      {
        confirmButtonText: '确认解封',
        cancelButtonText: '取消',
        type: 'info'
      }
    )
  } catch {
    return
  }

  loading.value = true
  try {
    await unbanPlayer({
      playerId: player.id,
      reason: 'GM 主动解封'
    })
    ElMessage.success('解封成功')
    fetchPlayers()
  } catch (error) {
    console.error('Unban failed:', error)
  } finally {
    loading.value = false
  }
}

// ==================== 多选与批量操作 ====================
const handleSelectionChange = (val: Player[]) => {
  multipleSelection.value = val
}

const clearSelection = () => {
  tableRef.value?.clearSelection()
  multipleSelection.value = []
}

const handleBatchBan = async () => {
  if (multipleSelection.value.length === 0) return

  // 确认弹窗
  try {
    const { value: reason } = await ElMessageBox.prompt(
      `确定要封禁选中的 ${multipleSelection.value.length} 个玩家吗？请输入封禁原因：`,
      '批量封禁确认',
      {
        confirmButtonText: '确认封禁',
        cancelButtonText: '取消',
        inputPattern: /.+/,
        inputErrorMessage: '请输入封禁原因',
        type: 'warning'
      }
    )

    batchBanning.value = true
    
    const playerIds = multipleSelection.value.map(p => p.id)
    const result = await batchBan({
      playerIds,
      reason: reason || '批量封禁',
      durationHours: 0 // 永久
    })

    ElNotification({
      title: '批量封禁已提交',
      message: `${result.message}，预计处理时间: ${result.estimatedSeconds} 秒`,
      type: 'success',
      duration: 5000
    })

    clearSelection()
    fetchPlayers()
  } catch {
    // 用户取消
  } finally {
    batchBanning.value = false
  }
}

const goBack = () => {
  router.push('/dashboard')
}

// SignalR 实时更新
const onStatsUpdated = () => {
  console.log('[PlayerList] Received StatsUpdated, refreshing...')
  fetchPlayers()
}

const onPlayerStatusChanged = (data: PlayerStatusChangedData) => {
  console.log('[PlayerList] Player status changed:', data.PlayerId, data.Status)
  
  // 局部更新：找到对应玩家并更新状态，实现视觉即时变红
  if (data.PlayerId && data.Status === 'Banned') {
    const player = players.value.find(p => p.id === data.PlayerId)
    if (player) {
      player.isBanned = true
      console.log('[PlayerList] Locally updated player', data.PlayerId, 'to banned')
    }
  }
}

const onBatchJobFinished = (data: BatchJobFinishedData) => {
  // 关闭处理中提示
  batchBanning.value = false
  
  ElNotification({
    title: '批量封禁完成',
    message: `批次 ${data.BatchId}: 成功 ${data.SuccessCount}, 失败 ${data.FailedCount}`,
    type: data.FailedCount ? 'warning' : 'success',
    duration: 5000
  })
  
  fetchPlayers()
}

onMounted(() => {
  fetchPlayers()
  
  // 订阅 SignalR 事件
  signalrEvents.on(SignalREvents.STATS_UPDATED, onStatsUpdated)
  signalrEvents.on(SignalREvents.PLAYER_STATUS_CHANGED, onPlayerStatusChanged)
  signalrEvents.on(SignalREvents.BATCH_JOB_FINISHED, onBatchJobFinished)
})

onUnmounted(() => {
  // 清理事件监听
  signalrEvents.off(SignalREvents.STATS_UPDATED, onStatsUpdated)
  signalrEvents.off(SignalREvents.PLAYER_STATUS_CHANGED, onPlayerStatusChanged)
  signalrEvents.off(SignalREvents.BATCH_JOB_FINISHED, onBatchJobFinished)
})
</script>

<style scoped>
.page-container {
  min-height: 100vh;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h1 {
  font-size: 24px;
  color: #ffffff;
  margin: 0;
}

.search-area {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}

:deep(.el-table) {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 12px;
  --el-table-bg-color: transparent;
  --el-table-tr-bg-color: transparent;
  --el-table-text-color: rgba(255, 255, 255, 0.8);
  --el-table-border-color: rgba(255, 255, 255, 0.1);
}

:deep(.el-table__row:hover) {
  --el-table-tr-bg-color: rgba(255, 255, 255, 0.05);
}

.pagination-area {
  display: flex;
  justify-content: flex-end;
  margin-top: 20px;
}

:deep(.el-pagination) {
  --el-pagination-bg-color: transparent;
  --el-pagination-text-color: rgba(255, 255, 255, 0.8);
  --el-pagination-button-bg-color: rgba(255, 255, 255, 0.1);
}

:deep(.el-dialog) {
  background: #1e293b;
  border-radius: 12px;
}

:deep(.el-dialog__header) {
  color: #ffffff;
}

:deep(.el-dialog__title) {
  color: #ffffff;
}

:deep(.el-form-item__label) {
  color: rgba(255, 255, 255, 0.8);
}

:deep(.el-alert) {
  background: rgba(245, 158, 11, 0.1);
  border: 1px solid rgba(245, 158, 11, 0.2);
  color: #f59e0b;
}

:deep(.el-alert__title) {
  color: #f59e0b;
}

:deep(.el-alert__icon) {
  color: #f59e0b;
}

.batch-toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: rgba(239, 68, 68, 0.1);
  border-radius: 8px;
  border: 1px solid rgba(239, 68, 68, 0.3);
}

.selection-info {
  color: #ef4444;
  font-weight: 600;
}
</style>
