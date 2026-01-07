<template>
  <div class="page-container">
    <div class="page-header">
      <h1>审批管理</h1>
      <el-button @click="goBack" type="default" plain>返回 Dashboard</el-button>
    </div>

    <!-- 工具栏 -->
    <div class="toolbar">
      <el-button type="primary" @click="handleExport" :loading="exporting">
        <el-icon><Download /></el-icon>
        导出 Excel
      </el-button>
      <el-button @click="fetchPendingList" :loading="loading">
        <el-icon><Refresh /></el-icon>
        刷新
      </el-button>
    </div>

    <!-- 待审批列表 -->
    <el-table 
      :data="pendingList" 
      v-loading="loading"
      style="width: 100%"
      :header-cell-style="{ background: 'rgba(255,255,255,0.05)', color: '#fff' }"
    >
      <el-table-column label="申请时间" width="180">
        <template #default="{ row }">
          {{ formatDate(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column prop="operatorName" label="申请人" width="120" />
      <el-table-column prop="playerNickname" label="目标玩家" width="150" />
      <el-table-column prop="itemType" label="道具类型" width="120">
        <template #default="{ row }">
          <el-tag type="info">{{ row.itemType }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="金额" width="150">
        <template #default="{ row }">
          <span style="color: #f59e0b; font-weight: 600;">
            {{ row.amount.toLocaleString() }}
          </span>
        </template>
      </el-table-column>
      <el-table-column label="操作" fixed="right" width="200">
        <template #default="{ row }">
          <el-button 
            type="success" 
            size="small"
            @click="handleApprove(row)"
            :loading="row.processing"
          >
            通过
          </el-button>
          <el-button 
            type="danger" 
            size="small"
            @click="openRejectDialog(row)"
            :loading="row.processing"
          >
            拒绝
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 空状态 -->
    <div v-if="!loading && pendingList.length === 0" class="empty-state">
      <el-icon size="64" color="rgba(255,255,255,0.3)"><DocumentChecked /></el-icon>
      <p>暂无待审批申请</p>
    </div>

    <!-- 拒绝原因对话框 -->
    <el-dialog
      v-model="rejectDialogVisible"
      title="拒绝原因"
      width="400px"
    >
      <el-input
        v-model="rejectReason"
        type="textarea"
        :rows="3"
        placeholder="请输入拒绝原因（必填）"
      />
      <template #footer>
        <el-button @click="rejectDialogVisible = false">取消</el-button>
        <el-button 
          type="danger" 
          @click="handleReject"
          :disabled="!rejectReason.trim()"
          :loading="submitting"
        >
          确认拒绝
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { DocumentChecked, Download, Refresh } from '@element-plus/icons-vue'
import { getPendingAudits, auditDecide } from '@/api/gm'
import type { PendingOperation } from '@/api/gm'
import request from '@/utils/request'

interface ProcessablePendingOperation extends PendingOperation {
  processing?: boolean
}

const router = useRouter()

// 状态
const loading = ref(false)
const submitting = ref(false)
const exporting = ref(false)
const pendingList = ref<ProcessablePendingOperation[]>([])

// 拒绝对话框
const rejectDialogVisible = ref(false)
const rejectReason = ref('')
const selectedItem = ref<ProcessablePendingOperation | null>(null)

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('zh-CN')
}

const fetchPendingList = async () => {
  loading.value = true
  try {
    const result = await getPendingAudits()
    let items: PendingOperation[] = []
    if (Array.isArray(result)) {
      items = result
    } else if (result && typeof result === 'object' && 'data' in result) {
      items = Array.isArray((result as any).data) ? (result as any).data : []
    }
    pendingList.value = items.map(item => ({ ...item, processing: false }))
  } catch (error) {
    console.error('Failed to fetch pending audits:', error)
    ElMessage.error('获取待审批列表失败')
    pendingList.value = []
  } finally {
    loading.value = false
  }
}

// 导出 Excel
const handleExport = async () => {
  exporting.value = true
  try {
    const response = await request.get('/Audit/export', {
      responseType: 'blob'
    })
    
    // 创建 Blob 下载
    const blob = new Blob([response as any], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    
    const now = new Date()
    const timestamp = `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}${String(now.getDate()).padStart(2, '0')}`
    link.href = url
    link.download = `GM_Audit_Logs_${timestamp}.xlsx`
    
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
    
    ElMessage.success('导出成功')
  } catch (error) {
    console.error('Export failed:', error)
    ElMessage.error('导出失败，请重试')
  } finally {
    exporting.value = false
  }
}


const handleApprove = async (item: ProcessablePendingOperation) => {
  item.processing = true
  try {
    await auditDecide({
      logId: item.logId,
      action: 'Approve'
    })
    ElMessage.success('审批通过！已发放至玩家账户')
    fetchPendingList()
  } catch (error) {
    console.error('Approve failed:', error)
  } finally {
    item.processing = false
  }
}

const openRejectDialog = (item: ProcessablePendingOperation) => {
  selectedItem.value = item
  rejectReason.value = ''
  rejectDialogVisible.value = true
}

const handleReject = async () => {
  if (!selectedItem.value || !rejectReason.value.trim()) return

  submitting.value = true
  try {
    await auditDecide({
      logId: selectedItem.value.logId,
      action: 'Reject',
      reason: rejectReason.value
    })
    ElMessage.success('已拒绝该申请')
    rejectDialogVisible.value = false
    fetchPendingList()
  } catch (error) {
    console.error('Reject failed:', error)
  } finally {
    submitting.value = false
  }
}

const goBack = () => {
  router.push('/dashboard')
}

onMounted(() => {
  fetchPendingList()
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

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px;
  color: rgba(255, 255, 255, 0.5);
}

.empty-state p {
  margin-top: 16px;
  font-size: 16px;
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

.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}
</style>
