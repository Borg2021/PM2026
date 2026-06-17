<script setup lang="ts">
import { ref } from 'vue'
import { getProjectOperationLogs, type OperationLogItem } from '@/api/project'
import type { UserInfo } from '@/types/template'

const props = defineProps<{
  projectId: number | null
  users: UserInfo[]
  deptTreeData: any[]
}>()

const operationLogs = ref<OperationLogItem[]>([])
const opLogLoading = ref(false)
const opLogDateRange = ref<[string, string] | null>(null)
const opLogDeptId = ref<number | null>(null)
const opLogUserId = ref<number | null>(null)

async function loadOperationLogs() {
  if (!props.projectId) return
  opLogLoading.value = true
  try {
    const params: Record<string, any> = {}
    if (opLogDateRange.value) {
      params.startDate = opLogDateRange.value[0]
      params.endDate = opLogDateRange.value[1]
    }
    if (opLogDeptId.value) params.deptId = opLogDeptId.value
    if (opLogUserId.value) params.userId = opLogUserId.value
    const res = await getProjectOperationLogs(props.projectId, params)
    operationLogs.value = res.data
  } finally { opLogLoading.value = false }
}

function handleOpLogFilter() {
  loadOperationLogs()
}

function handleOpLogReset() {
  opLogDateRange.value = null
  opLogDeptId.value = null
  opLogUserId.value = null
  loadOperationLogs()
}

function getOpColor(operation: string): string {
  if (operation.includes('新增') || operation.includes('创建') || operation.includes('从模板')) return '#67C23A'
  if (operation.includes('编辑') || operation.includes('更新')) return '#409EFF'
  if (operation.includes('删除')) return '#F56C6C'
  if (operation.includes('激活') || operation.includes('完成') || operation.includes('暂停')) return '#E6A23C'
  return '#909399'
}

function formatOpContent(content: string): string {
  return content
    .replace(/【(.+?)】/g, '<span style="color:#F06595;font-weight:600">【$1】</span>')
    .replace(/→/g, '<span style="color:#909399">→</span>')
    .replace(/「(.+?)」/g, '<span style="color:#E6A23C;font-weight:500">「$1」</span>')
}

defineExpose({ loadOperationLogs })
</script>

<template>
  <el-tab-pane label="操作日志" name="oplog" :disabled="!projectId">
    <el-card shadow="never" class="form-card" v-loading="opLogLoading">
      <template #header><span style="font-weight:600">操作日志</span></template>
      <div style="display:flex;flex-wrap:wrap;gap:12px;align-items:center;margin-bottom:16px">
        <el-date-picker v-model="opLogDateRange" type="daterange" range-separator="至" start-placeholder="开始日期" end-placeholder="结束日期" value-format="YYYY-MM-DD" style="width:220px;flex:none" />
        <el-tree-select v-model="opLogDeptId" :data="deptTreeData" :props="{ label: 'name', children: 'children', value: 'id' }" node-key="id" placeholder="责任部门" clearable check-strictly style="width:180px" />
        <el-select v-model="opLogUserId" placeholder="操作人" clearable filterable style="width:150px">
          <el-option v-for="u in users" :key="u.id" :label="u.realName" :value="u.id" />
        </el-select>
        <el-button type="primary" style="margin-left:auto" @click="handleOpLogFilter">查询</el-button>
        <el-button @click="handleOpLogReset">重置</el-button>
      </div>
      <div v-if="operationLogs.length === 0 && !opLogLoading" style="text-align:center;padding:48px;color:#909399;">暂无操作记录</div>
      <el-timeline v-else>
        <el-timeline-item
          v-for="log in operationLogs"
          :key="log.id"
          :timestamp="log.createdAt?.replace('T',' ').slice(0,19)"
          placement="top"
            :color="getOpColor(log.operation)"
        >
          <el-card shadow="hover" size="small">
            <div style="display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px">
              <span :style="{ fontWeight: 600, color: getOpColor(log.operation) }">{{ log.operation }}</span>
              <span style="font-size:12px;color:#909399">{{ log.userName }}</span>
            </div>
            <div style="font-size:13px;color:#606266;margin-top:4px" v-html="formatOpContent(log.content)" />
          </el-card>
        </el-timeline-item>
      </el-timeline>
    </el-card>
  </el-tab-pane>
</template>
