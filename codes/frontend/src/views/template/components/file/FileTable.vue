<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import type { FileTemplateItem, Department } from '@/types/template'
import { getDepartments } from '@/api/template'
import { buildDeptTree, type DeptTreeNode } from '@/utils/deptTree'

const props = defineProps<{
  modelValue: FileTemplateItem[]
  readonly: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: FileTemplateItem[]]
}>()

const departments = ref<DeptTreeNode[]>([])
const deptTreeData = computed(() => buildDeptTree(departments.value))
const tableData = ref<FileTemplateItem[]>([])

/** 上次 emit/apply 的干净 JSON，用于去重防循环 */
let lastCleanJson = ''

watch(() => props.modelValue, (val) => {
  const src = (val || [])
  const srcJson = JSON.stringify(src)
  if (srcJson === lastCleanJson) return
  lastCleanJson = srcJson
  const next = src.map((m) => {
    const item = { ...m } as FileTemplateItem & { _selectedRoles?: string[] }
    item._selectedRoles = parseViewRoles(item.viewRoles)
    return item
  })
  tableData.value = next as any
}, { immediate: true, deep: true })

watch(tableData, (val) => {
  const clean = val.map((m) => ({
    sortOrder: m.sortOrder,
    fileName: m.fileName,
    required: m.required,
    isPublic: m.isPublic,
    viewRoles: m.viewRoles,
    deptId: m.deptId,
    deptName: m.deptName,
    remark: m.remark
  }))
  const cleanJson = JSON.stringify(clean)
  if (cleanJson === lastCleanJson) return
  lastCleanJson = cleanJson
  emit('update:modelValue', clean as any)
}, { deep: true })

onMounted(async () => {
  try {
    const deptRes = await getDepartments()
    departments.value = deptRes.data as DeptTreeNode[]
  } catch {
    // 错误已在请求拦截器处理
  }
})

const viewRoleOptions = [
  { value: 'pm', label: '项目经理' },
  { value: 'member', label: '项目组成员' },
  { value: 'assignee', label: '文件责任人' }
]

/** 临时存储每行的权限勾选状态（key=row的uid, value=Record） */
const roleCheckState = ref<Record<number, Record<string, boolean>>>({})
/** 控制每行 popover 的可见性 */
const popoverVisible = ref<Record<number, boolean>>({})
let rowUidSeq = 0

function getRowKey(row: any): number {
  if (row._uid == null) row._uid = ++rowUidSeq
  return row._uid
}

function getViewRoleLabels(viewRoles?: string): string {
  if (!viewRoles) return ''
  try {
    const roles = JSON.parse(viewRoles) as string[]
    return viewRoleOptions.filter(o => roles.includes(o.value)).map(o => o.label).join('、')
  } catch { return '' }
}

function parseViewRoles(viewRoles?: string): string[] {
  if (!viewRoles) return []
  try { return JSON.parse(viewRoles) as string[] } catch { return [] }
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

/** 获取行的勾选状态（懒初始化），解决 el-popover 未显示时内容已渲染导致 undefined 报错 */
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

/** 公开↔非公开切换时，清空或初始化权限 */
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

function handleAddRow() {
  const newRow = {
    sortOrder: tableData.value.length + 1,
    fileName: '',
    required: false,
    isPublic: true,
    deptId: null,
    deptName: '',
    viewRoles: undefined as string | undefined,
    _selectedRoles: [] as string[]
  }
  tableData.value = [...tableData.value, newRow]
}

function handleDeleteRow(index: number) {
  const list = tableData.value.filter((_, i) => i !== index)
  list.forEach((item, i) => {
    item.sortOrder = i + 1
  })
  tableData.value = list
}

function handleDeptChange(row: FileTemplateItem, val: unknown) {
  const dept = departments.value.find((d) => d.id === val)
  row.deptName = dept ? dept.name : ''
}

/* ===================== 右键菜单 ===================== */

const contextMenu = ref<{
  visible: boolean
  x: number
  y: number
  row: FileTemplateItem | null
  index: number
}>({ visible: false, x: 0, y: 0, row: null, index: -1 })

function showContextMenu(row: FileTemplateItem, index: number, event: MouseEvent) {
  event.preventDefault()
  contextMenu.value = {
    visible: true,
    x: event.clientX,
    y: event.clientY,
    row,
    index
  }
}

function hideContextMenu() {
  contextMenu.value = { visible: false, x: 0, y: 0, row: null, index: -1 }
}

/** 在右键所在行前面插入一行 */
function handleInsertRow() {
  const idx = contextMenu.value.index
  if (idx < 0) return
  const newRow = {
    sortOrder: idx + 1,
    fileName: '',
    required: false,
    isPublic: true,
    deptId: null,
    deptName: '',
    viewRoles: undefined as string | undefined,
    _selectedRoles: [] as string[]
  }
  const list = [...tableData.value]
  list.splice(idx, 0, newRow)
  // 重新整理序号
  list.forEach((item, i) => { item.sortOrder = i + 1 })
  tableData.value = list
  hideContextMenu()
}

/** 上移一行 */
function handleMoveUp() {
  const idx = contextMenu.value.index
  if (idx <= 0) return
  const list = [...tableData.value]
  ;[list[idx - 1], list[idx]] = [list[idx], list[idx - 1]]
  list.forEach((item, i) => { item.sortOrder = i + 1 })
  tableData.value = list
  hideContextMenu()
}

/** 下移一行 */
function handleMoveDown() {
  const idx = contextMenu.value.index
  if (idx < 0 || idx >= tableData.value.length - 1) return
  const list = [...tableData.value]
  ;[list[idx], list[idx + 1]] = [list[idx + 1], list[idx]]
  list.forEach((item, i) => { item.sortOrder = i + 1 })
  tableData.value = list
  hideContextMenu()
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && contextMenu.value.visible) hideContextMenu()
}
onMounted(() => document.addEventListener('keydown', onKeydown))
onUnmounted(() => document.removeEventListener('keydown', onKeydown))
</script>

