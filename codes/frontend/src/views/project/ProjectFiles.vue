<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted } from 'vue'
import { getMyFiles, getFileDownloadUrl, uploadProjectFileItem, getProjectFileVersions, resetProjectFileItem } from '@/api/project'
import { ElMessage, ElMessageBox } from 'element-plus'
import { openProjectView } from '@/utils/projectNav'
import { useAuthStore } from '@/store/auth'

interface MyFileItem {
  id: number
  projectId: number
  projectCode: string
  projectName: string
  sortOrder: number
  fileName: string
  required: boolean
  isPublic: boolean
  assigneeId: number
  assigneeName: string
  deptId: number
  deptName: string
  planFinishDate: string | null
  planFinishStatus: string | null
  hasUpload: boolean
  latestVersion: { id: number; versionNumber: number; files?: { id: number; originalFileName: string; fileSize: number; fileExt?: string }[]; uploadedByName: string; uploadedAt: string } | null
  versionCount: number
  remark: string | null
}

const authStore = useAuthStore()
const isAdmin = computed(() => authStore.role === 'admin')

const loading = ref(false)
const files = ref<MyFileItem[]>([])

/* ───────── 上传 & 版本 ───────── */
const uploadingItemId = ref<number | null>(null)
const uploadProgress = ref(0)
const versionDialogVisible = ref(false)
const versionDialogTitle = ref('')
const versionDialogProjectId = ref(0)
const versionDialogItemId = ref<number | null>(null)
const downloadDialogVisible = ref(false)
const downloadDialogTitle = ref('')
const downloadDialogFiles = ref<any[]>([])
const downloadDialogProjectId = ref(0)
const downloadDialogItemId = ref<number | null>(null)

function handleDownloadFile(file: any) {
  window.open(getFileDownloadUrl(downloadDialogProjectId.value, downloadDialogItemId.value!, undefined, file.id), '_blank')
}
const versionList = ref<any[]>([])

/* ───────── 搜索表单 ───────── */
const searchForm = reactive({
  projectCode: '',
  projectName: '',
  planFinishDateFrom: '',
  planFinishDateTo: '',
  fileName: '',
  status: null as string | null,
  required: null as boolean | null
})

const statusOptions = [
  { value: 'uploaded', label: '已上传' },
  { value: 'not_uploaded', label: '未上传' },
  { value: 'overdue', label: '超期' },
  { value: 'expiring', label: '即将到期' }
]
const requiredOptions = [
  { value: true, label: '必须' },
  { value: false, label: '非必须' }
]

/* ───────── 客户端筛选 ───────── */
const filteredData = computed(() => {
  return files.value.filter(f => {
    if (searchForm.projectCode && !f.projectCode.toLowerCase().includes(searchForm.projectCode.toLowerCase())) return false
    if (searchForm.projectName && !f.projectName.toLowerCase().includes(searchForm.projectName.toLowerCase())) return false
    if (searchForm.fileName && !f.fileName.toLowerCase().includes(searchForm.fileName.toLowerCase())) return false
    if (searchForm.planFinishDateFrom && (!f.planFinishDate || f.planFinishDate.slice(0, 10) < searchForm.planFinishDateFrom)) return false
    if (searchForm.planFinishDateTo && (!f.planFinishDate || f.planFinishDate.slice(0, 10) > searchForm.planFinishDateTo)) return false
    if (searchForm.status !== null) {
      if (searchForm.status === 'uploaded' && !f.hasUpload) return false
      if (searchForm.status === 'not_uploaded' && f.hasUpload) return false
      if (searchForm.status === 'overdue' && f.planFinishStatus !== 'overdue') return false
      if (searchForm.status === 'expiring' && f.planFinishStatus !== 'expiring') return false
    }
    if (searchForm.required != null && f.required !== searchForm.required) return false
    return true
  })
})

/* ───────── 分页 ───────── */
const pagination = reactive({ page: 1, pageSize: 20 })
const paginatedData = computed(() => {
  const start = (pagination.page - 1) * pagination.pageSize
  return filteredData.value.slice(start, start + pagination.pageSize)
})
watch(filteredData, () => { pagination.page = 1 })

