<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { createProjectTask, updateProjectTask } from '@/api/project'
import { parsePreTaskCodes, serializePreTaskCodes } from '@/utils/preTaskHelpers'
import { taskStatusOptions, taskPriorityOptions } from '@/utils/taskConstants'
import { buildDeptTree } from '@/utils/deptTree'
import type { ProjectTaskItem } from '@/types/project'
import type { Department, UserInfo } from '@/types/template'

const props = defineProps<{
  visible: boolean
  mode: 'create' | 'edit'
  task: ProjectTaskItem | null
  parentTask: ProjectTaskItem | null
  projectId: number | null
  taskNoRule: string
  allTasks: ProjectTaskItem[]
  taskTree: ProjectTaskItem[]
  dictMap: Record<string, { id: number; dictCode: string; dictLabel: string }[]>
  departments: Department[]
  users: UserInfo[]
  formStatus: number
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  saved: [payload: { task: ProjectTaskItem; isNew: boolean; oldParentId: number | null }]
}>()

/* ── 前置任务类型 ── */
interface PredRow {
  rowKey: number
  taskId: number
  dependencyType: string
  lagDays: number
}

let predKeySeq = 0

/* ── 局部状态 ── */
const editingTask = ref<ProjectTaskItem>(newTaskForm())
const editingTaskOldParentId = ref<number | null>(null)
const predRows = ref<PredRow[]>([])
const taskSaving = ref(false)

function newTaskForm(): ProjectTaskItem {
  return {
    parentId: null,
    taskNo: '',
    wbsCode: '',
    taskName: '',
    nodeType: 1,
    taskCategory: '',
    sortOrder: props.allTasks.length + 1,
    status: 0,
    priority: 3,
    deliverableCnt: 0,
    progressPct: 0,
    remark: ''
  }
}

