<script setup lang="ts">
import { ref, reactive, onMounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getRbacRoles, createRbacRole, updateRbacRole, deleteRbacRole, getPermissionTree, getRolePermissions, updateRolePermissions } from '@/api/system'
import type { RbacRole, PermissionNode } from '@/types/system'

const roles = ref<RbacRole[]>([])
const selectedRoleId = ref<number | null>(null)
const permTree = ref<PermissionNode[]>([])
const checkedPermIds = ref<number[]>([])
const loadingRoles = ref(false)
const loadingPerms = ref(false)

/* 角色 CRUD */
const roleDialogVisible = ref(false)
const editingRole = ref<RbacRole | null>(null)
const DATA_SCOPE_OPTIONS = [
  { value: 1, label: '全部数据' },
  { value: 2, label: '本部门' },
  { value: 3, label: '本部门及下级' },
  { value: 4, label: '仅本人相关' },
  { value: 5, label: '仅项目成员' },
  { value: 6, label: '仅本人负责的项目' }
]

const roleForm = reactive({ name: '', code: '', description: '', dataScope: 5 })
const roleFormRef = ref()
const roleRules = {
  name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入角色编码', trigger: 'blur' }]
}

function fetchRoles() {
  loadingRoles.value = true
  getRbacRoles()
    .then(res => { roles.value = res.data })
    .finally(() => { loadingRoles.value = false })
}

function fetchPermTree() {
  loadingPerms.value = true
  getPermissionTree()
    .then(res => { permTree.value = res.data })
    .finally(() => { loadingPerms.value = false })
}

const treeRef = ref()
const selectedRoleName = ref('')

async function selectRole(role: RbacRole) {
  selectedRoleId.value = role.id
  selectedRoleName.value = role.name
  const res = await getRolePermissions(role.id)
  checkedPermIds.value = res.data
  await nextTick()
  treeRef.value?.setCheckedKeys(res.data)
}

function handleCreateRole() {
  editingRole.value = null
  roleForm.name = ''
  roleForm.code = ''
  roleForm.description = ''
  roleForm.dataScope = 5
  roleFormRef.value?.clearValidate()
  roleDialogVisible.value = true
}

function handleEditRole(row: RbacRole) {
  editingRole.value = row
  roleForm.name = row.name
  roleForm.code = row.code
  roleForm.description = row.description || ''
  roleForm.dataScope = row.dataScope ?? 5
  roleFormRef.value?.clearValidate()
  roleDialogVisible.value = true
}