/* ───────── 辅助函数 ───────── */
function getStatusTag(item: MyFileItem) {
  if (!item.hasUpload) return { text: '未上传', type: 'info' as const }
  if (item.planFinishStatus === 'overdue') return { text: '超期', type: 'danger' as const }
  if (item.planFinishStatus === 'expiring') return { text: '即将到期', type: 'warning' as const }
  return { text: '已上传', type: 'success' as const }
}

function formatDate(d: string | null) {
  if (!d) return '-'
  return new Date(d).toLocaleDateString('zh-CN')
}

function formatSize(bytes: number) {
  if (!bytes) return '-'
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
}

async function handleDownload(item: MyFileItem) {
  if (!item.latestVersion) return
  // 优先用 latestVersion 中的 files
  if (item.latestVersion.files?.length) {
    downloadDialogTitle.value = `文件列表 · ${item.fileName}（v${item.latestVersion.versionNumber}）`
    downloadDialogFiles.value = item.latestVersion.files
    downloadDialogProjectId.value = item.projectId
    downloadDialogItemId.value = item.id
    downloadDialogVisible.value = true
    return
  }
  // 兜底：请求版本列表
  try {
    const res = await getProjectFileVersions(item.projectId, item.id)
    const versions = res.data ?? []
    if (versions.length > 0 && versions[0].files?.length) {
      downloadDialogTitle.value = `文件列表 · ${item.fileName}（v${versions[0].versionNumber}）`
      downloadDialogFiles.value = versions[0].files
      downloadDialogProjectId.value = item.projectId
      downloadDialogItemId.value = item.id
      downloadDialogVisible.value = true
    } else {
      window.open(getFileDownloadUrl(item.projectId, item.id), '_blank')
    }
  } catch {
    window.open(getFileDownloadUrl(item.projectId, item.id), '_blank')
  }
}

/* ───────── 搜索 / 重置 ───────── */
function handleSearch() {
  // computed 自动响应，占位
}

function handleReset() {
  searchForm.projectCode = ''
  searchForm.projectName = ''
  searchForm.planFinishDateFrom = ''
  searchForm.planFinishDateTo = ''
  searchForm.fileName = ''
  searchForm.status = null
  searchForm.required = null
}

/* ───────── 上传 ───────── */
async function handleUpload(item: MyFileItem) {
  const input = document.createElement('input')
  input.type = 'file'
  input.multiple = true
  input.accept = '.pdf,.doc,.docx,.xls,.xlsx,.zip,.rar,.ppt,.pptx,.txt,.jpg,.png'
  input.onchange = async () => {
    const fileList = input.files
    if (!fileList || fileList.length === 0) return
    const files = Array.from(fileList)

    let remark = ''
    try {
      const { value } = await ElMessageBox.prompt('版本说明（可选）', `上传文件（已选择${files.length}个文件）`, {
        confirmButtonText: '上传', cancelButtonText: '取消',
        inputPlaceholder: '本次版本的变更说明...'
      })
      remark = value ?? ''
    } catch { return }

    uploadingItemId.value = item.id
    uploadProgress.value = 0
    try {
      await uploadProjectFileItem(item.projectId, item.id, files, remark, (pct) => { uploadProgress.value = pct })
      ElMessage.success(`上传成功（${files.length}个文件）`)
      await loadData()
    } catch { /* 错误由拦截器处理 */ }
    finally { uploadingItemId.value = null; uploadProgress.value = 0 }
  }
  input.click()
}

/* ───────── 文件重置 ───────── */
async function handleFileReset(item: MyFileItem) {
  try {
    await ElMessageBox.confirm(
      `确定要将「${item.fileName}」重置为待上传状态吗？历史版本记录将保留。`,
      '确认重置',
      { confirmButtonText: '重置', cancelButtonText: '取消', type: 'warning' }
    )
    await resetProjectFileItem(item.projectId, item.id)
    ElMessage.success('重置成功')
    await loadData()
  } catch { /* 取消或错误 */ }
}

