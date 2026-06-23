<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { overdueStatus } from '@/utils/taskConstants'
import { parsePreTaskCodes } from '@/utils/preTaskHelpers'
import type { ProjectTaskItem } from '@/types/project'

const props = withDefaults(defineProps<{
  tasks: ProjectTaskItem[]
  projectId: number | null
  isReadonly?: boolean
  cascadeLoading?: boolean
}>(), {
  isReadonly: false,
  cascadeLoading: false,
})

const emit = defineEmits<{
  navigate: [taskId: number]
}>()

/* ───────── 树形结构（与父组件 taskTree 逻辑一致） ───────── */
function buildTaskTree(flat: ProjectTaskItem[]): ProjectTaskItem[] {
  const map = new Map<number, ProjectTaskItem>()
  const roots: ProjectTaskItem[] = []
  const newChildren = new Map<number, ProjectTaskItem[]>()
  for (const t of flat) {
    if (t.id == null) continue
    map.set(t.id, t)
    newChildren.set(t.id, [])
  }
  for (const t of flat) {
    if (t.id == null) continue
    if (t.parentId && map.has(t.parentId)) {
      newChildren.get(t.parentId)!.push(t)
    } else {
      roots.push(t)
    }
  }
  const sortFn = (a: ProjectTaskItem, b: ProjectTaskItem) =>
    (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true })
  roots.sort(sortFn)
  for (const [, children] of newChildren) {
    if (children.length > 1) children.sort(sortFn)
  }
  for (const t of flat) {
    if (t.id == null) continue
    const nc = newChildren.get(t.id) || []
    const old = t.children || []
    const changed = nc.length !== old.length || nc.some((c, i) => c !== old[i])
    if (changed) t.children = nc
  }
  return roots
}

const taskTree = computed(() => buildTaskTree(props.tasks))

/* ───────── 甘特图 ───────── */
const ganttViewMode = ref<'day' | 'week'>('day')
const ganttUnitWidth = computed(() => ganttViewMode.value === 'week' ? 56 : 28)
const ganttPadUnits = computed(() => ganttViewMode.value === 'week' ? 1 : 3)
const ganttLeftPanelWidth = ref(400)
const ganttSeqWidth = ref(84)
const ganttLoading = ref(false)
const ganttRightRef = ref<HTMLElement | null>(null)
const ganttLeftBodyRef = ref<HTMLElement | null>(null)

/* 拖拽调整列宽 */
const ganttResizing = ref<'seq' | 'panel' | null>(null)
function startGanttResize(e: MouseEvent, target: 'seq' | 'panel') {
  e.preventDefault()
  ganttResizing.value = target
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
  document.addEventListener('mousemove', onGanttResizeMove)
  document.addEventListener('mouseup', stopGanttResize)
}
function onGanttResizeMove(e: MouseEvent) {
  if (ganttResizing.value === 'seq') {
    ganttSeqWidth.value = Math.max(48, Math.min(160, ganttSeqWidth.value + e.movementX))
  } else if (ganttResizing.value === 'panel') {
    ganttLeftPanelWidth.value = Math.max(240, Math.min(600, ganttLeftPanelWidth.value + e.movementX))
  }
}
function stopGanttResize() {
  ganttResizing.value = null
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
  document.removeEventListener('mousemove', onGanttResizeMove)
  document.removeEventListener('mouseup', stopGanttResize)
}

function parseDate(s?: string): Date | null {
  if (!s) return null
  const d = new Date(s)
  return isNaN(d.getTime()) ? null : d
}
function startOfDay(d: Date): Date { return new Date(d.getFullYear(), d.getMonth(), d.getDate()) }
function dayDiff(a: Date, b: Date): number { return Math.round((startOfDay(a).getTime() - startOfDay(b).getTime()) / 86400000) }
function addDays(date: Date, n: number): Date { const d = new Date(date); d.setDate(d.getDate() + n); return d }
function formatMonth(date: Date): string { return `${date.getFullYear()}年${String(date.getMonth() + 1).padStart(2, '0')}月` }
function isWeekend(date: Date): boolean { const day = date.getDay(); return day === 0 || day === 6 }
function getMonday(d: Date): Date {
  const day = d.getDay()
  const m = new Date(d)
  m.setDate(d.getDate() - (day === 0 ? 6 : day - 1))
  return startOfDay(m)
}
function getWeekNumber(d: Date): number {
  const target = new Date(d)
  target.setHours(0, 0, 0, 0)
  target.setDate(target.getDate() + 3 - (target.getDay() + 6) % 7)
  const jan1 = new Date(target.getFullYear(), 0, 4)
  return 1 + Math.round(((target.getTime() - jan1.getTime()) / 86400000 - 3 + (jan1.getDay() + 6) % 7) / 7)
}
function formatWeekLabel(d: Date): string {
  const mon = getMonday(d)
  const sun = addDays(mon, 6)
  return `${mon.getMonth() + 1}/${mon.getDate()}-${sun.getMonth() + 1}/${sun.getDate()}`
}
function getTimelineStart(e: Date): Date {
  if (ganttViewMode.value === 'week') return addDays(getMonday(e), -7 * ganttPadUnits.value)
  return addDays(e, -ganttPadUnits.value)
}

