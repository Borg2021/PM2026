<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import type { TemplateMember, Department } from '@/types/template'
import type { FunctionItem } from '@/types/system'
import { getDepartments, searchUsers } from '@/api/template'
import { getFunctionList } from '@/api/system'
import { buildDeptTree, type DeptTreeNode } from '@/utils/deptTree'

const props = defineProps<{
  modelValue: TemplateMember[]
  readonly: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: TemplateMember[]]
}>()

// ========== 缓存字典数据 ==========
const departments = ref<DeptTreeNode[]>([])
const deptTreeData = computed(() => buildDeptTree(departments.value))
const functions = ref<FunctionItem[]>([])

// ========== 本地可写数据 ==========
const tableData = ref<TemplateMember[]>([])

// 同步外部 prop 到本地（深拷贝，解除 readonly 约束）
watch(() => props.modelValue, (val) => {
  const next = val.map((m) => ({ ...m }))
  // 通过 JSON 比较避免回环
  if (JSON.stringify(next) !== JSON.stringify(tableData.value)) {
    tableData.value = next
  }
}, { immediate: true, deep: true })

// 所有变更（编辑、增删）通过深 watcher 自动同步回父组件
watch(tableData, (val) => {
  emit('update:modelValue', val.map((m) => ({ ...m })))
}, { deep: true })

// ========== 初始化字典 ==========
onMounted(async () => {
  try {
    const [deptRes, fnRes] = await Promise.all([
      getDepartments(),
      getFunctionList()
    ])
    departments.value = deptRes.data as DeptTreeNode[]
    functions.value = fnRes.data
  } catch {
    // 错误已在请求拦截器处理
  }
})

// ========== 行操作 ==========
function handleAddRow() {
  const newRow: TemplateMember = {
    sortOrder: tableData.value.length + 1,
    roleId: null,
    roleName: '',
    memberId: null,
    memberName: '',
    deptId: null,
    deptName: '',
    remark: ''
  }
  // 替换引用以触发深 watcher 自动 emit
  tableData.value = [...tableData.value, newRow]
}

function handleDeleteRow(index: number) {
  const list = tableData.value.filter((_, i) => i !== index)
  list.forEach((item, i) => {
    item.sortOrder = i + 1
  })
  tableData.value = list
}

// ========== 根据职能+部门自动匹配人员 ==========
async function autoFillMember(row: TemplateMember) {
  // 只有当职能和部门都选了才匹配
  if (!row.roleId || !row.deptId) {
    row.memberId = null
    row.memberName = ''
    return
  }
  try {
    const res = await searchUsers('', row.deptId, row.roleId)
    const users = res.data
    if (users && users.length === 1) {
      row.memberId = users[0].id
      row.memberName = users[0].realName
    } else {
      // 匹配不到唯一人员时清空
      row.memberId = null
      row.memberName = ''
      if (users && users.length > 1) {
        // 多个匹配不做静默处理（用户可手动选择）
      }
    }
  } catch {
    // 请求失败不阻塞操作
  }
}

// ========== 下拉变更时同步名称字段 ==========
function handleRoleChange(row: TemplateMember, val: unknown) {
  const fn = functions.value.find((f) => f.id === val)
  row.roleName = fn ? fn.name : ''
  row.memberId = null
  row.memberName = ''
  autoFillMember(row)
}

function findDeptById(tree: DeptTreeNode[], id: number): DeptTreeNode | undefined {
  for (const node of tree) {
    if (node.id === id) return node
    const found = findDeptById(node.children, id)
    if (found) return found
  }
  return undefined
}

function handleDeptChange(row: TemplateMember, val: unknown) {
  const dept = findDeptById(departments.value, val as number)
  row.deptName = dept ? dept.name : ''
  row.memberId = null
  row.memberName = ''
  autoFillMember(row)
}

// ========== 行拖拽排序 ==========
const dragRowIndex = ref<number | null>(null)
const dragOverIndex = ref<number | null>(null)

function handleRowDragStart(index: number, ev: DragEvent) {
  if (props.readonly) { ev.preventDefault(); return }
  dragRowIndex.value = index
  ev.dataTransfer!.effectAllowed = 'move'
  ev.dataTransfer!.setData('text/plain', String(index))
  // 让拖拽图像半透明
  const el = ev.target as HTMLElement
  const tr = el.closest('tr')
  if (tr) {
    requestAnimationFrame(() => { tr.classList.add('row-dragging') })
  }
}

function handleRowDragOver(index: number, ev: DragEvent) {
  if (dragRowIndex.value === null || dragRowIndex.value === index) {
    ev.dataTransfer!.dropEffect = 'none'
    return
  }
  ev.preventDefault()
  ev.dataTransfer!.dropEffect = 'move'
  dragOverIndex.value = index
}

