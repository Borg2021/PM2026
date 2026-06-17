<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  getProjectChanges, createProjectChange, updateProjectChange, deleteProjectChange
} from '@/api/project'
import type { ProjectChangeItem } from '@/types/project'

const props = defineProps<{
  projectId: number | null
  isReadonly: boolean
}>()

const changes = ref<ProjectChangeItem[]>([])
const changeSaving = ref(false)
const showAddChangeDialog = ref(false)
const editingChange = ref<ProjectChangeItem>({})

function openAddChange() {
  editingChange.value = {}
  showAddChangeDialog.value = true
}
function openEditChange(row: ProjectChangeItem) {
  editingChange.value = { ...row }
  showAddChangeDialog.value = true
}

async function handleSaveChange() {
  if (!props.projectId) return
  if (!editingChange.value.changeType && !editingChange.value.changeContent) {
    ElMessage.warning('请填写变更类型或变更内容'); return
  }
  changeSaving.value = true
  try {
    if (editingChange.value.id) {
      await updateProjectChange(props.projectId, editingChange.value.id as number, editingChange.value)
      const idx = changes.value.findIndex(c => c.id === editingChange.value.id)
      if (idx >= 0) changes.value[idx] = { ...editingChange.value }
    } else {
      const res = await createProjectChange(props.projectId, editingChange.value)
      changes.value.push({ ...editingChange.value, id: res.data.id })
    }
    showAddChangeDialog.value = false
    ElMessage.success('保存成功')
  } finally { changeSaving.value = false }
}

async function handleDeleteChange(row: ProjectChangeItem) {
  if (!props.projectId || !row.id) return
  await ElMessageBox.confirm('确定删除该变更记录吗？', '提示', { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' })
  await deleteProjectChange(props.projectId, row.id as number)
  changes.value = changes.value.filter(c => c.id !== row.id)
  ElMessage.success('已删除')
}

async function loadChanges() {
  if (!props.projectId) return
  const res = await getProjectChanges(props.projectId)
  changes.value = res.data
}

defineExpose({ loadChanges })
</script>

<template>
  <el-tab-pane label="变更记录" name="changes" :disabled="!projectId">
    <el-card shadow="never" class="form-card">
      <template #header>
        <div style="display:flex;justify-content:space-between;align-items:center">
          <span style="font-weight:600">变更记录</span>
          <el-button v-if="!isReadonly" type="danger" size="small" @click="openAddChange">+ 新增变更</el-button>
        </div>
      </template>
      <el-table :data="changes" border size="small" style="width:100%">
        <el-table-column type="index" label="序号" width="60" />
        <el-table-column prop="changeType" label="变更类型" width="120" show-overflow-tooltip />
        <el-table-column prop="changeParty" label="变更对象方" width="140" show-overflow-tooltip />
        <el-table-column prop="changeContent" label="变更内容" min-width="200" show-overflow-tooltip />
        <el-table-column prop="approverName" label="批复确认人" width="110" show-overflow-tooltip />
        <el-table-column label="效果截止日期" width="130">
          <template #default="{ row }">{{ row.effectEndDate?.slice(0,10) }}</template>
        </el-table-column>
        <el-table-column prop="createdByName" label="创建人" width="90" show-overflow-tooltip />
        <el-table-column label="创建时间" width="120">
          <template #default="{ row }">{{ row.createdAt?.slice(0,10) }}</template>
        </el-table-column>
        <el-table-column v-if="!isReadonly" label="操作" width="110" fixed="right">
          <template #default="{ row }">
            <el-button link style="color:#67c23a;padding:0 4px" @click="openEditChange(row)">编辑</el-button>
            <el-button link style="color:#f56c6c;padding:0 4px" @click="handleDeleteChange(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <div v-if="changes.length === 0" style="text-align:center;padding:24px;color:#909399;">暂无数据</div>
    </el-card>

    <el-dialog v-model="showAddChangeDialog" :title="editingChange.id ? '编辑变更记录' : '新增变更记录'" width="520px" :close-on-click-modal="false">
      <el-form :model="editingChange" label-width="100px">
        <el-form-item label="变更类型">
          <el-select v-model="editingChange.changeType" style="width:100%" clearable placeholder="请选择变更类型">
            <el-option label="工期变更" value="工期变更" />
            <el-option label="成本变更" value="成本变更" />
            <el-option label="范围变更" value="范围变更" />
            <el-option label="人员变更" value="人员变更" />
            <el-option label="其他" value="其他" />
          </el-select>
        </el-form-item>
        <el-form-item label="变更对象方">
          <el-input v-model="editingChange.changeParty" placeholder="如：客户方、供应商" />
        </el-form-item>
        <el-form-item label="变更内容">
          <el-input v-model="editingChange.changeContent" type="textarea" :rows="3" placeholder="请描述变更内容" />
        </el-form-item>
        <el-form-item label="批复确认人">
          <el-input v-model="editingChange.approverName" placeholder="批复确认人姓名" />
        </el-form-item>
        <el-form-item label="效果截止日期">
          <el-date-picker v-model="editingChange.effectEndDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showAddChangeDialog = false">取消</el-button>
        <el-button type="danger" :loading="changeSaving" @click="handleSaveChange">确定</el-button>
      </template>
    </el-dialog>
  </el-tab-pane>
</template>
