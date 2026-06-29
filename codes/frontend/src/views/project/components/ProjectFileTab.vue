<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useAuthStore } from '@/store/auth'
import {
  getProjectFileItems, saveProjectFileItems, uploadProjectFileItem,
  deleteProjectFileItem, resetProjectFileItem, getProjectFileVersions, getFileDownloadUrl
} from '@/api/project'
import { getTemplateList, getTemplateDetail, searchUsers, getDepartments } from '@/api/template'
import { buildDeptTree } from '@/utils/deptTree'
import type { ProjectFileItem, ProjectFileVersion } from '@/types/project'
import type { Template, FileTemplateItem, UserInfo, Department } from '@/types/template'

const viewRoleOptions = [
  { value: 'pm', label: '项目经理' },
  { value: 'member', label: '项目组成员' },
  { value: 'assignee', label: '文件责任人' }
]

const props = defineProps<{
  projectId: number
  projectManagerId?: number | null
  memberIds?: number[]
  readonly?: boolean
}>()

const authStore = useAuthStore()

/* ───────── 权限 ───────── */
const permissions = computed(() => authStore.permissions)
const canViewPublic = computed(() => permissions.value.includes('project:file:view:public'))
const canViewNonPublic = computed(() => permissions.value.includes('project:file:view:nonpublic'))
const isPm = computed(() => authStore.userId === props.projectManagerId)
const isAdmin = computed(() => authStore.role === 'admin')

/* ───────── 数据 ───────── */
const loading = ref(false)
const items = ref<any[]>([])
const departments = ref<DepartmentItem[]>([])
const deptTreeData = computed(() => buildDeptTree(departments.value))
const users = ref<UserInfo[]>([])

/* ───────── 加载 ───────── */
async function loadData() {
  if (!props.projectId) return
  loading.value = true
  try {
    const [res, deptRes, userRes] = await Promise.all([
      getProjectFileItems(props.projectId),
      getDepartments(),
      searchUsers('')
    ])
    departments.value = deptRes.data ?? []
    users.value = userRes.data ?? []
    const raw = res.data ?? []
    items.value = raw.map((m: any) => {
      const parsed = parseViewRoles(m.viewRoles)
      return { ...m, _selectedRoles: parsed.length > 0 ? parsed : ['pm', 'assignee'] }
    })
  } catch { /* 错误由拦截器处理 */ }
  finally { loading.value = false }
}

// 组件由父级 filesTabMounted 控制：首次挂载后永不销毁，切回 Tab 不再重载
onMounted(loadData)

/* ───────── 工具函数 ───────── */
function parseViewRoles(vr?: string): string[] {
  if (!vr) return []
  try { return JSON.parse(vr) } catch { return [] }
}

function getViewRoleLabels(vr?: string): string {
  if (!vr) return ''
  try {
    const roles = JSON.parse(vr) as string[]
    return viewRoleOptions.filter(o => roles.includes(o.value)).map(o => o.label).join('、')
  } catch { return '' }
}

/* ───────── 文件可见性校验 ───────── */
function canViewFile(item: any): boolean {
  // 公开文件：用户有「公开」权限即可查看
  if (item.isPublic && canViewPublic.value) return true
  // 非公开文件
  if (!item.isPublic) {
    // 用户有「非公开」权限 → 所有非公开文件可见
    if (canViewNonPublic.value) return true
    // 用户无「非公开」权限 → 按文件的实际权限配置判定
    if (item.viewRoles) {
      const roles = parseViewRoles(item.viewRoles)
      if (roles.length === 0) return false
      const isMember = props.memberIds?.includes(authStore.userId) ?? false
      const isAssigneeItem = item.assigneeId === authStore.userId
      if (roles.includes('pm') && isPm.value) return true
      if (roles.includes('member') && isMember) return true
      if (roles.includes('assignee') && isAssigneeItem) return true
    }
  }
  return false
}

function canUploadFile(item: any): boolean {
  const isAssignee = item.assigneeId === authStore.userId
  return isPm.value || isAssignee
}

