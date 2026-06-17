<script setup lang="ts">
import { ref, shallowRef, reactive, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/store/auth'
import { getProjectList, getMyTasks, getProjectTasks, updateProjectTask, type MyTaskItem, type TaskListItem } from '@/api/project'
import { getDepartments, searchUsers, getDictByType } from '@/api/template'
import { getSysParamByKey, getFunctionList } from '@/api/system'
import type { Project } from '@/types/project'
import type { Department, UserInfo } from '@/types/template'
import type { FunctionItem } from '@/types/system'
import { ElMessage } from 'element-plus'
import { openProjectView } from '@/utils/projectNav'
import { buildDeptTree } from '@/utils/deptTree'
import { dateAddDays } from '@/utils/dateUtils'
import { taskStatusOptions, taskPriorityOptions, statusLabel, priorityLabel } from '@/utils/taskConstants'
import { parsePreTaskCodes, serializePreTaskCodes } from '@/utils/preTaskHelpers'

const router = useRouter()
const auth = useAuthStore()
const currentTime = ref('')

/** 非管理员的待处理汇总（结构化，支持超链接） */
const isAdmin = computed(() => auth.role === 'admin')
const welcomeParts = computed(() => {
  if (isAdmin.value) return [] as { text: string; link?: string; color?: string }[]
  const parts: { text: string; link?: string; color?: string }[] = [{ text: '！您有' }]
  if (auth.pendingTaskCount > 0) {
    parts.push({ text: '待处理 ' })
    parts.push({ text: '项目任务(', link: '/project/tasks' })
    parts.push({ text: `${auth.pendingTaskCount}`, link: '/project/tasks', color: '#f56c6c' })
    parts.push({ text: ')', link: '/project/tasks' })
    parts.push({ text: ' 条' })
    if (auth.pendingFileTotal > 0) parts.push({ text: '，' })
  }
  if (auth.pendingFileTotal > 0) {
    parts.push({ text: '待处理 ' })
    parts.push({ text: '文件数量(', link: '/project/files' })
    parts.push({ text: `${auth.pendingFileTotal}`, link: '/project/files', color: '#f56c6c' })
    parts.push({ text: '，', link: '/project/files', color: '#f56c6c' })
    parts.push({ text: `${auth.pendingFileRequired}`, link: '/project/files', color: '#f56c6c' })
    parts.push({ text: ')', link: '/project/files' })
    parts.push({ text: ' 条！' })
  }
  return parts.length > 1 ? parts : []
})
const myProjects = shallowRef<Project[]>([])
const allTasks = shallowRef<MyTaskItem[]>([])
const loading = ref(false)

/** 合并单次遍历 allTasks，产出逾期/待处理/已完成 列表 + 项目统计，避免 5 次独立 O(n) */
const taskStats = computed(() => {
  const overdue: MyTaskItem[] = []
  const pending: MyTaskItem[] = []
  const completed: MyTaskItem[] = []
  const countMap: Record<number, number> = {}
  const overdueMap: Record<number, number> = {}
  const oneMonthAgo = new Date()
  oneMonthAgo.setMonth(oneMonthAgo.getMonth() - 1)

  for (const t of allTasks.value) {
    const isOverdue = t.isOverdue || t.status === 3
    if (isOverdue) overdue.push(t)
    if (t.status !== 2) {
      pending.push(t)
    } else if (t.actualFinishDate && new Date(t.actualFinishDate) >= oneMonthAgo) {
      completed.push(t)
    }
    countMap[t.projectId] = (countMap[t.projectId] || 0) + 1
    if (isOverdue) overdueMap[t.projectId] = (overdueMap[t.projectId] || 0) + 1
  }
  return { overdue, pending, completed, countMap, overdueMap }
})

const overdueTasks = computed(() => taskStats.value.overdue)
const pendingTasks = computed(() => taskStats.value.pending)
const completedTasks = computed(() => taskStats.value.completed)
const projectTaskCount = computed(() => taskStats.value.countMap)
const projectOverdueCount = computed(() => taskStats.value.overdueMap)

onMounted(async () => {
  const now = new Date()
  currentTime.value = now.toLocaleString('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit'
  })
  // 加载对话窗辅助数据
  const [deptRes, userRes, dictRes, fnRes] = await Promise.allSettled([
    getDepartments(), searchUsers(''), getDictByType('task_category'), getFunctionList()
  ])
  if (deptRes.status === 'fulfilled') departments.value = deptRes.value.data
  if (userRes.status === 'fulfilled') users.value = userRes.value.data
  if (dictRes.status === 'fulfilled') {
    dictMap.value = { task_category: dictRes.value.data }
  }
  if (fnRes.status === 'fulfilled') functions.value = fnRes.value.data ?? []
  // 待处理任务/文件统计（复用 store 缓存，MainLayout 已请求时不会重复）
  await auth.fetchPendingCounts()
  loadData()
})

