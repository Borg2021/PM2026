<script setup lang="ts">
import { reactive, ref, watch, computed, onMounted } from 'vue'
import type { PlanNode, NodeType, PredecessorInfo, UserInfo } from '@/types/template'
import { getDepartments, searchUsers, getDictByType } from '@/api/template'
import { buildDeptTree, type DeptTreeNode } from '@/utils/deptTree'

interface PredecessorRow {
  rowKey: number
  predecessorCode: string
  dependencyType: string
  lagDays: number
}

const props = defineProps<{
  visible: boolean
  nodeData: PlanNode | null
  allNodes: PlanNode[]
  currentParentNodeCode: string | null
}>()

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'confirm', node: PlanNode, targetParentCode: string | null): void
}>()

const formRef = ref()
let predKeySeq = 0

/* ---- 展平树结构 ---- */
function flattenTree(nodes: PlanNode[]): PlanNode[] {
  const result: PlanNode[] = []
  for (const node of nodes) {
    result.push(node)
    if (node.children?.length) result.push(...flattenTree(node.children))
  }
  return result
}

/* ---- 收集子树中所有 nodeCode ---- */
function gatherCodes(nodes: PlanNode[], out: Set<string>): void {
  for (const n of nodes) {
    out.add(n.nodeCode)
    if (n.children?.length) gatherCodes(n.children, out)
  }
}

function collectSubtreeCodes(nodes: PlanNode[], targetCode: string): Set<string> {
  for (const node of nodes) {
    if (node.nodeCode === targetCode) {
      const codes = new Set<string>()
      gatherCodes([node], codes)
      return codes
    }
    if (node.children?.length) {
      const result = collectSubtreeCodes(node.children, targetCode)
      if (result.size > 0) return result
    }
  }
  return new Set()
}

/* ---- 可选前置节点（排除自身及子孙） ---- */
const availableNodes = computed(() => {
  const flat = flattenTree(props.allNodes)
  if (!props.nodeData) return flat
  const excluded = collectSubtreeCodes(props.allNodes, props.nodeData.nodeCode)
  return flat.filter(n => !excluded.has(n.nodeCode))
})

/* ---- 可选父节点（flat 列表，排除自身及子孙，带层级缩进） ---- */
const flatParentOptions = computed(() => {
  const excluded = props.nodeData
    ? collectSubtreeCodes(props.allNodes, props.nodeData.nodeCode)
    : new Set<string>()

  const result: { nodeCode: string; displayLabel: string }[] = []
  function walk(nodes: PlanNode[], depth: number) {
    for (const n of nodes) {
      if (excluded.has(n.nodeCode)) continue
      const prefix = depth === 0 ? '' : '│  '.repeat(depth - 1) + '├─ '
      result.push({
        nodeCode: n.nodeCode,
        displayLabel: `${prefix}${n.nodeCode} - ${n.nodeName}`
      })
      if (n.children?.length) walk(n.children, depth + 1)
    }
  }
  walk(props.allNodes, 0)
  return result
})

/* ---- 上级节点选择（独立 ref，避免 reactive 深层响应问题） ---- */
const selectedParentCode = ref<string | null>(null)

/* ---- 表单模型 ---- */
const form = reactive({
  nodeType: 1 as NodeType,
  nodeName: '',
  nodeCode: '',
  stdDuration: 0,
  deptId: null as number | null,
  deptName: '',
  assigneeId: null as number | null,
  assigneeName: '',
  taskCategory: '',
  remark: '',
  predecessors: [] as PredecessorRow[]
})


const formRules = {
  nodeType: [{ required: true, message: '请选择节点类型', trigger: 'change' }],
  nodeName: [{ required: true, message: '请输入任务名称', trigger: 'blur' }]
}

/* ---- 部门 / 人员下拉数据 ---- */
const departments = ref<DeptTreeNode[]>([])
const deptTreeData = computed(() => buildDeptTree(departments.value))
const taskCategoryOptions = ref<{ dictCode: string; dictLabel: string }[]>([])
const users = ref<UserInfo[]>([])
const usersLoading = ref(false)

async function loadDepartments() {
  const res = await getDepartments()
  departments.value = res.data as DeptTreeNode[]
}

async function loadUsers(deptId: number | null) {
  usersLoading.value = true
  try {
    const res = await searchUsers('', deptId ?? undefined)
    users.value = res.data
  } finally {
    usersLoading.value = false
  }
}