const ganttEarliestDate = computed<Date | null>(() => {
  let earliest: Date | null = null
  for (const t of props.tasks) { const d = parseDate(t.planStartDate); if (d && (!earliest || d < earliest)) earliest = d }
  return earliest
})
const ganttLatestDate = computed<Date | null>(() => {
  let latest: Date | null = null
  for (const t of props.tasks) {
    const dp = parseDate(t.planFinishDate)
    if (dp && (!latest || dp > latest)) latest = dp
    const da = parseDate(t.actualFinishDate)
    if (da && (!latest || da > latest)) latest = da
    if (t.actualStartDate && !t.actualFinishDate && t.planDuration) {
      const aStart = parseDate(t.actualStartDate)
      if (aStart) {
        const est = addDays(aStart, t.planDuration)
        if (!latest || est > latest) latest = est
      }
    }
  }
  return latest
})
const ganttTotalUnits = computed(() => {
  const e = ganttEarliestDate.value; const l = ganttLatestDate.value
  if (!e || !l) return ganttViewMode.value === 'week' ? 6 : 30
  if (ganttViewMode.value === 'week') {
    const start = getTimelineStart(e)
    return Math.ceil(dayDiff(addDays(getMonday(l), 7 * (ganttPadUnits.value + 1)), start) / 7)
  }
  return dayDiff(l, e) + ganttPadUnits.value * 2 + 1
})
const ganttTimelineWidth = computed(() => Math.max(ganttTotalUnits.value * ganttUnitWidth.value, 800))
const ganttTodayOffset = computed(() => {
  const e = ganttEarliestDate.value; if (!e) return -1
  const start = getTimelineStart(e)
  if (ganttViewMode.value === 'week') {
    const offset = Math.floor(dayDiff(startOfDay(new Date()), start) / 7)
    return offset >= 0 ? offset * ganttUnitWidth.value : -1
  }
  const offset = dayDiff(startOfDay(new Date()), start)
  return offset >= 0 ? offset * ganttUnitWidth.value : -1
})

const ganttMonthHeaders = computed(() => {
  const e = ganttEarliestDate.value; if (!e) return []
  const headers: { label: string; pixels: number }[] = []
  const start = getTimelineStart(e)
  const end = addDays(start, ganttTotalUnits.value * (ganttViewMode.value === 'week' ? 7 : 1))
  let cursor = new Date(start.getFullYear(), start.getMonth(), 1)
  while (cursor < end) {
    const nextMonth = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1)
    const segStart = cursor > start ? cursor : start
    const segEnd = nextMonth < end ? nextMonth : end
    const unitCount = ganttViewMode.value === 'week'
      ? Math.ceil(dayDiff(segEnd, segStart) / 7)
      : Math.round(dayDiff(segEnd, segStart))
    if (unitCount > 0) headers.push({ label: formatMonth(cursor), pixels: unitCount * ganttUnitWidth.value })
    cursor = nextMonth
  }
  return headers
})

interface GanttUnitHeader { label: string; isWeekend: boolean; isToday: boolean }
const ganttUnitHeaders = computed<GanttUnitHeader[]>(() => {
  const e = ganttEarliestDate.value; if (!e) return []
  const headers: GanttUnitHeader[] = []
  const start = getTimelineStart(e)
  const today = startOfDay(new Date())
  if (ganttViewMode.value === 'week') {
    for (let i = 0; i < ganttTotalUnits.value; i++) {
      const mon = addDays(start, i * 7)
      const sun = addDays(mon, 6)
      const inWeek = today.getTime() >= mon.getTime() && today.getTime() <= sun.getTime()
      headers.push({ label: formatWeekLabel(mon), isWeekend: false, isToday: inWeek })
    }
  } else {
    for (let i = 0; i < ganttTotalUnits.value; i++) {
      const d = addDays(start, i)
      headers.push({ label: String(d.getDate()), isWeekend: isWeekend(d), isToday: d.getTime() === today.getTime() })
    }
  }
  return headers
})

const ganttWeekNumHeaders = computed<string[]>(() => {
  const e = ganttEarliestDate.value; if (!e || ganttViewMode.value !== 'week') return []
  const start = getTimelineStart(e)
  const nums: string[] = []
  for (let i = 0; i < ganttTotalUnits.value; i++) {
    nums.push(getWeekNumber(addDays(start, i * 7)) + '周')
  }
  return nums
})

interface GanttFlatItem {
  task: ProjectTaskItem; level: number; index: number
  barLeft: number; barWidth: number
  actualBarLeft: number; actualBarWidth: number
  displayStatus: number
  actualDisplayStatus: number
}
const taskIdMap = computed(() => new Map(props.tasks.filter(t => t.id).map(t => [t.id!, t] as [number, ProjectTaskItem])))

