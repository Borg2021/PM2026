<script setup lang="ts">
import { ref, shallowRef, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getPlanBundleList, deletePlanBundle, assemblePlanBundle } from '@/api/template'
import type { PlanBundle } from '@/types/template'

const router = useRouter()

const searchForm = reactive({ keyword: '' })
const pagination = reactive({ page: 1, pageSize: 20, total: 0 })
const tableData = shallowRef<PlanBundle[]>([])
const loading = ref(false)

const assembleVisible = ref(false)
const assembleBundleId = ref(0)
const assembleName = ref('')

function fetchData() {
  loading.value = true
  getPlanBundleList({
    pageIndex: pagination.page,
    pageSize: pagination.pageSize,
    keyword: searchForm.keyword || undefined
  })
    .then(res => {
      tableData.value = res.data.items
      pagination.total = res.data.total
    })
    .finally(() => { loading.value = false })
}

function handleSearch() { pagination.page = 1; fetchData() }
function handleReset() { searchForm.keyword = ''; pagination.page = 1; fetchData() }
function handleSizeChange(size: number) { pagination.pageSize = size; pagination.page = 1; fetchData() }
function handleCurrentChange(page: number) { pagination.page = page; fetchData() }

function handleCreate() { router.push('/template/bundles/create') }
function handleEdit(row: PlanBundle) { router.push(`/template/bundles/edit/${row.id}`) }

async function handleDelete(row: PlanBundle) {
  try {
    await ElMessageBox.confirm(`确定要删除模板集「${row.name}」吗？`, '确认删除', {
      confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning'
    })
    await deletePlanBundle(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消 */ }
}

function handleAssemble(row: PlanBundle) {
  assembleBundleId.value = row.id
  assembleName.value = ''
  assembleVisible.value = true
}

async function submitAssemble() {
  if (!assembleName.value.trim()) {
    ElMessage.warning('请输入新模板名称')
    return
  }
  const res = await assemblePlanBundle(assembleBundleId.value, assembleName.value.trim())
  ElMessage.success('组装成功')
  assembleVisible.value = false
  router.push(`/template/plan/edit/${res.data.id}`)
}

function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

onMounted(() => { fetchData() })
</script>

<template>
  <div class="page-container">
    <h2>计划模板集</h2>

    <el-card class="search-card" shadow="never">
      <el-form :model="searchForm" inline>
        <el-form-item label="关键字">
          <el-input v-model="searchForm.keyword" placeholder="模板集名称" clearable style="width: 240px" />
        </el-form-item>
        <el-form-item>
          <el-button type="danger" @click="handleSearch">搜索</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <div class="toolbar">
      <div />
      <el-button type="danger" @click="handleCreate">+ 新建模板集</el-button>
    </div>

    <el-card class="table-card" shadow="never">
      <el-table :data="tableData" v-loading="loading" stripe border style="width: 100%">
        <el-table-column prop="id" label="ID" width="70" />
        <el-table-column prop="name" label="名称" width="252" show-overflow-tooltip />
        <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
        <el-table-column prop="templateCount" label="模板数" width="90" align="center" />
        <el-table-column prop="createdByName" label="创建人" width="120" />
        <el-table-column label="创建时间" width="130">
          <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button link style="color: #409eff" @click="handleEdit(row)">编辑</el-button>
            <el-button link style="color: #67c23a" @click="handleAssemble(row)">组装</el-button>
            <el-button link style="color: #f56c6c" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

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

    <el-dialog
      :model-value="assembleVisible"
      @update:model-value="assembleVisible = $event"
      title="组装生成新模板"
      width="400px"
      :close-on-click-modal="false"
    >
      <el-form label-width="80px">
        <el-form-item label="新模板名称">
          <el-input v-model="assembleName" placeholder="请输入新模板名称" maxlength="100" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="assembleVisible = false">取消</el-button>
        <el-button type="danger" @click="submitAssemble">确定组装</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-container { padding: 24px; }
.page-container h2 { margin: 0 0 20px; font-size: 20px; font-weight: 600; color: #303133; }
.search-card { margin-bottom: 16px; }
.toolbar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.table-card { margin-bottom: 16px; }
.pagination-wrapper { display: flex; justify-content: flex-end; margin-top: 20px; }
</style>