/* ───────── 权限弹窗 ───────── */
const roleCheckState = ref<Record<number, Record<string, boolean>>>({})
const popoverVisible = ref<Record<number, boolean>>({})
let rowUidSeq = 0
function getRowKey(row: any): number {
  if (row._uid == null) row._uid = ++rowUidSeq
  return row._uid
}
function initRoleState(row: any) {
  const key = getRowKey(row)
  if (roleCheckState.value[key]) return
  const saved = parseViewRoles(row.viewRoles)
  const state: Record<string, boolean> = {}
  for (const opt of viewRoleOptions) {
    state[opt.value] = saved.length > 0 ? saved.includes(opt.value) : opt.value === 'pm' || opt.value === 'assignee'
  }
  roleCheckState.value[key] = state
}
function getRowCheckState(row: any): Record<string, boolean> {
  const key = getRowKey(row)
  if (!roleCheckState.value[key]) initRoleState(row)
  return roleCheckState.value[key]
}
function confirmViewRoles(row: any) {
  const key = getRowKey(row)
  const state = roleCheckState.value[key] ?? {}
  const selected = viewRoleOptions.filter(o => state[o.value]).map(o => o.value)
  row.viewRoles = selected.length > 0 ? JSON.stringify(selected) : undefined
  popoverVisible.value[key] = false
}
function handleIsPublicChange(row: any, val: boolean) {
  const key = getRowKey(row)
  if (val) {
    row.viewRoles = undefined
    roleCheckState.value[key] = Object.fromEntries(viewRoleOptions.map(o => [o.value, false]))
  } else {
    row.viewRoles = JSON.stringify(['pm', 'assignee'])
    roleCheckState.value[key] = { pm: true, member: false, assignee: true }
  }
}

/* ───────── 从模板创建 ───────── */
const templateDialogVisible = ref(false)
const templateList = ref<Template[]>([])
const templateListLoading = ref(false)
const selectedTemplateId = ref<number | null>(null)

async function openTemplateDialog() {
  templateListLoading.value = true
  templateDialogVisible.value = true
  selectedTemplateId.value = null
  try {
    const res = await getTemplateList({ templateType: 4, pageSize: 100 })
    templateList.value = res.data?.items ?? []
  } catch { templateList.value = [] }
  finally { templateListLoading.value = false }
}

async function confirmImportTemplate() {
  if (!selectedTemplateId.value) { ElMessage.warning('请选择一个模板'); return }
  try {
    const res = await getTemplateDetail(selectedTemplateId.value)
    const fileItems = res.data?.fileItems ?? []
    if (fileItems.length === 0) { ElMessage.warning('该模板没有文件项'); return }

    const memberIdSet = new Set(props.memberIds ?? [])
    const newItems = fileItems.map((f: FileTemplateItem) => {
      // 模板部门在项目成员中有对应部门人员 → 自动设为责任人
      let assigneeId: number | null = null
      let assigneeName = ''
      if (f.deptId) {
        const matched = users.value.find(u => u.departmentId === f.deptId && memberIdSet.has(u.id))
        if (matched) { assigneeId = matched.id; assigneeName = matched.realName }
      }
      return {
        templateItemId: f.id,
        sortOrder: f.sortOrder,
        fileName: f.fileName,
        required: f.required,
        isPublic: f.isPublic ?? true,
        viewRoles: f.viewRoles ? parseViewRoles(f.viewRoles) : [],
        deptId: f.deptId,
        deptName: f.deptName,
        assigneeId,
        assigneeName,
        planFinishDate: undefined,
        remark: f.remark ?? ''
      }
    })
    await saveProjectFileItems(props.projectId, newItems as any)
    ElMessage.success(`已从模板「${res.data.templateName}」导入 ${newItems.length} 项`)
    templateDialogVisible.value = false
    await loadData()
  } catch { /* 错误由拦截器处理 */ }
}

/* ───────── 手动新增 ───────── */
function addRow() {
  items.value.push({
    sortOrder: items.value.length + 1,
    fileName: '',
    required: false,
    isPublic: true,
    viewRoles: undefined,
    _selectedRoles: [],
    deptId: null,
    deptName: '',
    assigneeId: null,
    assigneeName: '',
    planFinishDate: undefined,
    versionCount: 0,
    remark: ''
  })
}