const ganttFlattenedTasks = computed<GanttFlatItem[]>(() => {
  const result: GanttFlatItem[] = []
  const earliest = ganttEarliestDate.value
  const timelineStart = earliest ? getTimelineStart(earliest) : null
  const uw = ganttUnitWidth.value
  const isWeek = ganttViewMode.value === 'week'
  let idx = 0
  function calcLeft(d: Date): number {
    return isWeek
      ? Math.floor(dayDiff(startOfDay(d), timelineStart!) / 7) * uw
      : dayDiff(startOfDay(d), timelineStart!) * uw
  }
  function calcWidth(s: Date, f: Date): number {
    return isWeek
      ? Math.max(uw, Math.ceil(dayDiff(startOfDay(f), startOfDay(s)) / 7) * uw)
      : Math.max(1, dayDiff(startOfDay(f), startOfDay(s))) * uw
  }
  function walk(nodes: ProjectTaskItem[], level: number) {
    for (const task of nodes) {
      let barLeft = 0; let barWidth = 0
      if (task.planStartDate && timelineStart) {
        const start = parseDate(task.planStartDate)
        const finish = parseDate(task.planFinishDate)
        if (start) barLeft = calcLeft(start)
        if (start && finish && task.nodeType === 1) barWidth = calcWidth(start, finish)
      }
      let actualBarLeft = barLeft
      let actualBarWidth = barWidth
      const isLeaf = !(task.children && task.children.length > 0)
      if (isLeaf && task.nodeType === 1 && timelineStart) {
        const aStart = parseDate(task.actualStartDate)
        const aFinish = parseDate(task.actualFinishDate)
        if (aStart) {
          actualBarLeft = calcLeft(aStart)
          actualBarWidth = aFinish ? calcWidth(aStart, aFinish) : barWidth
        }
      }
      let ds = task.status
      if (overdueStatus(task) === '已逾期') ds = 3
      let ads = ds
      if (overdueStatus(task) === '已逾期') {
        ads = 3
      } else if (task.actualFinishDate) {
        ads = 2
      } else if (task.actualStartDate) {
        ads = 1
      }
      result.push({ task, level, index: idx++, barLeft, barWidth, actualBarLeft, actualBarWidth, displayStatus: ds, actualDisplayStatus: ads })
      if (task.children && task.children.length > 0) walk(task.children, level + 1)
    }
  }
  walk(taskTree.value, 0)
  const itemById = new Map<number, GanttFlatItem>()
  for (const item of result) { if (item.task.id) itemById.set(item.task.id, item) }
  for (let i = result.length - 1; i >= 0; i--) {
    const item = result[i]
    if (!item.task.parentId) continue
    const parent = itemById.get(item.task.parentId)
    if (!parent) continue
    const childRight = item.actualBarLeft + item.actualBarWidth
    const parentRight = parent.actualBarLeft + parent.actualBarWidth
    if (childRight > parentRight) {
      parent.actualBarWidth = childRight - parent.actualBarLeft
    }
  }
  return result
})

/* ───────── 甘特图前置任务连线（MS Project FS 走线风格）───────── */
const GANTT_ROW_H = 54
const GANTT_PLAN_BAR_TOP = 6
const GANTT_PLAN_BAR_H  = 17
const GANTT_LINE_Y_OFFSET = GANTT_PLAN_BAR_TOP + Math.round(GANTT_PLAN_BAR_H / 2)

function buildDepPath(x1: number, y1: number, x2: number, y2: number): string {
  const STUB = 10
  const r = 5
  const WRAP_EXTRA = GANTT_ROW_H * 0.55
  const sameRow = Math.abs(y2 - y1) < 1
  const goingDown = sameRow ? true : y2 > y1
  const ys = goingDown ? 1 : -1
  const sw = goingDown ? 1 : 0
  const effectiveY2 = sameRow ? y1 : y2
  if (sameRow && x1 + STUB + r <= x2) {
    return `M ${x1} ${y1} L ${x2} ${y2}`
  }
  if (!sameRow && x2 > x1 + r) {
    const sw1 = goingDown ? 1 : 0
    const sw2 = goingDown ? 0 : 1
    const vx = Math.min(x1 + STUB, x2 - r)
    const initX = Math.max(x1, vx - r)
    const segs: string[] = [`M ${x1} ${y1}`]
    if (initX > x1) segs.push(`L ${initX} ${y1}`)
    segs.push(
      `A ${r} ${r} 0 0 ${sw1} ${vx} ${y1 + ys * r}`,
      `L ${vx} ${effectiveY2 - ys * r}`,
      `A ${r} ${r} 0 0 ${sw2} ${vx + r} ${effectiveY2}`,
      `L ${x2} ${effectiveY2}`,
    )
    return segs.join(' ')
  }
  if (!sameRow) {
    const S_STUB = 7
    const midY   = (y1 + effectiveY2) / 2
    const rightX = x1 + S_STUB
    const leftX  = x2 - S_STUB
    const sw1 = goingDown ? 1 : 0
    const sw2 = goingDown ? 1 : 0
    const sw3 = goingDown ? 0 : 1
    const sw4 = goingDown ? 0 : 1
    return [
      `M ${x1} ${y1}`,
      `L ${rightX - r} ${y1}`,
      `A ${r} ${r} 0 0 ${sw1} ${rightX} ${y1 + ys * r}`,
      `L ${rightX} ${midY - ys * r}`,
      `A ${r} ${r} 0 0 ${sw2} ${rightX - r} ${midY}`,
      `L ${leftX + r} ${midY}`,
      `A ${r} ${r} 0 0 ${sw3} ${leftX} ${midY + ys * r}`,
      `L ${leftX} ${effectiveY2 - ys * r}`,
      `A ${r} ${r} 0 0 ${sw4} ${leftX + r} ${effectiveY2}`,
      `L ${x2} ${effectiveY2}`,
    ].join(' ')
  }
  const x1s = x1 + STUB
  const x2s = x2 - STUB
  const wrapY = effectiveY2 + ys * WRAP_EXTRA
  const minHorizGap = r * 2 + STUB
  const leftX = (x1s - x2s) < (minHorizGap + r * 2)
    ? x1s - minHorizGap - r * 2
    : x2s
  return [
    `M ${x1} ${y1}`,
    `L ${x1s - r} ${y1}`,
    `A ${r} ${r} 0 0 ${sw} ${x1s} ${y1 + ys * r}`,
    `L ${x1s} ${wrapY - ys * r}`,
    `A ${r} ${r} 0 0 ${sw} ${x1s - r} ${wrapY}`,
    `L ${leftX + r} ${wrapY}`,
    `A ${r} ${r} 0 0 ${sw} ${leftX} ${wrapY - ys * r}`,
    `L ${leftX} ${effectiveY2 + ys * r}`,
    `A ${r} ${r} 0 0 ${sw} ${leftX + r} ${effectiveY2}`,
    `L ${x2} ${effectiveY2}`,
  ].join(' ')
}