/* ───────── 版本历史 ───────── */
async function openVersionDialog(item: MyFileItem) {
  versionDialogTitle.value = `版本历史 · ${item.fileName}`
  versionList.value = []
  versionDialogProjectId.value = item.projectId
  versionDialogItemId.value = item.id
  versionDialogVisible.value = true
  try {
    const res = await getProjectFileVersions(item.projectId, item.id)
    versionList.value = res.data ?? []
  } catch { versionList.value = [] }
}

function handleVersionDownload(versionFile: any) {
  if (!versionDialogItemId.value) return
  window.open(getFileDownloadUrl(versionDialogProjectId.value, versionDialogItemId.value, undefined, versionFile.id), '_blank')
}

function formatDateTime(dt: string | undefined | null): string {
  if (!dt) return '-'
  return dt.replace('T', ' ').split('.')[0]
}

/* ───────── 数据加载 ───────── */
async function loadData() {
  loading.value = true
  try {
    const res = await getMyFiles()
    if (res.code === 0) {
      files.value = res.data ?? []
    }
  } catch {
    ElMessage.error('获取文件列表失败')
  } finally {
    loading.value = false
  }
}

/* ───────── 生命周期 ───────── */
onMounted(loadData)
</script>

<template>
  <div class="file-management">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="searchForm" label-width="auto" size="small" inline>
        <el-form-item label="项目编号">
          <el-input v-model="searchForm.projectCode" placeholder="请输入项目编号" clearable style="width:150px" @keyup.enter="handleSearch" />
        </el-form-item>
        <el-form-item label="项目名称">
          <el-input v-model="searchForm.projectName" placeholder="请输入项目名称" clearable style="width:150px" @keyup.enter="handleSearch" />
        </el-form-item>
        <el-form-item label="计划完成">
          <el-date-picker
            v-model="searchForm.planFinishDateFrom"
            type="date"
            placeholder="开始日期"
            value-format="YYYY-MM-DD"
            style="width:140px"
          />
          <span style="margin:0 6px">~</span>
          <el-date-picker
            v-model="searchForm.planFinishDateTo"
            type="date"
            placeholder="结束日期"
            value-format="YYYY-MM-DD"
            style="width:140px"
          />
        </el-form-item>
        <el-form-item label="文件名称">
          <el-input v-model="searchForm.fileName" placeholder="请输入文件名称" clearable style="width:150px" @keyup.enter="handleSearch" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="searchForm.status" placeholder="全部" clearable style="width:120px">
            <el-option v-for="o in statusOptions" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="是否必须">
          <el-select v-model="searchForm.required" placeholder="全部" clearable style="width:100px">
            <el-option v-for="o in requiredOptions" :key="String(o.value)" :label="o.label" :value="o.value" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 表格 -->
    <el-card shadow="never" style="margin-top:12px">
      <el-table :data="paginatedData" v-loading="loading" row-key="id" border size="small" style="width:100%" max-height="calc(100vh - 280px)" :header-cell-style="{ textAlign: 'center' }">
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column label="项目编号" width="120" show-overflow-tooltip align="center">
          <template #default="{ row }">
            <el-link type="primary" underline="never" style="cursor:pointer" @click="openProjectView(row.projectId)">{{ row.projectCode }}</el-link>
          </template>
        </el-table-column>
        <el-table-column label="项目名称" width="200" show-overflow-tooltip align="center">
          <template #default="{ row }">
            <el-link type="primary" underline="never" style="cursor:pointer" @click="openProjectView(row.projectId)">{{ row.projectName }}</el-link>
          </template>
        </el-table-column>
        <el-table-column label="文件名称" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">
            <span>{{ row.fileName }}</span>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="getStatusTag(row).type" size="small" effect="plain">
              {{ getStatusTag(row).text }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="是否必需" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="row.required ? 'danger' : 'info'" size="small" effect="plain">
              {{ row.required ? '必需' : '非必需' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="assigneeName" label="责任人" width="100" show-overflow-tooltip />
        <el-table-column prop="deptName" label="责任部门" width="120" show-overflow-tooltip />
        <el-table-column label="计划完成" width="110" align="center">
          <template #default="{ row }">{{ formatDate(row.planFinishDate) }}</template>
        </el-table-column>
        <el-table-column label="版本" width="70" align="center">
          <template #default="{ row }">
            <span v-if="row.latestVersion">v{{ row.latestVersion.versionNumber }}</span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column label="文件大小" width="90" align="center">
          <template #default="{ row }">
            {{ row.latestVersion ? formatSize(row.latestVersion.fileSize) : '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="remark" label="备注" min-width="120" show-overflow-tooltip />
        <el-table-column label="操作" width="200" fixed="right" align="center">
          <template #default="{ row }">
            <el-button size="small" type="primary" link :loading="uploadingItemId === row.id"
              @click="handleUpload(row)">
              {{ uploadProgress > 0 && uploadingItemId === row.id ? `${uploadProgress}%` : row.hasUpload ? '重传' : '上传' }}
            </el-button>
            <el-button v-if="row.hasUpload" size="small" type="primary" link @click="handleDownload(row)">下载</el-button>
            <el-button v-if="row.hasUpload && row.versionCount > 1" size="small" type="primary" link @click="openVersionDialog(row)">版本</el-button>
            <el-button v-if="isAdmin && row.hasUpload" size="small" type="warning" link @click="handleFileReset(row)">重置</el-button>
          </template>
        </el-table-column>
      </el-table>
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :page-sizes="[10, 20, 50]"
          :total="filteredData.length"
          layout="total, sizes, prev, pager, next, jumper"
          background
        />
      </div>
    </el-card>

    <!-- 版本历史对话框 -->
    <el-dialog v-model="versionDialogVisible" :title="versionDialogTitle" width="900px">
      <el-table :data="versionList" border size="small" style="width:100%">
        <el-table-column label="版本" width="70" align="center">
          <template #default="{ row }">v{{ row.versionNumber }}</template>
        </el-table-column>
        <el-table-column label="上传人" width="100" prop="uploadedByName" />
        <el-table-column label="上传时间" width="160">
          <template #default="{ row }">{{ formatDateTime(row.uploadedAt) }}</template>
        </el-table-column>
        <el-table-column label="说明" min-width="120" prop="remark" show-overflow-tooltip />
        <el-table-column label="文件" min-width="280">
          <template #default="{ row }">
            <template v-if="row.files?.length">
              <div v-for="f in row.files" :key="f.id" style="display:flex;align-items:center;justify-content:space-between;padding:2px 0">
                <span style="flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;margin-right:8px">
                  {{ f.originalFileName }}
                  <span style="color:#909399;font-size:12px;margin-left:4px">
                    {{ f.fileSize < 1024 ? f.fileSize + 'B' : (f.fileSize / 1024).toFixed(1) + 'KB' }}
                  </span>
                </span>
                <el-button size="small" type="primary" link @click="handleVersionDownload(f)">下载</el-button>
              </div>
            </template>
            <span v-else style="color:#c0c4cc">-</span>
          </template>
        </el-table-column>
      </el-table>
    </el-dialog>

    <!-- 下载文件选择对话框 -->
    <el-dialog v-model="downloadDialogVisible" :title="downloadDialogTitle" width="560px">
      <el-table :data="downloadDialogFiles" border size="small" style="width:100%" empty-text="该版本没有文件">
        <el-table-column type="index" label="序号" width="55" />
        <el-table-column prop="originalFileName" label="文件名" min-width="250" show-overflow-tooltip />
        <el-table-column label="文件大小" width="110" align="right">
          <template #default="{ row }">
            {{ row.fileSize < 1024 ? row.fileSize + ' B' : row.fileSize < 1024 * 1024 ? (row.fileSize / 1024).toFixed(1) + ' KB' : (row.fileSize / (1024 * 1024)).toFixed(1) + ' MB' }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="80" fixed="right" align="center">
          <template #default="{ row }">
            <el-button size="small" type="primary" link @click="handleDownloadFile(row)">下载</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-dialog>
  </div>
</template>

<style scoped>
.file-management { padding: 20px; }
.pagination-wrapper { display: flex; justify-content: flex-end; margin-top: 12px; }
.search-card { margin-bottom: 0; }
</style>
