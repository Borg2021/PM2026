<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { buildDeptTree, type DeptTreeNode } from '@/utils/deptTree'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getSystemUsers, createSystemUser, updateSystemUser, resetUserPassword, deleteSystemUser, getDepartmentList, getRbacRoles, getUserRoles, updateUserRoles, getFunctionList, searchUsers, updateDepartment } from '@/api/system'
import type { SystemUser, DepartmentItem, RbacRole, FunctionItem } from '@/types/system'

const tableData = ref<SystemUser[]>([])
const loading = ref(false)

/* 搜索 */
const searchForm = reactive({ keyword: '' })

const pagination = reactive({ page: 1, pageSize: 20, total: 0 })

const deptOptions = ref<DepartmentItem[]>([])

const deptTreeData = computed(() => buildDeptTree(deptOptions.value))

const rbacRoles = ref<RbacRole[]>([])
const functionList = ref<FunctionItem[]>([])

const rbacMultiOptions = computed(() =>
  rbacRoles.value.map(r => ({ value: r.id, label: r.name }))
)

const roleTagMap = computed(() => {
  const tagTypes = ['danger', 'warning', 'success', '']
  const map: Record<string, string> = {}
  rbacRoles.value.forEach((r, i) => {
    map[r.code] = tagTypes[i % tagTypes.length]
  })
  return map
})

const roleCodeToName = computed(() => {
  const map: Record<string, string> = {}
  rbacRoles.value.forEach(r => {
    map[r.code] = r.name
  })
  return map
})

// RBAC 角色名 → 标签颜色
const roleNameTagType = computed(() => {
  const tagTypes = ['danger', 'warning', 'success', '']
  const map: Record<string, string> = {}
  rbacRoles.value.forEach((r, i) => {
    map[r.name] = tagTypes[i % tagTypes.length]
  })
  return map
})

function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/* ──── 部门树选中 ──── */
const selectedDeptId = ref<number | null>(null)

/** 当前选中部门信息 */
const currentDeptInfo = computed(() => {
  if (!selectedDeptId.value) return null
  return deptOptions.value.find(d => d.id === selectedDeptId.value) || null
})

/* ──── 部门负责人 ──── */
const leaderDialogVisible = ref(false)
const leaderDialogLoading = ref(false)
const leaderSearchResults = ref<{ id: number; realName: string; username: string; departmentName: string | null }[]>([])
const leaderSelectedIds = ref<number[]>([])

/** 递归收集部门节点及其所有后代 ID */
function collectDeptIds(node: DeptTreeNode): number[] {
  const ids = [node.id]
  for (const child of node.children) {
    ids.push(...collectDeptIds(child))
  }
  return ids
}

function handleDeptClick(data: DeptTreeNode) {
  selectedDeptId.value = data.id
  pagination.page = 1
  fetchData()
}

function handleShowAll() {
  selectedDeptId.value = null
  pagination.page = 1
  fetchData()
}

/** 根据选中的部门，获取该部门及所有子部门的 ID 列表 */
function getSelectedDeptIds(): string | undefined {
  if (selectedDeptId.value === null) return undefined
  for (const root of deptTreeData.value) {
    const found = findNode(root, selectedDeptId.value)
    if (found) {
      return collectDeptIds(found).join(',')
    }
  }
  return undefined
}

function findNode(node: DeptTreeNode, id: number): DeptTreeNode | null {
  if (node.id === id) return node
  for (const child of node.children) {
    const found = findNode(child, id)
    if (found) return found
  }
  return null
}

/* 数据获取 */
function fetchData() {
  loading.value = true
  getSystemUsers({
    pageIndex: pagination.page,
    pageSize: pagination.pageSize,
    keyword: searchForm.keyword || undefined,
    departmentIds: getSelectedDeptIds()
  })
    .then(res => {
      tableData.value = res.data.items
      pagination.total = res.data.total
    })
    .finally(() => { loading.value = false })
}

function handleSearch() {
  pagination.page = 1
  fetchData()
}

function handleReset() {
  searchForm.keyword = ''
  pagination.page = 1
  fetchData()
}

function handleSizeChange(size: number) {
  pagination.pageSize = size
  pagination.page = 1
  fetchData()
}

function handleCurrentChange(page: number) {
  pagination.page = page
  fetchData()
}