const ganttDependencyLines = computed<{ path: string }[]>(() => {
  const lines: { path: string }[] = []
  const flat = ganttFlattenedTasks.value
  if (flat.length === 0) return lines
  const map = new Map<number, GanttFlatItem>()
  for (const item of flat) {
    if (item.task.id) map.set(item.task.id, item)
  }
  for (const item of flat) {
    const preCodes = item.task.preTaskCodes
    if (!preCodes) continue
    const codes = preCodes.split(',').map(c => c.trim()).filter(Boolean)
    for (const code of codes) {
      const idMatch = code.match(/^(\d+)/)
      if (!idMatch) continue
      const pred = map.get(parseInt(idMatch[1], 10))
      if (!pred || !item.task.planStartDate || !pred.task.planStartDate) continue
      const x1 = pred.barLeft + pred.barWidth
      const x2 = item.barLeft
      const y1 = pred.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      const y2 = item.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      lines.push({ path: buildDepPath(x1, y1, x2, y2) })
    }
  }
  return lines
})

/* ─── 关键路径计算 ─── */
const showCriticalPath = ref(false)
function toggleCriticalPath() {
  showCriticalPath.value = !showCriticalPath.value
}

const criticalPathData = computed<{ taskNos: Set<number> }>(() => {
  const empty = { taskNos: new Set<number>() }
  if (!showCriticalPath.value) return empty
  const flat = ganttFlattenedTasks.value
  if (flat.length === 0) return empty
  const earliest = ganttEarliestDate.value
  if (!earliest) return empty
  const toDay = (s?: string): number => {
    if (!s) return 0
    const d = parseDate(s)
    return d ? dayDiff(startOfDay(d), startOfDay(earliest)) : 0
  }
  const taskMap = new Map<number, GanttFlatItem>()
  for (const item of flat) {
    if (item.task.id && !(item.task.children && item.task.children.length > 0) && item.task.status !== 2)
      taskMap.set(item.task.id, item)
  }
  const succMap = new Map<number, { id: number; lagDays: number }[]>()
  const inDeg   = new Map<number, number>()
  for (const [id] of taskMap) { succMap.set(id, []); inDeg.set(id, 0) }
  for (const [, item] of taskMap) {
    if (!item.task.preTaskCodes || !item.task.id) continue
    for (const p of parsePreTaskCodes(item.task.preTaskCodes)) {
      if (!taskMap.has(p.taskId)) continue
      succMap.get(p.taskId)?.push({ id: item.task.id ?? 0, lagDays: p.lagDays ?? 0 })
      inDeg.set(item.task.id ?? 0, (inDeg.get(item.task.id ?? 0) ?? 0) + 1)
    }
  }
  const topo: number[] = []
  const q = [...inDeg.entries()].filter(([, d]) => d === 0).map(([n]) => n)
  const deg2 = new Map(inDeg)
  while (q.length) {
    const cur = q.shift()!; topo.push(cur)
    for (const { id: no } of succMap.get(cur) ?? []) {
      deg2.set(no, (deg2.get(no) ?? 1) - 1)
      if (deg2.get(no) === 0) q.push(no)
    }
  }
  const es = new Map<number, number>()
  const ef = new Map<number, number>()
  for (const no of topo) {
    const item = taskMap.get(no); if (!item) continue
    const dur = Math.max(1, toDay(item.task.planFinishDate) - toDay(item.task.planStartDate) + 1)
    const knownPreds = item.task.preTaskCodes
      ? parsePreTaskCodes(item.task.preTaskCodes).filter(p => taskMap.has(p.taskId))
      : []
    let esVal = 0
    for (const p of knownPreds) {
      const constraint = (ef.get(p.taskId) ?? 0) + (p.lagDays ?? 0)
      if (constraint > esVal) esVal = constraint
    }
    es.set(no, esVal); ef.set(no, esVal + dur)
  }
  const projectEnd = Math.max(0, ...[...ef.values()])
  const lf = new Map<number, number>()
  const ls = new Map<number, number>()
  for (const no of topo) lf.set(no, projectEnd)
  for (const no of [...topo].reverse()) {
    const item = taskMap.get(no); if (!item) continue
    const dur = (ef.get(no) ?? 0) - (es.get(no) ?? 0)
    const lfVal = lf.get(no) ?? projectEnd
    ls.set(no, lfVal - dur)
    if (item.task.preTaskCodes) {
      for (const p of parsePreTaskCodes(item.task.preTaskCodes)) {
        if (!taskMap.has(p.taskId)) continue
        const constraint = (ls.get(no) ?? 0) - (p.lagDays ?? 0)
        const oldLF = lf.get(p.taskId)
        if (oldLF === undefined || constraint < oldLF) lf.set(p.taskId, constraint)
      }
    }
  }
  const taskNos = new Set<number>()
  for (const [no, esVal] of es) {
    const lsVal = ls.get(no) ?? 0
    if (lsVal - esVal <= 0) taskNos.add(no)
  }
  return { taskNos }
})

const criticalPathLines = computed<{ path: string }[]>(() => {
  if (!showCriticalPath.value) return []
  const { taskNos } = criticalPathData.value
  if (taskNos.size === 0) return []
  const flat = ganttFlattenedTasks.value
  const map  = new Map<number, GanttFlatItem>()
  for (const item of flat) { if (item.task.id) map.set(item.task.id, item) }
  const lines: { path: string }[] = []
  for (const item of flat) {
    if (!item.task.id || !item.task.preTaskCodes) continue
    if (!taskNos.has(item.task.id)) continue
    for (const code of item.task.preTaskCodes.split(',').map(c => c.trim()).filter(Boolean)) {
      const idMatch = code.match(/^(\d+)/)
      if (!idMatch) continue
      const predId = parseInt(idMatch[1], 10)
      if (!taskNos.has(predId)) continue
      const pred = map.get(predId)
      if (!pred || !item.task.planStartDate || !pred.task.planStartDate) continue
      const x1 = pred.barLeft + pred.barWidth
      const x2 = item.barLeft
      const y1 = pred.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      const y2 = item.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      lines.push({ path: buildDepPath(x1, y1, x2, y2) })
    }
  }
  return lines
})