<template>
  <div class="file-table">
    <el-table
      :data="tableData"
      border
      stripe
      style="width: 100%"
      max-height="600"
      @row-contextmenu="(row: any, _column: any, event: MouseEvent) => showContextMenu(row, tableData.indexOf(row), event)"
    >
      <el-table-column type="index" label="序号" width="60" />

      <el-table-column label="文件名称" min-width="250">
        <template #header>
          <span><span style="color: var(--el-color-danger); margin-right: 2px;">*</span>文件名称</span>
        </template>
        <template #default="{ row }">
          <el-input
            v-model="row.fileName"
            placeholder="请输入文件名称"
            :disabled="readonly"
            :class="{ 'is-error': !row.fileName?.trim() && row._touched }"
            clearable
            @blur="row._touched = true"
          />
        </template>
      </el-table-column>

      <el-table-column label="文件说明" min-width="260">
        <template #default="{ row }">
          <el-input
            v-model="row.remark"
            placeholder="请输入文件说明"
            :disabled="readonly"
            clearable
          />
        </template>
      </el-table-column>

      <el-table-column label="必须" width="70" align="center">
        <template #default="{ row }">
          <el-switch
            v-model="row.required"
            :disabled="readonly"
            size="small"
          />
        </template>
      </el-table-column>

      <el-table-column label="公开" width="70" align="center">
        <template #default="{ row }">
          <el-switch
            v-model="row.isPublic"
            :disabled="readonly"
            size="small"
            @change="(val: boolean) => handleIsPublicChange(row, val)"
          />
        </template>
      </el-table-column>

      <el-table-column label="文件权限" min-width="200">
        <template #default="{ row }">
          <span v-if="row.isPublic" class="perm-placeholder">-</span>
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
              <el-button v-if="row.viewRoles" type="info" link size="small">
                {{ getViewRoleLabels(row.viewRoles) }}
              </el-button>
              <el-button v-else type="info" link size="small" class="perm-btn">
                点击配置
              </el-button>
            </template>
            <div class="role-checkbox-group">
              <div v-for="opt in viewRoleOptions" :key="opt.value" class="role-checkbox-item">
                <el-checkbox
                  v-model="getRowCheckState(row)[opt.value]"
                  :disabled="readonly"
                >
                  {{ opt.label }}
                </el-checkbox>
              </div>
              <div class="role-checkbox-actions">
                <el-button size="small" type="primary" @click="confirmViewRoles(row)">确定</el-button>
              </div>
            </div>
          </el-popover>
        </template>
      </el-table-column>

      <el-table-column label="责任部门" min-width="240">
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
        + 添加行
      </el-button>
    </div>

    <!-- 右键菜单 -->
    <teleport to="body">
      <div v-if="contextMenu.visible" class="ctx-overlay" @click="hideContextMenu" @contextmenu.prevent="hideContextMenu" />
      <div v-if="contextMenu.visible" class="ctx-menu" :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }">
        <el-button text class="ctx-item" @click="handleInsertRow">插入行</el-button>
        <div class="ctx-divider" />
        <el-button text class="ctx-item" @click="handleMoveUp" :disabled="contextMenu.index <= 0">上移</el-button>
        <el-button text class="ctx-item" @click="handleMoveDown" :disabled="contextMenu.index < 0 || contextMenu.index >= tableData.length - 1">下移</el-button>
      </div>
    </teleport>
  </div>
</template>

<style scoped>
.file-table {
  width: 100%;
}

.file-table :deep(.el-table__body-wrapper) {
  overflow-x: auto;
}

.add-row {
  margin-top: 12px;
  text-align: left;
}

.perm-placeholder {
  color: #c0c4cc;
}

.perm-btn {
  color: #909399;
}

.role-checkbox-group {
  padding: 4px 0;
}

.role-checkbox-item {
  padding: 6px 12px;
  cursor: pointer;
  border-radius: 4px;
  display: flex;
  align-items: center;
  gap: 6px;
  user-select: none;
}

.role-checkbox-item:hover {
  background-color: #f5f7fa;
}

.role-checkbox-item.checked {
  color: #409eff;
}

.role-checkbox-icon {
  font-size: 16px;
  width: 18px;
  display: inline-block;
}

.role-checkbox-actions {
  text-align: right;
  padding-top: 8px;
  margin-top: 4px;
  border-top: 1px solid #f0f0f0;
}
</style>

<style>
/* 右键菜单（非 scoped，因 teleport 到 body） */
.ctx-overlay {
  position: fixed;
  inset: 0;
  z-index: 9998;
}
.ctx-menu {
  position: fixed;
  z-index: 9999;
  background: #fff;
  border-radius: 4px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.12);
  padding: 4px 0;
  display: flex;
  flex-direction: column;
  min-width: 120px;
}
.ctx-menu .ctx-item {
  justify-content: flex-start;
  padding: 7px 16px;
  margin: 0 !important;
  font-size: 13px;
  border-radius: 0;
}
.ctx-menu .ctx-item:hover {
  background: #f5f7fa;
}
</style>