async function loadData() {
  loading.value = true
  try {
    const [, uid] = await Promise.all([
      auth.permissions.length ? Promise.resolve() : auth.fetchPermissionsAndMenus(),
      auth.ensureUserId()
    ])
    if (!uid) return
    const [projRes, taskRes] = await Promise.all([
      getProjectList({ memberId: uid, assigneeId: uid, pageIndex: 1, pageSize: 100 }),
      getMyTasks()
    ])
    myProjects.value = projRes.data.items.sort((a, b) => a.projectCode.localeCompare(b.projectCode))
    allTasks.value = taskRes.data
  } catch { /* ignore */ }
  finally { loading.value = false }
}

function goProject(idOrRow: number | Project | MyTaskItem) {
  const id =
    typeof idOrRow === 'number'
      ? idOrRow
      : 'projectId' in idOrRow && idOrRow.projectId != null
        ? idOrRow.projectId
        : (idOrRow as Project).id
  void openProjectView(id)
}

function projectProgress(p: Project): string {
  if (p.firstTaskProgress == null) return '--'
  return `${p.firstTaskProgress}%`
}

function planProgress(p: Project): string {
  if (p.plannedProgress == null) return '--'
  return `${p.plannedProgress}%`
}

function progressColor(p: Project): string {
  const current = p.firstTaskProgress
  const planned = p.plannedProgress
  if (current == null || planned == null || planned === 0) return '#909399'
  const ratio = current / planned
  if (ratio >= 1) return '#67c23a'
  if (ratio >= 0.8) return '#409eff'
  if (ratio >= 0.5) return '#e6a23c'
  return '#f56c6c'
}

function taskStatusText(status: number, isOverdue: boolean): string {
  if (isOverdue || status === 3) return '已逾期'
  const map: Record<number, string> = { 0: '未开始', 1: '进行中', 2: '已完成' }
  return map[status] || '未知'
}

function taskStatusColor(status: number, isOverdue: boolean): string {
  if (isOverdue || status === 3) return '#f56c6c'
  if (status === 2) return '#67c23a'
  if (status === 1) return '#409eff'
  return '#909399'
}

function formatDate(d?: string): string {
  if (!d) return '--'
  return d.split('T')[0]
}

function formatDuration(d?: number): string {
  if (d == null) return '--'
  return `${d}天`
}

/* ───────── 辅助数据（对话窗使用） ───────── */
const departments = ref<Department[]>([])
const functions = ref<FunctionItem[]>([])
const users = ref<UserInfo[]>([])
const dictMap = ref<Record<string, { id: number; dictCode: string; dictLabel: string }[]>>({})

const deptTreeData = computed(() => buildDeptTree(departments.value))

/* ───────── 查看任务详情 ───────── */
const showViewTaskDialog = ref(false)
const viewingTask = ref<TaskListItem | null>(null)
function openViewTask(task: MyTaskItem) {
  viewingTask.value = task as unknown as TaskListItem
  showViewTaskDialog.value = true
}

/* ───────── 编辑任务 ───────── */
const showEditTaskDialog = ref(false)
const editingTask = ref<TaskListItem | null>(null)
const taskSaving = ref(false)
const projectTasks = ref<TaskListItem[]>([])
const predRows = ref<PredRow[]>([])
let predKeySeq = 0

interface PredRow {
  rowKey: number
  taskId: number
  dependencyType: string
  lagDays: number
}

function taskHasChildren(taskId?: number | null): boolean {
  if (!taskId) return false
  return projectTasks.value.some(t => t.parentId === taskId)
}

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