/* 序号列宽自动适配 */
function calcSeqAutoWidth() {
  let maxLen = 0
  for (const item of ganttFlattenedTasks.value) {
    const s = item.task.taskNo || ''
    if (s.length > maxLen) maxLen = s.length
  }
  return Math.max(56, maxLen * 9 + 30)
}
watch(ganttFlattenedTasks, () => {
  ganttSeqWidth.value = calcSeqAutoWidth()
}, { immediate: true })

async function loadGanttData() {
  if (!props.projectId) return
  ganttLoading.value = true
  try {
    await nextTick()
    scrollGanttToToday()
  } finally { ganttLoading.value = false }
}

function scrollGanttToToday() {
  if (!ganttRightRef.value || ganttTodayOffset.value < 0) return
  const container = ganttRightRef.value
  container.scrollLeft = Math.max(0, ganttTodayOffset.value - container.clientWidth / 2)
}

function syncGanttScroll() {
  if (!ganttRightRef.value || !ganttLeftBodyRef.value) return
  ganttLeftBodyRef.value.scrollTop = ganttRightRef.value.scrollTop
}

function syncGanttScrollFromLeft() {
  if (!ganttRightRef.value || !ganttLeftBodyRef.value) return
  ganttRightRef.value.scrollTop = ganttLeftBodyRef.value.scrollTop
}

function handleGanttBarDblClick(task: ProjectTaskItem) {
  if (task.id) emit('navigate', task.id)
}

/* 自动加载：当 tasks 就绪并且有 projectId 时调用 loadGanttData */
let initialLoadDone = false
watch(() => props.projectId, (id) => {
  if (id && props.tasks.length > 0 && !initialLoadDone) {
    initialLoadDone = true
    loadGanttData()
  }
}, { immediate: true })

defineExpose({ loadGanttData, ganttViewMode })
</script>

