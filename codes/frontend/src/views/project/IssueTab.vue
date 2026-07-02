<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getProjectIssues, createProjectIssue, updateProjectIssue, deleteProjectIssue, updateIssueStatus } from '@/api/issue'
import { issueStatusMap, issueStatusTagType, type ProjectIssue, type CreateIssueRequest } from '@/types/issue'
import IssueDialog from './IssueDialog.vue'

const props = defineProps<{ projectId: number }>()

const loading = ref(false)
const list = ref<ProjectIssue[]>([])
const total = ref(0)
const pagination = reactive({ page: 1, pageSize: 10 })
const filters = reactive({
  keyword: '', issueType: '', issueSource: '', severity: '', priority: '', status: null as number | null
})

const dialogVisible = ref(false)
const editingIssue = ref<ProjectIssue | null>(null)
const dialogRef = ref<InstanceType<typeof IssueDialog> | null>(null)

async function fetchData() {
  loading.value = true
  try {
    const res = await getProjectIssues(props.projectId, {
      ...filters, pageIndex: pagination.page, pageSize: pagination.pageSize
    })
    list.value = res.data?.items || []
    total.value = res.data?.total || 0
  } finally { loading.value = false }
}

function handleCreate() {
  editingIssue.value = null
  dialogVisible.value = true
}

function handleEdit(row: ProjectIssue) {
  editingIssue.value = row
  dialogVisible.value = true
}

async function handleSaved() {
  dialogVisible.value = false
  await fetchData()
}

async function handleDelete(row: ProjectIssue) {
  try {
    await ElMessageBox.confirm(`确认删除问题「${row.title}」吗？`, '删除确认', {
      confirmButtonText: '确认删除', cancelButtonText: '取消', type: 'warning'
    })
    await deleteProjectIssue(props.projectId, row.id)
    ElMessage.success('删除成功')
    await fetchData()
  } catch { /* 取消 */ }
}

async function handleStatusChange(row: ProjectIssue, newStatus: number) {
  try {
    await updateIssueStatus(props.projectId, row.id, { status: newStatus })
    ElMessage.success(newStatus === 1 && row.status === 2 ? '已重新打开' : '状态已更新')
    await fetchData()
  } catch { /* ignore */ }
}

function handleSizeChange(size: number) { pagination.pageSize = size; pagination.page = 1; fetchData() }
function handlePageChange(page: number) { pagination.page = page; fetchData() }

onMounted(fetchData)
</script>

<template>
  <div class="issue-tab">
    <!-- 工具栏 -->
    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px;flex-wrap:wrap;gap:8px">
      <div style="display:flex;gap:8px;flex-wrap:wrap;align-items:center">
        <el-input v-model="filters.keyword" placeholder="标题/编号搜索" clearable style="width:180px" size="small" @keyup.enter="fetchData" @clear="fetchData" />
        <el-select v-model="filters.issueType" placeholder="问题类型" clearable size="small" style="width:120px" @change="fetchData">
          <el-option label="技术缺陷" value="技术缺陷" /><el-option label="需求变更" value="需求变更" />
          <el-option label="进度风险" value="进度风险" /><el-option label="资源问题" value="资源问题" />
          <el-option label="质量问题" value="质量问题" /><el-option label="其他" value="其他" />
        </el-select>
        <el-select v-model="filters.severity" placeholder="严重程度" clearable size="small" style="width:100px" @change="fetchData">
          <el-option label="重要" value="重要" /><el-option label="一般" value="一般" />
        </el-select>
        <el-select v-model="filters.priority" placeholder="优先级" clearable size="small" style="width:100px" @change="fetchData">
          <el-option label="紧急" value="紧急" /><el-option label="一般" value="一般" />
        </el-select>
        <el-select v-model="filters.status" placeholder="状态" clearable size="small" style="width:100px" @change="fetchData">
          <el-option label="待处理" :value="0" /><el-option label="处理中" :value="1" /><el-option label="已完成" :value="2" />
        </el-select>
        <el-button size="small" @click="fetchData">查询</el-button>
      </div>
      <el-button type="primary" size="small" @click="handleCreate">新建问题</el-button>
    </div>

    <!-- 表格 -->
    <el-table :data="list" border size="small" v-loading="loading" max-height="calc(100vh - 300px)">
      <el-table-column label="编号" prop="issueCode" width="150" />
      <el-table-column label="标题" prop="title" min-width="180" show-overflow-tooltip />
      <el-table-column label="类型" prop="issueType" width="90" />
      <el-table-column label="严重程度" prop="severity" width="90">
        <template #default="{ row }"><el-tag v-if="row.severity === '重要'" type="danger" size="small">重要</el-tag><el-tag v-else size="small" type="info">一般</el-tag></template>
      </el-table-column>
      <el-table-column label="优先级" prop="priority" width="80">
        <template #default="{ row }"><el-tag v-if="row.priority === '紧急'" type="warning" size="small">紧急</el-tag><span v-else style="color:#909399">一般</span></template>
      </el-table-column>
      <el-table-column label="状态" width="80">
        <template #default="{ row }"><el-tag :type="issueStatusTagType[row.status]" size="small">{{ issueStatusMap[row.status] }}</el-tag></template>
      </el-table-column>
      <el-table-column label="责任人" prop="assigneeName" width="90" />
      <el-table-column label="责任部门" prop="responsibleDeptName" width="100" show-overflow-tooltip />
      <el-table-column label="发现日期" width="105">
        <template #default="{ row }">{{ row.discoveredDate?.slice(0, 10) }}</template>
      </el-table-column>
      <el-table-column label="计划完成" width="105">
        <template #default="{ row }">{{ row.plannedDate?.slice(0, 10) || '-' }}</template>
      </el-table-column>
      <el-table-column label="重新打开" prop="reopenCount" width="80" align="center" />
      <el-table-column label="操作" width="220" fixed="right">
        <template #default="{ row }">
          <el-button v-if="row.status === 0" link size="small" type="primary" @click="handleStatusChange(row, 1)">开始处理</el-button>
          <el-button v-if="row.status === 1" link size="small" type="success" @click="handleStatusChange(row, 2)">完成</el-button>
          <el-button v-if="row.status === 2" link size="small" type="warning" @click="handleStatusChange(row, 1)">重新打开</el-button>
          <el-button link size="small" @click="handleEdit(row)">编辑</el-button>
          <el-button link size="small" type="danger" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div style="display:flex;justify-content:flex-end;margin-top:12px">
      <el-pagination v-model:current-page="pagination.page" v-model:page-size="pagination.pageSize"
        :page-sizes="[10, 20, 50]" :total="total" layout="total, sizes, prev, pager, next" small
        @size-change="handleSizeChange" @current-change="handlePageChange" />
    </div>

    <IssueDialog :visible="dialogVisible" :issue="editingIssue" :project-id="props.projectId"
      @update:visible="dialogVisible = $event" @saved="handleSaved" ref="dialogRef" />
  </div>
</template>