async function openEditTask(row: MyTaskItem) {
  // 加载该项目的任务列表（用于前置任务选择和填充编辑数据）
  if (row.projectId) {
    try {
      const res = await getProjectTasks(row.projectId)
      projectTasks.value = res.data
      const full = res.data.find((t: any) => t.id === row.id)
      if (full) {
        editingTask.value = { ...full }
      } else {
        editingTask.value = { ...row } as unknown as TaskListItem
      }
    } catch {
      projectTasks.value = []
      editingTask.value = { ...row } as unknown as TaskListItem
    }
  } else {
    editingTask.value = { ...row } as unknown as TaskListItem
  }
  predRows.value = parsePreTaskCodes((editingTask.value as any).preTaskCodes).map((seg, i) => ({ ...seg, rowKey: Date.now() + (++predKeySeq) }))
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
    // 重新加载工作台数据
    loadData()
  } catch {
    // 错误已由请求拦截器处理
  } finally {
    taskSaving.value = false
  }
}
</script>

<template>
  <div class="workbench-container">
    <div class="welcome-card">
      <h2 class="welcome-title">欢迎回来，{{ auth.realName || '用户' }}<template v-for="(part, i) in welcomeParts" :key="i"><router-link v-if="part.link" :to="part.link" class="welcome-link" :style="part.color ? { color: part.color } : undefined">{{ part.text }}</router-link><span v-else class="welcome-summary" :style="part.color ? { color: part.color } : undefined">{{ part.text }}</span></template></h2>
      <span class="welcome-time">{{ currentTime }}</span>
    </div>

    <div v-loading="loading" class="grid-container">
      <el-row :gutter="16" class="grid-row">
        <!-- 左上：我的进行中项目 -->
        <el-col :span="12">
          <el-card shadow="hover" class="grid-card card-projects">
            <template #header>
              <div class="card-header-row">
                <span>
                  <el-icon style="margin-right:4px;color:#409eff;font-size:18px"><FolderOpened /></el-icon>
                  <span class="card-header-title">我的进行中项目</span>
                  <el-tag size="small" type="danger" style="margin-left:8px">{{ myProjects.length }}</el-tag>
                </span>
                <span class="progress-legend">
                  当前进度/计划进度
                  <span class="legend-dot" style="background:#67c23a"></span>≥100%
                  <span class="legend-dot" style="background:#409eff"></span>≥80%
                  <span class="legend-dot" style="background:#e6a23c"></span>≥50%
                  <span class="legend-dot" style="background:#f56c6c"></span>&lt;50%
                </span>
              </div>
            </template>
            <el-table :data="myProjects" stripe size="small" row-class-name="clickable-row">
              <el-table-column prop="projectCode" label="项目编号" width="77" />
              <el-table-column prop="projectName" label="项目名称" min-width="85" show-overflow-tooltip />
              <el-table-column label="计划开始" width="86">
                <template #default="{ row }">{{ formatDate(row.rootPlanStartDate) }}</template>
              </el-table-column>
              <el-table-column label="计划完成" width="86">
                <template #default="{ row }">{{ formatDate(row.rootPlanFinishDate) }}</template>
              </el-table-column>
              <el-table-column prop="projectManagerName" label="项目经理" width="85" show-overflow-tooltip />
              <el-table-column label="计划进度" width="75">
                <template #default="{ row }">
                  <span :style="{ color: progressColor(row), fontWeight: 600 }">
                    {{ planProgress(row) }}
                  </span>
                </template>
              </el-table-column>
              <el-table-column label="当前进度" width="70">
                <template #default="{ row }">
                  <span :style="{ color: progressColor(row), fontWeight: 600 }">
                    {{ projectProgress(row) }}
                  </span>
                </template>
              </el-table-column>
              <el-table-column label="我的任务数" width="85">
                <template #default="{ row }">
                  {{ projectTaskCount[row.id] || 0 }}
                </template>
              </el-table-column>
              <el-table-column label="我的逾期数" width="85">
                <template #default="{ row }">
                  <span :style="{ color: (projectOverdueCount[row.id] || 0) > 0 ? '#f56c6c' : '#909399' }">
                    {{ projectOverdueCount[row.id] || 0 }}
                  </span>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="60" fixed="right">
                <template #default="{ row }">
                  <el-button type="primary" link size="small" @click.stop="goProject(row.id)">查看</el-button>
                </template>
              </el-table-column>
            </el-table>
            <el-empty v-if="myProjects.length === 0" description="暂无" :image-size="60" />
          </el-card>
        </el-col>

        <!-- 右上：逾期任务 -->
        <el-col :span="12">
          <el-card shadow="hover" class="grid-card card-overdue">
            <template #header>
              <el-icon style="margin-right:4px;color:#f56c6c;font-size:18px"><WarningFilled /></el-icon>
              <span class="card-header-title">逾期任务</span>
              <el-tag size="small" type="danger" style="margin-left:8px">{{ overdueTasks.length }}</el-tag>
            </template>
            <el-table :data="overdueTasks" stripe size="small" row-class-name="clickable-row">
              <el-table-column prop="projectCode" label="项目编号" width="90" />
              <el-table-column prop="projectName" label="项目名称" min-width="90" show-overflow-tooltip />
              <el-table-column prop="taskNo" label="任务序号" width="90" />
              <el-table-column prop="taskName" label="任务名称" min-width="120" show-overflow-tooltip />
              <el-table-column label="状态" width="70">
                <template #default="{ row }">
                  <span :style="{ color: taskStatusColor(row.status, row.isOverdue), fontSize:'12px' }">
                    {{ taskStatusText(row.status, row.isOverdue) }}
                  </span>
                </template>
              </el-table-column>
              <el-table-column label="计划开始" width="86">
                <template #default="{ row }">{{ formatDate(row.planStartDate) }}</template>
              </el-table-column>
              <el-table-column label="计划完成" width="86">
                <template #default="{ row }">{{ formatDate(row.planFinishDate) }}</template>
              </el-table-column>
              <el-table-column label="实际开始" width="86">
                <template #default="{ row }">{{ formatDate(row.actualStartDate) }}</template>
              </el-table-column>
              <el-table-column label="实际完成" width="86">
                <template #default="{ row }">{{ formatDate(row.actualFinishDate) }}</template>
              </el-table-column>
              <el-table-column label="计划工期" width="75">
                <template #default="{ row }">{{ formatDuration(row.planDuration) }}</template>
              </el-table-column>
              <el-table-column label="实际工期" width="75">
                <template #default="{ row }">{{ formatDuration(row.actualDuration) }}</template>
              </el-table-column>
              <el-table-column label="操作" width="110" fixed="right">
                <template #default="{ row }">
                  <div style="display:flex;gap:6px;justify-content:center">
                    <el-button link style="color:#409eff;padding:0 4px" @click.stop="openViewTask(row)">查看</el-button>
                    <el-button link style="color:#67c23a;padding:0 4px" @click.stop="openEditTask(row)">编辑</el-button>
                  </div>
                </template>
              </el-table-column>
            </el-table>
            <el-empty v-if="overdueTasks.length === 0" description="暂无逾期任务" :image-size="60" />
          </el-card>
        </el-col>
      </el-row>

      <el-row :gutter="16" class="grid-row">
        <!-- 左下：待处理任务 -->
        <el-col :span="12">
          <el-card shadow="hover" class="grid-card card-pending">
            <template #header>
              <el-icon style="margin-right:4px;color:#e6a23c;font-size:18px"><Clock /></el-icon>
              <span class="card-header-title">待处理任务</span>
              <el-tag size="small" type="warning" style="margin-left:8px">{{ pendingTasks.length }}</el-tag>
            </template>
            <el-table :data="pendingTasks" stripe size="small" row-class-name="clickable-row">
              <el-table-column prop="projectCode" label="项目编号" width="90" />
              <el-table-column prop="projectName" label="项目名称" min-width="90" show-overflow-tooltip />
              <el-table-column prop="taskNo" label="任务序号" width="90" />
              <el-table-column prop="taskName" label="任务名称" min-width="120" show-overflow-tooltip />
              <el-table-column label="状态" width="70">
                <template #default="{ row }">
                  <span :style="{ color: taskStatusColor(row.status, row.isOverdue), fontSize:'12px' }">
                    {{ taskStatusText(row.status, row.isOverdue) }}
                  </span>
                </template>
              </el-table-column>
              <el-table-column label="计划开始" width="86">
                <template #default="{ row }">{{ formatDate(row.planStartDate) }}</template>
              </el-table-column>
              <el-table-column label="计划完成" width="86">
                <template #default="{ row }">{{ formatDate(row.planFinishDate) }}</template>
              </el-table-column>
              <el-table-column label="实际开始" width="86">
                <template #default="{ row }">{{ formatDate(row.actualStartDate) }}</template>
              </el-table-column>
              <el-table-column label="实际完成" width="86">
                <template #default="{ row }">{{ formatDate(row.actualFinishDate) }}</template>
              </el-table-column>
              <el-table-column label="计划工期" width="75">
                <template #default="{ row }">{{ formatDuration(row.planDuration) }}</template>
              </el-table-column>
              <el-table-column label="实际工期" width="75">
                <template #default="{ row }">{{ formatDuration(row.actualDuration) }}</template>
              </el-table-column>
              <el-table-column label="操作" width="110" fixed="right">
                <template #default="{ row }">
                  <div style="display:flex;gap:6px;justify-content:center">
                    <el-button link style="color:#409eff;padding:0 4px" @click.stop="openViewTask(row)">查看</el-button>
                    <el-button link style="color:#67c23a;padding:0 4px" @click.stop="openEditTask(row)">编辑</el-button>
                  </div>
                </template>
              </el-table-column>
            </el-table>
            <el-empty v-if="pendingTasks.length === 0" description="暂无待处理任务" :image-size="60" />
          </el-card>
        </el-col>

        <!-- 右下：已完成任务 -->
        <el-col :span="12">
          <el-card shadow="hover" class="grid-card card-completed">
            <template #header>
              <el-icon style="margin-right:4px;color:#67c23a;font-size:18px"><CircleCheckFilled /></el-icon>
              <span class="card-header-title">已完成任务</span>
              <el-tag size="small" type="success" style="margin-left:8px">{{ completedTasks.length }}</el-tag>
            </template>
            <el-table :data="completedTasks" stripe size="small" row-class-name="clickable-row">
              <el-table-column prop="projectCode" label="项目编号" width="90" />
              <el-table-column prop="projectName" label="项目名称" min-width="90" show-overflow-tooltip />
              <el-table-column prop="taskNo" label="任务序号" width="90" />
              <el-table-column prop="taskName" label="任务名称" min-width="120" show-overflow-tooltip />
              <el-table-column label="状态" width="70">
                <template #default="{ row }">
                  <span :style="{ color: taskStatusColor(row.status, row.isOverdue), fontSize:'12px' }">
                    {{ taskStatusText(row.status, row.isOverdue) }}
                  </span>
                </template>
              </el-table-column>
              <el-table-column label="计划开始" width="86">
                <template #default="{ row }">{{ formatDate(row.planStartDate) }}</template>
              </el-table-column>
              <el-table-column label="计划完成" width="86">
                <template #default="{ row }">{{ formatDate(row.planFinishDate) }}</template>
              </el-table-column>
              <el-table-column label="实际开始" width="86">
                <template #default="{ row }">{{ formatDate(row.actualStartDate) }}</template>
              </el-table-column>
              <el-table-column label="实际完成" width="86">
                <template #default="{ row }">{{ formatDate(row.actualFinishDate) }}</template>
              </el-table-column>
              <el-table-column label="计划工期" width="75">
                <template #default="{ row }">{{ formatDuration(row.planDuration) }}</template>
              </el-table-column>
              <el-table-column label="实际工期" width="75">
                <template #default="{ row }">{{ formatDuration(row.actualDuration) }}</template>
              </el-table-column>
            </el-table>
            <el-empty v-if="completedTasks.length === 0" description="暂无已完成任务" :image-size="60" />
          </el-card>
        </el-col>
      </el-row>
    </div>
  </div>
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
              placeholder="请选择部门"
              clearable filterable
              style="width:100%"
              @change="(val: number) => { (editingTask as any).deptName = departments.find(d => d.id === val)?.name ?? ''; (editingTask as any).assigneeName = ''; (editingTask as any).assigneeId = undefined }"
            />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="责任人">
            <el-select v-model="editingTask.assigneeName" filterable clearable placeholder="请选择人员" style="width:100%" @change="(val: string) => { if (!val) { editingTask.assigneeId = undefined; (editingTask as any).deptId = undefined; (editingTask as any).deptName = ''; return }; const u = users.find(u => u.realName === val); if(u) { editingTask.assigneeId = u.id; (editingTask as any).deptId = u.department?.id ?? u.departmentId; (editingTask as any).deptName = u.department?.name ?? '' } }">
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
              <el-button type="primary" size="small" @click="addPredRow" :disabled="projectTasks.filter(t => t.id).length === 0">+ 添加前置任务</el-button>
              <div v-if="predRows.length === 0" class="predecessor-empty">尚未设置前置任务</div>
              <div v-for="row in predRows" :key="row.rowKey" class="predecessor-row">
                <el-select v-model="row.taskId" placeholder="选择前置任务" filterable style="width:320px">
                  <el-option v-for="t in getAvailableForPredRow(row.rowKey)" :key="t.id" :label="t._label" :value="t.id">
                    <span :style="{ paddingLeft: (t._depth ?? 0) * 20 + 'px' }">{{ t._label }}</span>
                  </el-option>
                </el-select>
                <el-select v-model="row.dependencyType" style="width:150px;margin-left:8px">
                  <el-option label="完成-开始 (FS)" value="FS" />
                  <el-option label="开始-开始 (SS)" value="SS" />
                  <el-option label="完成-完成 (FF)" value="FF" />
                  <el-option label="开始-完成 (SF)" value="SF" />
                </el-select>
                <el-input-number v-model="row.lagDays" :min="-999" :max="999" controls-position="right" style="width:120px;margin-left:8px" />
                <span class="unit-suffix">天</span>
                <el-button type="danger" link size="small" style="margin-left:8px" @click="removePredRow(row.rowKey)">删除</el-button>
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
            <el-tree-select :model-value="(viewingTask as any).deptId" :data="deptTreeData" :props="{ label: 'name', children: 'children', value: 'id' }" node-key="id" placeholder="请选择部门" clearable filterable style="width:100%" />
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
</template>