function handleDeptChange(deptId: number | null) {
  form.assigneeId = null
  form.assigneeName = ''
  if (deptId) {
    const dept = departments.value.find(d => d.id === deptId)
    form.deptName = dept?.name ?? ''
    loadUsers(deptId)
  } else {
    form.deptName = ''
    users.value = []
  }
}

function handleUserChange(userId: number | null) {
  if (userId) {
    const user = users.value.find(u => u.id === userId)
    form.assigneeName = user?.realName ?? ''
  } else {
    form.assigneeName = ''
  }
}

/* ---- 里程碑切换时重置参考工期 ---- */
watch(() => form.nodeType, (val) => {
  if (val === 2) {
    form.stdDuration = 0
  }
})

onMounted(() => {
  loadDepartments()
  getDictByType('task_category').then(res => {
    taskCategoryOptions.value = res.data
  })
})

/* ---- 弹窗打开时回填 / 清空 ---- */
watch(
  () => props.visible,
  (visible) => {
    if (visible) {
      resetForm()
    }
  }
)

function resetForm(): void {
  if (props.nodeData) {
    form.nodeType = props.nodeData.nodeType
    form.nodeName = props.nodeData.nodeName
    form.nodeCode = props.nodeData.nodeCode
    form.stdDuration = props.nodeData.stdDuration ?? 0
    form.deptId = props.nodeData.deptId ?? null
    form.deptName = props.nodeData.deptName ?? ''
    form.assigneeId = props.nodeData.assigneeId ?? null
    form.assigneeName = props.nodeData.assigneeName ?? ''
    form.remark = props.nodeData.remark ?? ''
    form.taskCategory = props.nodeData.taskCategory ?? ''
    selectedParentCode.value = props.currentParentNodeCode
    form.predecessors = (props.nodeData.predecessors || []).map((p, i) => ({
      rowKey: Date.now() + i,
      predecessorCode: p.predecessorCode,
      dependencyType: p.dependencyType,
      lagDays: p.lagDays
    }))
    if (props.nodeData.deptId) loadUsers(props.nodeData.deptId)
  } else {
    form.nodeType = 1
    form.nodeName = ''
    form.nodeCode = ''
    form.stdDuration = 0
    form.deptId = null
    form.deptName = ''
    form.assigneeId = null
    form.assigneeName = ''
    form.remark = ''
    form.taskCategory = ''
    selectedParentCode.value = props.currentParentNodeCode
    form.predecessors = []
    users.value = []
  }
  formRef.value?.clearValidate()
}

/* ---- 前置任务行管理 ---- */
function getAvailableForRow(rowKey: number): PlanNode[] {
  const selectedCodes = form.predecessors
    .filter(p => p.rowKey !== rowKey && p.predecessorCode)
    .map(p => p.predecessorCode)
  return availableNodes.value.filter(n => !selectedCodes.includes(n.nodeCode))
}

function addPredecessorRow(): void {
  form.predecessors.push({
    rowKey: Date.now() + (++predKeySeq),
    predecessorCode: '',
    dependencyType: 'FS',
    lagDays: 0
  })
}

function removePredecessorRow(rowKey: number): void {
  form.predecessors = form.predecessors.filter(p => p.rowKey !== rowKey)
}

/* ---- 确认 ---- */
async function handleConfirm(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  const predecessors: PredecessorInfo[] = form.predecessors
    .filter(p => p.predecessorCode)
    .map(p => ({
      predecessorCode: p.predecessorCode,
      dependencyType: p.dependencyType,
      lagDays: p.lagDays
    }))

  const nodeData: PlanNode = {
    nodeCode: form.nodeCode.trim(),
    nodeName: form.nodeName.trim(),
    nodeType: form.nodeType,
    sortOrder: props.nodeData?.sortOrder ?? 0,
    stdDuration: form.stdDuration || null,
    predecessors,
    deliverableCnt: props.nodeData?.deliverableCnt ?? 0,
    deptId: form.deptId ?? undefined,
    deptName: form.deptName.trim() || undefined,
    assigneeId: form.assigneeId ?? undefined,
    assigneeName: form.assigneeName.trim() || undefined,
    taskCategory: form.taskCategory || undefined,
    remark: form.remark.trim(),
    children: props.nodeData?.children ?? []
  }

  emit('confirm', nodeData, selectedParentCode.value)
}