/* ───────── 保存 ───────── */
async function handleSave() {
  const emptyNames = items.value.filter((i: any) => !i.fileName?.trim())
  if (emptyNames.length > 0) { ElMessage.warning('请填写所有文件名称'); return }
  try {
    const payload = items.value.map((m: any) => ({
      id: m.id,
      sortOrder: m.sortOrder,
      fileName: m.fileName,
      required: m.required,
      isPublic: m.isPublic,
      viewRoles: m.viewRoles ? parseViewRoles(m.viewRoles) : [],
      deptId: m.deptId,
      deptName: m.deptName,
      assigneeId: m.assigneeId,
      assigneeName: m.assigneeName,
      planFinishDate: m.planFinishDate,
      remark: m.remark
    }))
    await saveProjectFileItems(props.projectId, payload)
    ElMessage.success('保存成功')
    await loadData()
  } catch { /* 错误由拦截器处理 */ }
}

/* ───────── 上传 ───────── */
const uploadingItemId = ref<number | null>(null)
const uploadProgress = ref(0)

async function handleUpload(item: any) {
  if (!item.id) { ElMessage.warning('请先保存文件清单后再上传'); return }
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

    uploadingItemId.value = item.id!
    uploadProgress.value = 0
    try {
      await uploadProjectFileItem(props.projectId, item.id!, files, remark, (pct) => { uploadProgress.value = pct })
      ElMessage.success(`上传成功（${files.length}个文件）`)
      await loadData()
    } catch { /* 错误由拦截器处理 */ }
    finally { uploadingItemId.value = null; uploadProgress.value = 0 }
  }
  input.click()
}

/* ───────── 下载 ───────── */
async function handleDownload(item: any) {
  if (!item.id) return
  // 优先用 latestVersion 中的 files（如果后端返回了）
  if (item.latestVersion?.files?.length) {
    downloadDialogTitle.value = `文件列表 · ${item.fileName}（v${item.latestVersion.versionNumber}）`
    downloadDialogFiles.value = item.latestVersion.files
    downloadDialogProjectId.value = props.projectId
    downloadDialogItemId.value = item.id!
    downloadDialogVisible.value = true
    return
  }
  // 兜底：请求版本列表获取文件
  try {
    const res = await getProjectFileVersions(props.projectId, item.id!)
    const versions = res.data ?? []
    if (versions.length > 0 && versions[0].files?.length) {
      downloadDialogTitle.value = `文件列表 · ${item.fileName}（v${versions[0].versionNumber}）`
      downloadDialogFiles.value = versions[0].files
      downloadDialogProjectId.value = props.projectId
      downloadDialogItemId.value = item.id!
      downloadDialogVisible.value = true
    } else {
      // 旧数据兼容：没有 files 数组，走直接下载
      window.open(getFileDownloadUrl(props.projectId, item.id!), '_blank')
    }
  } catch {
    window.open(getFileDownloadUrl(props.projectId, item.id!), '_blank')
  }
}

/* ───────── 版本历史 ───────── */
const versionDialogVisible = ref(false)
const versionDialogTitle = ref('')
const versionList = ref<ProjectFileVersion[]>([])
const versionDialogItemId = ref<number | null>(null)

/* ───────── 下载文件列表弹窗 ───────── */
const downloadDialogVisible = ref(false)
const downloadDialogTitle = ref('')
const downloadDialogFiles = ref<any[]>([])
const downloadDialogProjectId = ref(0)
const downloadDialogItemId = ref<number | null>(null)

function handleDownloadFile(file: any) {
  window.open(getFileDownloadUrl(downloadDialogProjectId.value, downloadDialogItemId.value!, undefined, file.id), '_blank')
}

function handleVersionFileDownload(file: any) {
  window.open(getFileDownloadUrl(props.projectId, versionDialogItemId.value!, undefined, file.id), '_blank')
}

async function openVersionDialog(item: any) {
  if (!item.id) return
  versionDialogTitle.value = `版本历史 · ${item.fileName}`
  versionList.value = []
  versionDialogItemId.value = item.id
  versionDialogVisible.value = true
  try {
    const res = await getProjectFileVersions(props.projectId, item.id!)
    versionList.value = res.data ?? []
  } catch { versionList.value = [] }
}