function handleRowDrop(index: number, ev: DragEvent) {
  ev.preventDefault()
  const srcIdx = dragRowIndex.value
  dragRowIndex.value = null
  dragOverIndex.value = null

  if (srcIdx === null || srcIdx === index) return

  // 移动行
  const list = [...tableData.value]
  const [moved] = list.splice(srcIdx, 1)
  list.splice(index, 0, moved)
  // 更新 sortOrder
  list.forEach((item, i) => { item.sortOrder = i + 1 })
  tableData.value = list
}

function handleRowDragEnd() {
  dragRowIndex.value = null
  dragOverIndex.value = null
}

// ========== 行样式 ==========
function rowClassName({ rowIndex }: { rowIndex: number }) {
  if (rowIndex === dragOverIndex.value) return 'row-drag-over'
  return ''
}

// ========== 表格拖拽事件委托 ==========
function onTableDragOver(ev: DragEvent) {
  if (dragRowIndex.value === null) return
  const tr = (ev.target as HTMLElement).closest('.el-table__row') as HTMLElement | null
  if (!tr) return
  const idx = Array.from(
    tr.parentElement?.querySelectorAll('.el-table__row') || []
  ).indexOf(tr)
  if (idx === -1) return
  handleRowDragOver(idx, ev)
}

function onTableDrop(ev: DragEvent) {
  if (dragRowIndex.value === null) return
  const tr = (ev.target as HTMLElement).closest('.el-table__row') as HTMLElement | null
  if (!tr) return
  const idx = Array.from(
    tr.parentElement?.querySelectorAll('.el-table__row') || []
  ).indexOf(tr)
  if (idx === -1) return
  handleRowDrop(idx, ev)
}
</script>

<template>
  <div
    class="member-table"
    @dragover="onTableDragOver"
    @drop="onTableDrop"
  >
    <el-table
      :data="tableData"
      border
      stripe
      style="width: 100%"
      max-height="480"
      :row-class-name="rowClassName"
    >
      <!-- 拖拽手柄列 -->
      <el-table-column v-if="!readonly" label="" width="40" align="center">
        <template #default="{ $index }">
          <span
            class="drag-handle"
            draggable="true"
            @dragstart="handleRowDragStart($index, $event)"
            @dragend="handleRowDragEnd"
            :data-row-index="$index"
          >⋮⋮</span>
        </template>
      </el-table-column>

      <el-table-column type="index" label="序号" width="60" />

      <el-table-column label="职能" min-width="320">
        <template #default="{ row }">
          <el-select
            v-model="row.roleId"
            placeholder="请选择职能"
            :disabled="readonly"
            filterable
            clearable
            style="width: 100%"
            @change="(val: unknown) => handleRoleChange(row, val)"
          >
            <el-option
              v-for="f in functions"
              :key="f.id"
              :label="f.name"
              :value="f.id"
            />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column label="部门" min-width="320">
        <template #default="{ row }">
          <el-tree-select
            v-model="row.deptId"
            :data="deptTreeData"
            :props="{ label: 'name', children: 'children', value: 'id' }"
            node-key="id"
            check-strictly
            placeholder="请选择部门"
            :disabled="readonly"
            clearable
            filterable
            style="width: 100%"
            @change="(val: unknown) => handleDeptChange(row, val)"
          />
        </template>
      </el-table-column>

      <el-table-column label="人员" width="180">
        <template #default="{ row }">
          <span v-if="row.memberName" style="color:#409eff;font-weight:500">{{ row.memberName }}</span>
          <span v-else-if="row.roleId && row.deptId" style="color:#c0c4cc">待匹配</span>
          <span v-else style="color:#c0c4cc">-</span>
        </template>
      </el-table-column>

      <el-table-column label="备注" min-width="300">
        <template #default="{ row }">
          <el-input
            v-model="row.remark"
            placeholder="请输入备注"
            :disabled="readonly"
            clearable
          />
        </template>
      </el-table-column>

      <el-table-column label="操作" width="70" fixed="right">
        <template #default="{ $index }">
          <el-button
            v-if="!readonly"
            type="danger"
            link
            @click="handleDeleteRow($index)"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <div v-if="!readonly" class="add-row">
      <el-button type="primary" link @click="handleAddRow">
        + 点击添加
      </el-button>
    </div>
  </div>
</template>

<style scoped>
.member-table {
  width: 100%;
}

/* ─── 拖拽手柄 ─── */
.drag-handle {
  display: inline-block;
  cursor: grab;
  user-select: none;
  font-size: 14px;
  color: #c0c4cc;
  line-height: 1;
  padding: 4px 2px;
  letter-spacing: -2px;
}
.drag-handle:hover {
  color: #909399;
}
.drag-handle:active {
  cursor: grabbing;
}

/* ─── 拖拽行样式 ─── */
:deep(.row-dragging) {
  opacity: 0.45;
}

:deep(.row-drag-over) > td {
  border-top: 2px solid #409eff !important;
}

.add-row {
  margin-top: 12px;
  text-align: left;
}
</style>