/* 创建用户对话框 */
const createVisible = ref(false)
const createForm = reactive({ username: '', password: '', realName: '', departmentId: null as number | null, functionIds: [] as number[], rbacRoleIds: [] as number[] })
const createFormRef = ref()
const createRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  realName: [{ required: true, message: '请输入姓名', trigger: 'blur' }]
}

/* ──── 设置部门负责人 ──── */
function handleSetLeader() {
  if (!selectedDeptId.value) return
  const dept = currentDeptInfo.value
  leaderSelectedIds.value = dept?.leaders?.map(l => l.userId) ?? []
  // 将当前负责人预填到搜索结果中，确保 el-select 能显示姓名而非编号
  leaderSearchResults.value = (dept?.leaders ?? []).map(l => ({
    id: l.userId,
    realName: l.realName,
    username: '',
    departmentName: null as string | null
  }))
  leaderDialogVisible.value = true
}

async function handleLeaderSearch(query: string) {
  const kw = query.trim()
  if (!kw) { leaderSearchResults.value = []; return }
  leaderDialogLoading.value = true
  try {
    const res = await searchUsers(kw)
    leaderSearchResults.value = res.data ?? []
  } catch { leaderSearchResults.value = [] }
  finally { leaderDialogLoading.value = false }
}

async function handleLeaderConfirm() {
  if (!selectedDeptId.value) return
  const dept = currentDeptInfo.value
  if (!dept) return
  try {
    await updateDepartment(selectedDeptId.value, {
      name: dept.name,
      parentId: dept.parentId,
      sortOrder: dept.sortOrder,
      leaderIds: leaderSelectedIds.value
    })
    ElMessage.success('负责人设置成功')
    leaderDialogVisible.value = false
    await getDepartmentList().then(res => { deptOptions.value = res.data })
  } catch { /* 错误由拦截器处理 */ }
}

function handleCreate() {
  createForm.username = ''
  createForm.password = ''
  createForm.realName = ''
  createForm.departmentId = selectedDeptId.value
  createForm.functionIds = []
  createForm.rbacRoleIds = []
  createFormRef.value?.clearValidate()
  createVisible.value = true
}

function deriveRoleCode(roleIds: number[]): string {
  if (roleIds.length === 0) return 'user'
  const firstRole = rbacRoles.value.find(r => r.id === roleIds[0])
  return firstRole?.code || 'user'
}

async function submitCreate() {
  const valid = await createFormRef.value?.validate().catch(() => false)
  if (!valid) return
  const roleCode = deriveRoleCode(createForm.rbacRoleIds)
  const res = await createSystemUser({
    username: createForm.username.trim(),
    password: createForm.password,
    realName: createForm.realName.trim(),
    role: roleCode,
    departmentId: createForm.departmentId || null,
    functionIds: createForm.functionIds
  })
  if (createForm.rbacRoleIds.length > 0) {
    await updateUserRoles(res.data.id, { roleIds: createForm.rbacRoleIds })
  }
  ElMessage.success('创建成功')
  createVisible.value = false
  fetchData()
}

/* 编辑用户对话框 */
const editVisible = ref(false)
const editForm = reactive({ id: 0, realName: '', status: 1, departmentId: null as number | null, functionIds: [] as number[], rbacRoleIds: [] as number[] })
const editFormRef = ref()
const editRules = {
  realName: [{ required: true, message: '请输入姓名', trigger: 'blur' }]
}

async function handleEdit(row: SystemUser) {
  editForm.id = row.id
  editForm.realName = row.realName
  editForm.status = row.status
  editForm.departmentId = row.departmentId
  editForm.functionIds = [...row.functionIds]
  editForm.rbacRoleIds = []
  editFormRef.value?.clearValidate()
  editVisible.value = true
  try {
    const res = await getUserRoles(row.id)
    editForm.rbacRoleIds = res.data || []
  } catch { /* ignore */ }
}

async function submitEdit() {
  const valid = await editFormRef.value?.validate().catch(() => false)
  if (!valid) return
  const roleCode = deriveRoleCode(editForm.rbacRoleIds)
  await updateSystemUser(editForm.id, {
    realName: editForm.realName.trim(),
    role: roleCode,
    status: editForm.status,
    departmentId: editForm.departmentId || null,
    functionIds: editForm.functionIds
  })
  await updateUserRoles(editForm.id, { roleIds: editForm.rbacRoleIds })
  ElMessage.success('更新成功')
  editVisible.value = false
  fetchData()
}

