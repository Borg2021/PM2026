<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { ElMessageBox } from 'element-plus'
import type { PlanNode, PredecessorInfo } from '@/types/template'
import { getDictByType } from '@/api/template'

const props = defineProps<{
  modelValue: PlanNode[]
  readonly: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', val: PlanNode[]): void
  (e: 'add-child', parentKey: string | null): void
  (e: 'edit-node', node: PlanNode): void
}>()

/* ===================== 内部状态 ===================== */

// 节点自增键生成器 —— 每条 node 拿到一个稳定且唯一的 _nodeKey
let keySeq = 0
function generateKey(): string {
  return `pn_${Date.now()}_${++keySeq}`
}

/** 递归为节点树注入 _nodeKey */
function injectKeys(nodes: PlanNode[]): PlanNode[] {
  return nodes.map((n) => {
    const node = n as PlanNode & { _nodeKey?: string }
    if (!node._nodeKey) {
      node._nodeKey = generateKey()
    }
    if (node.children?.length) {
      node.children = injectKeys(node.children)
    }
    return node
  })
}

/** 递归剥离 _nodeKey（用于回写父级） */
function stripKeys(nodes: PlanNode[]): PlanNode[] {
  return nodes.map((n) => {
    const { children, ...rest } = n as PlanNode & { _nodeKey?: string }
    delete (rest as Record<string, unknown>)._nodeKey
    return {
      ...rest,
      children: children?.length ? stripKeys(children) : []
    } as PlanNode
  })
}

/** 收集所有节点的 _nodeKey */
function collectKeys(nodes: PlanNode[]): string[] {
  const keys: string[] = []
  for (const n of nodes) {
    const k = (n as Record<string, unknown>)._nodeKey as string | undefined
    if (k) {
      keys.push(k)
      if (n.children?.length) {
        keys.push(...collectKeys(n.children))
      }
    }
  }
  return keys
}

/** 缓存列宽，仅在数据加载或节点新增/删除时重新计算 */
const seqColWidth = ref(130)
let seqColWidthDirty = true

function refreshSeqColWidth() {
  seqColWidthDirty = true
  calcSeqColWidth()
}

function calcSeqColWidth() {
  if (!seqColWidthDirty) return
  let maxLen = 0
  function walk(nodes: PlanNode[]) {
    for (const n of nodes) {
      if (n.nodeCode.length > maxLen) maxLen = n.nodeCode.length
      if (n.children?.length) walk(n.children)
    }
  }
  walk(treeData.value)
  const base = maxLen * 14 + 48
  seqColWidth.value = Math.max(Math.round(base * 1.3), 130)
  seqColWidthDirty = false
}

// 列宽缓存 + 强制刷新工具
const tableKey = ref(0)
/** el-table 实例引用，用于调用 doLayout 刷新列宽而不销毁重建 */
const elTableRef = ref<{ doLayout: () => void } | null>(null)
let _skipEchoRefresh = false   // 初始加载的 eo 不触发列宽重建

/**
 * 强制 el-table 刷新布局（列宽调整后重新计算）。
 * 不再通过销毁重建（tableKey++）方式，改用 doLayout()
 * 以避免丢失父容器滚动位置。
 */
function forceColumnRefresh() {
  calcSeqColWidth()
  nextTick(() => {
    elTableRef.value?.doLayout()
  })
}

function formatPredecessors(predecessors: PredecessorInfo[]): string {
  if (!predecessors || predecessors.length === 0) return ''
  return predecessors
    .map(p => {
      if (p.lagDays === 0) return `${p.predecessorCode} (${p.dependencyType})`
      const sign = p.lagDays > 0 ? '+' : ''
      return `${p.predecessorCode} (${p.dependencyType}${sign}${p.lagDays})`
    })
    .join(', ')
}

/* ===================== 任务类别字典 ===================== */

const taskCategoryMap = ref<Record<string, string>>({})
onMounted(() => {
  getDictByType('task_category').then(res => {
    const map: Record<string, string> = {}
    res.data.forEach(item => { map[item.dictCode] = item.dictLabel })
    taskCategoryMap.value = map
  })
})

/* ===================== 展开 / 折叠 ===================== */

const expandedKeys = ref<string[]>([])

