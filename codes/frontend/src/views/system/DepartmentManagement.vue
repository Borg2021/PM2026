<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getDepartmentList, createDepartment, updateDepartment, deleteDepartment } from '@/api/system'
import type { DepartmentItem, DepartmentTreeNode } from '@/types/system'

const flatList = ref<DepartmentItem[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)

const form = reactive({ name: '', parentId: null as number | null, sortOrder: 0 })
const formRules = {
  name: [{ required: true, message: '请输入部门名称', trigger: 'blur' }]
}
const formRef = ref()

function buildTree(items: DepartmentItem[]): DepartmentTreeNode[] {
  const map = new Map<number, DepartmentTreeNode>()
  const roots: DepartmentTreeNode[] = []
  for (const d of items) map.set(d.id, { ...d, children: [] })
  for (const d of items) {
    const node = map.get(d.id)!
    if (d.parentId && map.has(d.parentId)) {
      map.get(d.parentId)!.children.push(node)
    } else {
      roots.push(node)
    }
  }
  return roots
}

const treeData = computed(() => buildTree(flatList.value))

function fetchData() {
  loading.value = true
  getDepartmentList()
    .then(res => { flatList.value = res.data })
    .finally(() => { loading.value = false })
}

/* 收集某个节点的所有子孙节点 ID */
function collectDescendants(id: number, items: DepartmentItem[]): Set<number> {
  const children = items.filter(d => d.parentId === id)
  const result = new Set<number>(children.map(c => c.id))
  for (const c of children) {
    for (const dId of collectDescendants(c.id, items)) result.add(dId)
  }
  return result
}

/* 上级部门树：编辑时排除自身及子孙 */
const parentTreeData = computed(() => {
  if (!editingId.value) return treeData.value
  const exclude = new Set<number>([editingId.value, ...collectDescendants(editingId.value, flatList.value)])
  function filterTree(nodes: DepartmentTreeNode[]): DepartmentTreeNode[] {
    return nodes
      .filter(n => !exclude.has(n.id))
      .map(n => ({ ...n, children: filterTree(n.children) }))
  }
  return filterTree(treeData.value)
})

function handleCreate() {
  editingId.value = null
  form.name = ''
  form.parentId = null
  form.sortOrder = 0
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function handleEdit(row: DepartmentItem) {
  editingId.value = row.id
  form.name = row.name
  form.parentId = row.parentId
  form.sortOrder = row.sortOrder
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function handleDelete(row: DepartmentItem) {
  try {
    await ElMessageBox.confirm(`确定要删除部门「${row.name}」吗？`, '确认删除', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteDepartment(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消或错误 */ }
}

async function submit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  const data = {
    name: form.name.trim(),
    parentId: form.parentId || null,
    sortOrder: form.sortOrder
  }

  if (editingId.value) {
    await updateDepartment(editingId.value, data)
    ElMessage.success('更新成功')
  } else {
    await createDepartment(data)
    ElMessage.success('创建成功')
  }
  dialogVisible.value = false
  fetchData()
}

onMounted(() => { fetchData() })
</script>

<template>
  <div class="page-container">
    <h2>部门管理</h2>

    <div class="toolbar">
      <div />
      <el-button type="danger" @click="handleCreate">+ 新建部门</el-button>
    </div>

      <el-table
        :data="treeData"
        v-loading="loading"
        row-key="id"
        :tree-props="{ children: 'children' }"
        default-expand-all
        stripe
        border
        style="width: 100%"
      >
      <el-table-column prop="name" label="部门名称" min-width="200" show-overflow-tooltip />
      <el-table-column prop="sortOrder" label="排序号" width="100" align="center" />
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link style="color: #67c23a" @click="handleEdit(row)">编辑</el-button>
          <el-button link style="color: #f56c6c" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog
      :model-value="dialogVisible"
      @update:model-value="dialogVisible = $event"
      :title="editingId ? '编辑部门' : '新建部门'"
      width="480px"
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="90px">
        <el-form-item label="部门名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入部门名称" maxlength="50" />
        </el-form-item>
        <el-form-item label="上级部门">
          <el-tree-select
            v-model="form.parentId"
            :data="parentTreeData"
            :props="{ label: 'name', value: 'id' }"
            node-key="id"
            placeholder="无（顶级部门）"
            clearable
            check-strictly
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="排序号">
          <el-input-number v-model="form.sortOrder" :min="0" :max="9999" style="width: 160px" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="danger" @click="submit">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-container {
  padding: 24px;
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.page-container h2 {
  margin: 0 0 20px;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
/* 让 el-table 撑满剩余空间，表头固定 + body 滚动 */
.page-container :deep(.el-table) {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}
.page-container :deep(.el-table__inner-wrapper) {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}
.page-container :deep(.el-table__header-wrapper) {
  flex-shrink: 0;
}
.page-container :deep(.el-table__body-wrapper) {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}
</style>
