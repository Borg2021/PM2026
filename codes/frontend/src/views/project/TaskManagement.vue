<script setup lang="ts">
import { reactive, ref, shallowRef, computed, watch, onMounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { getTaskList, updateProjectTask, getProjectTasks, type TaskListItem } from '@/api/project'
import { getProjectList } from '@/api/project'
import { getDepartments, searchUsers, getDictByType } from '@/api/template'
import { getSysParamByKey } from '@/api/system'
import { getFunctionList } from '@/api/system'
import { ElMessage } from 'element-plus'
import { openProjectView } from '@/utils/projectNav'
import { useAuthStore } from '@/store/auth'
import { buildDeptTree } from '@/utils/deptTree'
import { taskStatusOptions, taskPriorityOptions, statusLabel, priorityLabel, overdueStatus } from '@/utils/taskConstants'
import { dateAddDays } from '@/utils/dateUtils'
import { parsePreTaskCodes, serializePreTaskCodes } from '@/utils/preTaskHelpers'
import type { Project } from '@/types/project'
import type { Department, UserInfo } from '@/types/template'
import type { FunctionItem } from '@/types/system'

const router = useRouter()
const authStore = useAuthStore()

/* ───────── 辅助数据 ───────── */
const departments = ref<Department[]>([])
const functions = ref<FunctionItem[]>([])
const users = ref<UserInfo[]>([])
const dictMap = ref<Record<string, { id: number; dictCode: string; dictLabel: string }[]>>({})
const taskNoRule = ref('')
const projectTasks = ref<TaskListItem[]>([])

const deptTreeData = computed(() => buildDeptTree(departments.value))

/* ───────── 筛选表单 ───────── */
const searchForm = reactive({
  projectId: null as number | null,
  planStartDateFrom: '',
  planStartDateTo: '',
  planFinishDateFrom: '',
  planFinishDateTo: '',
  status: null as number | null,
  overdue: null as boolean | null,
  assigneeId: null as number | null,
  assigneeName: ''
})

const statusOptions = [
  { value: 0, label: '未开始' },
  { value: 1, label: '进行中' },
  { value: 2, label: '已完成' }
]

const overdueOptions = [
  { value: true, label: '已逾期' },
  { value: false, label: '未逾期' }
]

/* ───────── 表格数据 ───────── */
const tableData = shallowRef<TaskListItem[]>([])
/** 全局任务查找表：ID → { taskNo, taskName }，优先用此表解析前置任务序号 */
const taskLookupMap = ref<Map<number, { taskNo: string; taskName: string }>>(new Map())
const loading = ref(false)
const tableRef = ref()
const flashingTaskId = ref<number | null>(null)

const filteredTableData = computed(() => {
  if (searchForm.overdue === null) return tableData.value
  const today = new Date().toISOString().slice(0, 10)
  return tableData.value.filter(t => {
    const isOverdue = t.status !== 2 && !!t.planFinishDate && t.planFinishDate.slice(0, 10) < today
    return isOverdue === searchForm.overdue
  })
})

/* ───────── 分页 ───────── */
const pagination = reactive({ page: 1, pageSize: 20 })
const paginatedData = computed(() => {
  const start = (pagination.page - 1) * pagination.pageSize
  return filteredTableData.value.slice(start, start + pagination.pageSize)
})
watch(filteredTableData, () => { pagination.page = 1 })

/* ───────── 任务详情弹窗 ───────── */
const showViewTaskDialog = ref(false)
const viewingTask = ref<TaskListItem | null>(null)
function openViewTask(task: TaskListItem) {
  viewingTask.value = task
  showViewTaskDialog.value = true
}

/* ───────── 编辑任务 ───────── */
const showEditTaskDialog = ref(false)
const editingTask = ref<TaskListItem | null>(null)
const taskSaving = ref(false)
const predRows = ref<PredRow[]>([])
let predKeySeq = 0

interface PredRow {
  rowKey: number
  taskId: number
  dependencyType: string
  lagDays: number
}

function statusTag(s: number): 'info' | 'warning' | 'success' {
  return ['info', 'warning', 'success'][s] ?? 'info'
}
/** 从 preTaskCodes 字符串中解析出任务 ID 列表 */
function parseTaskIdsFromCodes(codes: string): number[] {
  const ids: number[] = []
  const regex = /(\d+)\((\w+)([+-]\d+)?\)/g
  let m: RegExpExecArray | null
  while ((m = regex.exec(codes)) !== null) {
    ids.push(parseInt(m[1], 10))
  }
  return ids
}

/** 按 ID 查找任务（优先用全局查找表），生成 { id, taskNo, taskName } 列表 */
function getPredecessorTasks(row: TaskListItem): { id: number; taskNo: string; taskName: string }[] {
  if (!row.preTaskCodes) return []
  const ids = parseTaskIdsFromCodes(row.preTaskCodes)
  return ids.map(id => {
    const cached = taskLookupMap.value.get(id)
    if (cached) return { id, taskNo: cached.taskNo, taskName: cached.taskName }
    // 兜底：在已加载数据中查找
    const task = tableData.value.find(t => t.id === id)
    return {
      id,
      taskNo: task?.taskNo ?? `任务#${id}`,
      taskName: task?.taskName ?? '(未找到)'
    }
  })
}

/** 更新全局任务查找表 */
function rebuildTaskLookupMap() {
  for (const t of tableData.value) {
    if (t.id) taskLookupMap.value.set(t.id, { taskNo: t.taskNo, taskName: t.taskName })
  }
}

/** 前置任务列显示文本（逗号分隔的序号） */
function formatPreTaskLabels(row: TaskListItem): string {
  const tasks = getPredecessorTasks(row)
  return tasks.map(t => t.taskNo).join(', ')
}

/** 点击前置任务：定位并高亮闪 3 下 */
async function scrollToTask(taskId: number) {
  const idx = filteredTableData.value.findIndex(t => t.id === taskId)
  if (idx === -1) {
    ElMessage.warning('该前置任务不在当前筛选结果中')
    return
  }
  const page = Math.floor(idx / pagination.pageSize) + 1
  const pageIdx = idx - (page - 1) * pagination.pageSize
  pagination.page = page

  // 等待 popover 关闭 + 表格渲染完成
  await nextTick()
  await new Promise(r => setTimeout(r, 150))

  // 轮询等待目标行实际渲染到 DOM 中
  const el = tableRef.value?.$el as HTMLElement | undefined
  if (!el) return
  const scrollWrap = el.querySelector('.el-scrollbar__wrap') as HTMLElement | null
  const container = scrollWrap ?? el.querySelector('.el-table__body-wrapper') as HTMLElement | null
  if (!container) return

  let targetRow: HTMLElement | null = null
  for (let i = 0; i < 30; i++) {
    const rows = container.querySelectorAll<HTMLElement>('tr.el-table__row')
    if (rows[pageIdx] && rows[pageIdx].offsetHeight > 0) {
      targetRow = rows[pageIdx]
      break
    }
    await new Promise(r => requestAnimationFrame(r))
  }
  if (!targetRow) return

  // 累加目标行之前所有行的高度，得到精确的 scrollTop
  const allRows = container.querySelectorAll<HTMLElement>('tr.el-table__row')
  let offset = 0
  for (let i = 0; i < pageIdx && i < allRows.length; i++) {
    offset += allRows[i].offsetHeight
  }
  const centerOffset = container.clientHeight / 2 - targetRow.offsetHeight / 2
  container.scrollTop = Math.max(0, offset - centerOffset)

  flashingTaskId.value = taskId
  setTimeout(() => { flashingTaskId.value = null }, 3000)
}

/** 表格行 class：闪动高亮 */
function getRowClassName({ row }: { row: TaskListItem }) {
  if (row.id === flashingTaskId.value) return 'flash-row'
  return ''
}

function taskHasChildren(taskId?: number | null): boolean {
  if (!taskId) return false
  return projectTasks.value.some(t => t.parentId === taskId)
}

/** 按序号数字分段比较（001.01.03 < 001.03.02） */
function compareTaskNo(a: string, b: string): number {
  const segsA = a.split('.'), segsB = b.split('.')
  for (let i = 0; i < Math.max(segsA.length, segsB.length); i++) {
    const na = parseInt(segsA[i], 10) || 0
    const nb = parseInt(segsB[i], 10) || 0
    if (na !== nb) return na - nb
  }
  return 0
}

function getExcludedTaskIds(): Set<number> {
  const currentId = editingTask.value?.id
  if (!currentId) return new Set()
  const excluded = new Set<number>()
  excluded.add(currentId)
  function collectDescendants(parentId: number) {
    for (const t of projectTasks.value) {
      if ((t as any).parentId === parentId && t.id) {
        excluded.add(t.id)
        collectDescendants(t.id)
      }
    }
  }
  collectDescendants(currentId)
  return excluded
}

/** 按 taskNo 数字排序后的任务列表（带缩进标签），由 computed 缓存避免每次渲染重新排序 */
const sortedPredTasks = computed(() => {
  const depth = (no: string) => (no.match(/\./g) || []).length
  return [...projectTasks.value]
    .filter(t => t.id && t.taskNo)
    .sort((a, b) => compareTaskNo(a.taskNo!, b.taskNo!))
    .map(t => {
      const d = depth(t.taskNo!)
      const indent = d > 0 ? '　'.repeat(d) + '└ ' : ''
      return { ...t, _label: `${indent}${t.taskNo} - ${t.taskName}`, _depth: d }
    }) as (TaskListItem & { _label: string; _depth: number })[]
})

/** 前置任务可选列表：从 sortedPredTasks 中排除自身及其后代、已选任务 */
function getAvailableForPredRow(rowKey: number) {
  const excludedIds = getExcludedTaskIds()
  const selectedIds = new Set(
    predRows.value
      .filter(p => p.rowKey !== rowKey && p.taskId)
      .map(p => p.taskId)
  )
  return sortedPredTasks.value
    .filter(t => !excludedIds.has(t.id) && !selectedIds.has(t.id))
}

function addPredRow() {
  predRows.value.push({
    rowKey: Date.now() + (++predKeySeq),
    taskId: 0,
    dependencyType: 'FS',
    lagDays: 0
  })
}

function removePredRow(rowKey: number) {
  predRows.value = predRows.value.filter(p => p.rowKey !== rowKey)
}

// parsePreTaskCodes / serializePreTaskCodes 统一使用 @/utils/preTaskHelpers

function calcPlanStartFromPreds(): string | null {
  if (predRows.value.length === 0) return null
  let maxDate: string | null = null
  for (const pred of predRows.value) {
    if (!pred.taskId) continue
    const predTask = projectTasks.value.find(t => t.id === pred.taskId)
    if (!predTask?.planFinishDate) continue
    if (pred.dependencyType === 'FS') {
      const d = dateAddDays(predTask.planFinishDate, pred.lagDays)
      if (!maxDate || d > maxDate) maxDate = d
    } else if (pred.dependencyType === 'SS') {
      if (predTask.planStartDate) {
        const d = dateAddDays(predTask.planStartDate, pred.lagDays)
        if (!maxDate || d > maxDate) maxDate = d
      }
    }
  }
  return maxDate
}

async function openEditTask(row: TaskListItem) {
  editingTask.value = { ...row }
  predRows.value = parsePreTaskCodes(row.preTaskCodes).map((seg, i) => ({ ...seg, rowKey: Date.now() + (++predKeySeq) }))
  // 加载该项目的任务列表（用于前置任务选择）
  if (row.projectId) {
    try {
      const res = await getProjectTasks(row.projectId)
      projectTasks.value = res.data
    } catch { projectTasks.value = [] }
  }
  showEditTaskDialog.value = true
}

/* 里程碑自动逻辑 */
watch(() => (editingTask.value as any)?.nodeType, (val) => {
  if (val !== 2 || !editingTask.value) return
  ;(editingTask.value as any).planDuration = 0
  ;(editingTask.value as any).actualDuration = 0
  ;(editingTask.value as any).referenceDuration = 0
  if (!(editingTask.value as any).actualFinishDate) {
    ;(editingTask.value as any).progressPct = 0
  }
})

watch(() => (editingTask.value as any)?.planStartDate, (val) => {
  if ((editingTask.value as any)?.nodeType === 2 && val) {
    ;(editingTask.value as any).planFinishDate = val
  }
})

watch([() => (editingTask.value as any)?.planStartDate, () => (editingTask.value as any)?.planFinishDate], () => {
  const t = editingTask.value as any
  if (!t || taskHasChildren(t.id)) return
  if (t.planStartDate && t.planFinishDate) {
    const days = Math.round((new Date(t.planFinishDate).getTime() - new Date(t.planStartDate).getTime()) / (1000 * 60 * 60 * 24))
    if (days >= 0) t.planDuration = days
  }
})

watch([() => (editingTask.value as any)?.actualStartDate, () => (editingTask.value as any)?.actualFinishDate], () => {
  const t = editingTask.value as any
  if (!t || taskHasChildren(t.id)) return
  if (t.actualStartDate && t.actualFinishDate) {
    const days = Math.round((new Date(t.actualFinishDate).getTime() - new Date(t.actualStartDate).getTime()) / (1000 * 60 * 60 * 24))
    if (days >= 0) t.actualDuration = days
  }
})

watch([() => (editingTask.value as any)?.actualStartDate, () => (editingTask.value as any)?.actualFinishDate], () => {
  const t = editingTask.value as any
  if (!t) return
  if (t.actualFinishDate) {
    t.status = 2
    t.progressPct = 100
  } else if (t.actualStartDate) {
    t.status = 1
  } else if (!t.planStartDate && !t.planFinishDate) {
    t.status = 0
    t.progressPct = 0
  }
})

async function handleSaveTask() {
  if (!editingTask.value?.id || !editingTask.value?.projectId) return
  if (!editingTask.value.taskName) { ElMessage.warning('请输入任务名称'); return }
  ;(editingTask.value as any).preTaskCodes = serializePreTaskCodes(predRows.value) || undefined
  if (predRows.value.length > 0) {
    const calcStart = calcPlanStartFromPreds()
    const curStart = (editingTask.value as any).planStartDate ?? ''
    if (calcStart && calcStart.slice(0, 10) > curStart.slice(0, 10)) {
      ;(editingTask.value as any).planStartDate = calcStart
      if ((editingTask.value as any).planFinishDate && (editingTask.value as any).planDuration && (editingTask.value as any).planDuration > 0) {
        ;(editingTask.value as any).planFinishDate = dateAddDays(calcStart, (editingTask.value as any).planDuration)
      }
    }
  }
  taskSaving.value = true
  try {
    await updateProjectTask(editingTask.value.projectId, editingTask.value.id, editingTask.value as any)
    ElMessage.success('保存成功')
    showEditTaskDialog.value = false
    fetchData()
  } catch {
    // 错误已由请求拦截器处理
  } finally {
    taskSaving.value = false
  }
}

/* ───────── 项目下拉 ───────── */
const projectOptions = ref<Project[]>([])
async function loadProjects() {
  try {
    const res = await getProjectList({ pageIndex: 1, pageSize: 999 })
    projectOptions.value = res.data.items
  } catch { /* 忽略 */ }
}

/* ───────── 获取列表 ───────── */
async function fetchData() {
  loading.value = true
  try {
    const params: Record<string, any> = {}
    if (searchForm.projectId) params.projectId = searchForm.projectId
    if (searchForm.planStartDateFrom) params.planStartDateFrom = searchForm.planStartDateFrom
    if (searchForm.planStartDateTo) params.planStartDateTo = searchForm.planStartDateTo
    if (searchForm.planFinishDateFrom) params.planFinishDateFrom = searchForm.planFinishDateFrom
    if (searchForm.planFinishDateTo) params.planFinishDateTo = searchForm.planFinishDateTo
    if (searchForm.status !== null) params.status = searchForm.status
    if (searchForm.assigneeId) params.assigneeId = searchForm.assigneeId

    const res = await getTaskList(params)
    tableData.value = res.data
    rebuildTaskLookupMap()

  } catch {
    // 错误已由请求拦截器处理
  } finally {
    loading.value = false
  }
}

/* ───────── 搜索 / 重置 ───────── */
function handleSearch() {
  fetchData()
}

function handleReset() {
  searchForm.projectId = null
  searchForm.planStartDateFrom = ''
  searchForm.planStartDateTo = ''
  searchForm.planFinishDateFrom = ''
  searchForm.planFinishDateTo = ''
  searchForm.status = null
  searchForm.overdue = null
  searchForm.assigneeId = null
  searchForm.assigneeName = ''
  fetchData()
}

/* ───────── 跳转项目 ───────── */
function goToProject(projectId: number) {
  void openProjectView(projectId)
}

/* ───────── 生命周期 ───────── */
onMounted(async () => {
  const [deptRes, userRes, dictRes] = await Promise.allSettled([
    getDepartments(), searchUsers(''), getDictByType('task_category')
  ])
  if (deptRes.status === 'fulfilled') departments.value = deptRes.value.data
  if (userRes.status === 'fulfilled') users.value = userRes.value.data
  if (dictRes.status === 'fulfilled') {
    dictMap.value = { task_category: dictRes.value.data }
  }
  try {
    const ruleRes = await getSysParamByKey('plan_code_rule')
    taskNoRule.value = ruleRes.data.paramValue
  } catch { /* 默认使用 3,2,2 */ }
  try {
    const fnRes = await getFunctionList()
    functions.value = fnRes.data ?? []
  } catch { /* 忽略 */ }
  await loadProjects()
  searchForm.assigneeId = authStore.userId
  searchForm.assigneeName = (authStore as any).realName ?? ''
  // 并行：加载主列表 + 预加载全量任务查找表（解决跨项目前置任务序号解析）
  await Promise.all([
    fetchData(),
    (async () => {
      try {
        const all = await getTaskList({})
        for (const t of all.data) {
          if (t.id) taskLookupMap.value.set(t.id, { taskNo: t.taskNo, taskName: t.taskName })
        }
      } catch { /* 非关键 */ }
    })()
  ])
})
</script>

<template>
  <div class="task-management">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="searchForm" label-width="auto" size="small" inline>
        <el-form-item label="项目">
          <el-select v-model="searchForm.projectId" placeholder="全部项目" clearable filterable style="width:180px">
            <el-option v-for="p in projectOptions" :key="p.id" :label="p.projectName" :value="p.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="计划开始">
          <el-date-picker
            v-model="searchForm.planStartDateFrom"
            type="date"
            placeholder="开始日期起"
            value-format="YYYY-MM-DD"
            style="width:140px"
          />
          <span style="margin:0 6px">~</span>
          <el-date-picker
            v-model="searchForm.planStartDateTo"
            type="date"
            placeholder="开始日期止"
            value-format="YYYY-MM-DD"
            style="width:140px"
          />
        </el-form-item>
        <el-form-item label="计划完成">
          <el-date-picker
            v-model="searchForm.planFinishDateFrom"
            type="date"
            placeholder="完成日期起"
            value-format="YYYY-MM-DD"
            style="width:140px"
          />
          <span style="margin:0 6px">~</span>
          <el-date-picker
            v-model="searchForm.planFinishDateTo"
            type="date"
            placeholder="完成日期止"
            value-format="YYYY-MM-DD"
            style="width:140px"
          />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="searchForm.status" placeholder="全部" clearable style="width:100px">
            <el-option v-for="o in statusOptions" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="逾期状态">
          <el-select v-model="searchForm.overdue" placeholder="全部" clearable style="width:110px">
            <el-option v-for="o in overdueOptions" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="责任人">
          <el-select v-model="searchForm.assigneeId" placeholder="全部" clearable filterable style="width:140px">
            <el-option v-for="u in users" :key="u.id" :label="u.realName" :value="u.id" />
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
      <el-table :data="paginatedData" v-loading="loading" ref="tableRef" :row-class-name="getRowClassName" row-key="id" border size="small" style="width:100%" max-height="calc(100vh - 280px)" :header-cell-style="{ textAlign: 'center' }">
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column label="项目编号" width="120" show-overflow-tooltip align="center">
          <template #default="{ row }">
            <el-link type="primary" underline="never" style="cursor:pointer" @click="goToProject(row.projectId)">{{ row.projectCode }}</el-link>
          </template>
        </el-table-column>
        <el-table-column label="项目名称" width="200" show-overflow-tooltip align="center">
          <template #default="{ row }">
            <el-link type="primary" underline="never" style="cursor:pointer" @click="goToProject(row.projectId)">{{ row.projectName }}</el-link>
          </template>
        </el-table-column>
        <el-table-column label="任务编号" width="156" show-overflow-tooltip>
          <template #default="{ row }">{{ row.taskNo }}</template>
        </el-table-column>
        <el-table-column label="任务名称" min-width="243">
          <template #default="{ row }">
            <span class="task-name-cell" :title="row.taskName">
              <span v-if="row.nodeType === 2" style="color:#e74c3c">◆</span>
              <span v-else style="color:#909399">·</span>
              {{ row.taskName }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="进度" width="90" align="center">
          <template #default="{ row }">
            <el-progress :percentage="Number(row.progressPct)" :stroke-width="12" class="task-progress-bar" />
          </template>
        </el-table-column>
        <el-table-column label="计划开始" width="105" align="center">
          <template #default="{ row }">{{ row.planStartDate?.slice(0,10) }}</template>
        </el-table-column>
        <el-table-column label="计划完成" width="105" align="center">
          <template #default="{ row }">{{ row.planFinishDate?.slice(0,10) }}</template>
        </el-table-column>
        <el-table-column label="状态" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTag(row.status)" size="small">{{ statusLabel(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="逾期状态" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="overdueStatus(row) === '已逾期' ? 'danger' : 'info'" size="small">{{ overdueStatus(row) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="责任人" width="100" show-overflow-tooltip>
          <template #default="{ row }">{{ row.assigneeName || '-' }}</template>
        </el-table-column>
        <el-table-column label="责任部门" width="120" show-overflow-tooltip>
          <template #default="{ row }">{{ row.deptName || '-' }}</template>
        </el-table-column>
        <el-table-column label="优先级" width="70" align="center">
          <template #default="{ row }">{{ priorityLabel(row.priority) }}</template>
        </el-table-column>
        <el-table-column label="实际工期" width="71" align="center">
          <template #default="{ row }">{{ row.actualDuration }}</template>
        </el-table-column>
        <el-table-column label="前置任务" width="130">
          <template #default="{ row }">
            <el-popover
              v-if="getPredecessorTasks(row).length > 0"
              placement="top"
              trigger="hover"
              :width="280"
              :show-after="150"
              popper-class="pre-task-popover-wrap"
            >
              <template #reference>
                <span class="pre-task-cell">{{ formatPreTaskLabels(row) }}</span>
              </template>
              <div class="pre-task-popover" @click.stop>
                <div
                  v-for="pt in getPredecessorTasks(row)"
                  :key="pt.id"
                  class="pre-task-item"
                  @click.stop="scrollToTask(pt.id)"
                >
                  <span class="pre-task-no">{{ pt.taskNo }}</span>
                  <span class="pre-task-name">{{ pt.taskName }}</span>
                </div>
              </div>
            </el-popover>
            <span v-else class="pre-task-empty">-</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="168" fixed="right">
          <template #default="{ row }">
            <div style="display:flex;gap:6px;justify-content:center">
              <el-button link style="color:#409eff;padding:0 4px" @click="openViewTask(row)">查看</el-button>
              <el-button link style="color:#67c23a;padding:0 4px" :style="{ visibility: row.assigneeId === authStore.userId ? 'visible' : 'hidden', pointerEvents: row.assigneeId === authStore.userId ? 'auto' : 'none' }" @click="openEditTask(row)">编辑</el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :page-sizes="[10, 20, 50]"
          :total="filteredTableData.length"
          layout="total, sizes, prev, pager, next, jumper"
          background
        />
      </div>
    </el-card>
    <!-- ── 编辑任务对话框 ── -->
    <el-dialog v-model="showEditTaskDialog" title="编辑任务" width="840px" :close-on-click-modal="false" top="5vh">
      <el-form v-if="editingTask" :model="editingTask" label-width="100px" size="default">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="序号">
              <el-input v-model="editingTask.taskNo" disabled placeholder="保存后自动生成" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="任务名称" required>
              <el-input v-model="editingTask.taskName" placeholder="请输入任务名称" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="工序号">
              <el-input v-model="(editingTask as any).wbsCode" placeholder="如：T1-1" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="任务类别">
              <el-select v-model="(editingTask as any).taskCategory" placeholder="请选择" style="width:100%">
                <el-option v-for="item in (dictMap['task_category'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="节点类型">
              <el-radio-group v-model="(editingTask as any).nodeType">
                <el-radio :value="1">任务</el-radio>
                <el-radio :value="2">里程碑</el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="优先级">
              <el-select v-model="editingTask.priority" style="width:100%">
                <el-option v-for="o in taskPriorityOptions" :key="o.value" :label="o.label" :value="o.value" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="状态">
              <el-select v-model="editingTask.status" style="width:100%">
                <el-option v-for="o in taskStatusOptions" :key="o.value" :label="o.label" :value="o.value" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="责任部门">
              <el-tree-select
                v-model="(editingTask as any).deptId"
                :data="deptTreeData"
                :props="{ label: 'name', children: 'children', value: 'id' }"
                node-key="id"
                check-strictly
                placeholder="请选择部门"
                clearable
                filterable
                style="width:100%"
                @change="(val: number) => { (editingTask as any).deptName = departments.find(d => d.id === val)?.name ?? ''; (editingTask as any).assigneeName = ''; (editingTask as any).assigneeId = undefined }"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="责任人">
              <el-select v-model="editingTask.assigneeName" filterable clearable placeholder="请选择人员" style="width:100%" @change="(val: string) => { if (!val) { editingTask.assigneeId = undefined; (editingTask as any).deptId = undefined; (editingTask as any).deptName = ''; return }; const u = users.find(u => u.realName === val); if(u) { editingTask.assigneeId = u.id; (editingTask as any).deptId = u.departmentId; (editingTask as any).deptName = departments.find(d => d.id === u.departmentId)?.name ?? '' } }">
                <el-option v-for="u in users.filter(u => !(editingTask as any).deptId || u.departmentId === (editingTask as any).deptId)" :key="u.id" :label="u.realName" :value="u.realName" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="参考工期">
              <el-input-number v-model="(editingTask as any).referenceDuration" :min="0" style="width:100%" :disabled="(editingTask as any)?.nodeType === 2" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="前置任务">
              <div class="predecessor-section">
                <el-button
                  type="primary"
                  size="small"
                  @click="addPredRow"
                  :disabled="projectTasks.filter(t => t.id).length === 0"
                >
                  + 添加前置任务
                </el-button>
                <div v-if="predRows.length === 0" class="predecessor-empty">
                  尚未设置前置任务
                </div>
                <div v-for="row in predRows" :key="row.rowKey" class="predecessor-row">
                  <el-select
                    v-model="row.taskId"
                    placeholder="选择前置任务"
                    filterable
                    style="width: 320px"
                  >
                    <el-option
                      v-for="t in getAvailableForPredRow(row.rowKey)"
                      :key="t.id"
                      :label="t._label"
                      :value="t.id"
                    >
                      <span :style="{ paddingLeft: (t._depth ?? 0) * 20 + 'px' }">{{ t._label }}</span>
                    </el-option>
                  </el-select>
                  <el-select v-model="row.dependencyType" style="width: 150px; margin-left: 8px;">
                    <el-option label="完成-开始 (FS)" value="FS" />
                    <el-option label="开始-开始 (SS)" value="SS" />
                    <el-option label="完成-完成 (FF)" value="FF" />
                    <el-option label="开始-完成 (SF)" value="SF" />
                  </el-select>
                  <el-input-number
                    v-model="row.lagDays"
                    :min="-999"
                    :max="999"
                    controls-position="right"
                    style="width: 120px; margin-left: 8px;"
                  />
                  <span class="unit-suffix">天</span>
                  <el-button
                    type="danger"
                    link
                    size="small"
                    style="margin-left: 8px;"
                    @click="removePredRow(row.rowKey)"
                  >删除</el-button>
                </div>
              </div>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="完成进度(%)">
              <el-input-number v-model="editingTask.progressPct" :min="0" :max="100" :precision="1" style="width:100%" :disabled="(editingTask as any)?.nodeType === 2 || taskHasChildren(editingTask.id) || !(editingTask as any)?.actualStartDate" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-card shadow="never" class="group-card">
              <template #header><span class="group-title">计划时间</span></template>
              <el-row :gutter="24">
                <el-col :span="8">
                  <el-form-item label="计划开始">
                    <el-date-picker v-model="(editingTask as any).planStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="taskHasChildren(editingTask.id) || (editingTask as any)?.projectStatus !== 0" placeholder="有子节点时由子节点自动计算" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="计划完成">
                    <el-date-picker v-model="(editingTask as any).planFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="(editingTask as any)?.nodeType === 2 || taskHasChildren(editingTask.id) || (editingTask as any)?.projectStatus !== 0" placeholder="有子节点时由子节点自动计算" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="计划工期">
                    <el-input-number v-model="(editingTask as any).planDuration" :min="0" style="width:100%" disabled placeholder="由系统自动计算" />
                  </el-form-item>
                </el-col>
              </el-row>
            </el-card>
          </el-col>
          <el-col :span="24">
            <el-card shadow="never" class="group-card">
              <template #header><span class="group-title">实际时间</span></template>
              <el-row :gutter="24">
                <el-col :span="8">
                  <el-form-item label="实际开始">
                    <el-date-picker v-model="(editingTask as any).actualStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="(editingTask as any)?.nodeType === 2 || taskHasChildren(editingTask.id)" placeholder="有子节点时由子节点自动计算" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="实际完成">
                    <el-date-picker v-model="(editingTask as any).actualFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="taskHasChildren(editingTask.id)" placeholder="有子节点时由子节点自动计算" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="实际工期">
                    <el-input-number v-model="(editingTask as any).actualDuration" :min="0" style="width:100%" disabled placeholder="由系统自动计算" />
                  </el-form-item>
                </el-col>
              </el-row>
            </el-card>
          </el-col>
          <el-col :span="24">
            <el-form-item label="备注" class="remark-item">
              <el-input v-model="(editingTask as any).remark" type="textarea" :rows="2" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="showEditTaskDialog = false">取消</el-button>
        <el-button type="danger" :loading="taskSaving" @click="handleSaveTask">确定</el-button>
      </template>
    </el-dialog>

    <!-- ── 查看任务详情对话框 ── -->
    <el-dialog v-model="showViewTaskDialog" title="任务详情" width="840px" :close-on-click-modal="false" top="5vh">
      <el-form v-if="viewingTask" :model="viewingTask" label-width="100px" size="default" disabled>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="序号">
              <el-input :model-value="viewingTask.taskNo" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="任务名称">
              <el-input :model-value="viewingTask.taskName" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="工序号">
              <el-input :model-value="(viewingTask as any).wbsCode || ''" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="任务类别">
              <el-select :model-value="(viewingTask as any).taskCategory" placeholder="请选择" style="width:100%">
                <el-option v-for="item in (dictMap['task_category'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="节点类型">
              <el-radio-group :model-value="(viewingTask as any).nodeType">
                <el-radio :value="1">任务</el-radio>
                <el-radio :value="2">里程碑</el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="优先级">
              <el-select :model-value="viewingTask.priority" style="width:100%">
                <el-option v-for="o in taskPriorityOptions" :key="o.value" :label="o.label" :value="o.value" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="状态">
              <el-select :model-value="viewingTask.status" style="width:100%">
                <el-option v-for="o in taskStatusOptions" :key="o.value" :label="o.label" :value="o.value" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="责任部门">
              <el-tree-select
                :model-value="(viewingTask as any).deptId"
                :data="deptTreeData"
                :props="{ label: 'name', children: 'children', value: 'id' }"
                node-key="id"
                check-strictly
                placeholder="请选择部门"
                clearable
                filterable
                style="width:100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="责任人">
              <el-select :model-value="viewingTask.assigneeName" filterable clearable placeholder="请选择人员" style="width:100%">
                <el-option v-for="u in users.filter(u => !(viewingTask as any).deptId || u.departmentId === (viewingTask as any).deptId)" :key="u.id" :label="u.realName" :value="u.realName" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="参考工期">
              <el-input-number :model-value="(viewingTask as any).referenceDuration" :min="0" style="width:100%" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="前置任务">
              <el-input :model-value="viewingTask.preTaskCodes || '尚未设置前置任务'" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="完成进度(%)">
              <el-input-number :model-value="viewingTask.progressPct" :min="0" :max="100" :precision="1" style="width:100%" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-card shadow="never" class="group-card">
              <template #header><span class="group-title">计划时间</span></template>
              <el-row :gutter="24">
                <el-col :span="8">
                  <el-form-item label="计划开始">
                    <el-date-picker :model-value="(viewingTask as any).planStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="计划完成">
                    <el-date-picker :model-value="(viewingTask as any).planFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="计划工期">
                    <el-input-number :model-value="(viewingTask as any).planDuration" :min="0" style="width:100%" disabled />
                  </el-form-item>
                </el-col>
              </el-row>
            </el-card>
          </el-col>
          <el-col :span="24">
            <el-card shadow="never" class="group-card">
              <template #header><span class="group-title">实际时间</span></template>
              <el-row :gutter="24">
                <el-col :span="8">
                  <el-form-item label="实际开始">
                    <el-date-picker :model-value="(viewingTask as any).actualStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="实际完成">
                    <el-date-picker :model-value="(viewingTask as any).actualFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="实际工期">
                    <el-input-number :model-value="(viewingTask as any).actualDuration" :min="0" style="width:100%" disabled />
                  </el-form-item>
                </el-col>
              </el-row>
            </el-card>
          </el-col>
          <el-col :span="24">
            <el-form-item label="备注" class="remark-item">
              <el-input :model-value="(viewingTask as any).remark || ''" type="textarea" :rows="2" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button type="primary" @click="showViewTaskDialog = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.task-management { padding: 20px; }
.pagination-wrapper { display: flex; justify-content: flex-end; margin-top: 12px; }
.search-card { margin-bottom: 0; }
.task-progress-bar :deep(.el-progress__text) { font-size: 11px !important; }
.group-card {
  margin-bottom: 12px;
  border: 1px solid #dcdfe6;
}
.group-card :deep(.el-card__header) {
  padding: 8px 16px;
  background: #f5f7fa;
  border-bottom: 1px solid #dcdfe6;
}
.group-title {
  font-size: 13px;
  font-weight: 600;
  color: #606266;
}
.remark-item {
  margin-top: 12px;
}
.predecessor-section {
  width: 100%;
}
.predecessor-empty {
  color: #909399;
  font-size: 13px;
  margin-top: 8px;
}
.predecessor-row {
  display: flex;
  align-items: center;
  margin-top: 8px;
  flex-wrap: wrap;
}
.task-name-cell {
  white-space: normal;
  word-break: break-word;
  line-height: 1.4;
  display: inline-block;
  padding: 4px 0;
}
:deep(.flash-row > td) {
  background-color: rgba(64, 158, 255, 0.25);
  transition: background-color 0.6s;
}
.unit-suffix {
  margin-left: 8px;
  color: #909399;
  font-size: 14px;
}
.pre-task-cell {
  color: #409eff;
  cursor: pointer;
  white-space: nowrap;
}
.pre-task-cell:hover {
  color: #66b1ff;
  text-decoration: underline;
}
.pre-task-empty {
  color: #c0c4cc;
}
</style>
<style>
.pre-task-popover-wrap {
  padding: 8px 0 !important;
}
.pre-task-popover {
  max-height: 240px;
  overflow-y: auto;
}
.pre-task-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 16px;
  cursor: pointer;
  transition: background-color 0.15s;
}
.pre-task-item:hover {
  background-color: #ecf5ff;
}
.pre-task-no {
  font-weight: 600;
  color: #303133;
  white-space: nowrap;
}
.pre-task-name {
  color: #606266;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