// 内部树数据，始终携带 _nodeKey
const treeData = ref<PlanNode[]>([])
let internalUpdate = false
let treeHydrated = false   // 首轮数据加载标记，用于展开状态初始化

// 从 prop 同步到内部状态（仅当不是内部更新循环时）
watch(
  () => props.modelValue,
  (val) => {
    if (!internalUpdate) {
      // 仅在首次有数据时开始计时（避免 immediate watch 空数组误启）
      if (!treeHydrated && val.length > 0) {
      }
      treeData.value = injectKeys(val)
      // 首次加载时默认展开前2层
      if (!treeHydrated && treeData.value.length > 0) {
        const keys: string[] = []
        function collectTopLevels(nodes: PlanNode[], depth: number) {
          for (const n of nodes) {
            const k = (n as Record<string, unknown>)._nodeKey as string
            if (k && depth < 2) keys.push(k)
            if (n.children?.length && depth < 2) collectTopLevels(n.children, depth + 1)
          }
        }
        collectTopLevels(treeData.value, 0)
        expandedKeys.value = keys
        treeHydrated = true
        _skipEchoRefresh = true
        nextTick(() => {
          calcSeqColWidth()
        })
      }
    }
  },
  { immediate: true }
)

// 内部变化时 emit 回父级。无 deep（每次 mutation 都赋新数组引用，deep 徒增递归开销）
watch(
  treeData,
  (val) => {
    internalUpdate = true
    emit('update:modelValue', stripKeys(val))
    // 初始加载的 echo 不触发列宽重建（首次渲染无需 remount）
    const skip = _skipEchoRefresh
    _skipEchoRefresh = false
    if (!skip) forceColumnRefresh()
    Promise.resolve().then(() => {
      internalUpdate = false
    })
  }
)

/* ===================== 树查找工具 ===================== */

interface FindResult {
  parent: PlanNode[]
  node: PlanNode
  index: number
}

function findNode(nodes: PlanNode[], nodeKey: string): FindResult | null {
  for (let i = 0; i < nodes.length; i++) {
    if ((nodes[i] as Record<string, unknown>)._nodeKey === nodeKey) {
      return { parent: nodes, node: nodes[i], index: i }
    }
    if (nodes[i].children?.length) {
      const result = findNode(nodes[i].children, nodeKey)
      if (result) return result
    }
  }
  return null
}

function handleExpandChange(row: PlanNode, expanded: boolean) {
  const key = (row as Record<string, unknown>)._nodeKey as string
  if (expanded) {
    if (!expandedKeys.value.includes(key)) {
      expandedKeys.value = [...expandedKeys.value, key]
    }
  } else {
    expandedKeys.value = expandedKeys.value.filter((k) => k !== key)
  }
}

function expandAll(): void {
  expandedKeys.value = collectKeys(treeData.value)
}

function collapseAll(): void {
  expandedKeys.value = []
}

function toggleExpandAll(): void {
  if (expandedKeys.value.length > 0) {
    collapseAll()
  } else {
    expandAll()
  }
}

/** 对外暴露：移动节点到新的父节点下（newParentCode 为 nodeCode，null/undefined 表示移到根节点） */
function moveToParent(nodeKey: string, newParentCode: string | null | undefined): void {
  const targetCode = newParentCode || null  // 规范化 undefined → null
  const sourceResult = findNode(treeData.value, nodeKey)
  if (!sourceResult) return

  // 用 nodeCode 判断旧父节点是否与目标一致
  const oldParentCode = getParentNodeCode(nodeKey)
  if (oldParentCode === targetCode) return

  const currentNode = sourceResult.parent[sourceResult.index]
  sourceResult.parent.splice(sourceResult.index, 1)
  treeData.value = [...treeData.value]

  if (targetCode === null) {
    treeData.value = [...treeData.value, currentNode]
  } else {
    const targetKey = findNodeKeyByCode(treeData.value, targetCode)
    if (targetKey) {
      const targetResult = findNode(treeData.value, targetKey)
      if (targetResult) {
        targetResult.node.children = [...(targetResult.node.children ?? []), currentNode]
        treeData.value = [...treeData.value]
        return
      }
    }
    treeData.value = [...treeData.value, currentNode]
  }
}