async function handleDeleteRole(row: RbacRole) {
  try {
    await ElMessageBox.confirm(`确定要删除角色「${row.name}」吗？`, '确认删除', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteRbacRole(row.id)
    ElMessage.success('删除成功')
    if (selectedRoleId.value === row.id) selectedRoleId.value = null
    fetchRoles()
  } catch { /* 取消 */ }
}

async function submitRole() {
  const valid = await roleFormRef.value?.validate().catch(() => false)
  if (!valid) return
  if (editingRole.value) {
    await updateRbacRole(editingRole.value.id, {
      name: roleForm.name.trim(),
      code: roleForm.code.trim(),
      description: roleForm.description.trim(),
      dataScope: roleForm.dataScope
    })
    ElMessage.success('更新成功')
  } else {
    await createRbacRole({
      name: roleForm.name.trim(),
      code: roleForm.code.trim(),
      description: roleForm.description.trim(),
      dataScope: roleForm.dataScope
    })
    ElMessage.success('创建成功')
  }
  roleDialogVisible.value = false
  fetchRoles()
}

async function handleSavePerms() {
  if (selectedRoleId.value == null) return
  const leafIds = getLeafNodeIds(permTree.value, checkedPermIds.value)
  await updateRolePermissions(selectedRoleId.value, { permissionIds: leafIds })
  ElMessage.success('权限保存成功')
}

function getLeafNodeIds(tree: PermissionNode[], checked: number[]): number[] {
  const checkedSet = new Set(checked)
  const result: number[] = []
  function walk(nodes: PermissionNode[]) {
    for (const node of nodes) {
      const hasChildren = node.children && node.children.length > 0
      if (!hasChildren && checkedSet.has(node.id)) {
        result.push(node.id)
      }
      if (node.children) walk(node.children)
    }
  }
  walk(tree)
  return result
}

onMounted(() => {
  fetchRoles()
  fetchPermTree()
})
</script>

<template>
  <div class="page-container">
    <h2>权限管理</h2>

    <div class="perm-layout">
      <!-- 左侧：系统角色列表 -->
      <div class="left-panel">
        <div class="panel-toolbar">
          <span class="panel-title">系统角色</span>
          <el-button type="danger" size="small" @click="handleCreateRole">+ 新建</el-button>
        </div>
        <div class="role-list" v-loading="loadingRoles">
          <div
            v-for="r in roles"
            :key="r.id"
            :class="['role-item', { active: selectedRoleId === r.id }]"
            @click="selectRole(r)"
          >
            <div class="role-info">
              <span class="role-name">{{ r.name }}</span>
              <span class="role-code">{{ r.code }} · {{ DATA_SCOPE_OPTIONS.find(o => o.value === (r.dataScope ?? 5))?.label ?? '仅项目成员' }}</span>
            </div>
            <div class="role-actions">
              <el-button link size="small" @click.stop="handleEditRole(r)">编辑</el-button>
              <el-button link size="small" style="color: #f56c6c" @click.stop="handleDeleteRole(r)">删除</el-button>
            </div>
          </div>
          <el-empty v-if="!loadingRoles && roles.length === 0" description="暂无角色" :image-size="60" />
        </div>
      </div>

      <!-- 右侧：权限树 -->
      <div class="right-panel">
        <div class="panel-toolbar">
          <span class="panel-title">
            权限分配
            <template v-if="selectedRoleName">
              — <span style="color: #f56c6c">{{ selectedRoleName }}</span>
            </template>
          </span>
          <el-button
            type="danger"
            size="small"
            :disabled="selectedRoleId == null"
            @click="handleSavePerms"
          >
            保存权限
          </el-button>
        </div>
        <div class="perm-tree-wrapper" v-loading="loadingPerms">
          <el-empty v-if="selectedRoleId == null" description="请在左侧选择一个角色" :image-size="80" />
          <el-tree
            v-else
            ref="treeRef"
            :data="permTree"
            show-checkbox
            node-key="id"
            :props="{ label: 'name', children: 'children' }"
            default-expand-all
            @check="(_node: any, data: any) => { checkedPermIds = data.checkedKeys }"
          />
        </div>
      </div>
    </div>

    <!-- 角色对话框 -->
    <el-dialog
      :model-value="roleDialogVisible"
      @update:model-value="roleDialogVisible = $event"
      :title="editingRole ? '编辑角色' : '新建角色'"
      width="440px"
      :close-on-click-modal="false"
    >
      <el-form ref="roleFormRef" :model="roleForm" :rules="roleRules" label-width="80px">
        <el-form-item label="角色名称" prop="name">
          <el-input v-model="roleForm.name" placeholder="角色名称" maxlength="50" />
        </el-form-item>
        <el-form-item label="角色编码" prop="code">
          <el-input v-model="roleForm.code" placeholder="角色编码（英文）" maxlength="50" />
        </el-form-item>
        <el-form-item label="数据范围">
          <el-select v-model="roleForm.dataScope" style="width: 100%">
            <el-option v-for="opt in DATA_SCOPE_OPTIONS" :key="opt.value" :label="opt.label" :value="opt.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="roleForm.description" type="textarea" placeholder="角色描述（选填）" maxlength="200" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="roleDialogVisible = false">取消</el-button>
        <el-button type="danger" @click="submitRole">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-container {
  padding: 24px;
}
.page-container h2 {
  margin: 0 0 20px;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}

.perm-layout {
  display: flex;
  gap: 16px;
  height: calc(100vh - 140px);
}

.left-panel {
  width: 384px;
  flex-shrink: 0;
  background: #fff;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.right-panel {
  flex: 1;
  background: #fff;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.panel-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid #ebeef5;
}

.panel-title {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.role-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.role-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.2s;
}
.role-item:hover {
  background: #f5f7fa;
}
.role-item.active {
  background: #fef0f0;
  border-left: 3px solid #f56c6c;
}

.role-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.role-name {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}
.role-code {
  font-size: 12px;
  color: #909399;
}

.perm-tree-wrapper {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}
</style>
