<script setup lang="ts">
import { reactive, ref, shallowRef, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getTemplateList, deleteTemplate } from '@/api/template'
import type { Template } from '@/types/template'
import CreateTemplateDialog from './components/CreateTemplateDialog.vue'

const router = useRouter()

/* ───────── 搜索表单 ───────── */
const searchForm = reactive({
  templateCode: '',
  templateName: '',
  templateType: null as number | null,
  createdBy: '',
  description: ''
})

const templateTypeOptions = [
  { value: 1, label: '计划模板' },
  { value: 3, label: '项目成员' },
  { value: 4, label: '文件模板' }
]

/* ───────── 分页 ───────── */
const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

/* ───────── 表格数据 ───────── */
const tableData = shallowRef<Template[]>([])
const loading = ref(false)
const dialogVisible = ref(false)

/* ───────── 类型 → 路由段 ───────── */
const typeRouteMap: Record<number, string> = {
  1: 'plan',
  2: 'milestone',
  3: 'member',
  4: 'file'
}

/* ───────── 日期格式化 ───────── */
function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日`
}

/* ───────── 获取列表 ───────── */
async function fetchData() {
  loading.value = true
  try {
    const params: Record<string, any> = {
      page: pagination.page,
      pageSize: pagination.pageSize
    }
    // 只携带非空搜索条件
    if (searchForm.templateCode) params.templateCode = searchForm.templateCode
    if (searchForm.templateName) params.templateName = searchForm.templateName
    if (searchForm.templateType !== null) params.templateType = searchForm.templateType
    if (searchForm.createdBy) params.createdBy = searchForm.createdBy
    if (searchForm.description) params.description = searchForm.description

    const res = await getTemplateList(params)
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
  searchForm.templateCode = ''
  searchForm.templateName = ''
  searchForm.templateType = null
  searchForm.createdBy = ''
  searchForm.description = ''
  pagination.page = 1
  fetchData()
}

/* ───────── 分页变更 ───────── */
function handleSizeChange(size: number) {
  pagination.pageSize = size
  pagination.page = 1
  fetchData()
}

function handleCurrentChange(page: number) {
  pagination.page = page
  fetchData()
}

/* ───────── 操作 ───────── */
function handleView(row: Template) {
  const segment = typeRouteMap[row.templateType]
  router.push(`/template/${segment}/view/${row.id}`)
}

function handleEdit(row: Template) {
  const segment = typeRouteMap[row.templateType]
  router.push(`/template/${segment}/edit/${row.id}`)
}

async function handleDelete(row: Template) {
  try {
    await ElMessageBox.confirm(
      `确定要删除模板「${row.templateName}」吗？删除后不可恢复。`,
      '提示',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    await deleteTemplate(row.id)
    ElMessage.success('删除成功')
    await fetchData()
  } catch {
    // 用户取消或错误已处理
  }
}

/* ───────── 新建 ───────── */
function handleCreate() {
  dialogVisible.value = true
}

/* ───────── 新建对话框回调 ───────── */
function handleCreated() {
  dialogVisible.value = false
  fetchData()
}

/* ───────── 生命周期 ───────── */
onMounted(() => {
  fetchData()
})
</script>

<template>
  <div class="template-list">
    <!-- 搜索区域 -->
    <el-card class="search-card" shadow="never">
      <el-form :model="searchForm" label-width="80px">
        <el-row :gutter="20">
          <el-col :span="6">
            <el-form-item label="模板编号" label-position="left">
              <el-input
                v-model="searchForm.templateCode"
                placeholder="请输入模板编号"
                clearable
              />
            </el-form-item>
          </el-col>
          <el-col :span="6">
            <el-form-item label="模板名称" label-position="left">
              <el-input
                v-model="searchForm.templateName"
                placeholder="请输入模板名称"
                clearable
              />
            </el-form-item>
          </el-col>
          <el-col :span="6">
            <el-form-item label="模板种类" label-position="left">
              <el-select
                v-model="searchForm.templateType"
                placeholder="请选择模板种类"
                clearable
                style="width: 100%"
              >
                <el-option
                  v-for="item in templateTypeOptions"
                  :key="item.value"
                  :label="item.label"
                  :value="item.value"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="6">
            <el-form-item label="创建人" label-position="left">
              <el-input
                v-model="searchForm.createdBy"
                placeholder="请输入创建人"
                clearable
              />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="6">
            <el-form-item label="描述" label-position="left">
              <el-input
                v-model="searchForm.description"
                placeholder="请输入描述"
                clearable
              />
            </el-form-item>
          </el-col>
          <el-col :span="18">
            <el-form-item label-width="0">
              <el-button type="danger" @click="handleSearch">搜索</el-button>
              <el-button @click="handleReset">重置</el-button>
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </el-card>

    <!-- 工具栏 -->
    <div class="toolbar">
      <div />
      <el-button type="danger" @click="handleCreate">+ 新建模板</el-button>
    </div>

    <!-- 表格 -->
    <el-card class="table-card" shadow="never">
      <el-table
        :data="tableData"
        v-loading="loading"
        stripe
        border
        style="width: 100%"
      >
        <el-table-column prop="templateCode" label="模板编号" width="234" show-overflow-tooltip />
        <el-table-column prop="templateName" label="模板名称" width="280" show-overflow-tooltip />
        <el-table-column prop="templateTypeName" label="模板种类" width="120" />
        <el-table-column prop="createdByName" label="创建人" width="120" show-overflow-tooltip />
        <el-table-column label="创建日期" width="150">
          <template #default="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column prop="description" label="模板描述" min-width="1" show-overflow-tooltip />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button
              link
              style="color: #409eff; padding: 0 4px;"
              @click="handleView(row)"
            >
              查看
            </el-button>
            <el-button
              link
              style="color: #67c23a; padding: 0 4px;"
              @click="handleEdit(row)"
            >
              更新
            </el-button>
            <el-button
              link
              style="color: #f56c6c; padding: 0 4px;"
              @click="handleDelete(row)"
            >
              删除
            </el-button>
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

    <!-- 新建模板对话框 -->
    <CreateTemplateDialog
      v-model:visible="dialogVisible"
      @created="handleCreated"
    />
  </div>
</template>

<style scoped>
.template-list {
  padding: 20px;
}

.search-card {
  margin-bottom: 16px;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.table-card {
  margin-bottom: 16px;
}

.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  margin-top: 20px;
}
</style>