/** 在树中查找指定 nodeKey 的父节点的 _nodeKey */
function findParentKey(nodes: PlanNode[], targetKey: string, parentKey: string | null, cb: (pk: string | null) => void): boolean {
  for (const n of nodes) {
    const nk = (n as PlanNode & { _nodeKey?: string })._nodeKey as string
    if (nk === targetKey) {
      cb(parentKey)
      return true
    }
    if (n.children?.length) {
      if (findParentKey(n.children, targetKey, nk, cb)) return true
    }
  }
  return false
}

/** 对外暴露：获取节点的父节点 nodeCode */
function getParentNodeCode(nodeKey: string): string | null {
  let pk: string | null = null
  findParentKey(treeData.value, nodeKey, null, (parentKey) => {
    if (parentKey === null) { pk = null; return }
    const parent = findNode(treeData.value, parentKey)
    pk = parent?.node.nodeCode ?? null
  })
  return pk
}

/** 通过 nodeCode 在树中查找 _nodeKey */
function findNodeKeyByCode(nodes: PlanNode[], code: string): string | null {
  for (const n of nodes) {
    if (n.nodeCode === code) return (n as PlanNode & { _nodeKey?: string })._nodeKey as string
    if (n.children?.length) {
      const found = findNodeKeyByCode(n.children, code)
      if (found) return found
    }
  }
  return null
}

/** 通过 _nodeKey 查找 nodeCode */
function getNodeCodeByKey(nodeKey: string): string | null {
  const result = findNode(treeData.value, nodeKey)
  return result?.node.nodeCode ?? null
}

defineExpose({ expandAll, collapseAll, addNode, updateNode, updateNodeAndMove, moveToParent, getParentNodeCode, getNodeCodeByKey, getCleanedNodes })

/* ===================== 表格行列判定 ===================== */

function isFirst(node: PlanNode): boolean {
  const key = (node as Record<string, unknown>)._nodeKey as string
  const result = findNode(treeData.value, key)
  return !result || result.index === 0
}

function isLast(node: PlanNode): boolean {
  const key = (node as Record<string, unknown>)._nodeKey as string
  const result = findNode(treeData.value, key)
  return !result || result.index === result.parent.length - 1
}

/* ===================== 增删移动操作（均为本地状态变更） ===================== */

type MoveDir = 'first' | 'up' | 'down' | 'last'

function moveNode(nodeKey: string, dir: MoveDir): void {
  const result = findNode(treeData.value, nodeKey)
  if (!result) return

  const { parent, index } = result
  const len = parent.length

  if ((dir === 'first' || dir === 'up') && index === 0) return
  if ((dir === 'last' || dir === 'down') && index === len - 1) return

  const [item] = parent.splice(index, 1)

  switch (dir) {
    case 'first':
      parent.unshift(item)
      break
    case 'up':
      parent.splice(index - 1, 0, item)
      break
    case 'down':
      // splice 删除后后续元素左移一位，当前位置即原 index+1 的新位置
      parent.splice(index + 1, 0, item)
      break
    case 'last':
      parent.push(item)
      break
  }

  treeData.value = [...treeData.value]
}

function removeNode(nodeKey: string): void {
  ElMessageBox.confirm('将同时删除所有子节点，确定继续吗？', '确认删除', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  })
    .then(() => {
      const result = findNode(treeData.value, nodeKey)
      if (result) {
        result.parent.splice(result.index, 1)
        treeData.value = [...treeData.value]
      }
    })
    .catch(() => {
      /* 用户取消 */
    })
}

/** 对外暴露：新增子节点 */
function addNode(parentKey: string | null, node: PlanNode): void {
  // 里程碑不允许添加子节点
  if (parentKey !== null) {
    const parentResult = findNode(treeData.value, parentKey)
    if (parentResult && parentResult.node.nodeType === 2) {
      return
    }
  }
  const newNode: PlanNode & { _nodeKey?: string } = {
    ...node,
    id: undefined,
    sortOrder: node.sortOrder ?? 0,
    predecessors: node.predecessors ?? [],
    deliverableCnt: node.deliverableCnt ?? 0,
    children: [],
    _nodeKey: generateKey()
  }

  if (parentKey === null) {
    treeData.value = [...treeData.value, newNode]
  } else {
    const result = findNode(treeData.value, parentKey)
    if (result) {
      result.node.children = [...(result.node.children ?? []), newNode]
      treeData.value = [...treeData.value]
    }
  }
}