<template>
  <div class="gantt-wrapper" v-loading="ganttLoading || props.cascadeLoading" :element-loading-text="props.cascadeLoading ? '正在级联更新后续任务日期...' : '加载中...'">
    <template v-if="props.tasks.length === 0 && !ganttLoading">
      <div style="text-align:center;padding:48px;color:#909399;">暂无任务数据</div>
    </template>
    <template v-else>
      <div class="gantt-toolbar">
        <el-radio-group v-model="ganttViewMode" size="small" @change="scrollGanttToToday">
          <el-radio-button value="day">日</el-radio-button>
          <el-radio-button value="week">周</el-radio-button>
        </el-radio-group>
        <span style="font-size:12px;color:#909399;margin-left:30px">每格 {{ ganttUnitWidth }}px · 双击条图查看详情</span>
        <el-button
          :type="showCriticalPath ? 'primary' : 'default'"
          size="small"
          style="margin-left:12px"
          @click="toggleCriticalPath"
        >{{ showCriticalPath ? '隐藏关键路径' : '显示关键路径' }}</el-button>
        <div class="gantt-legend" style="margin-left:auto">
          <span class="gantt-legend-title">图例</span>
          <span class="gantt-legend-item"><span class="gantt-legend-swatch swatch-0"></span>未开始</span>
          <span class="gantt-legend-item"><span class="gantt-legend-swatch swatch-1"></span>进行中</span>
          <span class="gantt-legend-item"><span class="gantt-legend-swatch swatch-2"></span>已完成</span>
          <span class="gantt-legend-item"><span class="gantt-legend-swatch swatch-3"></span>已延误</span>
          <span class="gantt-legend-divider"></span>
          <span class="gantt-legend-item"><span class="gantt-legend-diamond"></span>里程碑</span>
          <span class="gantt-legend-item"><span class="gantt-legend-today"></span>今日</span>
          <span class="gantt-legend-divider"></span>
          <span class="gantt-legend-item"><span style="color:#409eff">▼</span> 父节点</span>
          <span class="gantt-legend-item"><span style="color:#909399">·</span> 叶子任务</span>
          <span class="gantt-legend-divider"></span>
          <span class="gantt-legend-item"><svg width="16" height="10" style="vertical-align:middle"><line x1="0" y1="5" x2="12" y2="5" stroke="#a855f7" stroke-width="1" /><polygon points="12,0 16,5 12,10" fill="#a855f7" /></svg> 前置连线</span>
          <span class="gantt-legend-item" v-if="showCriticalPath"><svg width="16" height="10" style="vertical-align:middle"><line x1="0" y1="5" x2="12" y2="5" stroke="#000000" stroke-width="1" stroke-dasharray="3,4" /><polygon points="12,0 16,5 12,10" fill="#000000" /></svg> 关键路径</span>
        </div>
      </div>
      <div class="gantt-container">
        <!-- 左侧任务名称 -->
        <div class="gantt-left" :style="{ width: ganttLeftPanelWidth + 'px' }">
          <div class="gantt-left-header" :style="{ height: (ganttViewMode === 'week' ? 81 : 54) + 'px', lineHeight: (ganttViewMode === 'week' ? 81 : 54) + 'px' }">
            <span class="gantt-header-seq" :style="{ width: ganttSeqWidth + 'px' }">序号</span>
            <span class="gantt-resize-handle" @mousedown="startGanttResize($event, 'seq')"></span>
            <span class="gantt-header-name">任务名称</span>
          </div>
          <div class="gantt-left-body" ref="ganttLeftBodyRef" @scroll="syncGanttScrollFromLeft" :style="{ height: 'calc(100% - ' + (ganttViewMode === 'week' ? 81 : 54) + 'px)' }">
            <div v-for="item in ganttFlattenedTasks" :key="item.task.id ?? item.task.taskNo" class="gantt-row" :class="{ 'gantt-row-alt': item.index % 2 === 1 }">
              <span class="gantt-task-no" :style="{ width: ganttSeqWidth + 'px' }" :title="item.task.taskNo">{{ item.task.taskNo }}</span>
              <span class="gantt-task-name" :style="{ paddingLeft: (item.level * 20 + 8) + 'px' }" :title="item.task.taskName">
                <span class="gantt-task-icon" v-if="item.task.nodeType === 2" style="color:#e74c3c">◆</span>
                <span class="gantt-task-icon" v-else-if="item.task.children && item.task.children.length" style="color:#409eff">▼</span>
                <span class="gantt-task-icon" v-else style="color:#909399">·</span>
                {{ item.task.taskName }}
              </span>
            </div>
          </div>
          <span class="gantt-panel-resize" @mousedown="startGanttResize($event, 'panel')"></span>
        </div>
        <!-- 右侧时间轴 -->
        <div class="gantt-right" ref="ganttRightRef" @scroll="syncGanttScroll">
          <div class="gantt-timeline" :style="{ width: ganttTimelineWidth + 'px' }">
            <div class="gantt-header">
              <div class="gantt-header-row">
                <div v-for="(m, mi) in ganttMonthHeaders" :key="'m'+mi" class="gantt-month-cell" :style="{ width: m.pixels + 'px' }">{{ m.label }}</div>
              </div>
              <div v-if="ganttViewMode === 'week'" class="gantt-header-row gantt-weeknum-row">
                <div v-for="(wn, wni) in ganttWeekNumHeaders" :key="'wn'+wni" class="gantt-weeknum-cell" :style="{ width: ganttUnitWidth + 'px' }">{{ wn }}</div>
              </div>
              <div class="gantt-header-row">
                <div v-for="(u, ui) in ganttUnitHeaders" :key="'u'+ui" class="gantt-day-cell" :class="{ 'gantt-day-weekend': u.isWeekend, 'gantt-day-today': u.isToday }" :style="{ width: ganttUnitWidth + 'px' }">{{ u.label }}</div>
              </div>
            </div>
            <div class="gantt-body">
              <svg v-if="ganttDependencyLines.length || criticalPathLines.length" class="gantt-dep-svg" :style="{ width: ganttTimelineWidth + 'px', height: (ganttFlattenedTasks.length * GANTT_ROW_H) + 'px', position: 'absolute', top: 0, left: 0, pointerEvents: 'none', zIndex: 1 }">
                <defs>
                  <marker id="ganttDepArrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="4" markerHeight="4" orient="auto-start-reverse">
                    <path d="M 0 0 L 8 4 L 0 8 Z" fill="#a855f7" />
                  </marker>
                  <marker id="ganttCriticalArrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="4" markerHeight="4" orient="auto-start-reverse">
                    <path d="M 0 0 L 8 4 L 0 8 Z" fill="#000000" />
                  </marker>
                </defs>
                <path v-for="(line, i) in ganttDependencyLines" :key="'dl'+i" :d="line.path" fill="none" stroke="#a855f7" stroke-width="1" stroke-linejoin="round" marker-end="url(#ganttDepArrow)" />
                <path v-for="(line, i) in criticalPathLines" :key="'cp'+i" :d="line.path" fill="none" stroke="#000000" stroke-width="2.5" stroke-linejoin="round" stroke-dasharray="5,8" class="gantt-critical-path" marker-end="url(#ganttCriticalArrow)" />
              </svg>
              <div class="gantt-today-line" v-if="ganttTodayOffset >= 0" :style="{ left: ganttTodayOffset + 'px' }">
                <div class="gantt-today-label">今日</div>
              </div>
              <div v-for="item in ganttFlattenedTasks" :key="item.task.id ?? item.task.taskNo" class="gantt-row" :class="{ 'gantt-row-alt': item.index % 2 === 1 }">
                <div v-if="item.task.nodeType === 2 && item.task.planStartDate" class="gantt-milestone" :style="{ left: item.barLeft + 'px' }" :title="item.task.taskName + ': ' + (item.task.planStartDate?.slice(0,10) ?? '')"></div>
                <div v-else-if="item.task.nodeType === 1 && item.task.planStartDate && item.barWidth > 0"
                     class="gantt-bar gantt-bar-plan" :class="'gantt-bar-status-' + item.displayStatus"
                     :style="{ left: item.barLeft + 'px', width: item.barWidth + 'px' }"
                     :title="'【计划】' + item.task.taskName + ' (' + (item.task.planStartDate?.slice(0,10) ?? '') + ' ~ ' + (item.task.planFinishDate?.slice(0,10) ?? '') + ')'"
                     @dblclick="handleGanttBarDblClick(item.task)">
                </div>
                <div v-if="item.task.nodeType === 1 && item.task.planStartDate && item.actualBarWidth > 0"
                     class="gantt-bar gantt-bar-actual" :class="'gantt-bar-status-' + item.actualDisplayStatus"
                     :style="{ left: item.actualBarLeft + 'px', width: item.actualBarWidth + 'px' }"
                     :title="'【实际】' + item.task.taskName + (item.task.actualStartDate ? ' (' + item.task.actualStartDate.slice(0,10) + (item.task.actualFinishDate ? ' ~ ' + item.task.actualFinishDate.slice(0,10) : ' ~ 进行中') + ')' : ' (同计划)')"
                     @dblclick="handleGanttBarDblClick(item.task)">
                  <div v-if="item.task.progressPct > 0" class="gantt-bar-progress" :style="{ width: item.task.progressPct + '%' }"></div>
                </div>
                <span v-if="item.task.nodeType === 1 && item.task.planStartDate && item.barWidth > 60" class="gantt-bar-label" :style="{ left: item.barLeft + 'px', width: item.barWidth + 'px' }">{{ item.task.taskName }}</span>
                <span v-if="item.task.nodeType === 1 && item.task.planStartDate && item.actualBarWidth > 0 && item.task.progressPct > 0" class="gantt-bar-pct" :class="'gantt-pct-status-' + item.actualDisplayStatus" :style="{ left: (item.actualBarLeft - 8) + 'px', top: '30px', transform: 'translateX(-100%)' }">{{ item.task.progressPct }}%</span>
                <span v-if="item.task.nodeType === 1 && item.task.planStartDate && item.barWidth > 0 && (item.task.deptName || item.task.assigneeName)" class="gantt-bar-info" :style="{ left: (item.barLeft + item.barWidth + 18) + 'px' }"><span v-if="item.task.deptName" class="gantt-info-dept">{{ item.task.deptName }}</span><span v-if="item.task.assigneeName" class="gantt-info-person" style="margin-left:10px">{{ item.task.assigneeName }}</span></span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* ───────── 甘特图 ───────── */