function handleCancel(): void {
  emit('update:visible', false)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    @update:model-value="emit('update:visible', $event)"
    :title="nodeData ? '编辑节点' : '新增计划'"
    width="620px"
    :close-on-click-modal="false"
    @close="handleCancel"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="formRules"
      label-width="90px"
      label-position="right"
    >
      <!-- 节点类型 -->
      <el-form-item label="节点类型" prop="nodeType">
        <el-radio-group v-model="form.nodeType">
          <el-radio :value="1">任务</el-radio>
          <el-radio :value="2">里程碑</el-radio>
        </el-radio-group>
      </el-form-item>

      <!-- 任务名称 -->
      <el-form-item label="任务名称" prop="nodeName">
        <el-input
          v-model="form.nodeName"
          placeholder="请输入任务名称"
          maxlength="100"
        />
      </el-form-item>

      <!-- 上级节点（仅编辑时显示） -->
      <el-form-item v-if="nodeData" label="上级节点">
        <el-select
          v-model="selectedParentCode"
          placeholder="无（根节点）"
          clearable
          filterable
          style="width: 100%"
          @change="(val: string | null) => { selectedParentCode = val }"
        >
          <el-option
            v-for="opt in flatParentOptions"
            :key="opt.nodeCode"
            :label="opt.displayLabel"
            :value="opt.nodeCode"
          />
        </el-select>
      </el-form-item>

      <!-- 参考工期 -->
      <el-form-item label="参考工期">
        <el-input-number
          v-model="form.stdDuration"
          :min="0"
          :max="99999"
          :disabled="form.nodeType === 2"
          controls-position="right"
          style="width: 160px"
        />
        <span class="unit-suffix">天</span>
      </el-form-item>

      <!-- 部门 -->
      <el-form-item label="部门">
        <el-tree-select
          v-model="form.deptId"
          :data="deptTreeData"
          :props="{ label: 'name', children: 'children', value: 'id' }"
          node-key="id"
          check-strictly
          placeholder="请选择部门"
          clearable
          filterable
          style="width: 100%"
          @change="handleDeptChange"
        />
      </el-form-item>

      <!-- 负责人 -->
      <el-form-item label="负责人">
        <el-select
          v-model="form.assigneeId"
          placeholder="请先选择部门再选择人员"
          clearable
          filterable
          :loading="usersLoading"
          :disabled="!form.deptId"
          style="width: 100%"
          @change="handleUserChange"
        >
          <el-option
            v-for="u in users"
            :key="u.id"
            :label="u.realName"
            :value="u.id"
          />
        </el-select>
      </el-form-item>

      <!-- 任务类别 -->
      <el-form-item label="任务类别">
        <el-select
          v-model="form.taskCategory"
          placeholder="请选择任务类别"
          clearable
          filterable
          style="width: 100%"
        >
          <el-option
            v-for="item in taskCategoryOptions"
            :key="item.dictCode"
            :label="item.dictLabel"
            :value="item.dictCode"
          />
        </el-select>
      </el-form-item>

      <!-- 前置任务 -->
      <el-form-item label="前置任务">
        <div class="predecessor-section">
          <el-button
            type="primary"
            size="small"
            @click="addPredecessorRow"
            :disabled="availableNodes.length === 0"
          >
            + 添加前置任务
          </el-button>

          <div v-if="form.predecessors.length === 0" class="predecessor-empty">
            尚未设置前置任务
          </div>

          <div
            v-for="row in form.predecessors"
            :key="row.rowKey"
            class="predecessor-row"
          >
            <el-select
              v-model="row.predecessorCode"
              placeholder="选择前置任务"
              filterable
              style="width: 180px"
            >
              <el-option
                v-for="node in getAvailableForRow(row.rowKey)"
                :key="node.nodeCode"
                :label="`${node.nodeCode} - ${node.nodeName}`"
                :value="node.nodeCode"
              />
            </el-select>

            <el-select
              v-model="row.dependencyType"
              style="width: 150px; margin-left: 8px;"
            >
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
              @click="removePredecessorRow(row.rowKey)"
            >
              删除
            </el-button>
          </div>
        </div>
      </el-form-item>

      <!-- 备注 -->
      <el-form-item label="备注">
        <el-input
          v-model="form.remark"
          type="textarea"
          :rows="3"
          placeholder="选填"
          maxlength="500"
          show-word-limit
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button type="danger" @click="handleConfirm">确定</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.unit-suffix {
  margin-left: 8px;
  color: #909399;
  font-size: 14px;
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
</style>