/** 对外暴露：更新节点（保留已有 children 和 _nodeKey） */
function updateNode(nodeKey: string, data: PlanNode): void {
  const result = findNode(treeData.value, nodeKey)
  if (!result) return

  const merged: PlanNode & { _nodeKey?: string } = {
    ...data,
    children: result.node.children ?? []
  }
  merged._nodeKey = nodeKey
  result.parent[result.index] = merged as PlanNode
  treeData.value = [...treeData.value]
}

/** 对外暴露：合并更新节点数据 + 移动父节点（一次操作完成，避免两次触发watcher） */
function updateNodeAndMove(nodeKey: string, data: PlanNode, newParentCode: string | null): void {
  const sourceResult = findNode(treeData.value, nodeKey)
  if (!sourceResult) {
    return
  }

  const oldParentCode = getParentNodeCode(nodeKey)
  const targetCode = newParentCode || null

  // 先更新节点数据
  const merged: PlanNode & { _nodeKey?: string } = {
    ...data,
    children: sourceResult.node.children ?? []
  }
  merged._nodeKey = nodeKey
  sourceResult.parent[sourceResult.index] = merged as PlanNode

  // 如果父节点变了，移动节点
  if (oldParentCode !== targetCode) {
    const currentNode = sourceResult.parent.splice(sourceResult.index, 1)[0]
    if (!targetCode) {
      treeData.value = [...treeData.value, currentNode]
    } else {
      const targetKey = findNodeKeyByCode(treeData.value, targetCode)
      if (targetKey) {
        const targetResult = findNode(treeData.value, targetKey)
        if (targetResult) {
          targetResult.node.children = [...(targetResult.node.children ?? []), currentNode]
          treeData.value = [...treeData.value]
          return
        }
      }
      treeData.value = [...treeData.value, currentNode]
    }
  } else {
    treeData.value = [...treeData.value]
  }
}

/** 对外暴露：获取清理过内部 key 的节点数据（用于保存时提交） */
function getCleanedNodes(): PlanNode[] {
  return stripKeys(treeData.value)
}

/* ===================== 浮动操作菜单（延迟加载，只渲染一个实例） ===================== */

type DropdownCommand = 'addChild' | 'edit' | 'delete' | 'moveFirst' | 'moveUp' | 'moveDown' | 'moveLast'

const contextMenu = ref<{
  visible: boolean
  x: number
  y: number
  row: PlanNode | null
  rowKey: string | null
}>({ visible: false, x: 0, y: 0, row: null, rowKey: null })

function showContextMenu(row: PlanNode, event: MouseEvent) {
  const key = (row as Record<string, unknown>)._nodeKey as string
  contextMenu.value = {
    visible: true,
    x: event.clientX,
    y: event.clientY,
    row,
    rowKey: key
  }
}

function hideContextMenu() {
  contextMenu.value = { visible: false, x: 0, y: 0, row: null, rowKey: null }
}

function handleMenuCmd(cmd: DropdownCommand) {
  const row = contextMenu.value.row
  const key = contextMenu.value.rowKey
  if (!row || !key) return
  switch (cmd) {
    case 'addChild':
      emit('add-child', key)
      break
    case 'edit':
      emit('edit-node', row)
      break
    case 'delete':
      removeNode(key)
      break
    case 'moveFirst':
      moveNode(key, 'first')
      break
    case 'moveUp':
      moveNode(key, 'up')
      break
    case 'moveDown':
      moveNode(key, 'down')
      break
    case 'moveLast':
      moveNode(key, 'last')
      break
  }
  hideContextMenu()
}

// 按下 Esc 键关闭菜单
function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && contextMenu.value.visible) hideContextMenu()
}
onMounted(() => document.addEventListener('keydown', onKeydown))
onUnmounted(() => document.removeEventListener('keydown', onKeydown))
</script>