<style scoped>
.workbench-container {
  padding: 16px 24px;
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.welcome-card {
  background: linear-gradient(135deg, #304156 0%, #445f7a 100%);
  border-radius: 8px;
  padding: 15px 32px;
  color: #fff;
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.welcome-title {
  font-size: 22px;
  margin: 0 0 4px 0;
}

.welcome-summary {
  font-weight: 400;
}

.welcome-link {
  font-weight: 400;
  color: #67c23a;
  text-decoration: none;
}

.welcome-link:hover {
  color: #67c23a;
}

.welcome-time {
  font-size: 13px;
  opacity: 0.8;
  margin: 0;
  white-space: nowrap;
}

.grid-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: 0;
}

.grid-row {
  flex: 1 !important;
  min-height: 0 !important;
}

.grid-row .el-col {
  height: 100%;
}

.grid-card {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.grid-card :deep(.el-card__body) {
  flex: 1;
  overflow: auto;
  padding: 5px;
  display: flex;
  flex-direction: column;
}
.grid-card :deep(.el-table) {
  flex: 1;
}
.grid-card :deep(.el-table__body-wrapper) {
  overflow: auto;
}
.grid-card :deep(.el-table__body-wrapper::-webkit-scrollbar) {
  width: 6px;
  height: 6px;
}
.grid-card :deep(.el-table__body-wrapper::-webkit-scrollbar-thumb) {
  background: #c0c4cc;
  border-radius: 3px;
}
.grid-card :deep(.el-table__body td .cell),
.grid-card :deep(.el-table__header th .cell) {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.card-header-title {
  font-size: 15px;
  font-weight: 600;
}

.card-header-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.progress-legend {
  font-size: 11px;
  display: flex;
  align-items: center;
  gap: 2px 8px;
  flex-wrap: wrap;
  color: #fff;
  opacity: 0.85;
}

.legend-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  margin-right: 2px;
}

.card-projects :deep(.el-card__header) {
  background: #596778;
  color: #fff;
}
.card-overdue :deep(.el-card__header) {
  background: #5d6f82;
  color: #fff;
}
.card-pending :deep(.el-card__header) {
  background: #61778b;
  color: #fff;
}
.card-completed :deep(.el-card__header) {
  background: #657f94;
  color: #fff;
}

.grid-card :deep(.el-card__header .el-tag) {
  font-size: 14px;
}

.grid-card :deep(.el-table__header-wrapper th) {
  background-color: #98a0ab;
  color: #fff;
}

.clickable-row {
  cursor: pointer;
}
/* 任务对话框样式 */
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
.unit-suffix {
  margin-left: 8px;
  color: #909399;
  font-size: 14px;
}
</style>
