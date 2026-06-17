<script setup lang="ts">
import { reactive, ref, shallowRef, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getProjectList, deleteProject } from '@/api/project'
import { openProjectView } from '@/utils/projectNav'
import { useAuthStore } from '@/store/auth'
import { getDictByType } from '@/api/template'
import type { Project } from '@/types/project'

const router = useRouter()

/* ───────── 搜索表单 ───────── */
const searchForm = reactive({
  projectCode: '',
  projectName: '',
  projectType: '',
  status: null as number | null,
  projectManagerName: ''
})

const statusOptions = [
  { value: 0, label: '未激活' },
  { value: 1, label: '进行中' },
  { value: 2, label: '已完成' },
  { value: 3, label: '暂停' }
]

/* ───────── 分页 ───────── */
const pagination = reactive({ page: 1, pageSize: 20, total: 0 })

/* ───────── 表格数据 ───────── */
const tableData = shallowRef<Project[]>([])
const loading = ref(false)
const projectTypeMap = ref<Record<string, string>>({})

/* ───────── 日期格式化 ───────── */
function formatDate(dateStr?: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日`
}

/* ───────── 状态标签颜色 ───────── */
function statusTagType(status: number): '' | 'success' | 'warning' | 'info' | 'danger' {
  const map: Record<number, '' | 'success' | 'warning' | 'info' | 'danger'> = {
    0: 'info',
    1: 'primary',
    2: 'success',
    3: 'warning'
  }
  return map[status] ?? 'info'
}

/* ───────── 获取列表 ───────── */
async function fetchData() {
  loading.value = true
  try {
    const params: Record<string, any> = {
      pageIndex: pagination.page,
      pageSize: pagination.pageSize
    }
    if (searchForm.projectCode) params.projectCode = searchForm.projectCode
    if (searchForm.projectName) params.projectName = searchForm.projectName
    if (searchForm.projectType) params.projectType = searchForm.projectType
    if (searchForm.status !== null) params.status = searchForm.status
    if (searchForm.projectManagerName) params.projectManagerName = searchForm.projectManagerName

    const res = await getProjectList(params)
    tableData.value = res.data.items
    pagination.total = res.data.total
  } catch {
    // 错误已由请求拦截器统一处理
  } finally {
    loading.value = false
  }
}

/* ───────── 搜索 / 重置 ───────── */
function handleSearch() {
  pagination.page = 1
  fetchData()
}

function handleReset() {
  searchForm.projectCode = ''
  searchForm.projectName = ''
  searchForm.projectType = ''
  searchForm.status = null
  searchForm.projectManagerName = ''
  pagination.page = 1
  fetchData()
}

/* ───────── 分页 ───────── */
function handleSizeChange(size: number) { pagination.pageSize = size; pagination.page = 1; fetchData() }
function handleCurrentChange(page: number) { pagination.page = page; fetchData() }

/* ───────── 操作 ───────── */
function handleCreate() {
  router.push('/project/create')
}

function handleView(row: Project) {
  void openProjectView(row.id)
}

function handleEdit(row: Project) {
  router.push(`/project/edit/${row.id}`)
}

async function handleDelete(row: Project) {
  try {
    await ElMessageBox.confirm(`确认删除项目「${row.projectName}」吗？此操作不可恢复。`, '删除确认', {
      confirmButtonText: '确认删除',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteProject(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch {
    // 取消或错误均不处理
  }
}

const authStore = useAuthStore()

onMounted(async () => {
  if (!authStore.permissions.length) await authStore.fetchPermissionsAndMenus()
  const dictRes = await getDictByType('project_type')
  const map: Record<string, string> = {}
  dictRes.data.forEach(d => { map[d.dictCode] = d.dictLabel })
  projectTypeMap.value = map
  fetchData()
})
</script>

<template>
  <div class="project-list">
    <!-- 搜索区域 -->
    <el-card class="search-card" shadow="never">
      <el-form :model="searchForm" label-width="90px">
        <el-row :gutter="20">
          <el-col :span="6">
            <el-form-item label="项目编号">
              <el-input v-model="searchForm.projectCode" placeholder="请输入项目编号" clearable />
            </el-form-item>
          </el-col>
          <el-col :span="6">
            <el-form-item label="项目名称">
              <el-input v-model="searchForm.projectName" placeholder="请输入项目名称" clearable />
            </el-form-item>
          </el-col>
          <el-col :span="6">
            <el-form-item label="项目类型">
              <el-input v-model="searchForm.projectType" placeholder="请输入项目类型" clearable />
            </el-form-item>
          </el-col>
          <el-col :span="6">
            <el-form-item label="项目状态">
              <el-select
                v-model="searchForm.status"
                placeholder="请选择状态"
                clearable
                style="width: 100%"
              >
                <el-option
                  v-for="item in statusOptions"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="6">
            <el-form-item label="项目经理">
              <el-input v-model="searchForm.projectManagerName" placeholder="请输入项目经理姓名" clearable />
            </el-form-item>
          </el-col>
          <el-col :span="18">
            <el-form-item label-width="0">
              <el-button type="danger" @click="handleSearch">查询</el-button>
              <el-button @click="handleReset">重置</el-button>
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </el-card>

    <!-- 工具栏 -->
    <div class="toolbar">
      <div />
      <el-button v-permission="'project:create'" type="danger" @click="handleCreate">+ 新建项目</el-button>
    </div>

    <!-- 表格 -->
    <el-card class="table-card" shadow="never">
      <el-table
        :data="tableData"
        v-loading="loading"
        stripe
        border
        style="width: 100%"
        @row-click="(row: Project) => handleView(row)"
        row-class-name="project-list-row"
      >
        <el-table-column prop="projectCode" label="项目编号" width="142" show-overflow-tooltip />
        <el-table-column prop="projectName" label="项目名称" min-width="158" show-overflow-tooltip />
        <el-table-column prop="customerName" label="客户名称" width="338" show-overflow-tooltip />
        <el-table-column prop="projectManagerName" label="项目经理" width="91" show-overflow-tooltip />
        <el-table-column label="项目类型" width="130" show-overflow-tooltip>
          <template #default="{ row }">{{ projectTypeMap[row.projectType ?? ''] || row.projectType }}</template>
        </el-table-column>
        <el-table-column label="状态" width="94">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)" size="small">{{ row.statusName }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="要求交期" width="138">
          <template #default="{ row }">{{ formatDate(row.requiredDelivery) }}</template>
        </el-table-column>
        <el-table-column label="承诺交期" width="138">
          <template #default="{ row }">{{ formatDate(row.acceptedDelivery) }}</template>
        </el-table-column>
        <el-table-column label="创建日期" width="172">
          <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="168" fixed="right" align="center">
          <template #default="{ row }">
            <el-button v-permission="'project:detail:view'" link style="color: #409eff; padding: 0 4px;" @click.stop="handleView(row)">查看</el-button>
            <el-button v-permission="'project:edit'" link style="color: #67c23a; padding: 0 4px;" @click.stop="handleEdit(row)">编辑</el-button>
            <el-button v-permission="'project:delete'" link style="color: #f56c6c; padding: 0 4px;" @click.stop="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :page-sizes="[10, 20, 50]"
          :total="pagination.total"
          layout="total, sizes, prev, pager, next, jumper"
          background
          @size-change="handleSizeChange"
          @current-change="handleCurrentChange"
        />
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.project-list { padding: 20px; }
.search-card { margin-bottom: 16px; }
.toolbar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.table-card { margin-bottom: 16px; }
.pagination-wrapper { display: flex; justify-content: flex-end; margin-top: 20px; }
:deep(.project-list-row) { cursor: pointer; }
</style>