.gantt-wrapper {
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  overflow: hidden;
  background: #fff;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.gantt-toolbar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding: 6px 12px;
  flex-shrink: 0;
  border-bottom: 1px solid #ebeef5;
  background: #fafafa;
}

.gantt-container {
  display: flex;
  flex: 1;
  min-height: 0;
}

.gantt-left {
  flex-shrink: 0;
  border-right: 1px solid #e4e7ed;
  overflow: hidden;
  background: #fff;
  position: relative;
}
.gantt-resize-handle {
  width: 6px;
  flex-shrink: 0;
  cursor: col-resize;
  background: transparent;
  transition: background 0.15s;
  align-self: stretch;
}
.gantt-resize-handle:hover,
.gantt-resize-handle:active {
  background: #409EFF;
}
.gantt-panel-resize {
  position: absolute;
  right: -3px;
  top: 0;
  bottom: 0;
  width: 6px;
  cursor: col-resize;
  z-index: 10;
}
.gantt-panel-resize:hover {
  background: rgba(64, 158, 255, 0.4);
}

.gantt-left-header {
  display: flex;
  height: 54px;
  line-height: 54px;
  font-size: 13px;
  font-weight: 600;
  color: #303133;
  border-bottom: 1px solid #e4e7ed;
  background: #fafafa;
  box-sizing: border-box;
}
.gantt-header-seq {
  flex-shrink: 0;
  padding-left: 16px;
  border-right: 1px solid #e4e7ed;
  box-sizing: border-box;
  overflow: hidden;
}
.gantt-header-name {
  flex: 1;
  padding-left: 8px;
}

.gantt-left-body {
  overflow-y: auto;
  height: calc(100% - 54px);
}