<template>
  <div class="plan-node-tree">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-button type="danger" size="small" @click="toggleExpandAll">
        {{ expandedKeys.length > 0 ? '全部收起' : '全部展开' }}
      </el-button>
      <el-button type="danger" size="small" @click="emit('add-child', null)" v-if="!readonly">
        + 新增节点任务
      </el-button>
    </div>

    <!-- 树表格 -->
    <el-table
      ref="elTableRef"
      :key="tableKey"
      :data="treeData"
      row-key="_nodeKey"
      :tree-props="{ children: 'children', hasChildren: 'hasChildren' }"
      :expand-row-keys="expandedKeys"
      @expand-change="handleExpandChange"
      border
      size="small"
      class="node-table"
      style="table-layout:auto"
    >
      <el-table-column label="序号" :width="seqColWidth">
        <template #default="{ row }">
          <span class="seq-cell">
            <el-icon :size="16" style="flex-shrink:0">
              <CircleCheck v-if="row.nodeType === 1" style="color:#409eff" />
              <Flag v-else style="color:#e74c3c" />
            </el-icon>
            {{ row.nodeCode || '-' }}
          </span>
        </template>
      </el-table-column>

      <el-table-column label="任务名称">
        <template #default="{ row }">
          <span class="node-name-cell">
            <el-icon :size="16" class="node-icon">
              <Connection v-if="row.children?.length" />
              <Document v-else />
            </el-icon>
            {{ row.nodeName }}
          </span>
        </template>
      </el-table-column>

      <el-table-column prop="deptName" label="部门" width="120" show-overflow-tooltip />

      <el-table-column prop="stdDuration" label="参考工期" width="100">
        <template #default="{ row }">
          {{ row.stdDuration ?? '-' }}
        </template>
      </el-table-column>

      <el-table-column label="任务类别" width="120">
        <template #default="{ row }">
          {{ (row.taskCategory && taskCategoryMap[row.taskCategory]) || row.taskCategory || '-' }}
        </template>
      </el-table-column>

      <el-table-column label="前置任务" width="220">
        <template #default="{ row }">
          {{ formatPredecessors(row.predecessors) || '-' }}
        </template>
      </el-table-column>

      <el-table-column label="操作" width="200" v-if="!readonly">
        <template #default="{ row }">
          <el-button size="small" @click.stop="showContextMenu(row, $event)">
            操作<el-icon style="margin-left: 4px"><ArrowDown /></el-icon>
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 浮动操作菜单（只渲染一个，点击时定位到按钮位置） -->
    <teleport to="body">
      <div v-if="contextMenu.visible" class="ctx-overlay" @click="hideContextMenu" @contextmenu.prevent="hideContextMenu" />
      <div v-if="contextMenu.visible" class="ctx-menu" :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }">
        <el-button text class="ctx-item" @click="handleMenuCmd('addChild')" :disabled="contextMenu.row?.nodeType === 2">新增子节点</el-button>
        <el-button text class="ctx-item" @click="handleMenuCmd('edit')">编辑</el-button>
        <div class="ctx-divider" />
        <el-button text class="ctx-item ctx-danger" @click="handleMenuCmd('delete')">删除</el-button>
        <div class="ctx-divider" />
        <el-button text class="ctx-item" @click="handleMenuCmd('moveFirst')" :disabled="contextMenu.row ? isFirst(contextMenu.row) : true">移至首位</el-button>
        <el-button text class="ctx-item" @click="handleMenuCmd('moveUp')" :disabled="contextMenu.row ? isFirst(contextMenu.row) : true">上移</el-button>
        <el-button text class="ctx-item" @click="handleMenuCmd('moveDown')" :disabled="contextMenu.row ? isLast(contextMenu.row) : true">下移</el-button>
        <el-button text class="ctx-item" @click="handleMenuCmd('moveLast')" :disabled="contextMenu.row ? isLast(contextMenu.row) : true">移至末位</el-button>
      </div>
    </teleport>
  </div>
</template>

<style scoped>
.plan-node-tree {
  margin-top: 16px;
}

.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 12px;
}

.node-table {
  width: 100%;
}

.node-name-cell,
.node-code-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.node-icon {
  flex-shrink: 0;
  color: #409eff;
}

.seq-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}
</style>

<style>
/* 浮动菜单（非 scoped，因 teleport 到 body） */
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
  min-width: 130px;
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
.ctx-menu .ctx-item.ctx-danger {
  color: #f56c6c;
}
.ctx-menu .ctx-item:disabled {
  color: #c0c4cc !important;
  cursor: not-allowed;
  background: transparent;
}
.ctx-divider {
  height: 1px;
  background: #e4e7ed;
  margin: 4px 0;
}
</style>