/* ───────── 删除 ───────── */
async function handleDelete(item: any) {
  if (!item.id) {
    // 本地新增行（未保存），直接从列表移除
    const idx = items.value.indexOf(item)
    if (idx >= 0) items.value.splice(idx, 1)
    return
  }
  const msg = item.versionCount > 0
    ? `该文件有 ${item.versionCount} 个版本，确定要删除吗？${item.required ? '（必填项将保留清单行）' : ''}`
    : '确定要删除该文件项吗？'
  try {
    await ElMessageBox.confirm(msg, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' })
    await deleteProjectFileItem(props.projectId, item.id!)
    ElMessage.success('删除成功')
    await loadData()
  } catch { /* 取消或错误 */ }
}

/* ───────── 重置 ───────── */
async function handleReset(item: any) {
  if (!item.id) return
  try {
    await ElMessageBox.confirm(
      `确定要将「${item.fileName}」重置为待上传状态吗？历史版本记录将保留。`,
      '确认重置',
      { confirmButtonText: '重置', cancelButtonText: '取消', type: 'warning' }
    )
    await resetProjectFileItem(props.projectId, item.id!)
    ElMessage.success('重置成功')
    await loadData()
  } catch { /* 取消或错误 */ }
}

/* ───────── 责任人选择器 ───────── */
function onAssigneeSearch(query: string) {
  return users.value.filter(u =>
    u.realName.includes(query) &&
    (!props.memberIds?.length || props.memberIds.includes(u.id))
  )
}
function filteredUsersByDept(deptId: number | null | undefined) {
  if (!deptId) return users.value
  return users.value.filter(u => u.departmentId === deptId)
}
function onAssigneeSelect(user: UserInfo, item: any) {
  item.assigneeId = user.id
  item.assigneeName = user.realName
  if (user.departmentId) {
    item.deptId = user.departmentId
    item.deptName = departments.value.find((d: any) => d.id === user.departmentId)?.name ?? ''
  }
}

/* ───────── 部门选择器 ───────── */
function handleDeptChange(row: any, val: unknown) {
  const dept = departments.value.find((d: any) => d.id === val)
  row.deptName = dept ? dept.name : ''
  // 部门变更后，清空不属于该部门的责任人
  if (val && row.assigneeId) {
    const user = users.value.find(u => u.id === row.assigneeId)
    if (user && user.departmentId !== val) {
      row.assigneeId = null
      row.assigneeName = ''
    }
  }
  // 如果没责任人，尝试从同部门的其他文件中复用
  if (val && !row.assigneeId) {
    const sameDeptRow = items.value.find((i: any) => i !== row && i.deptId === val && i.assigneeId)
    if (sameDeptRow) {
      row.assigneeId = sameDeptRow.assigneeId
      row.assigneeName = sameDeptRow.assigneeName
    }
  }
}

/* ───────── 版本/状态显示 ───────── */
function getVersionDisplay(item: any): string {
  if (!item.latestVersion) return '-'
  const fileCount = item.latestVersion.files?.length ?? 1
  return `v${item.latestVersion.versionNumber}`
}

function canDownload(item: any): boolean {
  return !!item.latestVersion && canViewFile(item)
}

function formatDateTime(dt: string | undefined | null): string {
  if (!dt) return '-'
  // ISO 格式 "2026-02-12T13:10:35.2319726" → "2026-02-12 13:10:35"
  return dt.replace('T', ' ').split('.')[0]
}

function getPlanFinishStatus(item: any): { text: string; cls: string } {
  if (!item.planFinishDate) return { text: '-', cls: '' }
  const dateStr = item.planFinishDate.slice(0, 10)
  if (item.planFinishStatus === 'overdue') return { text: `${dateStr}`, cls: 'status-overdue' }
  if (item.planFinishStatus === 'expiring') return { text: `${dateStr}`, cls: 'status-expiring' }
  return { text: dateStr, cls: '' }
}
</script>

<template>
  <div class="project-file-tab">
    <!-- 工具栏 -->
    <div class="file-toolbar" v-if="!readonly">
      <el-button type="primary" plain size="small" @click="addRow"><el-icon style="margin-right:2px"><Plus /></el-icon>新增</el-button>
      <el-button type="primary" size="small" @click="openTemplateDialog">从模板创建</el-button>
    </div>

    <!-- 表格 -->
    <div class="file-table-wrap">
      <el-table ref="fileTableRef" :data="items" border size="small" style="width:100%" height="100%" v-loading="loading" empty-text="暂无文件资料">
      <el-table-column type="index" label="序号" width="55" fixed="left" />

      <el-table-column label="文件名称" min-width="180" show-overflow-tooltip>
        <template #header>
          <span><span style="color:var(--el-color-danger);margin-right:2px">*</span>文件名称</span>
        </template>
        <template #default="{ row }">
          <el-input v-if="!readonly" v-model="row.fileName" size="small" placeholder="请输入文件名称" clearable />
          <span v-else>{{ row.fileName }}<el-tag v-if="row.required" size="small" type="danger" style="margin-left:4px">必填</el-tag></span>
        </template>
      </el-table-column>

      <el-table-column label="文件说明" min-width="160">
        <template #default="{ row }">
          <el-input v-if="!readonly" v-model="row.remark" size="small" placeholder="文件说明" clearable />
          <span v-else>{{ row.remark || '-' }}</span>
        </template>
      </el-table-column>

      <el-table-column label="必须" width="70" align="center">
        <template #default="{ row }">
          <el-switch v-if="!readonly" v-model="row.required" size="small" />
          <el-switch v-else :model-value="row.required" disabled size="small" />
        </template>
      </el-table-column>

      <el-table-column label="公开" width="70" align="center">
        <template #default="{ row }">
          <el-switch
            v-if="!readonly"
            v-model="row.isPublic"
            size="small"
            @change="(val: boolean) => handleIsPublicChange(row, val)"
          />
          <el-switch v-else :model-value="row.isPublic" disabled size="small" />
        </template>
      </el-table-column>

      <el-table-column label="文件权限" min-width="170">
        <template #default="{ row }">
          <span v-if="row.isPublic" style="color:#c0c4cc">-</span>
          <el-popover
            v-else
            :disabled="readonly"
            :visible="popoverVisible[getRowKey(row)]"
            placement="bottom"
            trigger="click"
            width="220"
            @show="initRoleState(row)"
            @update:visible="(v: boolean) => popoverVisible[getRowKey(row)] = v"
          >
            <template #reference>
              <el-button v-if="row.viewRoles" type="info" link size="small">{{ getViewRoleLabels(row.viewRoles) }}</el-button>
              <el-button v-else type="info" link size="small" style="color:#909399">点击配置</el-button>
            </template>
            <div class="role-checkbox-group">
              <div v-for="opt in viewRoleOptions" :key="opt.value" class="role-checkbox-item">
                <el-checkbox v-model="getRowCheckState(row)[opt.value]" :disabled="readonly">{{ opt.label }}</el-checkbox>
              </div>
              <div class="role-checkbox-actions">
                <el-button size="small" type="primary" @click="confirmViewRoles(row)">确定</el-button>
              </div>
            </div>
          </el-popover>
        </template>
      </el-table-column>

      <el-table-column label="责任部门" min-width="180">
        <template #default="{ row }">
          <el-tree-select
            v-if="!readonly"
            v-model="row.deptId"
            :data="deptTreeData"
            :props="{ label: 'name', children: 'children', value: 'id' }"
            node-key="id"
            check-strictly
            placeholder="请选择部门"
            clearable
            filterable
            size="small"
            style="width:100%"
            @change="(val: unknown) => handleDeptChange(row, val)"
          />
          <span v-else>{{ row.deptName || '-' }}</span>
        </template>
      </el-table-column>

      <el-table-column label="责任人" width="150">
        <template #default="{ row }">
          <el-select
            v-if="!readonly"
            v-model="row.assigneeId"
            filterable
            placeholder="选择责任人"
            size="small"
            style="width:140px"
            clearable
            @change="(id: number) => {
              const user = users.find(u => u.id === id)
              if (user) onAssigneeSelect(user, row)
            }"
          >
            <el-option v-for="u in filteredUsersByDept(row.deptId)" :key="u.id" :label="u.realName" :value="u.id" />
          </el-select>
          <span v-else>{{ row.assigneeName || '-' }}</span>
        </template>
      </el-table-column>

      <el-table-column label="计划完成" width="130" align="center">
        <template #default="{ row }">
          <el-date-picker
            v-if="!readonly"
            v-model="row.planFinishDate"
            type="date"
            placeholder="选择日期"
            size="small"
            style="width:120px"
            value-format="YYYY-MM-DDTHH:mm:ss"
            clearable
          />
          <span v-else :class="getPlanFinishStatus(row).cls">{{ getPlanFinishStatus(row).text }}</span>
        </template>
      </el-table-column>

      <el-table-column label="版本" width="60" align="center">
        <template #default="{ row }">
          <span>{{ getVersionDisplay(row) }}</span>
        </template>
      </el-table-column>

      <el-table-column label="状态" width="70" align="center">
        <template #default="{ row }">
          <span v-if="row.hasUpload" style="color:#67c23a">已上传</span>
          <span v-else style="color:#909399">待上传</span>
        </template>
      </el-table-column>

      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button v-if="canUploadFile(row)" size="small" type="primary" link :loading="uploadingItemId === row.id"
            @click="handleUpload(row)">
            {{ uploadProgress > 0 && uploadingItemId === row.id ? `${uploadProgress}%` : row.latestVersion ? '重传' : '上传' }}
          </el-button>
          <el-button v-if="canDownload(row)" size="small" type="primary" link @click="handleDownload(row)">下载</el-button>
          <el-button v-if="canViewFile(row) && row.versionCount > 1" size="small" type="primary" link @click="openVersionDialog(row)">版本</el-button>
          <el-button v-if="isAdmin && row.hasUpload" size="small" type="warning" link @click="handleReset(row)">重置</el-button>
          <el-button v-if="!readonly && (!row.required || !row.latestVersion)" size="small" type="danger" link @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    </div>

    <!-- 底部操作栏 -->
    <div class="form-footer" v-if="!readonly">
      <el-button type="danger" :loading="loading" @click="handleSave">保存</el-button>
      <el-button @click="loadData">取消</el-button>
    </div>

    <!-- 从模板创建对话框 -->
    <el-dialog v-model="templateDialogVisible" title="从文件模板创建" width="560px">
      <template v-if="templateList.length === 0 && !templateListLoading">
        <el-empty description="暂无文件模板">
          <template #description>
            <p style="color:#909399;margin-bottom:12px">请先到<strong>模板配置 → 模板管理</strong>创建文件模板</p>
          </template>
          <el-button type="primary" size="small" @click="templateDialogVisible = false">知道了</el-button>
        </el-empty>
      </template>
      <el-table
        v-else
        :data="templateList"
        border
        size="small"
        style="width:100%"
        max-height="400"
        v-loading="templateListLoading"
        highlight-current-row
        @current-change="(row: Template | null) => selectedTemplateId = row?.id ?? null"
      >
        <el-table-column width="50" align="center">
          <template #default="{ row }">
            <el-radio :model-value="selectedTemplateId" :value="row.id" @change="selectedTemplateId = row.id">
              <span></span>
            </el-radio>
          </template>
        </el-table-column>
        <el-table-column prop="templateCode" label="模板编号" width="140" />
        <el-table-column prop="templateName" label="模板名称" min-width="180" show-overflow-tooltip />
        <el-table-column prop="description" label="描述" min-width="120" show-overflow-tooltip />
      </el-table>
      <template #footer>
        <el-button @click="templateDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmImportTemplate" :disabled="!selectedTemplateId">导入</el-button>
      </template>
    </el-dialog>

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
                <el-button size="small" type="primary" link
                  @click="handleVersionFileDownload(f)">
                  下载
                </el-button>
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
.project-file-tab {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.file-toolbar {
  margin-bottom: 12px;
  display: flex;
  gap: 8px;
  align-items: center;
  justify-content: flex-end;
  flex-shrink: 0;
}
.file-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
.status-overdue { color: #f56c6c; font-weight: 600; }
.status-expiring { color: #e6a23c; font-weight: 600; }
.role-checkbox-group { padding: 4px 0; }
.role-checkbox-item { padding: 6px 12px; border-radius: 4px; display: flex; align-items: center; gap: 6px; }
.role-checkbox-item:hover { background-color: #f5f7fa; }
.role-checkbox-actions { text-align: right; padding-top: 8px; margin-top: 4px; border-top: 1px solid #f0f0f0; }
.form-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #e4e7ed;
  flex-shrink: 0;
}
</style>