/* 重置密码对话框 */
const pwdVisible = ref(false)
const pwdForm = reactive({ id: 0, newPassword: '' })
const pwdFormRef = ref()
const pwdRules = {
  newPassword: [{ required: true, message: '请输入新密码', trigger: 'blur' }]
}

function handleResetPwd(row: SystemUser) {
  pwdForm.id = row.id
  pwdForm.newPassword = ''
  pwdFormRef.value?.clearValidate()
  pwdVisible.value = true
}

async function submitResetPwd() {
  const valid = await pwdFormRef.value?.validate().catch(() => false)
  if (!valid) return
  await resetUserPassword(pwdForm.id, { newPassword: pwdForm.newPassword })
  ElMessage.success('密码重置成功')
  pwdVisible.value = false
}

/* 删除 */
async function handleDelete(row: SystemUser) {
  try {
    await ElMessageBox.confirm(`确定要删除用户「${row.realName}(${row.username})」吗？`, '确认删除', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteSystemUser(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消或错误 */ }
}

/* 状态切换 */
async function handleStatusChange(row: SystemUser) {
  await updateSystemUser(row.id, {
    realName: row.realName,
    role: row.role,
    status: row.status,
    departmentId: row.departmentId,
    functionIds: row.functionIds
  })
  ElMessage.success(row.status === 1 ? '已启用' : '已停用')
}

onMounted(() => {
  fetchData()
  getDepartmentList().then(res => { deptOptions.value = res.data })
  getRbacRoles().then(res => { rbacRoles.value = res.data })
  getFunctionList().then(res => { functionList.value = res.data })
})
</script>

<template>
  <div class="page-container">
    <h2>人员管理</h2>

    <div class="dept-user-layout">
      <!-- 左侧部门树 -->
      <el-card class="dept-tree-card" shadow="never">
        <template #header>
          <div class="dept-tree-header">
            <span class="dept-tree-title">部门结构</span>
          </div>
        </template>
        <div class="dept-tree-wrapper">
          <div
            class="dept-all-item"
            :class="{ active: selectedDeptId === null }"
            @click="handleShowAll"
          >
            <svg class="dept-all-icon" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>
            <span>全部部门</span>
          </div>
          <el-tree
            :data="deptTreeData"
            :props="{ label: 'name', children: 'children' }"
            node-key="id"
            default-expand-all
            highlight-current
            :current-node-key="selectedDeptId"
            @node-click="handleDeptClick"
          />
        </div>
      </el-card>

      <!-- 右侧用户列表 -->
      <div class="user-content">
        <!-- 搜索 -->
        <el-card class="search-card" shadow="never">
          <el-form :model="searchForm" label-width="80px" inline>
            <el-form-item label="关键字">
              <el-input v-model="searchForm.keyword" placeholder="用户名 / 姓名" clearable style="width: 240px" />
            </el-form-item>
            <el-form-item label-width="0">
              <el-button type="danger" @click="handleSearch">搜索</el-button>
              <el-button @click="handleReset">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <!-- 工具栏 -->
        <div class="toolbar">
          <div class="toolbar-info">
            当前部门：
            <strong>{{ selectedDeptId ? deptOptions.find(d => d.id === selectedDeptId)?.name || '未知' : '全部部门' }}</strong>
            <template v-if="currentDeptInfo?.leaders?.length">
              <span style="margin-left: 12px; color: #909399;">|</span>
              <span style="margin-left: 12px;">负责人：</span>
              <el-tag v-for="l in currentDeptInfo!.leaders" :key="l.userId" type="warning" size="small" style="margin-right: 4px;">{{ l.realName }}</el-tag>
            </template>
          </div>
          <div style="display: flex; gap: 8px;">
            <el-button v-if="selectedDeptId" @click="handleSetLeader">设置负责人</el-button>
            <el-button type="danger" @click="handleCreate">+ 新建用户</el-button>
          </div>
        </div>

        <!-- 表格 -->
        <el-card class="table-card" shadow="never">
          <el-table :data="tableData" v-loading="loading" stripe border style="width: 100%">
            <el-table-column prop="id" label="ID" width="70" />
            <el-table-column prop="username" label="用户名" width="130" show-overflow-tooltip />
            <el-table-column prop="realName" label="姓名" width="100" show-overflow-tooltip />
            <el-table-column label="系统角色" width="156">
              <template #default="{ row }">
                <div style="white-space: nowrap;">
                <template v-if="row.rbacRoleNames?.length">
                  <el-tag
                    v-for="name in row.rbacRoleNames"
                    :key="name"
                    :type="roleNameTagType[name] || 'info'"
                    size="small"
                    style="margin-right: 4px;"
                  >{{ name }}</el-tag>
                </template>
                <span v-else style="color: #909399;">—</span>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="部门" width="130" show-overflow-tooltip>
              <template #default="{ row }">
                {{ row.departmentName || '—' }}
              </template>
            </el-table-column>
            <el-table-column label="职能" width="160" show-overflow-tooltip>
              <template #default="{ row }">
                {{ row.functionNames || '—' }}
              </template>
            </el-table-column>
            <el-table-column label="状态" width="90">
              <template #default="{ row }">
                <el-switch
                  :model-value="row.status === 1"
                  active-text="启用"
                  inactive-text="停用"
                  inline-prompt
                  size="small"
                  @change="(val: boolean) => { row.status = val ? 1 : 0; handleStatusChange(row) }"
                />
              </template>
            </el-table-column>
            <el-table-column label="创建时间" width="130">
              <template #default="{ row }">
                {{ formatDate(row.createdAt) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <el-button link style="color: #67c23a" @click="handleEdit(row)">编辑</el-button>
                <el-button link style="color: #409eff" @click="handleResetPwd(row)">重置密码</el-button>
                <el-button link style="color: #f56c6c" @click="handleDelete(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>

          <div class="pagination-wrapper">
            <el-pagination
              v-model:current-page="pagination.page"
              v-model:page-size="pagination.pageSize"
              :page-sizes="[10, 20, 50]"
              :total="pagination.total"
              layout="total, sizes, prev, pager, next, jumper"
              background
              @size-change="handleSizeChange"
              @current-change="handleCurrentChange"
            />
          </div>
        </el-card>
      </div>
    </div>

    <!-- 新建用户对话框 -->
    <el-dialog
      :model-value="createVisible"
      @update:model-value="createVisible = $event"
      title="新建用户"
      width="440px"
      :close-on-click-modal="false"
    >
      <el-form ref="createFormRef" :model="createForm" :rules="createRules" label-width="80px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="createForm.username" placeholder="登录账号" maxlength="50" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="createForm.password" type="password" placeholder="登录密码" maxlength="50" show-password />
        </el-form-item>
        <el-form-item label="姓名" prop="realName">
          <el-input v-model="createForm.realName" placeholder="真实姓名" maxlength="50" />
        </el-form-item>
        <el-form-item label="系统角色">
          <el-select v-model="createForm.rbacRoleIds" multiple placeholder="请选择系统角色" style="width: 100%">
            <el-option v-for="r in rbacMultiOptions" :key="r.value" :label="r.label" :value="r.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="部门">
          <el-tree-select
            v-model="createForm.departmentId"
            :data="deptTreeData"
            :props="{ label: 'name', value: 'id' }"
            node-key="id"
            placeholder="选择部门（选填）"
            clearable
            check-strictly
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="职能">
          <el-select v-model="createForm.functionIds" multiple placeholder="选择职能（选填）" clearable style="width: 100%">
            <el-option v-for="f in functionList" :key="f.id" :label="f.name" :value="f.id" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="createVisible = false">取消</el-button>
        <el-button type="danger" @click="submitCreate">确定</el-button>
      </template>
    </el-dialog>

    <!-- 编辑用户对话框 -->
    <el-dialog
      :model-value="editVisible"
      @update:model-value="editVisible = $event"
      title="编辑用户"
      width="440px"
      :close-on-click-modal="false"
    >
      <el-form ref="editFormRef" :model="editForm" :rules="editRules" label-width="80px">
        <el-form-item label="姓名" prop="realName">
          <el-input v-model="editForm.realName" placeholder="真实姓名" maxlength="50" />
        </el-form-item>
        <el-form-item label="系统角色">
          <el-select v-model="editForm.rbacRoleIds" multiple placeholder="请选择系统角色" style="width: 100%">
            <el-option v-for="r in rbacMultiOptions" :key="r.value" :label="r.label" :value="r.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-switch
            v-model="editForm.status"
            :active-value="1"
            :inactive-value="0"
            active-text="启用"
            inactive-text="停用"
          />
        </el-form-item>
        <el-form-item label="部门">
          <el-tree-select
            v-model="editForm.departmentId"
            :data="deptTreeData"
            :props="{ label: 'name', value: 'id' }"
            node-key="id"
            placeholder="选择部门（选填）"
            clearable
            check-strictly
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="职能">
          <el-select v-model="editForm.functionIds" multiple placeholder="选择职能（选填）" clearable style="width: 100%">
            <el-option v-for="f in functionList" :key="f.id" :label="f.name" :value="f.id" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="danger" @click="submitEdit">确定</el-button>
      </template>
    </el-dialog>

    <!-- 重置密码对话框 -->
    <el-dialog
      :model-value="pwdVisible"
      @update:model-value="pwdVisible = $event"
      title="重置密码"
      width="400px"
      :close-on-click-modal="false"
    >
      <el-form ref="pwdFormRef" :model="pwdForm" :rules="pwdRules" label-width="80px">
        <el-form-item label="新密码" prop="newPassword">
          <el-input v-model="pwdForm.newPassword" type="password" placeholder="请输入新密码" maxlength="50" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdVisible = false">取消</el-button>
        <el-button type="danger" @click="submitResetPwd">确定</el-button>
      </template>
    </el-dialog>

    <!-- 设置部门负责人对话框 -->
    <el-dialog
      v-model="leaderDialogVisible"
      title="设置部门负责人（多选）"
      width="500px"
      :close-on-click-modal="false"
    >
      <div style="margin-bottom: 12px; color: #606266;">
        当前部门：<strong>{{ currentDeptInfo?.name }}</strong>
      </div>
      <el-select
        v-model="leaderSelectedIds"
        multiple
        filterable
        remote
        reserve-keyword
        placeholder="搜索用户（姓名 / 用户名）"
        :remote-method="handleLeaderSearch"
        :loading="leaderDialogLoading"
        style="width: 100%"
      >
        <el-option
          v-for="u in leaderSearchResults"
          :key="u.id"
          :label="u.username ? `${u.realName} (${u.username})` : u.realName"
          :value="u.id"
        />
      </el-select>
      <template #footer>
        <el-button @click="leaderDialogVisible = false">取消</el-button>
        <el-button type="danger" @click="handleLeaderConfirm">确定</el-button>
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

.dept-user-layout {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  flex: 1;
  overflow: hidden;
  min-height: 0;
}

/* 左侧部门树 */
.dept-tree-card {
  width: 312px;
  min-width: 312px;
  flex-shrink: 0;
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.dept-tree-card :deep(.el-card__header) {
  padding: 10px 16px;
  border-bottom: 1px solid #ebeef5;
}
.dept-tree-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.dept-tree-title {
  font-weight: 600;
  font-size: 14px;
  color: #303133;
}
.dept-tree-wrapper {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}
.dept-tree-wrapper :deep(.el-tree-node__content) {
  height: 36px;
}
.dept-all-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  cursor: pointer;
  border-radius: 4px;
  font-size: 14px;
  color: #606266;
  transition: all 0.15s;
  margin-bottom: 4px;
}
.dept-all-item:hover {
  background-color: #f5f7fa;
  color: #409eff;
}
.dept-all-item.active {
  background-color: #ecf5ff;
  color: #409eff;
  font-weight: 600;
}
.dept-all-icon {
  flex-shrink: 0;
}

/* 右侧用户内容 */
.user-content {
  flex: 1;
  min-width: 0;
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.search-card {
  margin-bottom: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.toolbar-info {
  font-size: 14px;
  color: #606266;
}
.toolbar-info strong {
  color: #303133;
}
.table-card {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
  margin-bottom: 0;
}
.table-card :deep(.el-card__body) {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
}
/* 让 el-table 撑满卡片内部，表头固定 + body 滚动 */
.table-card :deep(.el-table) {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}
.table-card :deep(.el-table__inner-wrapper) {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}
.table-card :deep(.el-table__header-wrapper) {
  flex-shrink: 0;
}
.table-card :deep(.el-table__body-wrapper) {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}
.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  border-top: 1px solid #ebeef5;
}
</style>