.gantt-left-body::-webkit-scrollbar { width: 8px; }
.gantt-left-body::-webkit-scrollbar-track { background: transparent; }
.gantt-left-body::-webkit-scrollbar-thumb { background: #c0c4cc; border-radius: 4px; }
.gantt-left-body::-webkit-scrollbar-thumb:hover { background: #a8abb2; }

.gantt-right {
  flex: 1;
  overflow: auto;
  position: relative;
}

.gantt-right::-webkit-scrollbar { width: 8px; height: 8px; }
.gantt-right::-webkit-scrollbar-track { background: transparent; }
.gantt-right::-webkit-scrollbar-thumb { background: #c0c4cc; border-radius: 4px; }
.gantt-right::-webkit-scrollbar-thumb:hover { background: #a8abb2; }
.gantt-right::-webkit-scrollbar-corner { background: transparent; }

.gantt-timeline {
  position: relative;
  min-width: 100%;
}

.gantt-header {
  position: sticky;
  top: 0;
  z-index: 10;
  background: #fafafa;
  border-bottom: 1px solid #e4e7ed;
}

.gantt-header-row {
  display: flex;
  height: 27px;
  line-height: 27px;
}

.gantt-month-cell {
  font-size: 12px;
  font-weight: 600;
  color: #303133;
  text-align: center;
  border-right: 1px solid #ebeef5;
  box-sizing: border-box;
  overflow: hidden;
  white-space: nowrap;
}

.gantt-day-cell {
  font-size: 10px;
  color: #606266;
  text-align: center;
  border-right: 1px solid #ebeef5;
  box-sizing: border-box;
  overflow: hidden;
}

.gantt-day-weekend {
  color: #e74c3c;
  background: #fef0f0;
}

.gantt-day-today {
  background: #e6f7ff;
  font-weight: 700;
  color: #1890ff;
}
.gantt-weeknum-row {
  background: #f5f7fa;
}
.gantt-weeknum-cell {
  font-size: 12px;
  font-weight: 600;
  color: #303133;
  text-align: center;
  border-right: 1px solid #ebeef5;
  box-sizing: border-box;
  overflow: hidden;
}

.gantt-body {
  position: relative;
}
.gantt-dep-svg {
  overflow: hidden;
}

.gantt-row {
  display: flex;
  align-items: center;
  height: 54px;
  box-sizing: border-box;
  position: relative;
  border-bottom: 1px solid #f2f2f2;
}

.gantt-row-alt {
  background: #fafafa;
}

.gantt-task-name {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
  line-height: 54px;
  cursor: default;
}

.gantt-task-no {
  flex-shrink: 0;
  box-sizing: border-box;
  font-size: 12px;
  color: #606266;
  text-align: left;
  padding-left: 16px;
  line-height: 54px;
  border-right: 1px solid #ebeef5;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.gantt-task-icon {
  font-size: 12px;
  flex-shrink: 0;
  line-height: 1;
}

.gantt-bar {
  position: absolute;
  height: 17px;
  border-radius: 3px;
  min-width: 4px;
  cursor: pointer;
  transition: opacity 0.15s;
  overflow: hidden;
  box-sizing: border-box;
  display: flex;
  align-items: center;
  z-index: 2;
}

.gantt-bar-plan {
  top: 6px;
  opacity: 1;
}

.gantt-bar-actual {
  top: 30px;
  border-radius: 2px;
}

.gantt-bar:hover {
  opacity: 0.92;
  box-shadow: 0 1px 4px rgba(0,0,0,0.15);
}

.gantt-bar-label {
  position: absolute;
  top: 6px;
  height: 17px;
  line-height: 17px;
  z-index: 20;
  font-size: 10px;
  font-weight: 600;
  color: #303133;
  padding: 0 4px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  pointer-events: none;
  box-sizing: border-box;
}

/* 计划条：靓紫底黑字 */
.gantt-bar-plan { background: #e4b8ff !important; border: 1px solid #a855f7 !important; }

/* 实际条：沿用原状态颜色 */
.gantt-bar-actual.gantt-bar-status-0 { background: #d9d9d9; border: 1px solid #bfbfbf; }
.gantt-bar-actual.gantt-bar-status-1 { background: #b3d8ff; border: 1px solid #409EFF; }
.gantt-bar-actual.gantt-bar-status-2 { background: #b3e19d; border: 1px solid #67C23A; }
.gantt-bar-actual.gantt-bar-status-3 { background: #fbc4c4; border: 1px solid #F56C6C; }

.gantt-bar-progress {
  height: 100%;
  background: rgba(255,255,255,0.5);
  border-radius: 2px 0 0 2px;
  position: absolute;
  z-index: 1;
  left: 0;
  top: 0;
  transition: width 0.3s;
}

.gantt-bar-status-2 .gantt-bar-progress { background: #67C23A; }
.gantt-bar-status-1 .gantt-bar-progress { background: #409EFF; }

.gantt-bar-info {
  position: absolute;
  top: 6px;
  height: 17px;
  line-height: 17px;
  font-size: 10px;
  white-space: nowrap;
  pointer-events: none;
  z-index: 20;
}
.gantt-bar-pct {
  position: absolute;
  top: 6px;
  height: 17px;
  line-height: 17px;
  font-size: 10px;
  font-weight: 700;
  text-align: right;
  white-space: nowrap;
  z-index: 20;
}
.gantt-pct-status-0 { color: #909399; }
.gantt-pct-status-1 { color: #409EFF; }
.gantt-pct-status-2 { color: #67C23A; }
.gantt-pct-status-3 { color: #F56C6C; }
.gantt-info-dept { color: #303133; font-weight: 600; }
.gantt-info-person { color: #303133; }

.gantt-milestone {
  position: absolute;
  top: 20px;
  width: 14px;
  height: 14px;
  background: #e74c3c;
  transform: rotate(45deg);
  border-radius: 2px;
  cursor: pointer;
  z-index: 20;
  box-shadow: 0 1px 3px rgba(231,76,60,0.4);
}

.gantt-milestone:hover {
  transform: rotate(45deg) scale(1.2);
}

.gantt-today-line {
  position: absolute;
  top: 0;
  bottom: 0;
  width: 2px;
  background: #F56C6C;
  z-index: 5;
  pointer-events: none;
}

.gantt-today-label {
  position: absolute;
  top: 0;
  left: 4px;
  font-size: 10px;
  color: #F56C6C;
  white-space: nowrap;
  font-weight: 600;
}

/* 图例 */
.gantt-legend {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}
.gantt-legend-title {
  font-size: 12px;
  font-weight: 600;
  color: #303133;
  margin-right: 4px;
}
.gantt-legend-item {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 11px;
  color: #606266;
}
.gantt-legend-swatch {
  display: inline-block;
  width: 16px;
  height: 10px;
  border-radius: 2px;
  flex-shrink: 0;
}
.swatch-0 { background: #d9d9d9; border: 1px solid #bfbfbf; }
.swatch-1 { background: #b3d8ff; border: 1px solid #409EFF; }
.swatch-2 { background: #b3e19d; border: 1px solid #67C23A; }
.swatch-3 { background: #fbc4c4; border: 1px solid #F56C6C; }
.gantt-legend-diamond {
  display: inline-block;
  width: 10px;
  height: 10px;
  background: #e74c3c;
  transform: rotate(45deg);
  border-radius: 2px;
  flex-shrink: 0;
}
.gantt-legend-today {
  display: inline-block;
  width: 2px;
  height: 14px;
  background: #F56C6C;
  flex-shrink: 0;
}
.gantt-legend-divider {
  width: 1px;
  height: 14px;
  background: #dcdfe6;
  margin: 0 4px;
}

.gantt-critical-path {
  stroke-dashoffset: 13;
  animation: gantt-critical-flow 0.8s linear infinite;
}
@keyframes gantt-critical-flow {
  to { stroke-dashoffset: 0; }
}
</style>