/* ── 工具函数 ── */
function dateAddDays(dateStr: string, days: number): string {
  const [y, m, d] = dateStr.slice(0, 10).split('-').map(Number)
  const dt = new Date(y, m - 1, d)
  dt.setDate(dt.getDate() + days)
  const yy = dt.getFullYear()
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${yy}-${mm}-${dd}T00:00:00`
}

function dateStrGt(a: string, b: string): boolean {
  return a.slice(0, 10) > b.slice(0, 10)
}

/* ── 辅助 computed ── */
const deptTreeData = computed(() => buildDeptTree(props.departments))

function taskHasChildren(taskId?: number | null): boolean {
  if (!taskId) return false
  return props.allTasks.some(t => t.parentId === taskId)
}

/* ── 前置任务相关 ── */
const availablePredTasks = computed(() => {
  const currentId = editingTask.value?.id
  if (!currentId) {
    return props.allTasks.filter(t => t.id).sort((a, b) =>
      (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true })
    )
  }
  const excluded = new Set<number>()
  function collectDescendants(parentId: number) {
    for (const t of props.allTasks) {
      if (t.parentId === parentId && t.id) {
        excluded.add(t.id)
        collectDescendants(t.id)
      }
    }
  }
  excluded.add(currentId)
  collectDescendants(currentId)
  return props.allTasks
    .filter(t => t.id && !excluded.has(t.id))
    .sort((a, b) => (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true }))
})

const flatParentOptions = computed(() => {
  const currentId = editingTask.value?.id
  const excluded = new Set<number>()
  if (currentId) {
    excluded.add(currentId)
    function collectDescendants(pid: number) {
      for (const t of props.allTasks) {
        if (t.parentId === pid && t.id) {
          excluded.add(t.id)
          collectDescendants(t.id)
        }
      }
    }
    collectDescendants(currentId)
  }

  const result: { id: number; displayLabel: string }[] = []
  function walk(nodes: ProjectTaskItem[], depth: number) {
    for (const n of nodes) {
      if (n.id == null || excluded.has(n.id)) continue
      const prefix = depth === 0 ? '' : '│  '.repeat(depth - 1) + '├─ '
      result.push({
        id: n.id,
        displayLabel: `${prefix}${n.taskNo || ''} - ${n.taskName}`
      })
      if (n.children?.length) walk(n.children, depth + 1)
    }
  }
  walk(props.taskTree, 0)
  return result
})

function getAvailableForPredRow(rowKey: number) {
  const selectedIds = predRows.value
    .filter(p => p.rowKey !== rowKey && p.taskId)
    .map(p => p.taskId)
  return availablePredTasks.value.filter(t => t.id && !selectedIds.includes(t.id))
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

function calcPlanStartFromPreds(): string | null {
  if (predRows.value.length === 0) return null
  const allTasks = new Map<number, ProjectTaskItem>()
  for (const t of props.allTasks) { if (t.id) allTasks.set(t.id, t) }

  let latest: string | null = null
  for (const row of predRows.value) {
    const pred = allTasks.get(row.taskId)
    if (!pred) continue
    let base: string | undefined
    if (row.dependencyType === 'FS' || row.dependencyType === 'FF') base = pred.planFinishDate
    else if (row.dependencyType === 'SS' || row.dependencyType === 'SF') base = pred.planStartDate
    if (!base) continue
    const ds = dateAddDays(base, row.lagDays)
    if (!latest || dateStrGt(ds, latest)) latest = ds
  }
  return latest
}

/* ── 对话框初始化 ── */
function initDialog() {
  if (props.mode === 'edit' && props.task) {
    editingTask.value = { ...props.task }
    predRows.value = parsePreTaskCodes(props.task.preTaskCodes).map((seg, i) => ({
      ...seg,
      rowKey: Date.now() + (++predKeySeq)
    }))
    editingTaskOldParentId.value = props.task.parentId ?? null
  } else {
    editingTask.value = newTaskForm()
    editingTaskOldParentId.value = null
    predRows.value = []

    if (props.parentTask) {
      const p = props.parentTask
      editingTask.value.parentId = p.id
      editingTask.value.deptId = p.deptId
      editingTask.value.deptName = p.deptName
      editingTask.value.assigneeId = p.assigneeId
      editingTask.value.assigneeName = p.assigneeName
    }

    const parentId = props.parentTask?.id ?? null
    const siblings = props.allTasks.filter(t => t.parentId === parentId)
    editingTask.value.sortOrder = siblings.length > 0
      ? Math.max(...siblings.map(s => s.sortOrder ?? 0)) + 1
      : 1

    const rule = props.taskNoRule || '3,2,2'
    const parts = rule.split(',').map(Number)
    if (!props.parentTask) {
      const digits = parts[0] || 3
      const num = siblings.length + 1
      editingTask.value.taskNo = String(num).padStart(digits, '0')
    } else {
      const digits = parts[1] || 2
      const parentNo = props.parentTask.taskNo || ''
      const num = siblings.length + 1
      editingTask.value.taskNo = parentNo + '.' + String(num).padStart(digits, '0')
    }
  }
}

watch(() => props.visible, (val) => {
  if (val) initDialog()
})

/* ── 上级节点切换 ── */
function onParentChange(newParentId: number | null) {
  if (!editingTask.value) return
  const parent = newParentId != null ? props.allTasks.find(t => t.id === newParentId) : null
  const parentNo = parent?.taskNo || ''
  const rule = props.taskNoRule || '3,2,2'
  const parts = rule.split(',').map(Number)
  const depth = parentNo ? parentNo.split('.').length : 0
  const digits = parts[Math.min(depth, parts.length - 1)] ?? 2
  const siblings = props.allTasks.filter(t => t.parentId === newParentId && t.id !== editingTask.value!.id)
  const num = siblings.length + 1
  const padded = String(num).padStart(digits, '0')
  editingTask.value.taskNo = parentNo ? `${parentNo}.${padded}` : padded
}

/* ── 里程碑自动逻辑 ── */
watch(() => editingTask.value?.nodeType, (val) => {
  if (val !== 2 || !editingTask.value) return
  editingTask.value.planDuration = 0
  editingTask.value.actualDuration = 0
  editingTask.value.referenceDuration = 0
  if (!editingTask.value.actualFinishDate) {
    editingTask.value.progressPct = 0
  }
  if (editingTask.value.planFinishDate) {
    editingTask.value.planStartDate = editingTask.value.planFinishDate
  }
  if (editingTask.value.actualFinishDate) {
    editingTask.value.actualStartDate = editingTask.value.actualFinishDate
  }
})

watch(() => editingTask.value?.planFinishDate, (val) => {
  if (editingTask.value?.nodeType === 2 && val) {
    editingTask.value.planStartDate = val
  }
})

watch(() => editingTask.value?.actualFinishDate, (val) => {
  if (editingTask.value?.nodeType === 2) {
    editingTask.value.actualStartDate = val || undefined
  }
})

/* 无子节点时，根据计划开始/完成自动计算计划工期 */
watch([() => editingTask.value?.planStartDate, () => editingTask.value?.planFinishDate], () => {
  const t = editingTask.value
  if (!t || taskHasChildren(t.id)) return
  if (t.planStartDate && t.planFinishDate) {
    const days = Math.round((new Date(t.planFinishDate).getTime() - new Date(t.planStartDate).getTime()) / (1000 * 60 * 60 * 24))
    if (days >= 0) t.planDuration = days
  }
})

/* 无子节点时，根据实际开始/完成自动计算实际工期 */
watch([() => editingTask.value?.actualStartDate, () => editingTask.value?.actualFinishDate], () => {
  const t = editingTask.value
  if (!t || taskHasChildren(t.id)) return
  if (t.actualStartDate && t.actualFinishDate) {
    const days = Math.round((new Date(t.actualFinishDate).getTime() - new Date(t.actualStartDate).getTime()) / (1000 * 60 * 60 * 24))
    if (days >= 0) t.actualDuration = days
  }
})

/* 根据实际开始/完成自动推断状态和进度 */
watch([() => editingTask.value?.actualStartDate, () => editingTask.value?.actualFinishDate], () => {
  const t = editingTask.value
  if (!t) return
  if (t.actualFinishDate) {
    t.status = 2
    t.progressPct = 100
  } else if (t.actualStartDate) {
    t.status = 1
  } else {
    t.status = 0
    t.progressPct = 0
  }
})

/* ── 保存 ── */
async function handleSaveTask() {
  if (!props.projectId || !editingTask.value) return
  if (!editingTask.value.taskName) { ElMessage.warning('请输入任务名称'); return }
  if (editingTask.value.planStartDate && editingTask.value.planFinishDate && editingTask.value.planFinishDate < editingTask.value.planStartDate) {
    ElMessage.warning('计划完成时间不能早于计划开始时间'); return
  }
  if (editingTask.value.actualStartDate && editingTask.value.actualFinishDate && editingTask.value.actualFinishDate < editingTask.value.actualStartDate) {
    ElMessage.warning('实际完成时间不能早于实际开始时间'); return
  }
  editingTask.value.preTaskCodes = serializePreTaskCodes(predRows.value) || undefined

  // 有前置任务时，仅当前置约束比当前计划开始更晚时才自动前推（不回拉）
  if (predRows.value.length > 0) {
    const calcStart = calcPlanStartFromPreds()
    const curStart = editingTask.value.planStartDate ?? ''
    if (calcStart && calcStart.slice(0, 10) > curStart.slice(0, 10)) {
      editingTask.value.planStartDate = calcStart
      if (editingTask.value.planFinishDate && editingTask.value.planDuration && editingTask.value.planDuration > 0) {
        editingTask.value.planFinishDate = dateAddDays(calcStart, editingTask.value.planDuration)
      }
    }
  }

  taskSaving.value = true
  try {
    const oldParentId = editingTaskOldParentId.value
    const isNew = !editingTask.value.id

    if (!isNew && editingTask.value.id) {
      await updateProjectTask(props.projectId, editingTask.value.id, editingTask.value)
    } else {
      const res = await createProjectTask(props.projectId, editingTask.value)
      editingTask.value.id = res.data.id
    }

    emit('saved', {
      task: { ...editingTask.value },
      isNew,
      oldParentId
    })

    emit('update:visible', false)
  } finally {
    taskSaving.value = false
  }
}
</script>

<template>
  <el-dialog :model-value="visible" :title="editingTask?.id ? '编辑任务' : '新增任务'" width="840px" :close-on-click-modal="false" top="5vh" @update:model-value="emit('update:visible', $event)">
    <el-form :model="editingTask" label-width="100px" size="default">
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
          <el-form-item label="上级节点">
            <el-select
              v-model="editingTask.parentId"
              placeholder="无（根节点）"
              clearable
              filterable
              style="width:100%"
              @change="(val: number | null) => { onParentChange(val) }"
            >
              <el-option
                v-for="opt in flatParentOptions"
                :key="opt.id"
                :label="opt.displayLabel"
                :value="opt.id"
              />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="工序号">
            <el-input v-model="editingTask.wbsCode" placeholder="如：T1-1" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="任务类别">
            <el-select v-model="editingTask.taskCategory" placeholder="请选择" clearable style="width:100%">
              <el-option v-for="item in (dictMap['task_category'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="节点类型">
            <el-radio-group v-model="editingTask.nodeType">
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
              v-model="editingTask.deptId"
              :data="deptTreeData"
              :props="{ label: 'name', children: 'children', value: 'id' }"
              node-key="id"
              check-strictly
              placeholder="请选择部门"
              clearable
              filterable
              style="width:100%"
              @change="(val: number) => { editingTask.deptName = departments.find(d => d.id === val)?.name ?? ''; editingTask.assigneeName = ''; editingTask.assigneeId = undefined }"
            />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="责任人">
            <el-select v-model="editingTask.assigneeName" filterable clearable placeholder="请选择人员" style="width:100%" @change="(val: string) => { if (!val) { editingTask.assigneeId = undefined; return }; const u = users.find(u => u.realName === val); if(u) { editingTask.assigneeId = u.id; editingTask.deptId = u.departmentId; editingTask.deptName = departments.find(d => d.id === u.departmentId)?.name ?? '' } }">
              <el-option v-for="u in users.filter(u => !editingTask.deptId || u.departmentId === editingTask.deptId)" :key="u.id" :label="u.realName" :value="u.realName" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="参考工期">
            <el-input-number v-model="editingTask.referenceDuration" :min="0" style="width:100%" :disabled="editingTask?.nodeType === 2" />
          </el-form-item>
        </el-col>
        <el-col :span="24">
          <el-form-item label="前置任务">
            <div class="predecessor-section">
              <el-button
                type="primary"
                size="small"
                @click="addPredRow"
                :disabled="availablePredTasks.length === 0"
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
                  style="width: 180px"
                >
                  <el-option
                    v-for="t in getAvailableForPredRow(row.rowKey)"
                    :key="t.id"
                    :label="`${t.taskNo} - ${t.taskName}`"
                    :value="t.id"
                  />
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
            <el-input-number v-model="editingTask.progressPct" :min="0" :max="100" :precision="1" style="width:100%" :disabled="editingTask?.nodeType === 2 || taskHasChildren(editingTask.id) || !editingTask?.actualStartDate" />
          </el-form-item>
        </el-col>
        <el-col :span="24">
          <el-card shadow="never" class="group-card">
            <template #header><span class="group-title">计划时间</span></template>
            <el-row :gutter="24">
              <el-col :span="8">
                <el-form-item label="计划开始">
                  <el-date-picker v-model="editingTask.planStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="editingTask?.nodeType === 2 || taskHasChildren(editingTask.id) || formStatus !== 0" :placeholder="editingTask?.nodeType === 2 ? '里程碑与计划完成同步' : '有子节点时由子节点自动计算'" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="计划完成">
                  <el-date-picker v-model="editingTask.planFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="taskHasChildren(editingTask.id) || formStatus !== 0" placeholder="有子节点时由子节点自动计算" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="计划工期">
                  <el-input-number v-model="editingTask.planDuration" :min="0" style="width:100%" disabled placeholder="由系统自动计算" />
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
                  <el-date-picker v-model="editingTask.actualStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="editingTask?.nodeType === 2 || taskHasChildren(editingTask.id)" placeholder="有子节点时由子节点自动计算" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="实际完成">
                  <el-date-picker v-model="editingTask.actualFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="taskHasChildren(editingTask.id)" placeholder="有子节点时由子节点自动计算" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="实际工期">
                  <el-input-number v-model="editingTask.actualDuration" :min="0" style="width:100%" disabled placeholder="由系统自动计算" />
                </el-form-item>
              </el-col>
            </el-row>
          </el-card>
        </el-col>
        <el-col :span="24">
          <el-form-item label="备注" class="remark-item">
            <el-input v-model="editingTask.remark" type="textarea" :rows="2" />
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>
    <template #footer>
      <el-button @click="emit('update:visible', false)">取消</el-button>
      <el-button type="danger" :loading="taskSaving" @click="handleSaveTask">确定</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
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
