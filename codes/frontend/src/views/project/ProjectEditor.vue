<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useAuthStore } from '@/store/auth'
import {
  getProjectDetail, createProject, updateProject,
  activateProject, completeProject, suspendProject, resumeProject, deactivateProject,
  saveProjectMembers,
  getProjectTasks, createProjectTask, updateProjectTask, deleteProjectTask, createTasksFromTemplate,
} from '@/api/project'
import { getDepartments, searchUsers, getDictByType, getTemplateList, getTemplateDetail } from '@/api/template'
import { getSysParamByKey, getFunctionList } from '@/api/system'
import { formatPreTaskCodes, parsePreTaskCodes, serializePreTaskCodes } from '@/utils/preTaskHelpers'
import { buildDeptTree } from '@/utils/deptTree'
import { taskStatusOptions, taskPriorityOptions, overdueStatus, statusLabel as taskStatusLabel, priorityLabel } from '@/utils/taskConstants'
import ProjectFileTab from './components/ProjectFileTab.vue'
import ProjectChangeTab from './components/ProjectChangeTab.vue'
import ProjectFinanceTab from './components/ProjectFinanceTab.vue'
import ProjectOperationLogTab from './components/ProjectOperationLogTab.vue'
import type {
  ProjectDetail, ProductItem, ProjectMemberItem,
  ProjectTaskItem
} from '@/types/project'
import type { Department, UserInfo, Template } from '@/types/template'
import type { FunctionItem } from '@/types/system'

/* ───────── 路由参数 ───────── */
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const mode = computed<'create' | 'edit' | 'view'>(() => {
  if (route.path.includes('/create')) return 'create'
  if (route.path.includes('/edit/')) return 'edit'
  return 'view'
})
const projectId = computed(() => route.params.id ? Number(route.params.id) : null)
const isReadonly = computed(() => mode.value === 'view')
/** 已激活/暂停/完成的项目不允许修改任务计划（只有未激活状态可操作） */
const isTaskLocked = computed(() => !isReadonly.value && form.status !== 0)
const hasFieldPerm = (code: string) => authStore.hasPermission(`project:field:${code}`) || authStore.hasPermission('project:field:basic')
const canViewFileTab = computed(() =>
  authStore.hasPermission('project:file:view:public') ||
  authStore.hasPermission('project:file:view:nonpublic')
)
const pageTitle = computed(() => mode.value === 'create' ? '新建项目' : mode.value === 'edit' ? '编辑项目' : '查看项目')

/* ───────── Tab ───────── */
const activeTab = ref('basic')
const filesTabMounted = ref(false)  // 首次切到文件 Tab 后设为 true，避免组件销毁重建（见 onTabChange）

/* ───────── 辅助数据 ───────── */
const departments = ref<Department[]>([])
const functions = ref<FunctionItem[]>([])
const users = ref<UserInfo[]>([])
const taskNoRule = ref('')

/** 项目基本信息选人：按用户职能筛选（见 docs/业务逻辑.md） */
const projectManagerOptions = ref<UserInfo[]>([])
const salesManagerOptions = ref<UserInfo[]>([])
const preSalesOptions = ref<UserInfo[]>([])

function pickStaffName(id: number | undefined, candidates: UserInfo[]): string {
  if (id == null) return ''
  return candidates.find(u => u.id === id)?.realName
    ?? users.value.find(u => u.id === id)?.realName
    ?? ''
}

async function loadStaffOptionsByFunction(functionList: FunctionItem[]) {
  const fnId = (name: string) => functionList.find(f => f.name === name)?.id
  const load = async (name: string) => {
    const id = fnId(name)
    if (!id) return [] as UserInfo[]
    const res = await searchUsers('', undefined, id)
    return res.data ?? []
  }
  const [pm, sales, pre] = await Promise.all([
    load('项目经理'),
    load('销售'),
    load('售前'),
  ])
  projectManagerOptions.value = pm
  salesManagerOptions.value = sales
  preSalesOptions.value = pre
}

function ensureStaffOption(
  id: number | undefined,
  name: string | undefined,
  list: typeof projectManagerOptions
) {
  if (!id || list.value.some(u => u.id === id)) return
  list.value.push({ id, realName: name || `用户#${id}`, username: '' })
}

function syncStaffOptionsFromForm() {
  ensureStaffOption(form.projectManagerId, form.projectManagerName, projectManagerOptions)
  ensureStaffOption(form.salesManagerId, form.salesManagerName, salesManagerOptions)
  ensureStaffOption(form.preSalesManagerId, form.preSalesManagerName, preSalesOptions)
}

/** 创建模式下，当前用户是否为项目经理（按职能匹配）→ 自动填充并禁用项目经理选择 */
const isPmCreator = computed(() =>
  mode.value === 'create' && projectManagerOptions.value.some(u => u.id === authStore.userId)
)

/** 当前用户是否就是该项目的项目经理 → 不可修改项目经理字段（系统管理员/项目管理员除外） */
const isCurrentUserPm = computed(() => {
  const adminRoles = ['admin', 'project_admin']
  if (adminRoles.includes(authStore.role)) return false
  return form.projectManagerId === authStore.userId
})

/** 通过模板导入后，保护基本信息中已设置的成员不被修改/删除 */

/** 判断成员行是否被锁定（不可修改/删除）—— 项目经理、销售、售前始终锁定 */
function isMemberLocked(row: { memberId?: number | null }): boolean {
  if (row.memberId == null) return false
  return (
    row.memberId === form.projectManagerId ||
    row.memberId === form.salesManagerId ||
    row.memberId === form.preSalesManagerId
  )
}

const dictMap = ref<Record<string, { id: number; dictCode: string; dictLabel: string }[]>>({})

function getDictLabel(dictType: string, code: string | undefined): string {
  if (!code) return ''
  return dictMap.value[dictType]?.find(d => d.dictCode === code)?.dictLabel ?? code
}

const deptTreeData = computed(() => buildDeptTree(departments.value))

const uploadedFileIds = ref<number[]>([])

/* ───────── 表单数据 ───────── */
const formRef = ref()
const loading = ref(false)
const saving = ref(false)
const form = reactive<Partial<ProjectDetail>>({
  projectCode: '',
  projectName: '',
  projectType: '',
  contractCode: '',
  engineeringCenter: '',
  categoryCode: '',
  customerName: '',
  regionalManagerName: '',
  customerContactPhone: '',
  customerContactEmail: '',
  salesManagerName: '',
  salesManagerId: undefined,
  preSalesManagerId: undefined,
  preSalesManagerName: '',
  salesRegion: '',
  projectManagerName: '',
  projectManagerId: undefined,
  pmCenter: '',
  ownerContactPhone: '',
  businessContactEmail: '',
  planStartDate: undefined,
  requiredDelivery: undefined,
  acceptedDelivery: undefined,
  actualFinishDate: undefined,
  deliveryLocation: '',
  finalCustomer: '',
  projectScope: '',
  specialTerms: '',
  remark: '',
  qualityStrategy: '',
  projectDelivery: '',
  reportContent: '',
  riskStatus: '',
  currentPhaseDate: undefined,
  nextStatus: '',
  progressDesc: '',
  status: 0,
  canManageStatus: false,
  canDeactivate: false,
  engineeringCenterId: null,
  products: [],
  members: [],
})

const rules = {
  projectCode: [{ required: true, message: '请输入项目编号', trigger: 'blur' }],
  projectName: [{ required: true, message: '请输入项目名称', trigger: 'blur' }],
  contractCode: [{ required: true, message: '请输入客户合同编号', trigger: 'blur' }],
  categoryCode: [{ required: true, message: '请输入项目地点(省、市)', trigger: 'blur' }],
  customerName: [{ required: true, message: '请输入客户名称', trigger: 'blur' }],
  regionalManagerName: [{ required: true, message: '请输入客户联系人', trigger: 'blur' }],
  customerContactPhone: [{ required: true, message: '请输入客户联系电话', trigger: 'blur' }],
  customerContactEmail: [{ required: true, message: '请输入客户联系邮箱', trigger: 'blur' }],
  finalCustomer: [{ required: true, message: '请输入最终业主', trigger: 'blur' }],
  pmCenter: [{ required: true, message: '请输入业主联系人', trigger: 'blur' }],
  ownerContactPhone: [{ required: true, message: '请输入业主联系电话', trigger: 'blur' }],
  businessContactEmail: [{ required: true, message: '请输入业主联系邮箱', trigger: 'blur' }],
  deliveryLocation: [{ required: true, message: '请输入交付详细地址', trigger: 'blur' }],
  projectType: [{ required: true, message: '请选择项目类型', trigger: 'change' }],
  engineeringCenterId: [{ required: true, message: '请选择责任部门', trigger: 'change' }],
  preSalesManagerId: [{ required: true, message: '请选择售前联系人', trigger: 'change' }],
  projectManagerId: [{ required: true, message: '请选择项目经理', trigger: 'change' }],
  salesManagerId: [{ required: true, message: '请选择销售负责人', trigger: 'change' }],
  requiredDelivery: [{ required: true, message: '请选择合同要求交期', trigger: 'change' }],
  acceptedDelivery: [{ required: true, message: '请选择承诺交期', trigger: 'change' }],
}

/* ───────── 产品列表 ───────── */
const products = ref<ProductItem[]>([])

function addProduct() {
  products.value.push({ sortOrder: products.value.length + 1, productType: '', quantity: 1, remark: '' })
}

function removeProduct(idx: number) {
  products.value.splice(idx, 1)
}

/* ───────── 项目范围列表 ───────── */
const projectScopes = ref<{ sortOrder: number; scopeName: string; scopeDesc: string }[]>([])

function addProjectScope() {
  projectScopes.value.push({ sortOrder: projectScopes.value.length + 1, scopeName: '', scopeDesc: '' })
}

function removeProjectScope(idx: number) {
  projectScopes.value.splice(idx, 1)
}

/* ───────── 成员列表 ───────── */
const members = ref<ProjectMemberItem[]>([])

function addMember() {
  members.value.push({ sortOrder: members.value.length + 1, remark: '' })
}

function removeMember(idx: number) {
  members.value.splice(idx, 1)
  members.value.forEach((m, i) => { m.sortOrder = i + 1 })
}

// ========== 成员拖拽排序 ==========
const memberDragFromIdx = ref<number | null>(null)
const memberDragOverIdx = ref<number | null>(null)

function onMemberDragStart(idx: number, ev: DragEvent) {
  memberDragFromIdx.value = idx
  ev.dataTransfer!.effectAllowed = 'move'
  ev.dataTransfer!.setData('text/plain', String(idx))
  const tr = (ev.target as HTMLElement).closest('tr')
  if (tr) requestAnimationFrame(() => tr.classList.add('row-dragging'))
}

function onMemberDragEnd() {
  memberDragFromIdx.value = null
  memberDragOverIdx.value = null
}

function onMemberTableDragOver(ev: DragEvent) {
  if (memberDragFromIdx.value === null) return
  const tr = (ev.target as HTMLElement).closest('.el-table__row') as HTMLElement | null
  if (!tr) return
  const idx = Array.from(tr.parentElement?.querySelectorAll('.el-table__row') || []).indexOf(tr)
  if (idx === -1 || idx === memberDragFromIdx.value) { ev.dataTransfer!.dropEffect = 'none'; return }
  ev.preventDefault()
  ev.dataTransfer!.dropEffect = 'move'
  memberDragOverIdx.value = idx
}

function onMemberTableDrop(ev: DragEvent) {
  if (memberDragFromIdx.value === null) return
  const tr = (ev.target as HTMLElement).closest('.el-table__row') as HTMLElement | null
  if (!tr) return
  const idx = Array.from(tr.parentElement?.querySelectorAll('.el-table__row') || []).indexOf(tr)
  if (idx === -1 || idx === memberDragFromIdx.value) return
  ev.preventDefault()
  const list = [...members.value]
  const [moved] = list.splice(memberDragFromIdx.value, 1)
  list.splice(idx, 0, moved)
  list.forEach((m, i) => { m.sortOrder = i + 1 })
  members.value = list
  memberDragFromIdx.value = null
  memberDragOverIdx.value = null
}

function memberRowClassName({ rowIndex }: { rowIndex: number }) {
  if (rowIndex === memberDragOverIdx.value) return 'row-drag-over'
  return ''
}

/* ───────── 从模板导入成员 ───────── */
const memberTemplateDialogVisible = ref(false)
const memberTemplateList = ref<Template[]>([])
const memberTemplateLoading = ref(false)
const selectedMemberTemplateId = ref<number | null>(null)

async function openMemberTemplateDialog() {
  memberTemplateLoading.value = true
  memberTemplateDialogVisible.value = true
  selectedMemberTemplateId.value = null
  try {
    const res = await getTemplateList({ templateType: 3, pageSize: 100 })
    memberTemplateList.value = res.data?.items ?? []
  } catch { memberTemplateList.value = [] }
  finally { memberTemplateLoading.value = false }
}

async function confirmImportMemberTemplate() {
  if (!selectedMemberTemplateId.value) { ElMessage.warning('请选择一个模板'); return }
  try {
    const res = await getTemplateDetail(selectedMemberTemplateId.value)
    let tmplMembers = res.data?.members ?? []
    if (tmplMembers.length === 0) { ElMessage.warning('该模板没有成员项'); return }

    // 保留基本信息中的成员（项目经理、销售、售前），移除其他成员
    const basicInfoIds = [form.projectManagerId, form.salesManagerId, form.preSalesManagerId].filter(Boolean)
    members.value = members.value.filter(m => m.memberId && basicInfoIds.includes(m.memberId))

    // 过滤掉模板中与基本信息重复的职能（项目经理、销售、售前）
    const skipFnNames = ['项目经理', '销售', '售前']
    const skipFnIds = functions.value.filter(f => skipFnNames.includes(f.name)).map(f => f.id)
    if (skipFnIds.length > 0) {
      tmplMembers = tmplMembers.filter((m: any) => !m.roleId || !skipFnIds.includes(m.roleId))
    }

    // 导入时，对每个成员做一次自动匹配：如果模板中未带出人员但职能+部门可确定唯一人员，则自动填充
    const enriched = await Promise.all(tmplMembers.map(async (m: any) => {
      const item: ProjectMemberItem = {
        sortOrder: m.sortOrder,
        roleId: m.roleId,
        roleName: m.roleName ?? '',
        memberId: m.memberId ?? null,
        memberName: m.memberName ?? '',
        functionId: m.roleId,
        functionName: m.roleName ?? '',
        deptId: m.deptId,
        deptName: m.deptName ?? '',
        remark: m.remark ?? ''
      }
      // 模板中未带出人员，但职能+部门可唯一确定时自动匹配
      if (!item.memberId && item.roleId && item.deptId) {
        try {
          const userRes = await searchUsers('', item.deptId, item.roleId)
          const users = userRes.data
          if (users && users.length === 1) {
            item.memberId = users[0].id
            item.memberName = users[0].realName ?? ''
          }
        } catch { /* 单个匹配失败不影响其他成员 */ }
      }
      return item
    }))

    // 追加新成员到基本信息成员之后
    members.value.push(...enriched)
    // 重新排序号
    members.value.forEach((m, i) => { m.sortOrder = i + 1 })
    ElMessage.success(`已从模板「${res.data.templateName}」导入 ${enriched.length} 项`)
    // 关闭弹框
    memberTemplateDialogVisible.value = false
  } catch { /* 错误由拦截器处理 */ }
}

function filteredMemberUsers(row: ProjectMemberItem) {
  return users.value.filter(u => {
    // 部门过滤
    if (row.deptId && u.departmentId !== row.deptId) return false
    // 职能过滤：人员的职能列表中包含所选职能
    if (row.roleId && (!u.functionIds || !u.functionIds.includes(row.roleId))) return false
    return true
  })
}

function onMemberSelect(member: ProjectMemberItem, user: UserInfo) {
  member.memberId = user.id
  member.memberName = user.realName
  // 自动补全部门信息
  if (!member.deptId && user.departmentId) {
    member.deptId = user.departmentId
    member.deptName = departments.value.find(d => d.id === user.departmentId)?.name ?? ''
  }
  // 自动补全职能信息（取用户第一个职能）
  const firstFnId = user.functionIds?.[0]
  const firstFnName = user.functionNames?.split('、')[0]
  if (!member.functionId && firstFnId) {
    member.functionId = firstFnId
    member.functionName = firstFnName ?? ''
    member.roleId = firstFnId
    member.roleName = firstFnName ?? ''
  }
}

/* 基本信息中的选人自动同步到成员列表 */
const staffToFunctionMap = [
  { id: () => form.projectManagerId, name: () => form.projectManagerName, fnName: '项目经理' },
  { id: () => form.salesManagerId, name: () => form.salesManagerName, fnName: '销售' },
  { id: () => form.preSalesManagerId, name: () => form.preSalesManagerName, fnName: '售前' },
] as const

function syncSelectedStaffToMembers() {
  for (const item of staffToFunctionMap) {
    const fn = functions.value.find(f => f.name === item.fnName)
    const staffId = item.id()
    const staffName = item.name()
    // 按职能查找已有成员行（如「项目经理」行），确保基本信息变更时更新而非新增
    let existingIdx = members.value.findIndex(m => m.functionName === item.fnName && item.fnName)
    if (existingIdx < 0) existingIdx = members.value.findIndex(m => m.memberId === staffId)

    const user = staffId ? users.value.find(u => u.id === staffId) : undefined

    if (staffId && staffName) {
      if (existingIdx >= 0) {
        // 已存在，更新完整信息（人员、部门、职能）
        members.value[existingIdx].memberId = staffId
        members.value[existingIdx].memberName = staffName
        members.value[existingIdx].deptId = user?.departmentId
        members.value[existingIdx].deptName = user?.departmentId ? departments.value.find(d => d.id === user.departmentId)?.name : undefined
        if (fn) {
          members.value[existingIdx].roleId = fn.id
          members.value[existingIdx].roleName = fn.name
          members.value[existingIdx].functionId = fn.id
          members.value[existingIdx].functionName = fn.name
        }
      } else {
        // 新增成员
        members.value.push({
          sortOrder: members.value.length + 1,
          roleId: fn?.id,
          roleName: fn?.name ?? '',
          functionId: fn?.id,
          functionName: fn?.name ?? '',
          memberId: staffId,
          memberName: staffName,
          deptId: user?.departmentId,
          deptName: user?.departmentId ? departments.value.find(d => d.id === user.departmentId)?.name : undefined,
          remark: ''
        })
      }
    } else if (existingIdx >= 0) {
      // 人员被清空，从成员列表中移除
      members.value.splice(existingIdx, 1)
    }
  }
}


/* ───────── 加载详情 ───────── */
async function loadDetail() {
  if (!projectId.value) return
  loading.value = true
  try {
    const res = await getProjectDetail(projectId.value)
    const d = res.data
    Object.assign(form, {
      projectCode: d.projectCode,
      projectName: d.projectName,
      projectType: d.projectType ?? '',
      contractCode: d.contractCode ?? '',
      engineeringCenter: d.engineeringCenter ?? '',
      categoryCode: d.categoryCode ?? '',
      customerName: d.customerName ?? '',
      regionalManagerId: d.regionalManagerId,
      regionalManagerName: d.regionalManagerName ?? '',
      customerContactPhone: d.customerContactPhone ?? '',
      customerContactEmail: d.customerContactEmail ?? '',
      salesManagerId: d.salesManagerId,
      salesManagerName: d.salesManagerName ?? '',
      preSalesManagerId: d.preSalesManagerId,
      preSalesManagerName: d.preSalesManagerName ?? '',
      salesRegion: d.salesRegion ?? '',
      projectManagerId: d.projectManagerId,
      projectManagerName: d.projectManagerName ?? '',
      pmCenter: d.pmCenter ?? '',
      ownerContactPhone: d.ownerContactPhone ?? '',
      businessContactEmail: d.businessContactEmail ?? '',
      planStartDate: d.planStartDate ?? undefined,
      requiredDelivery: d.requiredDelivery ?? undefined,
      acceptedDelivery: d.acceptedDelivery ?? undefined,
      actualFinishDate: d.actualFinishDate ?? undefined,
      deliveryLocation: d.deliveryLocation ?? '',
      finalCustomer: d.finalCustomer ?? '',
      projectScope: typeof d.projectScope === 'string' && d.projectScope.startsWith('[') ? d.projectScope : (d.projectScope || ''),
      specialTerms: d.specialTerms ?? '',
      remark: d.remark ?? '',
      qualityStrategy: d.qualityStrategy ?? '',
      projectDelivery: d.projectDelivery ?? '',
      reportContent: d.reportContent ?? '',
      riskStatus: d.riskStatus ?? '',
      currentPhaseDate: d.currentPhaseDate ?? undefined,
      nextStatus: d.nextStatus ?? '',
      progressDesc: d.progressDesc ?? '',
      status: d.status,
      canManageStatus: d.canManageStatus ?? false,
      canDeactivate: d.canDeactivate ?? false
    })
    // 解析项目范围 JSON → 表格数据
    try {
      const parsed = JSON.parse(d.projectScope || '[]')
      projectScopes.value = Array.isArray(parsed) ? parsed.map((s: any, i: number) => ({ sortOrder: s.sortOrder || i + 1, scopeName: s.scopeName || '', scopeDesc: s.scopeDesc || '' })) : []
    } catch { projectScopes.value = [] }
    products.value = d.products.map(p => ({ ...p }))
    members.value = d.members.map(m => ({ ...m }))
    resolveEngineeringCenterId()
    syncStaffOptionsFromForm()
    syncSelectedStaffToMembers()
  } catch (e) {
    // 加载失败，不阻塞页面
  } finally {
    loading.value = false
  }
}

/** 根据部门名称回填 engineeringCenterId */
function resolveEngineeringCenterId() {
  if (departments.value.length > 0 && form.engineeringCenter) {
    form.engineeringCenterId = departments.value.find(d => d.name === form.engineeringCenter)?.id ?? null
  }
}

/* ───────── 补全成员部门/职能信息 ───────── */
async function fillMemberInfo(): Promise<boolean> {
  if (members.value.length === 0) return false

  // 收集缺失信息的成员
  const needFill = members.value.filter(m => m.memberId && (!m.deptId || !m.functionId))
  if (needFill.length === 0) return false

  // 先尝试从已缓存的 users 中查找
  const missingIds: number[] = []
  for (const member of needFill) {
    const user = users.value.find(u => u.id === member.memberId)
    if (user) {
      applyUserInfo(member, user)
    } else {
      missingIds.push(member.memberId!)
    }
  }

  if (missingIds.length === 0) return true

  // 缓存中找不到的，拉取全量用户列表（最多50条）来补全
  let allUsers = users.value
  if (allUsers.length === 0) {
    const res = await searchUsers('')
    allUsers = res.data ?? []
    users.value = allUsers
  }

  for (const member of needFill) {
    if (member.deptId && member.functionId) continue
    const user = allUsers.find(u => u.id === member.memberId)
    if (user) {
      applyUserInfo(member, user)
    }
  }

  return true
}

function applyUserInfo(member: ProjectMemberItem, user: UserInfo) {
  if (!member.deptId && user.departmentId) {
    member.deptId = user.departmentId
    member.deptName = departments.value.find(d => d.id === user.departmentId)?.name ?? ''
  }
  // 自动补全职能信息（取用户第一个职能）
  const firstFnId = user.functionIds?.[0]
  const firstFnName = user.functionNames?.split('、')[0]
  if (!member.functionId && firstFnId) {
    member.functionId = firstFnId
    member.functionName = firstFnName ?? ''
    member.roleId = firstFnId
    member.roleName = firstFnName ?? ''
  }
}

/* ───────── 保存 ───────── */
async function handleSave() {
  if (!formRef.value) { ElMessage.error('表单未加载'); return }
  const valid = await formRef.value.validate().then(() => true).catch(() => false)
  if (!valid) return

  saving.value = true
  try {
    const payload = {
      ...form,
      projectScope: JSON.stringify(projectScopes.value.filter(s => s.scopeName)),
      products: products.value.map((p, i) => ({ ...p, sortOrder: i + 1 })),
      uploadedFileIds: uploadedFileIds.value
    }

    if (mode.value === 'create') {
      const res = await createProject(payload)
      const newId = res.data.id
      ElMessage.success('项目创建成功')

      await fillMemberInfo()
      if (members.value.length > 0) {
        const data = members.value.map((m, i) => ({ ...m, sortOrder: i + 1 }))
        await saveProjectMembers(newId, data)
      }

      router.replace(`/project/edit/${newId}`)
    } else {
      await updateProject(projectId.value!, payload)

      const updated = await fillMemberInfo()
      if (updated) {
        const data = members.value.map((m, i) => ({ ...m, sortOrder: i + 1 }))
        await saveProjectMembers(projectId.value!, data)
      }

      ElMessage.success('保存成功')
    }
  } catch {
    // 错误已由拦截器处理
  } finally {
    saving.value = false
  }
}

/* ───────── 保存成员 ───────── */
async function handleSaveMembers() {
  if (!projectId.value) { ElMessage.warning('请先保存基本信息'); return }
  saving.value = true
  try {
    await fillMemberInfo()
    const data = members.value.map((m, i) => ({ ...m, sortOrder: i + 1 }))
    await saveProjectMembers(projectId.value, data)
    ElMessage.success('成员保存成功')
  } finally {
    saving.value = false
  }
}

/* ───────── 状态操作 ───────── */
async function handleActivate() {
  if (!projectId.value) return
  await ElMessageBox.confirm('确定要激活该项目吗？', '提示', { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' })
  await activateProject(projectId.value)
  ElMessage.success('项目已激活')
  await loadDetail()
}

async function handleComplete() {
  if (!projectId.value) return
  await ElMessageBox.confirm('确定要将项目标记为已完成吗？', '提示', { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' })
  await completeProject(projectId.value)
  ElMessage.success('项目已完成')
  await loadDetail()
}

async function handleSuspend() {
  if (!projectId.value) return
  await ElMessageBox.confirm('确定要暂停该项目吗？', '提示', { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' })
  await suspendProject(projectId.value)
  ElMessage.success('项目已暂停')
  await loadDetail()
}

async function handleDeactivate() {
  if (!projectId.value) return
  await ElMessageBox.confirm('确定要取消激活该项目吗？取消后项目将回到未激活状态。', '提示', { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' })
  await deactivateProject(projectId.value)
  ElMessage.success('项目已反激活')
  await loadDetail()
}

async function handleResume() {
  if (!projectId.value) return
  await ElMessageBox.confirm('确定要取消暂停该项目吗？', '提示', { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' })
  await resumeProject(projectId.value)
  ElMessage.success('项目已恢复')
  await loadDetail()
}

function handleBack() {
  window.location.hash = '#/project/list'
}

/* ───────── 列拖拽 ───────── */
interface TaskColumnDef {
  key: string; label: string; width?: number | string; minWidth?: number | string
  align?: string; fixed?: string; showOverflowTooltip?: boolean; type?: string
  draggable: boolean; className?: string
}
const DEFAULT_TASK_COLUMNS: TaskColumnDef[] = [
  { key: 'index', label: '序号', width: 70, draggable: false, className: 'col-index' },
  { key: 'taskName', label: '任务名称', minWidth: 316, width: 316, draggable: false, className: 'col-taskname' },
  { key: 'progress', label: '进度', width: 106, align: 'center', draggable: true },
  { key: 'planStartDate', label: '计划开始', width: 110, align: 'center', draggable: true },
  { key: 'planFinishDate', label: '计划完成', width: 110, align: 'center', draggable: true },
  { key: 'actualStartDate', label: '实际开始', width: 110, align: 'center', draggable: true },
  { key: 'actualFinishDate', label: '实际完成', width: 110, align: 'center', draggable: true },
  { key: 'planDuration', label: '计划工期', width: 69, align: 'center', draggable: true },
  { key: 'preTaskCodes', label: '前置任务', width: 165, draggable: true },
  { key: 'dept', label: '责任部门', width: 130, showOverflowTooltip: true, draggable: true },
  { key: 'assignee', label: '责任人', width: 130, showOverflowTooltip: true, draggable: true },
  { key: 'priority', label: '优先级', width: 80, align: 'center', draggable: true },
  { key: 'status', label: '状态', width: 90, align: 'center', draggable: true },
  { key: 'overdueStatus', label: '逾期状态', width: 88, align: 'center', draggable: true },
  { key: 'taskCategory', label: '任务类别', width: 130, align: 'center', draggable: true },
  { key: 'actualDuration', label: '实际工期', width: 71, align: 'center', draggable: true },
  { key: 'referenceDuration', label: '参考工期', width: 80, align: 'center', draggable: true },
  { key: 'wbsCode', label: '工序号', width: 120, align: 'center', showOverflowTooltip: true, draggable: true },
  { key: 'actions', label: '操作', width: 168, draggable: false, className: 'col-sticky-right' },
]
const COLUMN_ORDER_STORAGE_KEY = computed(() => `project_task_column_order_${authStore.userId}`)
function loadColumnOrder(): string[] {
  try {
    const saved = localStorage.getItem(COLUMN_ORDER_STORAGE_KEY.value)
    if (saved) {
      const parsed = JSON.parse(saved) as string[]
      const defaultKeys = DEFAULT_TASK_COLUMNS.map(c => c.key)
      if (parsed.length === defaultKeys.length && defaultKeys.every(k => parsed.includes(k))) return parsed
    }
  } catch { /* 忽略损坏的数据 */ }
  return DEFAULT_TASK_COLUMNS.map(c => c.key)
}
function saveColumnOrder(keys: string[]) { localStorage.setItem(COLUMN_ORDER_STORAGE_KEY.value, JSON.stringify(keys)) }
const taskColumnKeys = ref<string[]>(loadColumnOrder())
const taskColumnDefs = computed(() => {
  const defMap = new Map(DEFAULT_TASK_COLUMNS.map(c => [c.key, c]))
  return taskColumnKeys.value.map(k => defMap.get(k)!).filter(Boolean)
})
function taskHeaderCellClassName(data: { columnIndex: number }): string {
  const col = taskColumnDefs.value[data.columnIndex]
  if (!col) return ''
  const classes = [`task-col-${col.key}`]
  if (col.draggable) classes.push('task-col-draggable')
  return classes.join(' ')
}
const dragSourceKey = ref<string | null>(null)
function getColKeyFromTh(th: HTMLElement): string | null {
  for (const cls of th.classList) { const m = cls.match(/^task-col-(.+)$/); if (m) return m[1] }
  return null
}
function isDraggableCol(key: string): boolean { return DEFAULT_TASK_COLUMNS.find(c => c.key === key)?.draggable ?? false }
function reorderColumns(srcKey: string, tgtKey: string) {
  const keys = [...taskColumnKeys.value]
  const srcIdx = keys.indexOf(srcKey)
  const tgtIdx = keys.indexOf(tgtKey)
  if (srcIdx === -1 || tgtIdx === -1 || srcIdx === tgtIdx) return
  if (srcIdx < 2 || srcIdx >= keys.length - 1) return
  if (tgtIdx < 2 || tgtIdx >= keys.length - 1) return
  const [removed] = keys.splice(srcIdx, 1)
  const insertAt = srcIdx < tgtIdx ? tgtIdx - 1 : tgtIdx
  keys.splice(insertAt, 0, removed)
  taskColumnKeys.value = keys
  saveColumnOrder(keys)
  nextTick(() => { (taskTableRef.value as any)?.doLayout?.() })
}
const vColumnDrag = {
  mounted(el: HTMLElement) {
    const headerRow = el.querySelector('.el-table__header-wrapper tr') as HTMLElement | null
    if (!headerRow) return
    headerRow.querySelectorAll<HTMLElement>('.task-col-draggable').forEach(th => { th.draggable = true })
    const h = {
      dragstart(e: Event) {
        const ev = e as DragEvent; const th = (ev.target as HTMLElement).closest('th')!; const key = getColKeyFromTh(th)
        if (!key || !isDraggableCol(key)) { ev.preventDefault(); return }
        dragSourceKey.value = key; ev.dataTransfer!.effectAllowed = 'move'; ev.dataTransfer!.setData('text/plain', key)
        th.classList.add('task-col-dragging')
      },
      dragover(e: Event) {
        const ev = e as DragEvent; const th = (ev.target as HTMLElement).closest('th'); if (!th) return
        const key = getColKeyFromTh(th)
        if (!key || !isDraggableCol(key) || key === dragSourceKey.value) { ev.dataTransfer!.dropEffect = 'none'; return }
        ev.preventDefault(); ev.dataTransfer!.dropEffect = 'move'; th.classList.add('task-col-drag-over')
      },
      dragleave(e: Event) { const th = (e.target as HTMLElement).closest('th'); if (th) th.classList.remove('task-col-drag-over') },
      drop(e: Event) {
        const ev = e as DragEvent; ev.preventDefault()
        const th = (ev.target as HTMLElement).closest('th'); if (!th) return
        th.classList.remove('task-col-drag-over')
        const tgtKey = getColKeyFromTh(th); const srcKey = ev.dataTransfer!.getData('text/plain')
        if (srcKey && tgtKey && srcKey !== tgtKey && isDraggableCol(srcKey) && isDraggableCol(tgtKey)) reorderColumns(srcKey, tgtKey)
      },
      dragend(e: Event) {
        const th = (e.target as HTMLElement).closest('th'); if (th) th.classList.remove('task-col-dragging')
        dragSourceKey.value = null
        headerRow.querySelectorAll<HTMLElement>('.task-col-drag-over').forEach(c => c.classList.remove('task-col-drag-over'))
      }
    }
    ;(el as any).__dragHandlers = h
    headerRow.addEventListener('dragstart', h.dragstart); headerRow.addEventListener('dragover', h.dragover)
    headerRow.addEventListener('dragleave', h.dragleave); headerRow.addEventListener('drop', h.drop)
    headerRow.addEventListener('dragend', h.dragend)
  },
  updated(el: HTMLElement) {
    const headerRow = el.querySelector('.el-table__header-wrapper tr') as HTMLElement | null
    if (!headerRow) return
    headerRow.querySelectorAll<HTMLElement>('.task-col-draggable').forEach(th => { th.draggable = true })
  },
  unmounted(el: HTMLElement) {
    const h = (el as any).__dragHandlers; if (!h) return
    const headerRow = el.querySelector('.el-table__header-wrapper tr') as HTMLElement | null
    if (!headerRow) return
    headerRow.removeEventListener('dragstart', h.dragstart); headerRow.removeEventListener('dragover', h.dragover)
    headerRow.removeEventListener('dragleave', h.dragleave); headerRow.removeEventListener('drop', h.drop)
    headerRow.removeEventListener('dragend', h.dragend); delete (el as any).__dragHandlers
  }
}
/* ───────── 任务计划 ───────── */
const tasks = ref<ProjectTaskItem[]>([])
const taskSaving = ref(false)
const listEditMode = ref(false)
const editingRowId = ref<number | null>(null)
/* ───────── 前置任务悬浮提示 ───────── */
const pv = reactive({ show: false, x: 0, y: 0, row: null as ProjectTaskItem | null, tm: 0 })
const flashId = ref<number | null>(null)
let _ft = 0
function onCellEnter(_r: ProjectTaskItem, col: any, cell: HTMLElement) {
  if (col.label !== '前置任务') return
  const row = _r as ProjectTaskItem
  if (!row.preTaskCodes) return
  if (pv.tm) { clearTimeout(pv.tm); pv.tm = 0 }
  const rect = cell.getBoundingClientRect()
  pv.x = rect.right + 6; pv.y = rect.top - 2; pv.row = row; pv.show = true
}
function onCellLeave(_r: ProjectTaskItem, col: any) {
  if (col.label !== '前置任务') return
  if (pv.tm) clearTimeout(pv.tm)
  pv.tm = window.setTimeout(() => { pv.show = false; pv.row = null }, 200)
}
function pvCancel() { if (pv.tm) { clearTimeout(pv.tm); pv.tm = 0 } }
function pvHide() { if (pv.tm) { clearTimeout(pv.tm); pv.tm = 0 }; pv.show = false; pv.row = null }
const taskTableRef = ref()
function flashRow(taskId: number) {
  pvHide()
  void scrollToTaskRow(taskId)
}
/** 深度优先遍历 treeData，返回目标 taskId 在展开树中的扁平索引（从0开始），找不到返回 -1 */
function findFlatIndex(treeData: ProjectTaskItem[], targetId: number): number {
  let idx = 0
  function walk(nodes: ProjectTaskItem[]): boolean {
    for (const node of nodes) {
      if (node.id === targetId) return true
      idx++
      if (node.children && node.children.length > 0) {
        if (walk(node.children)) return true
      }
    }
    return false
  }
  return walk(treeData) ? idx : -1
}

async function scrollToTaskRow(taskId: number) {
  // 解析 taskTree 得到目标的扁平索引（树顺序 = DOM 渲染顺序）
  const flatIdx = findFlatIndex(taskTree.value, taskId)
  if (flatIdx < 0) {
    ElMessage.warning('未找到该前置任务所在行')
    return
  }

  await nextTick()
  await new Promise(r => setTimeout(r, 300))

  const el = taskTableRef.value?.$el as HTMLElement | undefined
  if (!el) return

  const scrollWrap = (el.querySelector('.el-scrollbar__wrap')
    ?? el.querySelector('.el-table__body-wrapper')) as HTMLElement | null
  if (!scrollWrap) return

  const allRows = scrollWrap.querySelectorAll<HTMLElement>('tr.el-table__row')
  const targetRow = allRows[flatIdx]
  if (!targetRow) {
    ElMessage.warning('未找到该前置任务所在行')
    return
  }

  // 累加行高得到精确 scrollTop
  let offset = 0
  for (let i = 0; i < flatIdx; i++) {
    offset += allRows[i]?.offsetHeight ?? 0
  }
  const centerOffset = scrollWrap.clientHeight / 2 - targetRow.offsetHeight / 2
  scrollWrap.scrollTop = Math.max(0, offset - centerOffset)

  // 高亮闪烁：JS inline !important 击败 scoped CSS !important
  if (_ft) clearTimeout(_ft)
  // 清除所有旧高亮
  el.querySelectorAll('td[data-flash]').forEach(td => {
    td.style.removeProperty('background')
    td.removeAttribute('data-flash')
  })
  flashId.value = taskId
  const HIGHLIGHT = '#fce4ec'
  targetRow.querySelectorAll('td').forEach(td => {
    td.setAttribute('data-flash', '')
    td.style.setProperty('background', HIGHLIGHT, 'important')
  })
  _ft = window.setTimeout(() => {
    flashId.value = null
    el.querySelectorAll('td[data-flash]').forEach(td => {
      td.style.removeProperty('background')
      td.removeAttribute('data-flash')
    })
  }, 3000)
}
/* ─────────────────────────────────────── */
const showAddTaskDialog = ref(false)
const editingTask = ref<ProjectTaskItem>({ parentId: null, taskNo: '', wbsCode: '', taskName: '', nodeType: 1, taskCategory: '', sortOrder: 0, status: 0, priority: 3, deliverableCnt: 0, progressPct: 0, remark: '' })
const showViewTaskDialog = ref(false)
const viewingTask = ref<ProjectTaskItem | null>(null)
const showTemplateDialog = ref(false)
const templateList = ref<Template[]>([])
const templateLoading = ref(false)
const selectedTemplateId = ref<number | null>(null)
const templateSearch = ref('')

function openViewTask(task: ProjectTaskItem) {
  viewingTask.value = task
  showViewTaskDialog.value = true
}

function taskStatusTag(s: number): '' | 'success' | 'warning' | 'info' | 'danger' {
  return ['info', 'primary', 'success'][s] as any ?? 'info'
}

/* ───────── 前置任务行管理 ───────── */
interface PredRow {
  rowKey: number
  taskId: number
  dependencyType: string
  lagDays: number
}
let predKeySeq = 0
const predRows = ref<PredRow[]>([])

// 可用前置任务（排除自身及子孙）
const availablePredTasks = computed(() => {
  const currentId = editingTask.value?.id
  if (!currentId) { const r = tasks.value.filter(t => t.id).sort((a, b) => (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true })); return r }
  const excluded = new Set<number>()
  function collectDescendants(parentId: number) {
    for (const t of tasks.value) {
      if (t.parentId === parentId && t.id) {
        excluded.add(t.id)
        collectDescendants(t.id)
      }
    }
  }
  if (currentId) {
    excluded.add(currentId)
    collectDescendants(currentId)
  }
  const r = tasks.value
    .filter(t => t.id && !excluded.has(t.id))
    .sort((a, b) => (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true }))
  return r
})

// 可用上级节点（排除自身及子孙）
const availableParentTree = computed(() => {
  const currentId = editingTask.value?.id
  if (!currentId) return taskTree.value
  function filterTree(nodes: ProjectTaskItem[]): ProjectTaskItem[] {
    const result: ProjectTaskItem[] = []
    for (const node of nodes) {
      if (node.id === currentId) continue
      if (node.children && node.children.length > 0) {
        result.push({ ...node, children: filterTree(node.children) })
      } else {
        result.push({ ...node, children: [] })
      }
    }
    return result
  }
  return filterTree(taskTree.value)
})

// 上级节点 flat 选项（带层级缩进，排除自身及子孙）
const flatParentOptions = computed(() => {
  const currentId = editingTask.value?.id
  const excluded = new Set<number>()
  if (currentId) {
    excluded.add(currentId)
    function collectDescendants(pid: number) {
      for (const t of tasks.value) {
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
  walk(taskTree.value, 0)
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

// parsePreTaskCodes / serializePreTaskCodes 统一使用 @/utils/preTaskHelpers


/* 根据前置任务自动计算计划开始日期 */
function calcPlanStartFromPreds(): string | null {
  if (predRows.value.length === 0) return null
  // 直接用 tasks.value 平铺列表
  const allTasks = new Map<number, ProjectTaskItem>()
  for (const t of tasks.value) { if (t.id) allTasks.set(t.id, t) }

  let latest: string | null = null
  for (const row of predRows.value) {
    const pred = allTasks.get(row.taskId)
    if (!pred) continue
    let base: string | undefined
    if (row.dependencyType === 'FS' || row.dependencyType === 'FF') base = pred.planFinishDate
    else if (row.dependencyType === 'SS' || row.dependencyType === 'SF') base = pred.planStartDate
    if (!base) continue
    const ds = dateAddDays(base, row.lagDays)   // 时区安全
    if (!latest || dateStrGt(ds, latest)) latest = ds
  }
  return latest
}

function newTaskForm(): ProjectTaskItem {
  return { parentId: null, taskNo: '', wbsCode: '', taskName: '', nodeType: 1, taskCategory: '', sortOrder: tasks.value.length + 1, status: 0, priority: 3, deliverableCnt: 0, progressPct: 0, remark: '' }
}

function openAddTask(parent?: ProjectTaskItem) {
  editingTask.value = newTaskForm()
  editingTaskOldParentId.value = null
  predRows.value = []
  if (parent) {
    editingTask.value.parentId = parent.id
    editingTask.value.deptId = parent.deptId
    editingTask.value.deptName = parent.deptName
    editingTask.value.assigneeId = parent.assigneeId
    editingTask.value.assigneeName = parent.assigneeName
  }
  // 自动计算序号和排序
  const siblings = tasks.value.filter(t => t.parentId === (parent?.id ?? null))
  editingTask.value.sortOrder = siblings.length > 0
    ? Math.max(...siblings.map(s => s.sortOrder ?? 0)) + 1
    : 1
  const rule = taskNoRule.value || '3,2,2'
  const parts = rule.split(',').map(Number)
  if (!parent) {
    // 根节点：使用第一级位数
    const digits = parts[0] || 3
    const num = siblings.length + 1
    editingTask.value.taskNo = String(num).padStart(digits, '0')
  } else {
    // 子节点：在父节点序号后追加下一级位数
    const digits = parts[1] || 2
    // 找到父节点的完整序号
    const parentNo = parent.taskNo || ''
    const num = siblings.length + 1
    editingTask.value.taskNo = parentNo + '.' + String(num).padStart(digits, '0')
  }
  showAddTaskDialog.value = true
}
function openEditTask(row: ProjectTaskItem) {
  editingTask.value = { ...row }
  predRows.value = parsePreTaskCodes(row.preTaskCodes).map((seg, i) => ({ ...seg, rowKey: Date.now() + (++predKeySeq) }))
  editingTaskOldParentId.value = row.parentId ?? null
  showAddTaskDialog.value = true
}

const editingTaskOldParentId = ref<number | null>(null)

function onParentChange(newParentId: number | null) {
  if (!editingTask.value) return
  const parent = newParentId != null ? tasks.value.find(t => t.id === newParentId) : null
  const parentNo = parent?.taskNo || ''
  const rule = taskNoRule.value || '3,2,2'
  const parts = rule.split(',').map(Number)
  const depth = parentNo ? parentNo.split('.').length : 0
  const digits = parts[Math.min(depth, parts.length - 1)] ?? 2
  const siblings = tasks.value.filter(t => t.parentId === newParentId && t.id !== editingTask.value!.id)
  const num = siblings.length + 1
  const padded = String(num).padStart(digits, '0')
  editingTask.value.taskNo = parentNo ? `${parentNo}.${padded}` : padded
}

/* 判断任务是否有子节点 */
function taskHasChildren(taskId?: number | null): boolean {
  if (!taskId) return false
  return tasks.value.some(t => t.parentId === taskId)
}

/* 递归获取子节点中最小的计划开始时间 */
function getEarliestChildStart(taskId?: number | null): string | undefined {
  if (!taskId) return undefined
  const children = tasks.value.filter(t => t.parentId === taskId)
  let earliest: string | undefined
  for (const child of children) {
    if (child.planStartDate && (!earliest || child.planStartDate < earliest)) {
      earliest = child.planStartDate
    }
    const childEarliest = getEarliestChildStart(child.id)
    if (childEarliest && (!earliest || childEarliest < earliest)) {
      earliest = childEarliest
    }
  }
  return earliest
}

/* 递归获取子节点中最晚的计划完成时间 */
function getLatestChildFinish(taskId?: number | null): string | undefined {
  if (!taskId) return undefined
  const children = tasks.value.filter(t => t.parentId === taskId)
  let latest: string | undefined
  for (const child of children) {
    if (child.planFinishDate && (!latest || child.planFinishDate > latest)) {
      latest = child.planFinishDate
    }
    const childLatest = getLatestChildFinish(child.id)
    if (childLatest && (!latest || childLatest > latest)) {
      latest = childLatest
    }
  }
  return latest
}

/* 递归获取子节点中最早的实际开始时间 */
function getEarliestChildActualStart(taskId?: number | null): string | undefined {
  if (!taskId) return undefined
  const children = tasks.value.filter(t => t.parentId === taskId)
  let earliest: string | undefined
  for (const child of children) {
    if (child.actualStartDate && (!earliest || child.actualStartDate < earliest)) {
      earliest = child.actualStartDate
    }
    const childEarliest = getEarliestChildActualStart(child.id)
    if (childEarliest && (!earliest || childEarliest < earliest)) {
      earliest = childEarliest
    }
  }
  return earliest
}

/* 递归检查子节点是否全部完成（全部有实际完成时间） */
function areAllChildrenFinished(taskId?: number | null): boolean {
  if (!taskId) return true
  const children = tasks.value.filter(t => t.parentId === taskId)
  if (children.length === 0) return true
  for (const child of children) {
    if (!child.actualFinishDate) return false
    if (!areAllChildrenFinished(child.id)) return false
  }
  return true
}

/* 递归获取子节点中最晚的实际完成时间（仅当全部完成时有效） */
function getLatestChildActualFinish(taskId?: number | null): string | undefined {
  if (!taskId || !areAllChildrenFinished(taskId)) return undefined
  const children = tasks.value.filter(t => t.parentId === taskId)
  let latest: string | undefined
  for (const child of children) {
    if (child.actualFinishDate && (!latest || child.actualFinishDate > latest)) {
      latest = child.actualFinishDate
    }
    const childLatest = getLatestChildActualFinish(child.id)
    if (childLatest && (!latest || childLatest > latest)) {
      latest = childLatest
    }
  }
  return latest
}

/* 计算节点在树中的深度（根节点深度为0） */
function getTaskDepth(taskId: number): number {
  const task = tasks.value.find(t => t.id === taskId)
  if (!task || !task.parentId) return 0
  return 1 + getTaskDepth(task.parentId)
}

/* 自动同步父节点时间为子节点最早/最晚时间 */
function syncParentPlanDates() {
  // 同步所有任务的进度、状态和实际工期
  for (const t of tasks.value) {
    if (t.actualFinishDate) {
      if (t.progressPct !== 100) t.progressPct = 100
      if (t.status !== 2) t.status = 2
    } else if (t.actualStartDate) {
      if (t.status !== 1) t.status = 1
    } else {
      if (t.progressPct !== 0) t.progressPct = 0
      if (t.status !== 0) t.status = 0
    }
    if (!taskHasChildren(t.id) && t.actualStartDate && t.actualFinishDate && !t.actualDuration) {
      const days = Math.round((new Date(t.actualFinishDate).getTime() - new Date(t.actualStartDate).getTime()) / (1000 * 60 * 60 * 24))
      if (days >= 0) t.actualDuration = days
    }
  }
  const parentIds = [...new Set(tasks.value.filter(t => t.parentId).map(t => t.parentId))]
  // 按子节点优先排序，确保深度大的先处理
  parentIds.sort((a, b) => {
    const depthA = getTaskDepth(a), depthB = getTaskDepth(b)
    return depthB - depthA
  })
  for (const pid of parentIds) {
    const parent = tasks.value.find(t => t.id === pid)
    if (!parent) continue
    const earliestStart = getEarliestChildStart(pid)
    if (earliestStart && parent.planStartDate !== earliestStart) {
      parent.planStartDate = earliestStart
    } else if (!earliestStart && parent.planStartDate) {
      parent.planStartDate = undefined
    }
    const latestFinish = getLatestChildFinish(pid)
    if (latestFinish && parent.planFinishDate !== latestFinish) {
      parent.planFinishDate = latestFinish
    } else if (!latestFinish && parent.planFinishDate) {
      parent.planFinishDate = undefined
    }
    const earliestActualStart = getEarliestChildActualStart(pid)
    if (earliestActualStart) {
      if (parent.actualStartDate !== earliestActualStart)
        parent.actualStartDate = earliestActualStart
    } else if (parent.actualStartDate) {
      parent.actualStartDate = undefined
    }
    const allDone = areAllChildrenFinished(pid)
    const latestActualFinish = allDone ? getLatestChildActualFinish(pid) : undefined
    if (latestActualFinish) {
      if (parent.actualFinishDate !== latestActualFinish)
        parent.actualFinishDate = latestActualFinish
    } else if (!allDone && parent.actualFinishDate) {
      parent.actualFinishDate = undefined
    }
    // 父节点实际日期汇聚完毕后，重新推导 status
    if (parent.actualFinishDate && allDone) {
      parent.status = 2
    } else if (parent.actualStartDate) {
      parent.status = 1
    } else {
      parent.status = 0
    }
    // 计划工期 = 父节点自身日期跨度（日历天数），而非子节点工期之和
    if (parent.planStartDate && parent.planFinishDate) {
      const planDays = Math.round((new Date(parent.planFinishDate).getTime() - new Date(parent.planStartDate).getTime()) / (1000 * 60 * 60 * 24))
      parent.planDuration = planDays >= 0 ? planDays : 0
    }
    // 实际工期 = 父节点自身日期跨度
    if (parent.actualStartDate && parent.actualFinishDate) {
      const actualDays = Math.round((new Date(parent.actualFinishDate).getTime() - new Date(parent.actualStartDate).getTime()) / (1000 * 60 * 60 * 24))
      parent.actualDuration = actualDays >= 0 ? actualDays : 0
    }
    const children = tasks.value.filter(t => t.parentId === pid)
    // 进度 = 子节点计划工期加权平均
    const totalDuration = children.reduce((sum, c) => sum + (c.planDuration || 0), 0)
    const weightedPct = children.reduce((sum, c) => sum + (c.planDuration || 0) * (c.progressPct || 0), 0)
    if (totalDuration > 0) {
      const avg = Math.round(weightedPct / totalDuration)
      if (parent.progressPct !== avg) parent.progressPct = avg
    }
  }
}

/* 里程碑自动逻辑：当节点类型切换为里程碑时，关联字段自动同步 */
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

async function handleSaveTask() {
  if (!projectId.value || !editingTask.value) return
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
    const newParentId = editingTask.value.parentId ?? null
    const parentChanged = editingTask.value.id && oldParentId !== newParentId

    if (editingTask.value.id) {
      await updateProjectTask(projectId.value, editingTask.value.id, editingTask.value)
      const idx = tasks.value.findIndex(t => t.id === editingTask.value!.id)
      if (idx >= 0) tasks.value[idx] = { ...editingTask.value }
    } else {
      const res = await createProjectTask(projectId.value, editingTask.value)
      tasks.value.push({ ...editingTask.value, id: res.data.id })
    }

    if (parentChanged) {
      await renumberSiblings(oldParentId)
      await renumberSiblings(newParentId)
    }

    // 步骤2：递归调整子节点的计划日期（基于前置任务约束）
    const changedDescendantNos: number[] = editingTask.value.id
      ? adjustDescendantsByPredecessors(editingTask.value.id)
      : []
    for (const childId of changedDescendantNos) {
      const child = tasks.value.find(t => t.id === childId)
      if (child && child.id) {
        await updateProjectTask(projectId.value, child.id, child)
      }
    }

    const cascadeNos = syncParentAndCollectChangedNos(
      editingTask.value.id ? new Set([editingTask.value.id]) : new Set<number>()
    )
    // 将递归调整的子节点也加入级联传播队列
    for (const id of changedDescendantNos) cascadeNos.add(id)
    showAddTaskDialog.value = false

    if (cascadeNos.size > 0) {
      const cascadedCount = await cascadeForwardSchedule(cascadeNos)
      if (cascadedCount > 0) {
        ElMessage.success(`保存成功，已自动更新 ${cascadedCount} 个后续任务的计划日期`)
      } else {
        ElMessage.success('保存成功')
      }
    } else {
      ElMessage.success('保存成功')
    }
  } finally { taskSaving.value = false }
}

async function handleDeleteTask(row: ProjectTaskItem) {
  if (!projectId.value || !row.id) return
  const hasChildren = !!(row.children?.length)
  const message = hasChildren
    ? `确定删除任务「${row.taskName}」及其所有子任务吗？`
    : `确定删除任务「${row.taskName}」吗？`
  await ElMessageBox.confirm(message, '提示', { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' })
  await deleteProjectTask(projectId.value, row.id)
  // 递归收集被删的 id 并过滤
  const deletedIds = new Set<number>()
  collectIds(row, deletedIds)
  tasks.value = tasks.value.filter(t => t.id && !deletedIds.has(t.id))
  // 从其他任务的 preTaskCodes 中移除对被删任务的引用
  for (const t of tasks.value) {
    if (!t.preTaskCodes || !t.id) continue
    const segments = parsePreTaskCodes(t.preTaskCodes)
    const filtered = segments.filter(s => !deletedIds.has(s.taskId))
    if (filtered.length === segments.length) continue
    t.preTaskCodes = filtered.length > 0
      ? filtered.map(s => `${s.taskId}(${s.dependencyType}${s.lagDays ? (s.lagDays > 0 ? '+' : '') + s.lagDays : ''})`).join(',')
      : undefined
    await updateProjectTask(projectId.value, t.id, t)
  }
  // 删除后重新编号同级节点
  await renumberSiblings(row.parentId ?? null)
  ElMessage.success('已删除')
}

function collectIds(task: ProjectTaskItem, ids: Set<number>) {
  if (task.id) ids.add(task.id)
  task.children?.forEach(c => collectIds(c, ids))
}

/* ─── 右键上下文菜单 ─── */
const ctxMenuVisible = ref(false)
const ctxMenuX = ref(0)
const ctxMenuY = ref(0)
const ctxMenuTask = ref<ProjectTaskItem | null>(null)
const ctxMenuLoading = ref(false)

function handleRowContextMenu(row: ProjectTaskItem, _col: unknown, event: MouseEvent) {
  if (isReadonly.value || isTaskLocked.value) return
  event.preventDefault()
  ctxMenuTask.value = row
  ctxMenuX.value = event.clientX
  ctxMenuY.value = event.clientY
  ctxMenuVisible.value = true
}

function closeCtxMenu() {
  ctxMenuVisible.value = false
}

function handleCtxAddChild() {
  const target = ctxMenuTask.value
  ctxMenuVisible.value = false
  if (!target) return
  openAddTask(target)
}

function handleCtxEdit() {
  const target = ctxMenuTask.value
  ctxMenuVisible.value = false
  if (!target) return
  openEditTask(target)
}

async function handleCtxDelete() {
  const target = ctxMenuTask.value
  ctxMenuVisible.value = false
  if (!target) return
  await handleDeleteTask(target)
}

async function handleCtxRenumber() {
  const target = ctxMenuTask.value
  ctxMenuVisible.value = false
  if (!target?.id || !projectId.value) return
  if (!tasks.value.some(t => t.parentId === target.id)) {
    ElMessage.warning('该节点下没有子节点')
    return
  }
  ctxMenuLoading.value = true
  try {
    await renumberSubtree(target.id)
    ElMessage.success('已重新编号子节点')
  } catch {
    ElMessage.error('重新编号失败')
  } finally {
    ctxMenuLoading.value = false
  }
}

/**
 * 将所有子任务的 taskNo 前缀从 oldPrefix 替换为 newPrefix（递归）
 */
/**
 * 阶段一辅助：递归收集某任务所有子孙的 oldNo→newNo 映射（纯计算，不修改任何对象）
 */
function collectDescendantRenames(
  parentId: number | null | undefined,
  oldParentNo: string,
  newParentNo: string,
  renameMap: Map<string, string>,
  affectedIds: Set<number>
) {
  const children = tasks.value.filter(t => t.parentId === parentId)
  for (const child of children) {
    if (child.id) affectedIds.add(child.id)
    const oldNo = child.taskNo || ''
    let suffix: string
    if (oldNo.startsWith(oldParentNo + '.')) {
      // 正常情况：子节点编号以旧父编号为前缀
      suffix = oldNo.slice(oldParentNo.length)
    } else if (oldNo === oldParentNo) {
      suffix = ''
    } else {
      // 跨层级移动：子节点编号还保留着旧位置的层级前缀，不匹配当前 oldParentNo
      // 从最后一个 . 截取后缀（保留子节点在自身层级中的序号）
      const dotIdx = oldNo.lastIndexOf('.')
      suffix = dotIdx >= 0 ? oldNo.substring(dotIdx) : '.' + oldNo
    }
    const newNo = newParentNo + suffix
    if (oldNo !== newNo) {
      renameMap.set(oldNo, newNo)
    }
    // 递归时使用子节点自身的 oldNo 作为下一层的 oldParentNo
    collectDescendantRenames(child.id, oldNo, newNo, renameMap, affectedIds)
  }
}

/**
 * 对 parentId 下所有兄弟任务重新编号（两阶段：先建映射表，再原子更新）。
 *
 * 阶段一：计算所有旧编号→新编号的完整映射（含子孙任务），不做任何修改。
 * 阶段二a：按映射表更新所有任务的 taskNo / sortOrder。
 * 阶段三：批量保存到后端。
 *
 * @param shallow 为 true 时仅重编当前层兄弟，不沿编号前缀改写更深层（供子树递归使用）
 */
async function renumberSiblings(
  parentId: number | null,
  skipSave = false,
  options?: { shallow?: boolean }
): Promise<ProjectTaskItem[]> {
  const shallow = options?.shallow ?? false
  const rule = taskNoRule.value || '3,2,2'
  const parts = rule.split(',').map(Number)

  const parent = parentId != null ? tasks.value.find(t => t.id === parentId) : null
  const parentNo = parent?.taskNo || ''
  const depth = parentNo ? parentNo.split('.').length : 0
  const digits = parts[Math.min(depth, parts.length - 1)] ?? 2

  const siblings = tasks.value
    .filter(t => t.parentId === parentId)
    .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))

  // ── 阶段一：建立完整 oldNo→newNo 映射表 ──────────────────────────────
  const renameMap = new Map<string, string>()
  const affectedIds = new Set<number>()
  siblings.forEach((sib, i) => {
    if (sib.id) affectedIds.add(sib.id)
    const newNo = parentNo
      ? `${parentNo}.${String(i + 1).padStart(digits, '0')}`
      : String(i + 1).padStart(digits, '0')
    const oldNo = sib.taskNo || ''
    if (oldNo && oldNo !== newNo) {
      renameMap.set(oldNo, newNo)
      if (!shallow) {
        collectDescendantRenames(sib.id, oldNo, newNo, renameMap, affectedIds)
      }
    }
  })

  const toUpdate: ProjectTaskItem[] = []

  // ── 阶段二a：更新所有任务的 taskNo / sortOrder ────────────────────────
  // 直接按位置赋值：兼容新任务（taskNo 为空）和被重命名任务
  siblings.forEach((sib, i) => {
    sib.sortOrder = i + 1
    const newNo = parentNo
      ? `${parentNo}.${String(i + 1).padStart(digits, '0')}`
      : String(i + 1).padStart(digits, '0')
    if ((sib.taskNo || '') !== newNo) {
      sib.taskNo = newNo
      if (!toUpdate.includes(sib)) toUpdate.push(sib)
    }
  })
  // 子孙任务通过 renameMap 更新（不含兄弟层，避免重复处理；仅更新子树内节点）
  for (const t of tasks.value) {
    if (siblings.includes(t)) continue   // 兄弟层已在上面处理
    if (t.id && !affectedIds.has(t.id)) continue
    const oldNo = t.taskNo || ''
    const newNo = renameMap.get(oldNo)
    if (newNo) {
      t.taskNo = newNo
      if (!toUpdate.includes(t)) toUpdate.push(t)
    }
  }

  // ── 阶段三：批量保存 ──────────────────────────────────────────────────
  if (!skipSave && toUpdate.length > 0 && projectId.value) {
    const seen = new Set<number>()
    for (const t of toUpdate) {
      if (t.id && !seen.has(t.id)) {
        seen.add(t.id)
        await updateProjectTask(projectId.value, t.id, t)
      }
    }
  }
  return toUpdate
}

/** 仅重编 rootId 下整棵子树（各层按 sortOrder 递归编号，不改动子树外节点） */
async function renumberSubtree(rootId: number) {
  const allUpdated: ProjectTaskItem[] = []

  async function walk(parentId: number) {
    const batch = await renumberSiblings(parentId, true, { shallow: true })
    allUpdated.push(...batch)
    for (const child of tasks.value.filter(t => t.parentId === parentId)) {
      if (child.id && tasks.value.some(t => t.parentId === child.id)) {
        await walk(child.id)
      }
    }
  }

  await walk(rootId)

  if (allUpdated.length > 0 && projectId.value) {
    const seen = new Set<number>()
    for (const t of allUpdated) {
      if (t.id && !seen.has(t.id)) {
        seen.add(t.id)
        await updateProjectTask(projectId.value, t.id, t)
      }
    }
  }
}

/**
 * 右键插入/新增同级任务
 * @param before true=在当前任务之前插入，false=之后新增
 */
async function handleCtxInsert(before: boolean) {
  const target = ctxMenuTask.value
  if (!target || !projectId.value) return
  ctxMenuVisible.value = false
  ctxMenuLoading.value = true
  try {
    // 找出所有同级兄弟，按 sortOrder 排序
    const siblings = tasks.value
      .filter(t => t.parentId === target.parentId)
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
    const targetIdx = siblings.findIndex(s => s.id === target.id)

    // 新任务插入位置（0-based）
    const insertIdx = before ? targetIdx : targetIdx + 1

    // 将 insertIdx 之后的兄弟 sortOrder 各加 1，腾出位置
    for (let i = insertIdx; i < siblings.length; i++) {
      siblings[i].sortOrder = (siblings[i].sortOrder ?? i) + 1
    }

    // 构造新任务（临时 sortOrder = insertIdx + 1，taskNo 稍后由 renumberSiblings 修正）
    const newTask: ProjectTaskItem = {
      ...newTaskForm(),
      parentId: target.parentId ?? null,
      sortOrder: insertIdx + 1,
      taskNo: '',    // 由 renumber 确定
      taskName: '新任务',
    }

    const res = await createProjectTask(projectId.value, newTask)
    newTask.id = res.data.id
    // 插入到 tasks.value 的正确位置
    const flatIdx = tasks.value.indexOf(siblings[insertIdx] ?? siblings[siblings.length - 1])
    if (before && flatIdx >= 0) {
      tasks.value.splice(flatIdx, 0, newTask)
    } else {
      const afterTask = siblings[insertIdx]
      const afterFlatIdx = afterTask ? tasks.value.indexOf(afterTask) : -1
      if (afterFlatIdx >= 0) tasks.value.splice(afterFlatIdx, 0, newTask)
      else tasks.value.push(newTask)
    }

    // 重新编号所有兄弟（及其子任务），批量保存
    await renumberSiblings(target.parentId ?? null)

    ElMessage.success(before ? '已在当前任务前插入同级任务' : '已在当前任务后新增同级任务')
  } catch {
    ElMessage.error('操作失败')
  } finally {
    ctxMenuLoading.value = false
  }
}

/* 列表编辑模式 */
const dirtyRowIds = ref<Set<number>>(new Set())
const originalRowSnapshots = ref<Map<number, ProjectTaskItem>>(new Map())

function markDirty(row: ProjectTaskItem) {
  if (!row.id) return
  const next = new Set(dirtyRowIds.value)
  next.add(row.id)
  dirtyRowIds.value = next
}

/** 列表编辑行内日期变更校验：完成日期不能早于开始日期；清空计划日期时重算父节点 */
function handleDateChange(row: ProjectTaskItem, field: 'planStartDate' | 'planFinishDate' | 'actualStartDate' | 'actualFinishDate', value: string | null | undefined) {
  if (!value) {
    // 清空日期：标记脏、重算父节点
    markDirty(row)
    if ((field === 'planStartDate' || field === 'planFinishDate') && !taskHasChildren(row.id)) {
      syncParentPlanDates()
    }
    return
  }
  // 校验完成 >= 开始
  if (field === 'planFinishDate' && row.planStartDate && value < row.planStartDate) {
    ElMessage.warning('计划完成不能早于计划开始')
    row.planFinishDate = undefined as any
    return
  }
  if (field === 'planStartDate' && row.planFinishDate && value > row.planFinishDate) {
    ElMessage.warning('计划开始不能晚于计划完成')
    row.planStartDate = undefined as any
    return
  }
  if (field === 'actualFinishDate' && row.actualStartDate && value < row.actualStartDate) {
    ElMessage.warning('实际完成不能早于实际开始')
    row.actualFinishDate = undefined as any
    return
  }
  if (field === 'actualStartDate' && row.actualFinishDate && value > row.actualFinishDate) {
    ElMessage.warning('实际开始不能晚于实际完成')
    row.actualStartDate = undefined as any
    return
  }
  markDirty(row)
  // 里程碑：计划完成 → 计划开始联动
  if (field === 'planFinishDate' && row.nodeType === 2 && value) {
    row.planStartDate = value
  }
  // 里程碑：实际完成 → 实际开始联动
  if (field === 'actualFinishDate' && row.nodeType === 2) {
    row.actualStartDate = value || undefined
  }
  // 非叶子节点的计划日期变更，向上同步父节点
  if (!taskHasChildren(row.id) && (field === 'planStartDate' || field === 'planFinishDate')) {
    syncParentPlanDates()
  }
}

/** 保存列表编辑中的脏行，保存后仍保持编辑状态（刷新快照） */
async function saveListEdits(): Promise<boolean> {
  const dirtyCount = dirtyRowIds.value.size
  if (dirtyCount === 0) {
    ElMessage.info('无数据变化')
    return true
  }
  try {
    const cascadeNos = new Set<number>()
    const allMap = new Map<number, ProjectTaskItem>()
    for (const t of tasks.value) { if (t.id) allMap.set(t.id, t) }

    for (const id of dirtyRowIds.value) {
      const row = tasks.value.find(t => t.id === id)
      if (!row || !projectId.value) continue
      applyInlineCalc(row)
      // 计划日期交叉校验
      if (row.planStartDate && row.planFinishDate && row.planFinishDate.slice(0, 10) < row.planStartDate.slice(0, 10)) {
        ElMessage.warning(`任务「${row.taskName}」计划完成时间不能早于计划开始时间，已跳过`)
        continue
      }
      // 根据前置任务约束调整当前任务的计划日期（仅前推不拉回）
      if (row.preTaskCodes) {
        const calcStart = calcStartForTaskGlobal(row, allMap)
        const curStart = row.planStartDate ?? ''
        if (calcStart && calcStart.slice(0, 10) > curStart.slice(0, 10)) {
          row.planStartDate = calcStart
          if (row.nodeType === 2) {
            row.planFinishDate = calcStart
          } else if (row.planDuration && row.planDuration > 0) {
            row.planFinishDate = dateAddDays(calcStart, row.planDuration)
          } else if (row.planFinishDate) {
            const origDur = Math.max(0, Math.round(
              (new Date(row.planFinishDate.slice(0, 10)).getTime() -
               new Date(curStart.slice(0, 10)).getTime()) / 86400000
            ))
            if (origDur > 0) row.planFinishDate = dateAddDays(calcStart, origDur)
          }
        }
      }
      await updateProjectTask(projectId.value, id, row)
      if (row.id) allMap.set(row.id, { ...row })
      cascadeNos.add(id)
      // 递归调整子节点的计划日期（基于前置任务约束）
      const changedDescendantNos = adjustDescendantsByPredecessors(id)
      for (const childId of changedDescendantNos) {
        const child = tasks.value.find(t => t.id === childId)
        if (child && child.id) {
          await updateProjectTask(projectId.value, child.id, child)
        }
      }
      for (const childId of changedDescendantNos) cascadeNos.add(childId)
      // 将脏行的直接父节点加入级联集合，确保父节点日期变化能传播到依赖它的后续任务
      const parentTask = tasks.value.find(t => t.id === row.parentId)
      if (parentTask?.id) cascadeNos.add(parentTask.id)
    }
    // 同步父节点日期 + 收集级联集合
    const parentCascadeNos = syncParentAndCollectChangedNos(cascadeNos)
    dirtyRowIds.value = new Set()
    // 保存后重新拍快照，保持编辑状态
    const snapshots = new Map<number, ProjectTaskItem>()
    for (const t of tasks.value) {
      if (t.id != null) snapshots.set(t.id, { ...t })
    }
    originalRowSnapshots.value = snapshots

    const cascadedCount = await cascadeForwardSchedule(parentCascadeNos)
    let msg = `保存成功，${dirtyCount} 条数据已更新`
    if (cascadedCount > 0) msg += `，联动更新 ${cascadedCount} 个后续任务`
    ElMessage.success(msg)
    return true
  } catch {
    return false
  }
}

/* ───────── 导出任务计划到 Excel ───────── */
async function handleExportTasks() {
  if (taskTree.value.length === 0) {
    ElMessage.warning('暂无任务计划数据')
    return
  }
  const XLSX = await import('xlsx')

  // 递归扁平化树，保留层级缩进 + 分级显示级别
  const rows: Record<string, any>[] = []
  const rowLevels: number[] = [] // Excel 分级显示级别
  function walk(nodes: any[], depth: number) {
    const indent = '　'.repeat(depth) // 全角空格确保 Excel 中可见
    for (const node of nodes) {
      rows.push({
        '序号': (node.taskNo || ''),
        '任务名称': indent + (node.taskName || ''),
        'WBS': (node.wbsCode || ''),
        '任务类别': getDictLabel('task_category', node.taskCategory),
        '进度': node.nodeType === 2 ? '里程碑' : (node.progressPct ?? 0) + '%',
        '计划开始': node.planStartDate ? node.planStartDate.slice(0, 10) : '',
        '计划完成': node.planFinishDate ? node.planFinishDate.slice(0, 10) : '',
        '实际开始': node.actualStartDate ? node.actualStartDate.slice(0, 10) : '',
        '实际完成': node.actualFinishDate ? node.actualFinishDate.slice(0, 10) : '',
        '计划工期': node.nodeType === 2 ? '0' : (node.planDuration ?? ''),
        '参考工期': node.nodeType === 2 ? '0' : (node.referenceDuration ?? ''),
        '前置任务': formatPreTaskDisplay(node.preTaskCodes) ?? '',
        '责任部门': (node.deptName || ''),
        '责任人': (node.assigneeName || ''),
        '优先级': priorityLabel(node.priority),
        '状态': taskStatusLabel(node.status),
        '逾期状态': overdueStatus(node),
        '备注': (node.remark || ''),
      })
      rowLevels.push(depth)
      if (node.children?.length) walk(node.children, depth + 1)
    }
  }
  walk(taskTree.value, 0)

  // 生成前端显示用的前置任务文本（taskName 代替 taskId）
  function formatPreTaskDisplay(raw: string | undefined): string {
    if (!raw) return ''
    const segments = raw.split(',').filter(Boolean)
    return segments.map(s => {
      const m = s.match(/^(\d+)\((.+)\)$/)
      if (!m) return s
      const id = parseInt(m[1])
      const depType = m[2]
      const taskName = tasks.value.find(t => t.id === id)?.taskName ?? `任务#${id}`
      return `${taskName}(${depType})`
    }).join(',')
  }

  const ws = XLSX.utils.json_to_sheet(rows)
  // 设置列宽
  ws['!cols'] = [
    { wch: 12 }, { wch: 30 }, { wch: 14 }, { wch: 10 }, { wch: 8 },
    { wch: 12 }, { wch: 12 }, { wch: 12 }, { wch: 12 },
    { wch: 10 }, { wch: 10 }, { wch: 25 }, { wch: 16 },
    { wch: 12 }, { wch: 8 }, { wch: 8 }, { wch: 8 }, { wch: 30 },
  ]
  // 设置行分级显示（树状折叠展开），表头行不分级
  const outlineRows: any[] = []
  rowLevels.forEach((level, idx) => {
    outlineRows[idx + 1] = { level }
  })
  ws['!rows'] = outlineRows
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, '任务计划')
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  const dateTimeStr = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
  const fileName = [form.projectCode, form.projectName, dateTimeStr].filter(Boolean).join('.')
  XLSX.writeFile(wb, `${fileName}.xlsx`)
}

async function handleToggleEditMode(newVal: boolean) {
  if (newVal) {
    listEditMode.value = true
    editingRowId.value = null
    dirtyRowIds.value = new Set()
    // 进入编辑模式时预拍所有行快照，用于"不保存"时还原
    const snapshots = new Map<number, ProjectTaskItem>()
    for (const t of tasks.value) {
      if (t.id != null) snapshots.set(t.id, { ...t })
    }
    originalRowSnapshots.value = snapshots
    return
  }
  // 退出编辑模式
  if (dirtyRowIds.value.size === 0) {
    listEditMode.value = false
    editingRowId.value = null
    return
  }

  // 先确认，与后续保存分离，避免 API 错误触发还原
  let confirmed = false
  try {
    await ElMessageBox.confirm(
      `有 ${dirtyRowIds.value.size} 条任务已修改，是否保存？`,
      '提示',
      { confirmButtonText: '保存', cancelButtonText: '不保存', type: 'warning' }
    )
    confirmed = true
  } catch { /* 用户取消 */ }

  if (confirmed) {
    const ok = await saveListEdits()
    if (ok) {
      listEditMode.value = false
      editingRowId.value = null
    }
  } else {
    // 用户选择不保存 - 还原所有脏行
    for (const [id, original] of originalRowSnapshots.value) {
      const row = tasks.value.find(t => t.id === id)
      if (row) Object.assign(row, original)
    }
    dirtyRowIds.value = new Set()
    originalRowSnapshots.value = new Map()
    listEditMode.value = false
    editingRowId.value = null
  }
}

/** 点击任务行进入该行编辑，再次点击取消选中 */
function handleTaskRowClick(row: ProjectTaskItem, _column: unknown, event: Event) {
  if (!listEditMode.value) return
  if ((event.target as HTMLElement).closest('button, .el-button, a, .el-switch, .el-select, .el-date-picker, .el-input-number, .el-tree-select, .el-input, .el-tag, .el-progress')) return
  editingRowId.value = editingRowId.value === row.id ? null : (row.id ?? null)
}

/* ─────────────────────────────────────────────────────────────────────────
 * 前向排程级联（Forward Scheduling Cascade）
 * ────────────────────────────────────────────────────────────────────────*/

/**
 * 时区安全的日期加减：只操作日历日期（本地时区），不涉及 UTC 转换。
 * 避免 new Date(str).toISOString() 在 UTC+ 时区偏移 1 天的 bug。
 */
/**
 * 递归检查当前任务节点的子节点，根据前置任务约束（步骤1）调整子节点的计划开始和计划完成。
 * 返回所有被调整的子节点 ID 数组。
 */
function adjustDescendantsByPredecessors(taskId: number): number[] {
  const changed: number[] = []
  const allMap = new Map<number, ProjectTaskItem>()
  for (const t of tasks.value) { if (t.id) allMap.set(t.id, t) }

  function walk(nodeId: number) {
    const children = tasks.value.filter(t => t.parentId === nodeId)
    for (const child of children) {
      if (!child.id) continue
      // 步骤1：根据前置任务约束调整子节点
      if (child.preTaskCodes) {
        const calcStart = calcStartForTaskGlobal(child, allMap)
        if (calcStart) {
          const curStart = child.planStartDate ?? ''
          if (calcStart.slice(0, 10) > curStart.slice(0, 10)) {
            child.planStartDate = calcStart
            if (child.nodeType === 2) {
              child.planFinishDate = calcStart
            } else if (child.planDuration && child.planDuration > 0) {
              child.planFinishDate = dateAddDays(calcStart, child.planDuration)
            } else if (child.planFinishDate) {
              const origDur = Math.max(0, Math.round(
                (new Date(child.planFinishDate.slice(0, 10)).getTime() -
                 new Date(curStart.slice(0, 10)).getTime()) / 86400000
              ))
              if (origDur > 0) child.planFinishDate = dateAddDays(calcStart, origDur)
            }
            changed.push(child.id)
            if (child.id) allMap.set(child.id, child)
          }
        }
      }
      // 递归处理子节点的子节点
      walk(child.id)
    }
  }

  walk(taskId)
  return changed
}

function dateAddDays(dateStr: string, days: number): string {
  // 仅取 "YYYY-MM-DD" 部分，用本地时区构造，避免 UTC 偏差
  const [y, m, d] = dateStr.slice(0, 10).split('-').map(Number)
  const dt = new Date(y, m - 1, d)
  dt.setDate(dt.getDate() + days)
  const yy = dt.getFullYear()
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${yy}-${mm}-${dd}T00:00:00`
}

/** 两个日期字符串比较，只比较 YYYY-MM-DD 部分 */
function dateStrGt(a: string, b: string): boolean {
  return a.slice(0, 10) > b.slice(0, 10)
}

/**
 * 执行 syncParentPlanDates() 并返回"包含初始变更 + 所有日期改变的父节点 taskNo"的集合。
 *
 * 解决的问题：直接调用 syncParentPlanDates() 会先把父节点日期更新，
 * 导致后续 cascadeForwardSchedule 里的 bubbleUpParent 认为父节点没变化而不入队，
 * 从而漏掉依赖父节点的后续任务（如 002、003 依赖 001）。
 */
function syncParentAndCollectChangedNos(seedNos: Set<number>): Set<number> {
  // 快照所有父节点（有子任务的节点）的计划日期
  const before = new Map<number, { start?: string; finish?: string }>()
  for (const t of tasks.value) {
    if (taskHasChildren(t.id)) {
      before.set(t.id, { start: t.planStartDate, finish: t.planFinishDate })
    }
  }

  syncParentPlanDates()

  // 找出日期发生变化的父节点，加入级联集合
  const result = new Set<number>(seedNos)
  for (const t of tasks.value) {
    if (!t.id || !taskHasChildren(t.id)) continue
    const snap = before.get(t.id)
    if (!snap) continue
    if (
      !dateStrSameDay(t.planStartDate, snap.start ?? null) ||
      !dateStrSameDay(t.planFinishDate, snap.finish ?? null)
    ) {
      result.add(t.id)
    }
  }
  return result
}

/** 两个日期字符串是否在同一日（YYYY-MM-DD 相等） */
function dateStrSameDay(a: string | undefined, b: string | null): boolean {
  if (!a || !b) return false
  return a.slice(0, 10) === b.slice(0, 10)
}

/**
 * 根据某任务的全部前置约束，计算其最早可开始日期（取所有约束的最晚值）。
 * allMap: taskNo → 当前最新 task 对象。
 */
function calcStartForTaskGlobal(
  task: ProjectTaskItem,
  allMap: Map<number, ProjectTaskItem>
): string | null {
  if (!task.preTaskCodes) return null
  const preds = parsePreTaskCodes(task.preTaskCodes)
  if (preds.length === 0) return null

  let latest: string | null = null
  for (const row of preds) {
    const pred = allMap.get(row.taskId)
    if (!pred) continue
    let base: string | undefined
    if (row.dependencyType === 'FS' || row.dependencyType === 'FF') base = pred.planFinishDate
    else if (row.dependencyType === 'SS' || row.dependencyType === 'SF') base = pred.planStartDate
    if (!base) continue
    const ds = dateAddDays(base, row.lagDays ?? 0)   // ← 时区安全
    if (!latest || dateStrGt(ds, latest)) latest = ds
  }
  return latest
}

async function cascadeForwardSchedule(changedTaskNos: Set<number>): Promise<number> {
  if (!projectId.value || changedTaskNos.size === 0) return 0

  // taskNo → task（直接引用 tasks.value 元素，修改即实时生效）
  const allMap = new Map<number, ProjectTaskItem>()
  // id → task（用于父节点向上冒泡）
  const idMap = new Map<number, ProjectTaskItem>()
  for (const t of tasks.value) {
    if (t.id) allMap.set(t.id, t)
    if (t.id)     idMap.set(t.id, t)
  }

  // 反向索引：前置 taskNo → 依赖它的后续任务[]
  const successorIndex = new Map<number, ProjectTaskItem[]>()
  for (const t of tasks.value) {
    if (!t.preTaskCodes) continue
    for (const p of parsePreTaskCodes(t.preTaskCodes)) {
      if (!p.taskId) continue
      if (!successorIndex.has(p.taskId)) successorIndex.set(p.taskId, [])
      successorIndex.get(p.taskId)!.push(t)
    }
  }

  // BFS 广度优先传播（enqueued 防重入）
  const queue: number[] = [...changedTaskNos]
  const enqueued = new Set<number>()
  const tasksToSave: ProjectTaskItem[] = []

  /**
   * 当某任务日期发生变化后，向上冒泡更新所有祖先节点的汇总日期。
   * 若祖先日期因此改变，将祖先也加入传播队列，以便继续传播给依赖祖先的后续任务。
   */
  function bubbleUpParent(child: ProjectTaskItem) {
    if (!child.parentId) return
    const parent = idMap.get(child.parentId)
    if (!parent?.id) return

    let parentChanged = false

    // 取所有直接子节点的最早开始 / 最晚结束
    const siblings = tasks.value.filter(t => t.parentId === parent.id)
    const starts   = siblings.map(t => t.planStartDate).filter(Boolean) as string[]
    const finishes = siblings.map(t => t.planFinishDate).filter(Boolean) as string[]
    if (starts.length > 0) {
      const earliest = starts.reduce((a, b) => (a.slice(0, 10) < b.slice(0, 10) ? a : b))
      if (!dateStrSameDay(parent.planStartDate, earliest)) {
        parent.planStartDate = earliest
        parentChanged = true
      }
    }
    if (finishes.length > 0) {
      const latest = finishes.reduce((a, b) => (a.slice(0, 10) > b.slice(0, 10) ? a : b))
      if (!dateStrSameDay(parent.planFinishDate, latest)) {
        parent.planFinishDate = latest
        parentChanged = true
      }
    }

    if (parentChanged) {
      if (!tasksToSave.includes(parent)) tasksToSave.push(parent)
      // 父节点日期变了 → 加入 BFS 队列，传播给依赖父节点的后续任务
      if (parent.id && !enqueued.has(parent.id)) {
        enqueued.add(parent.id)
        queue.push(parent.id)
      }
      // 继续向上冒泡
      bubbleUpParent(parent)
    }
  }

  while (queue.length > 0) {
    const predNo = queue.shift()!
    const successors = successorIndex.get(predNo) ?? []

    for (const succ of successors) {
      if (!succ.id || enqueued.has(succ.id)) continue

      const newStart = calcStartForTaskGlobal(succ, allMap)
      if (!newStart) continue

      // 前向排程：只"前推"（newStart 晚于当前计划开始时才调整），不回拉
      // 即：A 已安排在 B+5 之后，保持不动；只有 A 比约束更早时才推迟
      const currentStart = succ.planStartDate ?? ''
      if (newStart.slice(0, 10) <= currentStart.slice(0, 10)) continue

      let changed = false

      succ.planStartDate = newStart
      changed = true

      if (succ.nodeType === 2) {
        if (!dateStrSameDay(succ.planFinishDate, newStart)) {
          succ.planFinishDate = newStart
        }
      } else if (succ.planDuration && succ.planDuration > 0) {
        succ.planFinishDate = dateAddDays(newStart, succ.planDuration)
      } else if (succ.planFinishDate) {
        // 无工期时保持原有间隔整体平移
        const origDur = Math.max(0, Math.round(
          (new Date(currentStart.slice(0, 10)).getTime()
            ? (new Date(succ.planFinishDate.slice(0, 10)).getTime() -
               new Date(currentStart.slice(0, 10)).getTime()) / 86400000
            : 0)
        ))
        if (origDur > 0) succ.planFinishDate = dateAddDays(newStart, origDur)
      }

      if (changed) {
        if (succ.id) enqueued.add(succ.id)
        if (succ.id) queue.push(succ.id)
        tasksToSave.push(succ)
        // 后续任务日期变了 → 向上冒泡更新其父节点
        bubbleUpParent(succ)
      }
    }
  }

  // 批量保存到后端（去重）
  const uniqueTasks = [...new Map(tasksToSave.map(t => [t.id, t])).values()]
  if (uniqueTasks.length > 0) {
    cascadeLoading.value = true
    try {
      for (const task of uniqueTasks) {
        if (task.id) await updateProjectTask(projectId.value, task.id, task)
      }
      syncParentPlanDates()
    } finally {
      cascadeLoading.value = false
    }
  }

  return uniqueTasks.length
}

const cascadeLoading = ref(false)

/* 复刻弹窗编辑时的 watch 逻辑，批量保存前计算冗余字段 */
function applyInlineCalc(row: ProjectTaskItem) {
  const hasChildren = taskHasChildren(row.id)
  if (row.nodeType === 2) {
    if (row.planFinishDate) row.planStartDate = row.planFinishDate
    if (row.actualFinishDate) row.actualStartDate = row.actualFinishDate
    else row.actualStartDate = undefined
    row.planDuration = 0
    row.actualDuration = 0
    row.referenceDuration = 0
  }
  if (!hasChildren && row.planStartDate && row.planFinishDate) {
    const days = Math.round((new Date(row.planFinishDate).getTime() - new Date(row.planStartDate).getTime()) / (1000 * 60 * 60 * 24))
    if (days >= 0) row.planDuration = days
  }
  if (!hasChildren && row.actualStartDate && row.actualFinishDate) {
    const days = Math.round((new Date(row.actualFinishDate).getTime() - new Date(row.actualStartDate).getTime()) / (1000 * 60 * 60 * 24))
    if (days >= 0) row.actualDuration = days
  }
  if (row.actualFinishDate) {
    row.status = 2
    row.progressPct = 100
  } else if (row.actualStartDate) {
    row.status = 1
  } else {
    row.status = 0
    row.progressPct = 0
  }
}

async function loadTasks() {
  if (!projectId.value) return
  const res = await getProjectTasks(projectId.value)
  tasks.value = res.data
  syncParentPlanDates()
}

/* 构建树形结构 - 仅当 children 实际变化时才修改原始对象，避免不必要响应式触发 */
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
  // 按 sortOrder → taskNo 排序
  const sortFn = (a: ProjectTaskItem, b: ProjectTaskItem) =>
    (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true })
  roots.sort(sortFn)
  for (const [, children] of newChildren) {
    if (children.length > 1) children.sort(sortFn)
  }
  // 仅当 children 内容实际变化时才更新，避免 computed 求值期间触发自身依赖
  for (const t of flat) {
    if (t.id == null) continue
    const nc = newChildren.get(t.id) || []
    const old = t.children || []
    const changed = nc.length !== old.length || nc.some((c, i) => c !== old[i])
    if (changed) t.children = nc
  }
  return roots
}

const taskTree = computed(() => buildTaskTree(tasks.value))
const taskIdMap = computed(() => new Map(tasks.value.filter(t => t.id).map(t => [t.id!, t] as [number, ProjectTaskItem])))

/** 里程碑列表：从任务计划中筛选 nodeType === 2 的任务 */
const milestoneTasks = computed(() =>
  tasks.value.filter(t => t.nodeType === 2).sort((a, b) => (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true }))
)

/** 考核任务编码：从任务类别字典中查找「考核任务」对应的 dictCode */
const assessmentTaskCode = computed(() =>
  dictMap.value['task_category']?.find(d => d.dictLabel === '考核任务')?.dictCode ?? null
)

/** 考核任务列表：从任务计划中筛选任务类别为考核任务的任务 */
const assessmentTasks = computed(() => {
  if (!assessmentTaskCode.value) return []
  return tasks.value
    .filter(t => t.taskCategory === assessmentTaskCode.value)
    .sort((a, b) => (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true }))
})

/** 序号列宽根据最长 taskNo 和树深度自动适配（树缩进 16px/层 + 展开图标 18px） */
const taskNoColWidth = computed(() => {
  let maxLen = 0
  let maxDepth = 0
  function walk(nodes: ProjectTaskItem[], depth: number) {
    for (const n of nodes) {
      const s = n.taskNo || ''
      if (s.length > maxLen) maxLen = s.length
      if (depth > maxDepth) maxDepth = depth
      if (n.children) walk(n.children, depth + 1)
    }
  }
  walk(taskTree.value, 0)
  // 每字符 10px + 树缩进 16px/层 + 展开图标 ~22px + 单元格内边距 ~26px
  return Math.max(60, maxLen * 10 + maxDepth * 16 + 48)
})

// 序号列宽变化时同步 fixed 列表格布局，避免固定列宽度不同步导致换行
watch(taskNoColWidth, () => {
  nextTick(() => { (taskTableRef.value as any)?.doLayout?.() })
})

/* ───────── 模板新增 ───────── */
const filteredTemplates = computed(() => {
  if (!templateSearch.value) return templateList.value
  const kw = templateSearch.value.toLowerCase()
  return templateList.value.filter(t =>
    t.templateName.toLowerCase().includes(kw) ||
    t.templateCode.toLowerCase().includes(kw)
  )
})

async function openTemplateDialog() {
  templateLoading.value = true
  selectedTemplateId.value = null
  templateSearch.value = ''
  try {
    const res = await getTemplateList({ pageIndex: 1, pageSize: 200, templateType: 1 })
    templateList.value = res.data.items
    showTemplateDialog.value = true
  } catch { /* 拦截器统一处理 */ }
  finally { templateLoading.value = false }
}

async function handleCreateFromTemplate() {
  if (!projectId.value || !selectedTemplateId.value) {
    ElMessage.warning('请选择一个模板')
    return
  }
  try {
    await ElMessageBox.confirm(
      '从模板新增将清空当前所有任务计划数据，确定继续吗？',
      '提示',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' }
    )
  } catch {
    return // 用户取消
  }
  templateLoading.value = true
  try {
    const res = await createTasksFromTemplate(projectId.value, selectedTemplateId.value)
    ElMessage.success(`成功创建 ${res.data.count} 条任务`)
    showTemplateDialog.value = false
    await loadTasks()
  } catch { /* 拦截器统一处理 */ }
  finally { templateLoading.value = false }
}

/* ───────── 任务列表（看板分组） ───────── */
const boardGroupMode = ref<'category' | 'assignee' | 'dept'>('assignee')

function getGroupKey(task: ProjectTaskItem): string {
  if (boardGroupMode.value === 'assignee') return task.assigneeName || '未指定'
  if (boardGroupMode.value === 'dept') return task.deptName || '未指定'
  return task.taskCategory || ''
}
function getGroupLabel(key: string): string {
  if (!key) return '其他'
  if (boardGroupMode.value === 'assignee') return key
  if (boardGroupMode.value === 'dept') return key
  return dictMap.value['task_category']?.find(d => d.dictCode === key)?.dictLabel ?? key
}

const boardData = computed(() => {
  const parentIds = new Set(tasks.value.filter(t => t.parentId).map(t => t.parentId))
  const leafTasks = tasks.value.filter(t => t.id && !parentIds.has(t.id) && t.nodeType === 1)
  const groups = [...new Set(leafTasks.map(t => getGroupKey(t)))]
  groups.sort((a, b) => {
    if (!a || a === '未指定') return 1
    if (!b || b === '未指定') return -1
    return 0
  })
  return groups.map(g => ({
    category: g,
    tasks: leafTasks.filter(t => getGroupKey(t) === g)
  }))
})

const changeTabRef = ref<InstanceType<typeof ProjectChangeTab> | null>(null)
const financeTabRef = ref<InstanceType<typeof ProjectFinanceTab> | null>(null)

/* ───────── 甘特图虚拟起止节点 ───────── */
const VIRTUAL_START_ID = -1
const VIRTUAL_END_ID = -2

function makeVirtualTask(id: number, taskNo: string, taskName: string, planDate: string): ProjectTaskItem {
  return {
    id, taskNo, taskName, wbsCode: '', nodeType: 2, sortOrder: 0,
    status: 2, priority: 3, planStartDate: planDate, planFinishDate: planDate,
    deliverableCnt: 0, progressPct: 100, children: [],
  }
}
function isVirtualTask(id?: number) { return id === VIRTUAL_START_ID || id === VIRTUAL_END_ID }
function isLeafTask(task: ProjectTaskItem) { return !(task.children && task.children.length > 0) }

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
  for (const t of tasks.value) { const d = parseDate(t.planStartDate); if (d && (!earliest || d < earliest)) earliest = d }
  return earliest
})
const ganttLatestDate = computed<Date | null>(() => {
  let latest: Date | null = null
  for (const t of tasks.value) {
    // 计划结束
    const dp = parseDate(t.planFinishDate)
    if (dp && (!latest || dp > latest)) latest = dp
    // 实际结束（若有）
    const da = parseDate(t.actualFinishDate)
    if (da && (!latest || da > latest)) latest = da
    // 有实际开始但无实际结束：估算结束 = 实际开始 + 计划工期
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
      // 叶子任务才按实际日期绘制；父/汇总任务实际条始终与计划条对齐
      const isLeaf = !(task.children && task.children.length > 0)
      if (isLeaf && task.nodeType === 1 && timelineStart) {
        const aStart = parseDate(task.actualStartDate)
        const aFinish = parseDate(task.actualFinishDate)
        if (aStart) {
          actualBarLeft = calcLeft(aStart)
          actualBarWidth = aFinish ? calcWidth(aStart, aFinish) : barWidth
        }
      }
      // 逾期状态取自与任务计划一致的 overdueStatus 判断
      let ds = task.status
      if (overdueStatus(task) === '已逾期') ds = 3
      // 实际条颜色：逾期优先；否则按实际日期推断（完成→已完成，已开始→进行中）
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
  // 二次处理：父任务实际条宽度向上传递，覆盖子孙任务最右侧的实际结束位置
  const itemById = new Map<number, GanttFlatItem>()
  for (const item of result) { if (item.task.id) itemById.set(item.task.id, item) }
  // 倒序遍历：子任务先于父任务被处理，保证多级汇总正确
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

  // ── 插入虚拟起止节点 ──
  if (result.length > 0 && timelineStart) {
    // 收集叶子任务中有效日期的 planStartDate 最早值
    const leafTasks = result.filter(item => isLeafTask(item.task) && item.task.planStartDate)
    const startDateStr = leafTasks.length > 0
      ? leafTasks.reduce((min, item) => item.task.planStartDate! < min ? item.task.planStartDate! : min, leafTasks[0].task.planStartDate!)
      : null

    // 收集被引用任务 ID（出度计算）
    const referencedIds = new Set<number>()
    for (const item of result) {
      if (!item.task.preTaskCodes) continue
      for (const p of parsePreTaskCodes(item.task.preTaskCodes)) {
        referencedIds.add(p.taskId)
      }
    }
    // 无后继叶子：叶子任务中 ID 不在被引用集合中的
    const noSuccessorLeafs = leafTasks.filter(item => {
      const id = item.task.id
      return id && !referencedIds.has(id)
    })
    const endDateStr = noSuccessorLeafs.length > 0
      ? noSuccessorLeafs.reduce((max, item) => {
          const d = item.task.planFinishDate || item.task.planStartDate!
          return d > max ? d : max
        }, noSuccessorLeafs[0].task.planFinishDate || noSuccessorLeafs[0].task.planStartDate!)
      : (leafTasks.length > 0 ? leafTasks[leafTasks.length - 1].task.planFinishDate || leafTasks[leafTasks.length - 1].task.planStartDate! : null)

    if (startDateStr) {
      const startDate = parseDate(startDateStr)!
      const startBarLeft = calcLeft(startDate)
      const startTask = makeVirtualTask(VIRTUAL_START_ID, '开始', '项目开始', startDateStr)
      result.unshift({ task: startTask, level: 0, index: 0, barLeft: startBarLeft, barWidth: 0, actualBarLeft: startBarLeft, actualBarWidth: 0, displayStatus: 2, actualDisplayStatus: 2 })
    }
    if (endDateStr) {
      const endDate = parseDate(endDateStr)!
      const endBarLeft = calcLeft(endDate)
      const endTask = makeVirtualTask(VIRTUAL_END_ID, '结束', '项目结束', endDateStr)
      result.push({ task: endTask, level: 0, index: 0, barLeft: endBarLeft, barWidth: 0, actualBarLeft: endBarLeft, actualBarWidth: 0, displayStatus: 2, actualDisplayStatus: 2 })
    }
    // 全局重新编号
    result.forEach((item, i) => { item.index = i })
  }

  return result
})

/* ───────── 甘特图前置任务连线（MS Project FS 走线风格）───────── */
const GANTT_ROW_H = 54
const GANTT_PLAN_BAR_TOP = 6
const GANTT_PLAN_BAR_H  = 17
const GANTT_LINE_Y_OFFSET = GANTT_PLAN_BAR_TOP + Math.round(GANTT_PLAN_BAR_H / 2)
const GANTT_VIRTUAL_LINE_Y  = Math.round(GANTT_ROW_H / 2)  // Start/End 线/点与左侧文本居中对齐

/**
 * 构建 FS（Finish→Start）依赖连线路径
 *
 * 规则（与 MS Project 一致）：
 *  - 出口：前置任务右侧向右伸出 STUB 距离
 *  - 入口：后续任务左侧从左向右接入（箭头始终朝右 →）
 *
 *  正向（x1+STUB+r ≤ x2）：
 *    右出→ 竖直（在 x=x1+STUB）→ 横向到达 x2 并进入 ↩
 *
 *  反向（x1+STUB+r > x2，前置结束比后续开始还靠右）：
 *    右出→ 竖直越过后续所在行→ 向左绕到 x2-STUB→ 竖直回到后续行→ 右进入 ↩
 *    四个圆角方向全部一致（向下时全 CW=1，向上时全 CCW=0）
 *
 *  同行反向：向下绕行半行再折回
 */
function buildDepPath(x1: number, y1: number, x2: number, y2: number): string {
  const STUB = 10  // 出入口横向短桩
  const r = 5      // 圆角半径
  const WRAP_EXTRA = GANTT_ROW_H * 0.55  // 绕行时超出目标行的额外距离

  const sameRow = Math.abs(y2 - y1) < 1
  const goingDown = sameRow ? true : y2 > y1
  const ys = goingDown ? 1 : -1
  const sw = goingDown ? 1 : 0
  const effectiveY2 = sameRow ? y1 : y2

  // ── 同行正向：简单水平线 ──
  if (sameRow && x1 + STUB + r <= x2) {
    return `M ${x1} ${y1} L ${x2} ${y2}`
  }

  // ── 不同行 + 后续在右侧（x2 > x1 + r）：Z 形路线（右→下→右）──
  // 确保 x2 - r >= x1（有入口弧空间），只要后续任务在右侧就用 Z 形，避免绕圈
  if (!sameRow && x2 > x1 + r) {
    const sw1 = goingDown ? 1 : 0  // 右→下/上
    const sw2 = goingDown ? 0 : 1  // 下/上→右
    // 纵向折转 x：优先标准 STUB，但不超过 x2-r（保留入口弧空间，确保最终段向右）
    const vx = Math.min(x1 + STUB, x2 - r)
    // 初始水平段（vx > x1+r 才有空间）
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

  // ── 不同行 backward（x2 <= x1+r）：S 形阶梯路线（右→下→左→下→右）──
  // 经过两行中点，宽度紧凑（S_STUB 控制横向宽度）
  if (!sameRow) {
    const S_STUB = 7   // S 形两侧桩长，控制 S 的横向宽度
    const midY   = (y1 + effectiveY2) / 2
    const rightX = x1 + S_STUB
    const leftX  = x2 - S_STUB
    const sw1 = goingDown ? 1 : 0
    const sw2 = goingDown ? 1 : 0
    const sw3 = goingDown ? 0 : 1   // left→down/up sweep 与其余相反
    const sw4 = goingDown ? 0 : 1
    return [
      `M ${x1} ${y1}`,
      `L ${rightX - r} ${y1}`,
      `A ${r} ${r} 0 0 ${sw1} ${rightX} ${y1 + ys * r}`,         // C1
      `L ${rightX} ${midY - ys * r}`,
      `A ${r} ${r} 0 0 ${sw2} ${rightX - r} ${midY}`,             // C2
      `L ${leftX + r} ${midY}`,
      `A ${r} ${r} 0 0 ${sw3} ${leftX} ${midY + ys * r}`,         // C3
      `L ${leftX} ${effectiveY2 - ys * r}`,
      `A ${r} ${r} 0 0 ${sw4} ${leftX + r} ${effectiveY2}`,       // C4
      `L ${x2} ${effectiveY2}`,
    ].join(' ')
  }

  // ── 同行反向：绕行到行下方 ──
  const x1s = x1 + STUB
  const x2s = x2 - STUB
  const wrapY = effectiveY2 + ys * WRAP_EXTRA

  // 确保绕行宽度足够，防止弧段重叠形成视觉环路
  const minHorizGap = r * 2 + STUB
  const leftX = (x1s - x2s) < (minHorizGap + r * 2)
    ? x1s - minHorizGap - r * 2
    : x2s

  return [
    `M ${x1} ${y1}`,
    `L ${x1s - r} ${y1}`,
    `A ${r} ${r} 0 0 ${sw} ${x1s} ${y1 + ys * r}`,        // 角1: 右→下/上
    `L ${x1s} ${wrapY - ys * r}`,
    `A ${r} ${r} 0 0 ${sw} ${x1s - r} ${wrapY}`,           // 角2: 下/上→左
    `L ${leftX + r} ${wrapY}`,
    `A ${r} ${r} 0 0 ${sw} ${leftX} ${wrapY - ys * r}`,    // 角3: 左→上/下
    `L ${leftX} ${effectiveY2 + ys * r}`,
    `A ${r} ${r} 0 0 ${sw} ${leftX + r} ${effectiveY2}`,   // 角4: 上/下→右
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
  // 构建被引用任务 ID 集合（出度判断）
  const referencedIds = new Set<number>()
  for (const item of flat) {
    if (!item.task.preTaskCodes || isVirtualTask(item.task.id)) continue
    for (const p of parsePreTaskCodes(item.task.preTaskCodes)) {
      referencedIds.add(p.taskId)
    }
  }
  const startItem = flat.find(it => it.task.id === VIRTUAL_START_ID)
  const endItem = flat.find(it => it.task.id === VIRTUAL_END_ID)

  for (const item of flat) {
    if (isVirtualTask(item.task.id)) continue
    const preCodes = item.task.preTaskCodes
    if (preCodes) {
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
    // Start → 无前置叶子任务
    if (!preCodes && startItem && isLeafTask(item.task) && item.task.planStartDate) {
      const x1 = startItem.barLeft + startItem.barWidth
      const x2 = item.barLeft
      const y1 = startItem.index * GANTT_ROW_H + GANTT_VIRTUAL_LINE_Y
      const y2 = item.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      lines.push({ path: buildDepPath(x1, y1, x2, y2) })
    }
    // 无后继叶子 → End
    if (endItem && isLeafTask(item.task) && item.task.id && !referencedIds.has(item.task.id) && item.task.planStartDate) {
      const x1 = item.barLeft + item.barWidth
      const x2 = endItem.barLeft
      const y1 = item.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      const y2 = endItem.index * GANTT_ROW_H + GANTT_VIRTUAL_LINE_Y
      lines.push({ path: buildDepPath(x1, y1, x2, y2) })
    }
  }
  return lines
})

/* ─── 关键路径计算 ─────────────────────────────────────────────────────────
 * 算法：前向传递（ES/EF）+ 后向传递（LS/LF）+ 总时差 = LS - ES
 * 总时差 = 0 的任务即为关键任务，它们之间的依赖连线构成关键路径。
 * ─────────────────────────────────────────────────────────────────────── */
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

  // 构建拓扑排序所需的结构
  // 只对叶子任务运行 CPM，排除父/汇总任务 + 已完成任务（调度已固化的不再参与）
  const taskMap = new Map<number, GanttFlatItem>()
  for (const item of flat) {
    if (item.task.id && isLeafTask(item.task) && item.task.status !== 2)
      taskMap.set(item.task.id, item)
  }
  if (taskMap.size === 0) return empty

  // 构建依赖图和入度
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

  // ── 引入 Start / End 虚拟节点（单源单汇）──
  const startItem = flat.find(it => it.task.id === VIRTUAL_START_ID)
  const endItem = flat.find(it => it.task.id === VIRTUAL_END_ID)
  let startNodeId = VIRTUAL_START_ID
  let endNodeId = VIRTUAL_END_ID
  if (startItem) {
    succMap.set(startNodeId, [])
    inDeg.set(startNodeId, 0)
    // Start → 所有无前置叶子任务
    for (const [id, item] of taskMap) {
      const hasPre = item.task.preTaskCodes && parsePreTaskCodes(item.task.preTaskCodes).some(p => taskMap.has(p.taskId))
      if (!hasPre) {
        succMap.get(startNodeId)!.push({ id, lagDays: 0 })
        inDeg.set(id, (inDeg.get(id) ?? 0) + 1)
      }
    }
  }
  if (endItem) {
    succMap.set(endNodeId, [])
    inDeg.set(endNodeId, 0)
    // 收集被引用任务 ID（本 CPM 范围内的）
    const cpmReferenced = new Set<number>()
    for (const [, item] of taskMap) {
      if (!item.task.preTaskCodes) continue
      for (const p of parsePreTaskCodes(item.task.preTaskCodes)) {
        if (taskMap.has(p.taskId)) cpmReferenced.add(p.taskId)
      }
    }
    // 所有无后继叶子 → End
    for (const [id] of taskMap) {
      if (cpmReferenced.has(id)) continue
      succMap.get(id)!.push({ id: endNodeId, lagDays: 0 })
      inDeg.set(endNodeId, (inDeg.get(endNodeId) ?? 0) + 1)
    }
  }

  // 拓扑排序（Kahn 算法）
  const topo: number[] = []
  const q: number[] = []
  for (const [n, d] of inDeg) {
    if (d === 0) q.push(n)
  }
  const deg2 = new Map(inDeg)
  while (q.length) {
    const cur = q.shift()!; topo.push(cur)
    for (const { id: no } of succMap.get(cur) ?? []) {
      deg2.set(no, (deg2.get(no) ?? 1) - 1)
      if (deg2.get(no) === 0) q.push(no)
    }
  }

  // 前向传递：ES / EF（日偏移量，以 earliest 为日期 0 基准）
  const es = new Map<number, number>()
  const ef = new Map<number, number>()

  // Start 节点：ES=EF=0
  if (startItem) {
    es.set(startNodeId, 0)
    ef.set(startNodeId, 0)
  }

  for (const no of topo) {
    if (no === startNodeId || no === endNodeId) continue
    const item = taskMap.get(no); if (!item) continue
    // planFinishDate 是最后工作日（含），+1 转为排他性结束天
    const dur = Math.max(1, toDay(item.task.planFinishDate) - toDay(item.task.planStartDate) + 1)
    // ES = max(所有前置.EF + lag)；Start 已通过 succMap 连接，统一处理
    let esVal = 0
    if (startItem && es.has(startNodeId)) {
      // 遍历所有可能的前置节点
      for (const [predId, preds] of succMap) {
        for (const s of preds) {
          if (s.id === no) {
            const constraint = (ef.get(predId) ?? 0) + s.lagDays
            if (constraint > esVal) esVal = constraint
          }
        }
      }
    }
    // 同时检查原始 preTaskCodes 中的延迟（补充 lag 不为 0 的精确约束）
    if (item.task.preTaskCodes) {
      for (const p of parsePreTaskCodes(item.task.preTaskCodes)) {
        if (!taskMap.has(p.taskId) && (!startItem || p.taskId !== startNodeId)) continue
        const constraint = (ef.get(p.taskId) ?? 0) + (p.lagDays ?? 0)
        if (constraint > esVal) esVal = constraint
      }
    }
    es.set(no, esVal); ef.set(no, esVal + dur)
  }

  // End.ES = max(所有无后继叶子.EF), End.EF = End.ES
  if (endItem) {
    let endES = 0
    for (const [id] of taskMap) {
      const val = ef.get(id)
      if (val !== undefined && val > endES) endES = val
    }
    es.set(endNodeId, endES)
    ef.set(endNodeId, endES)
  }

  const projectEnd = ef.get(endNodeId) ?? Math.max(0, ...[...ef.values()])

  // 后向传递：LF / LS
  const lf = new Map<number, number>()
  const ls = new Map<number, number>()
  for (const no of topo) lf.set(no, projectEnd)

  // End 的 LS=LF=projectEnd
  if (endItem) {
    lf.set(endNodeId, projectEnd)
    ls.set(endNodeId, projectEnd)
  }

  for (const no of [...topo].reverse()) {
    if (no === endNodeId) continue
    const item = taskMap.get(no)
    const dur = item ? (ef.get(no) ?? 0) - (es.get(no) ?? 0) : 0
    const lfVal = lf.get(no) ?? projectEnd
    ls.set(no, lfVal - dur)
    // 反传到前置任务
    const preds: { taskId: number; lagDays: number }[] = []
    if (item?.task.preTaskCodes) {
      preds.push(...parsePreTaskCodes(item.task.preTaskCodes).filter(p => taskMap.has(p.taskId)))
    }
    // Start 也是前置
    if (startItem) {
      const hasRealPre = item?.task.preTaskCodes && parsePreTaskCodes(item.task.preTaskCodes).some(p => taskMap.has(p.taskId))
      if (!hasRealPre && item) preds.push({ taskId: startNodeId, lagDays: 0 })
    }
    for (const p of preds) {
      if (!taskMap.has(p.taskId) && p.taskId !== startNodeId) continue
      const constraint = (ls.get(no) ?? 0) - (p.lagDays ?? 0)
      const oldLF = lf.get(p.taskId)
      if (oldLF === undefined || constraint < oldLF) lf.set(p.taskId, constraint)
    }
  }

  // Start 的 LS/LF 最终确定（来自后向传递的结果）
  if (startItem) {
    const startLF = lf.get(startNodeId)
    if (startLF !== undefined) ls.set(startNodeId, startLF)
  }

  // 总时差 = LS - ES ≤ 0 → 关键任务（排除虚拟节点）
  const taskNos = new Set<number>()
  for (const [no, esVal] of es) {
    if (no === startNodeId || no === endNodeId) continue
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
  const startItem = flat.find(it => it.task.id === VIRTUAL_START_ID)
  const endItem   = flat.find(it => it.task.id === VIRTUAL_END_ID)

  // 收集已被关键前置引用的关键任务（用于找"第一个关键任务"和"最后一个关键任务"）
  const hasCriticalPred = new Set<number>()
  for (const item of flat) {
    if (!item.task.id || !item.task.preTaskCodes) continue
    if (!taskNos.has(item.task.id)) continue
    for (const code of item.task.preTaskCodes.split(',').map(c => c.trim()).filter(Boolean)) {
      const idMatch = code.match(/^(\d+)/)
      if (!idMatch) continue
      const predId = parseInt(idMatch[1], 10)
      if (taskNos.has(predId)) hasCriticalPred.add(item.task.id)
    }
  }

  // 普通关键连线
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

  // Start → 首个关键叶子任务（无关键前置的关键任务）
  if (startItem) {
    for (const item of flat) {
      const id = item.task.id
      if (!id || isVirtualTask(id)) continue
      if (!taskNos.has(id)) continue
      if (hasCriticalPred.has(id)) continue
      if (!item.task.planStartDate) continue
      const x1 = startItem.barLeft + startItem.barWidth
      const x2 = item.barLeft
      const y1 = startItem.index * GANTT_ROW_H + GANTT_VIRTUAL_LINE_Y
      const y2 = item.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      lines.push({ path: buildDepPath(x1, y1, x2, y2) })
    }
  }

  // 最后一个关键叶子任务 → End（不被其他关键任务作为前置）
  if (endItem) {
    const criticalAsPred = new Set<number>()
    for (const item of flat) {
      if (!item.task.preTaskCodes) continue
      for (const code of item.task.preTaskCodes.split(',').map(c => c.trim()).filter(Boolean)) {
        const idMatch = code.match(/^(\d+)/)
        if (!idMatch) continue
        const pId = parseInt(idMatch[1], 10)
        if (taskNos.has(pId)) criticalAsPred.add(pId)
      }
    }
    for (const item of flat) {
      const id = item.task.id
      if (!id || isVirtualTask(id)) continue
      if (!taskNos.has(id)) continue
      if (criticalAsPred.has(id)) continue
      if (!item.task.planStartDate) continue
      const x1 = item.barLeft + item.barWidth
      const x2 = endItem.barLeft
      const y1 = item.index * GANTT_ROW_H + GANTT_LINE_Y_OFFSET
      const y2 = endItem.index * GANTT_ROW_H + GANTT_VIRTUAL_LINE_Y
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
  if (!projectId.value) return
  ganttLoading.value = true
  try {
    if (tasks.value.length === 0) await loadTasks()
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
  if (isReadonly.value) {
    openViewTask(task)
    return
  }
  openEditTask(task)
}

const opLogTabRef = ref<InstanceType<typeof ProjectOperationLogTab> | null>(null)

/* Tab 切换时按需加载 */
async function onTabChange(tab: string) {
  if (!projectId.value) return
  if (tab === 'tasks' && tasks.value.length === 0) await loadTasks()
  if (tab === 'milestones' && tasks.value.length === 0) await loadTasks()
  if (tab === 'board' && tasks.value.length === 0) await loadTasks()
  if (tab === 'gantt') await loadGanttData()
  if (tab === 'changes') await changeTabRef.value?.loadChanges()
  if (tab === 'finance') await financeTabRef.value?.loadFinance()
  if (tab === 'oplog') await opLogTabRef.value?.loadOperationLogs()
  if (tab === 'files') filesTabMounted.value = true
  // files tab 首次挂载后不再销毁，filesTabMounted 标记保持 true
}

/* ───────── 生命周期 ───────── */
onMounted(async () => {
  // 基本信息优先加载，让用户尽快看到表单内容
  if (projectId.value) await loadDetail()

  // 以下辅助数据后台加载，不影响基本信息显示
  const [deptRes, userRes, dictRes, prodDictRes, taskCatRes] = await Promise.allSettled([
    getDepartments(), searchUsers(''),
    getDictByType('project_type'), getDictByType('product_type'), getDictByType('task_category')
  ])
  if (deptRes.status === 'fulfilled') {
    departments.value = deptRes.value.data
    resolveEngineeringCenterId()
  }
  if (userRes.status === 'fulfilled') users.value = userRes.value.data
  if (dictRes.status === 'fulfilled' && prodDictRes.status === 'fulfilled' && taskCatRes.status === 'fulfilled') {
    dictMap.value = { project_type: dictRes.value.data, product_type: prodDictRes.value.data, task_category: taskCatRes.value.data }
  }
  try {
    const ruleRes = await getSysParamByKey('plan_code_rule')
    taskNoRule.value = ruleRes.data.paramValue
  } catch { /* 默认使用 3,2,2 */ }
  try {
    const fnRes = await getFunctionList()
    functions.value = fnRes.data ?? []
    await loadStaffOptionsByFunction(functions.value)
    // 创建模式下，当前用户为项目经理→自动填充项目经理字段
    if (isPmCreator.value) {
      form.projectManagerId = authStore.userId
      form.projectManagerName = authStore.realName || ''
      syncSelectedStaffToMembers()
    }
  } catch { /* 职能加载失败不影响页面 */ }
})
</script>

<template>
  <div class="project-editor">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <el-button :icon="'ArrowLeft'" type="primary" @click="handleBack">返回列表</el-button>
        <span class="page-title">{{ pageTitle }}<template v-if="projectId && form.projectCode"> - {{ form.projectCode }} {{ form.projectName }}</template></span>
        <el-tag v-if="projectId && form.status !== undefined" :type="form.status === 1 ? 'primary' : form.status === 2 ? 'success' : form.status === 3 ? 'warning' : 'info'" size="small" style="margin-left: 12px;">
          {{ ['未激活','进行中','已完成','暂停'][form.status as number] ?? '' }}
        </el-tag>
      </div>
      <div v-if="projectId" class="header-actions">
        <el-button v-if="form.status === 0" type="danger" @click="handleActivate">激活</el-button>
        <el-button v-if="form.status === 1" type="success" :disabled="!form.canManageStatus" @click="handleComplete">确认完成</el-button>
        <el-button v-if="form.status === 1" type="warning" :disabled="!form.canManageStatus" @click="handleSuspend">暂停</el-button>
        <el-button v-if="form.status === 1" type="info" :disabled="!form.canDeactivate" @click="handleDeactivate">取消激活</el-button>
        <el-button v-if="form.status === 3" type="primary" :disabled="!form.canManageStatus" @click="handleResume">取消暂停</el-button>
      </div>
    </div>

    <!-- Tab 内容 -->
    <el-tabs v-model="activeTab" class="editor-tabs" @tab-change="onTabChange" v-loading="loading">
      <!-- ── Tab 1：基本信息 ── -->
      <el-tab-pane label="基本信息" name="basic" class="tab-pane-fill">
        <div class="basic-tab-wrapper">
        <el-form ref="formRef" :model="form" :rules="rules" label-width="143px" :disabled="isReadonly || form.status !== 0" style="flex:1;min-height:0;overflow-y:auto">
          <el-card shadow="never" class="form-card">
            <!-- 第1行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="项目编号" prop="projectCode">
                  <el-input v-model="form.projectCode" :placeholder="isReadonly ? '' : '请输入项目编号'" :readonly="mode !== 'create'" maxlength="64" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="项目名称" prop="projectName">
                  <el-input v-model="form.projectName" :placeholder="isReadonly ? '' : '请输入项目名称'" maxlength="128" />
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第2行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="客户合同编号" prop="contractCode">
                  <el-input v-if="!isReadonly" v-model="form.contractCode" placeholder="请输入客户合同编号" />
                  <span v-else-if="hasFieldPerm('contract-code')" class="field-value">{{ form.contractCode || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="项目地点(省、市)" prop="categoryCode">
                  <el-input v-if="!isReadonly" v-model="form.categoryCode" placeholder="请输入项目地点(省、市)" />
                  <span v-else-if="hasFieldPerm('category-code')" class="field-value">{{ form.categoryCode || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第3行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="客户名称" prop="customerName">
                  <el-input v-if="!isReadonly" v-model="form.customerName" placeholder="请输入客户名称" />
                  <span v-else-if="hasFieldPerm('customer-name')" class="field-value">{{ form.customerName || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="客户联系人" prop="regionalManagerName">
                  <el-input v-if="!isReadonly" v-model="form.regionalManagerName" placeholder="请输入客户联系人" />
                  <span v-else-if="hasFieldPerm('regional-manager')" class="field-value">{{ form.regionalManagerName || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 客户联系电话/邮箱 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="客户联系电话" prop="customerContactPhone">
                  <el-input v-if="!isReadonly" v-model="form.customerContactPhone" placeholder="请输入客户联系电话" />
                  <span v-else-if="hasFieldPerm('customer-contact-phone')" class="field-value">{{ form.customerContactPhone || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="客户联系邮箱" prop="customerContactEmail">
                  <el-input v-if="!isReadonly" v-model="form.customerContactEmail" placeholder="请输入客户联系邮箱" />
                  <span v-else-if="hasFieldPerm('customer-contact-email')" class="field-value">{{ form.customerContactEmail || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第4行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="最终业主" prop="finalCustomer">
                  <el-input v-if="!isReadonly" v-model="form.finalCustomer" placeholder="请输入最终业主" />
                  <span v-else-if="hasFieldPerm('final-customer')" class="field-value">{{ form.finalCustomer || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="业主联系人" prop="pmCenter">
                  <el-input v-if="!isReadonly" v-model="form.pmCenter" placeholder="请输入业主联系人" />
                  <span v-else-if="hasFieldPerm('pm-center')" class="field-value">{{ form.pmCenter || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 业主联系电话/业主联系邮箱 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="业主联系电话" prop="ownerContactPhone">
                  <el-input v-if="!isReadonly" v-model="form.ownerContactPhone" placeholder="请输入业主联系电话" />
                  <span v-else-if="hasFieldPerm('owner-contact-phone')" class="field-value">{{ form.ownerContactPhone || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="业主联系邮箱" prop="businessContactEmail">
                  <el-input v-if="!isReadonly" v-model="form.businessContactEmail" placeholder="请输入业主联系邮箱" />
                  <span v-else-if="hasFieldPerm('business-contact-email')" class="field-value">{{ form.businessContactEmail || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第5行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="交付详细地址" prop="deliveryLocation">
                  <el-input v-if="!isReadonly" v-model="form.deliveryLocation" placeholder="请输入交付详细地址" />
                  <span v-else-if="hasFieldPerm('delivery-location')" class="field-value">{{ form.deliveryLocation || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="项目类型" prop="projectType">
                  <el-select v-if="!isReadonly" v-model="form.projectType" placeholder="请选择项目类型" clearable style="width:100%">
                    <el-option v-for="item in (dictMap['project_type'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
                  </el-select>
                  <span v-else-if="hasFieldPerm('project-type')" class="field-value">{{ form.projectType || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第6行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="责任部门" prop="engineeringCenterId">
                  <el-tree-select v-if="!isReadonly"
                    v-model="form.engineeringCenterId"
                    :data="deptTreeData"
                    :props="{ label: 'name', children: 'children', value: 'id' }"
                    node-key="id"
                    placeholder="请选择责任部门"
                    check-strictly clearable filterable style="width:100%"
                    @change="(val: number | null) => { form.engineeringCenter = val ? departments.find(d => d.id === val)?.name ?? '' : '' }"
                  />
                  <span v-else-if="hasFieldPerm('engineering-center')" class="field-value">{{ form.engineeringCenter || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="售前联系人" prop="preSalesManagerId">
                  <el-select v-if="!isReadonly" v-model="form.preSalesManagerId" placeholder="请选择售前联系人" filterable clearable style="width:100%" @change="(val: number | undefined) => { form.preSalesManagerName = pickStaffName(val, preSalesOptions); syncSelectedStaffToMembers() }">
                    <el-option v-for="u in preSalesOptions" :key="u.id" :label="u.realName" :value="u.id" />
                  </el-select>
                  <span v-else-if="hasFieldPerm('pre-sales')" class="field-value">{{ form.preSalesManagerName || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第7行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="项目经理" prop="projectManagerId">
                  <el-select v-if="!isReadonly" v-model="form.projectManagerId" placeholder="请选择项目经理" filterable clearable style="width:100%" :disabled="isPmCreator || isCurrentUserPm || form.status !== 0" @change="(val: number | undefined) => { form.projectManagerName = pickStaffName(val, projectManagerOptions); syncSelectedStaffToMembers() }">
                    <el-option v-for="u in projectManagerOptions" :key="u.id" :label="u.realName" :value="u.id" />
                  </el-select>
                  <span v-else-if="hasFieldPerm('project-manager')" class="field-value">{{ form.projectManagerName || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="销售负责人" prop="salesManagerId">
                  <el-select v-if="!isReadonly" v-model="form.salesManagerId" placeholder="请选择销售负责人" filterable clearable style="width:100%" @change="(val: number | undefined) => { form.salesManagerName = pickStaffName(val, salesManagerOptions); syncSelectedStaffToMembers() }">
                    <el-option v-for="u in salesManagerOptions" :key="u.id" :label="u.realName" :value="u.id" />
                  </el-select>
                  <span v-else-if="hasFieldPerm('sales-manager')" class="field-value">{{ form.salesManagerName || '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第8行 -->
            <el-row :gutter="24">
              <el-col :span="12">
                <el-form-item label="合同要求交期" prop="requiredDelivery">
                  <el-date-picker v-if="!isReadonly" v-model="form.requiredDelivery" type="date" placeholder="选择日期" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  <span v-else-if="hasFieldPerm('required-delivery')" class="field-value">{{ form.requiredDelivery ? new Date(form.requiredDelivery).toLocaleDateString('zh-CN') : '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="承诺交期" prop="acceptedDelivery">
                  <el-date-picker v-if="!isReadonly" v-model="form.acceptedDelivery" type="date" placeholder="选择日期" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  <span v-else-if="hasFieldPerm('accepted-delivery')" class="field-value">{{ form.acceptedDelivery ? new Date(form.acceptedDelivery).toLocaleDateString('zh-CN') : '-' }}</span>
                  <span v-else class="field-masked">****</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第9行 -->
            <el-row v-if="!isReadonly || hasFieldPerm('special-terms')" :gutter="24">
              <el-col :span="24">
                <el-form-item label="特殊条款">
                  <el-input v-if="!isReadonly" v-model="form.specialTerms" type="textarea" :rows="3" placeholder="请输入特殊条款" />
                  <span v-else class="field-value">{{ form.specialTerms || '-' }}</span>
                </el-form-item>
              </el-col>
            </el-row>
            <!-- 第10行 -->
            <el-row v-if="!isReadonly || hasFieldPerm('remark')" :gutter="24">
              <el-col :span="24">
                <el-form-item label="备注">
                  <el-input v-if="!isReadonly" v-model="form.remark" type="textarea" :rows="3" placeholder="请输入备注" />
                  <span v-else class="field-value">{{ form.remark || '-' }}</span>
                </el-form-item>
              </el-col>
            </el-row>
          </el-card>

          <!-- 项目范围 -->
          <el-card shadow="never" class="form-card" style="margin-top:12px">
            <template #header>
              <div style="display:flex;justify-content:space-between;align-items:center">
                <span style="font-weight:600">项目范围</span>
                <el-button v-if="!isReadonly" type="danger" size="small" @click="addProjectScope">+ 添加范围</el-button>
              </div>
            </template>
            <el-table :data="projectScopes" border size="small" style="width:100%" empty-text="暂无项目范围">
              <el-table-column type="index" label="序号" width="55" />
              <el-table-column label="项目范围" width="324">
                <template #default="{ row, $index }">
                  <el-input v-if="!isReadonly" v-model="row.scopeName" size="small" placeholder="请输入项目范围" />
                  <span v-else>{{ row.scopeName }}</span>
                </template>
              </el-table-column>
              <el-table-column label="范围说明" min-width="240">
                <template #default="{ row, $index }">
                  <el-input v-if="!isReadonly" v-model="row.scopeDesc" size="small" placeholder="请输入范围说明" />
                  <span v-else>{{ row.scopeDesc }}</span>
                </template>
              </el-table-column>
              <el-table-column v-if="!isReadonly" label="操作" width="70">
                <template #default="{ $index }">
                  <el-button link style="color:#f56c6c" @click="removeProjectScope($index)">删除</el-button>
                </template>
              </el-table-column>
            </el-table>
          </el-card>

          <!-- 产品列表 -->
          <el-card shadow="never" class="form-card" style="margin-top:12px">
            <template #header>
              <div style="display:flex;justify-content:space-between;align-items:center">
                <span style="font-weight:600">产品列表</span>
                <el-button v-if="!isReadonly" type="danger" size="small" @click="addProduct">+ 添加产品</el-button>
              </div>
            </template>
            <el-table :data="products" border size="small" style="width:100%">
              <el-table-column type="index" label="序号" width="55" />
              <el-table-column label="产品类型" width="150">
                <template #default="{ row }">
                  <el-select v-if="!isReadonly" v-model="row.productType" placeholder="请选择" size="small" clearable style="width:100%">
                    <el-option v-for="item in (dictMap['product_type'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
                  </el-select>
                  <span v-else>{{ getDictLabel('product_type', row.productType) }}</span>
                </template>
              </el-table-column>
              <el-table-column label="数量" width="80">
                <template #default="{ row }">
                  <el-input-number v-if="!isReadonly" v-model="row.quantity" :min="1" size="small" style="width:100%" />
                  <span v-else>{{ row.quantity }}</span>
                </template>
              </el-table-column>
              <el-table-column label="计划交付日期" width="150">
                <template #default="{ row }">
                  <el-date-picker v-if="!isReadonly" v-model="row.plannedDelivery" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  <span v-else>{{ row.plannedDelivery ? row.plannedDelivery.slice(0,10) : '' }}</span>
                </template>
              </el-table-column>
              <el-table-column label="备注" min-width="120">
                <template #default="{ row }">
                  <el-input v-if="!isReadonly" v-model="row.remark" size="small" />
                  <span v-else>{{ row.remark }}</span>
                </template>
              </el-table-column>
              <el-table-column v-if="!isReadonly" label="操作" width="70">
                <template #default="{ $index }">
                  <el-button link style="color:#f56c6c" @click="removeProduct($index)">删除</el-button>
                </template>
              </el-table-column>
            </el-table>
          </el-card>

          <!-- 底部操作 -->
        </el-form>
        <div class="form-footer" v-if="!isReadonly">
          <el-button type="danger" :loading="saving" @click="handleSave">保存</el-button>
        </div>
        </div>
      </el-tab-pane>

      <!-- ── Tab 2：成员人员 ── -->
      <el-tab-pane label="成员列表" name="members" :disabled="!projectId" class="tab-pane-fill">
        <div class="member-tab-wrapper">
          <div class="member-toolbar">
            <span style="font-weight:600">成员列表</span>
            <div style="display:flex;gap:8px">
              <el-button v-if="!isReadonly" type="primary" plain size="small" @click="addMember">+ 添加成员</el-button>
              <el-button v-if="!isReadonly" type="primary" size="small" @click="openMemberTemplateDialog">从模板创建</el-button>
            </div>
          </div>
          <div class="member-table-wrap">
            <el-table
              :data="members"
              border
              size="small"
              style="width:100%"
              :row-class-name="memberRowClassName"
              @dragover.prevent="onMemberTableDragOver"
              @drop.prevent="onMemberTableDrop"
            >
            <!-- 拖拽手柄列 -->
            <el-table-column v-if="!isReadonly" label="" width="40" align="center">
              <template #default="{ $index }">
                <span
                  class="drag-handle"
                  draggable="true"
                  @dragstart="onMemberDragStart($index, $event)"
                  @dragend="onMemberDragEnd"
                >⋮⋮</span>
              </template>
            </el-table-column>

            <el-table-column type="index" label="序号" width="60" />
            <el-table-column label="职能" width="140">
              <template #default="{ row }">
                <el-select v-if="!isReadonly" v-model="row.roleId" size="small" filterable clearable style="width:100%" :disabled="isMemberLocked(row)" @change="(val: number) => { const fn = functions.find(f => f.id === val); row.roleName = fn?.name ?? ''; row.functionId = fn?.id; row.functionName = fn?.name ?? ''; row.memberName = ''; row.memberId = undefined }">
                  <el-option v-for="f in functions" :key="f.id" :label="f.name" :value="f.id" />
                </el-select>
                <span v-else>{{ row.roleName }}</span>
              </template>
            </el-table-column>
            <el-table-column label="部门" width="150">
              <template #default="{ row }">
                <el-tree-select
                  v-if="!isReadonly"
                  v-model="row.deptId"
                  :data="deptTreeData"
                  :props="{ label: 'name', children: 'children', value: 'id' }"
                  node-key="id"
                  check-strictly
                  placeholder="请选择部门"
                  size="small"
                  clearable
                  filterable
                  style="width:100%"
                  :disabled="isMemberLocked(row)"
                  @change="(val: number) => { row.deptName = departments.find(d => d.id === val)?.name ?? ''; row.memberName = ''; row.memberId = undefined }"
                />
                <span v-else>{{ row.deptName }}</span>
              </template>
            </el-table-column>
            <el-table-column label="人员" width="150">
              <template #default="{ row }">
                <el-select
                  v-if="!isReadonly"
                  v-model="row.memberName"
                  size="small"
                  filterable
                  clearable
                  style="width:100%"
                  :disabled="isMemberLocked(row)"
                  @change="(val: string) => { const u = filteredMemberUsers(row).find(u => u.realName === val); if(u) onMemberSelect(row, u) }"
                >
                  <el-option v-for="u in filteredMemberUsers(row)" :key="u.id" :label="u.realName" :value="u.realName" />
                </el-select>
                <span v-else>{{ row.memberName }}</span>
              </template>
            </el-table-column>
            <el-table-column label="备注" min-width="120">
              <template #default="{ row }">
                <el-input v-if="!isReadonly" v-model="row.remark" size="small" />
                <span v-else>{{ row.remark }}</span>
              </template>
            </el-table-column>
            <el-table-column v-if="!isReadonly" label="操作" width="70">
              <template #default="{ row, $index }">
                <el-button link :disabled="isMemberLocked(row)" :style="isMemberLocked(row) ? undefined : { color: '#f56c6c' }" @click="removeMember($index)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          </div>

        <div class="form-footer" v-if="!isReadonly">
          <el-button type="danger" :loading="saving" @click="handleSaveMembers">保存</el-button>
        </div>
        </div>

        <!-- 从模板导入成员对话框 -->
        <el-dialog v-model="memberTemplateDialogVisible" title="从项目成员模板导入" width="560px">
          <template v-if="memberTemplateList.length === 0 && !memberTemplateLoading">
            <el-empty description="暂无成员模板">
              <template #description>
                <p style="color:#909399;margin-bottom:12px">请先到<strong>模板配置 → 模板管理</strong>创建成员模板</p>
              </template>
              <el-button type="primary" size="small" @click="memberTemplateDialogVisible = false">知道了</el-button>
            </el-empty>
          </template>
          <el-table
            v-else
            :data="memberTemplateList"
            border
            size="small"
            style="width:100%"
            max-height="400"
            v-loading="memberTemplateLoading"
            highlight-current-row
            @current-change="(row: Template | null) => selectedMemberTemplateId = row?.id ?? null"
          >
            <el-table-column width="50" align="center">
              <template #default="{ row }">
                <el-radio :model-value="selectedMemberTemplateId" :value="row.id" @change="selectedMemberTemplateId = row.id">
                  <span></span>
                </el-radio>
              </template>
            </el-table-column>
            <el-table-column prop="templateCode" label="模板编号" width="140" />
            <el-table-column prop="templateName" label="模板名称" min-width="180" show-overflow-tooltip />
            <el-table-column prop="description" label="描述" min-width="120" show-overflow-tooltip />
          </el-table>
          <template #footer>
            <el-button @click="memberTemplateDialogVisible = false">取消</el-button>
            <el-button type="primary" @click="confirmImportMemberTemplate" :disabled="!selectedMemberTemplateId">导入</el-button>
          </template>
        </el-dialog>
      </el-tab-pane>

      <!-- ── Tab 3：文件资料 ── -->
      <el-tab-pane v-if="canViewFileTab" label="文件资料" name="files" :disabled="!projectId" class="tab-pane-fill">
        <ProjectFileTab
          v-if="projectId && filesTabMounted"
          :project-id="projectId"
          :project-manager-id="form.projectManagerId"
          :member-ids="members.map(m => m.memberId).filter(Boolean) as number[]"
          :readonly="isReadonly"
        />
      </el-tab-pane>

      <!-- ── Tab 4：项目任务计划 ── -->
      <el-tab-pane label="任务计划" name="tasks" :disabled="!projectId" class="tab-pane-fill">
        <div class="tasks-tab-wrapper">
          <div class="tasks-toolbar" :class="{ 'list-edit-active': listEditMode }">
            <div style="display:flex;justify-content:space-between;align-items:center">
              <span style="font-weight:600">任务计划</span>
              <div style="display:flex;gap:8px">
                <span v-if="!isReadonly" style="display:flex;align-items:center;gap:8px;font-size:13px;color:#606266">
                  <el-button size="small" type="primary" :disabled="!listEditMode" @click="saveListEdits()">保存</el-button>
                  列表编辑 <el-switch :model-value="listEditMode" size="small" @update:model-value="handleToggleEditMode" />
                </span>
                <el-button size="small" type="success" @click="handleExportTasks()">导出</el-button>
                <el-button v-if="!isReadonly" type="danger" size="small" :disabled="isTaskLocked" @click="openAddTask()">+ 手动新增</el-button>
                <el-button v-if="!isReadonly" type="primary" size="small" :disabled="isTaskLocked" @click="openTemplateDialog()">模板新增</el-button>
              </div>
            </div>
          </div>
          <div class="tasks-table-wrap">
            <el-table
              ref="taskTableRef"
              :data="taskTree"
              row-key="id"
              :style="{ '--index-col-width': taskNoColWidth + 'px' as any }"
              :tree-props="{ children: 'children' }"
              v-loading="cascadeLoading || ctxMenuLoading"
              element-loading-text="正在级联更新后续任务日期..."
              border
              size="small"
              style="width:100%"
              height="100%"
              default-expand-all
              :header-cell-style="{ textAlign: 'center' }"
              :header-cell-class-name="taskHeaderCellClassName"
              v-column-drag
              @row-click="handleTaskRowClick"
              @row-contextmenu="handleRowContextMenu"
              @cell-mouse-enter="onCellEnter"
              @cell-mouse-leave="onCellLeave"
              :row-class-name="({ row }: { row: ProjectTaskItem }) => {
                if (ctxMenuVisible && ctxMenuTask?.id === row.id) return 'ctx-row-active'
                if (listEditMode && editingRowId === row.id) return 'editing-row'
                if (flashId === row.id) return 'pf'
                return ''
              }"
            >
            <el-table-column
              v-for="col in taskColumnDefs"
              :key="col.key"
              :label="col.label"
              :width="col.key === 'index' ? taskNoColWidth : col.width"
              :min-width="col.minWidth"
              :align="col.align"
              :fixed="col.fixed"
              :type="col.type"
              :show-overflow-tooltip="col.showOverflowTooltip"
              :class-name="col.className"
            >
              <template #default="{ row }">
                <!-- 序号 -->
                <template v-if="col.key === 'index'">
                  <span style="white-space:nowrap">{{ row.taskNo }}</span>
                </template>
                <!-- 任务名称 -->
                <template v-else-if="col.key === 'taskName'">
                  <span v-if="listEditMode && editingRowId === row.id" class="node-name-cell">
                    <span class="node-name-icon" v-if="row.nodeType === 2" style="color:#e74c3c">◆</span>
                    <span class="node-name-icon" v-else-if="!row.children || !row.children.length" style="color:#909399">·</span>
                    <el-input v-model="row.taskName" size="small" @input="markDirty(row)" style="flex:1" />
                  </span>
                  <span v-else class="node-name-cell node-name-wrap" :title="row.taskName">
                    <span class="node-name-icon" v-if="row.nodeType === 2" style="color:#e74c3c">◆</span>
                    <span class="node-name-icon" v-else-if="!row.children || !row.children.length" style="color:#909399">·</span>
                    {{ row.taskName }}
                  </span>
                </template>
                <!-- 进度 -->
                <template v-else-if="col.key === 'progress'">
                  <el-input-number v-if="listEditMode && editingRowId === row.id" v-model="row.progressPct" :min="0" :max="100" size="small" style="width:80px" :disabled="row.nodeType === 2 || taskHasChildren(row.id)" @change="markDirty(row)" />
                  <el-progress v-else :percentage="Number(row.progressPct) || 0" :stroke-width="12" class="task-progress-bar" />
                </template>
                <!-- 计划开始 -->
                <template v-else-if="col.key === 'planStartDate'">
                  <el-date-picker v-if="listEditMode && editingRowId === row.id" v-model="row.planStartDate" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="row.nodeType === 2 || taskHasChildren(row.id) || form.status !== 0" @change="handleDateChange(row, 'planStartDate', $event)" />
                  <span v-else>{{ row.planStartDate?.slice(0,10) }}</span>
                </template>
                <!-- 计划完成 -->
                <template v-else-if="col.key === 'planFinishDate'">
                  <el-date-picker v-if="listEditMode && editingRowId === row.id" v-model="row.planFinishDate" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="taskHasChildren(row.id) || form.status !== 0" @change="handleDateChange(row, 'planFinishDate', $event)" />
                  <span v-else>{{ row.planFinishDate?.slice(0,10) }}</span>
                </template>
                <!-- 实际开始 -->
                <template v-else-if="col.key === 'actualStartDate'">
                  <el-date-picker v-if="listEditMode && editingRowId === row.id" v-model="row.actualStartDate" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="row.nodeType === 2 || taskHasChildren(row.id)" @change="handleDateChange(row, 'actualStartDate', $event)" />
                  <span v-else>{{ row.actualStartDate?.slice(0,10) }}</span>
                </template>
                <!-- 实际完成 -->
                <template v-else-if="col.key === 'actualFinishDate'">
                  <el-date-picker v-if="listEditMode && editingRowId === row.id" v-model="row.actualFinishDate" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="taskHasChildren(row.id)" @change="handleDateChange(row, 'actualFinishDate', $event)" />
                  <span v-else>{{ row.actualFinishDate?.slice(0,10) }}</span>
                </template>
                <!-- 计划工期 -->
                <template v-else-if="col.key === 'planDuration'">{{ row.planDuration }}</template>
                <!-- 前置任务 -->
                <template v-else-if="col.key === 'preTaskCodes'">
                  <span v-if="row.preTaskCodes" class="pcell">{{ formatPreTaskCodes(row.preTaskCodes, taskIdMap) }}</span>
                  <span v-else class="pcell-off">-</span>
                </template>
                <!-- 责任部门 -->
                <template v-else-if="col.key === 'dept'">
                  <el-tree-select
                    v-if="listEditMode && editingRowId === row.id"
                    v-model="row.deptId"
                    :data="deptTreeData"
                    :props="{ label: 'name', children: 'children', value: 'id' }"
                    node-key="id"
                    check-strictly
                    size="small"
                    clearable
                    filterable
                    style="width:100%"
                    @change="(val: number) => { row.deptName = departments.find(d => d.id === val)?.name ?? ''; row.assigneeName = ''; row.assigneeId = undefined; markDirty(row) }"
                  />
                  <span v-else>{{ row.deptName }}</span>
                </template>
                <!-- 责任人 -->
                <template v-else-if="col.key === 'assignee'">
                  <el-select
                    v-if="listEditMode && editingRowId === row.id"
                    v-model="row.assigneeName"
                    size="small"
                    filterable
                    clearable
                    style="width:100%"
                    @change="(val: string) => { if (!val) { row.assigneeId = undefined; markDirty(row); return }; const u = users.find(u => u.realName === val); if(u) { row.assigneeId = u.id; row.deptId = u.departmentId; row.deptName = departments.find(d => d.id === u.departmentId)?.name ?? ''; markDirty(row) } }"
                  >
                    <el-option v-for="u in users.filter(u => !row.deptId || u.departmentId === row.deptId)" :key="u.id" :label="u.realName" :value="u.realName" />
                  </el-select>
                  <span v-else>{{ row.assigneeName }}</span>
                </template>
                <!-- 优先级 -->
                <template v-else-if="col.key === 'priority'">
                  <el-select v-if="listEditMode && editingRowId === row.id" v-model="row.priority" size="small" style="width:100%" @change="markDirty(row)">
                    <el-option v-for="o in taskPriorityOptions" :key="o.value" :label="o.label" :value="o.value" />
                  </el-select>
                  <span v-else>{{ priorityLabel(row.priority) }}</span>
                </template>
                <!-- 状态 -->
                <template v-else-if="col.key === 'status'">
                  <el-select v-if="listEditMode && editingRowId === row.id" v-model="row.status" size="small" style="width:100%" @change="markDirty(row)">
                    <el-option v-for="o in taskStatusOptions" :key="o.value" :label="o.label" :value="o.value" />
                  </el-select>
                  <el-tag v-else :type="taskStatusTag(row.status)" size="small">{{ taskStatusLabel(row.status) }}</el-tag>
                </template>
                <!-- 逾期状态 -->
                <template v-else-if="col.key === 'overdueStatus'">
                  <el-tag :type="overdueStatus(row) === '已逾期' ? 'danger' : 'info'" size="small">{{ overdueStatus(row) }}</el-tag>
                </template>
                <!-- 任务类别 -->
                <template v-else-if="col.key === 'taskCategory'">
                  <el-select v-if="listEditMode && editingRowId === row.id" v-model="row.taskCategory" size="small" style="width:100%" clearable @change="markDirty(row)">
                    <el-option v-for="item in (dictMap['task_category'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
                  </el-select>
                  <span v-else>{{ dictMap['task_category']?.find(d => d.dictCode === row.taskCategory)?.dictLabel ?? row.taskCategory }}</span>
                </template>
                <!-- 实际工期 -->
                <template v-else-if="col.key === 'actualDuration'">{{ row.actualDuration }}</template>
                <!-- 参考工期 -->
                <template v-else-if="col.key === 'referenceDuration'">
                  <el-input-number v-if="listEditMode && editingRowId === row.id" v-model="row.referenceDuration" :min="0" size="small" style="width:100%" :disabled="row.nodeType === 2" @change="markDirty(row)" />
                  <span v-else>{{ row.referenceDuration }}</span>
                </template>
                <!-- 工序号 -->
                <template v-else-if="col.key === 'wbsCode'">
                  <el-input v-if="listEditMode && editingRowId === row.id" v-model="row.wbsCode" size="small" @input="markDirty(row)" />
                  <span v-else>{{ row.wbsCode }}</span>
                </template>
                <!-- 操作 -->
                <template v-else-if="col.key === 'actions'">
                  <div style="display:flex;gap:6px;justify-content:center">
                    <template v-if="isReadonly">
                      <el-button link style="color:#409eff;padding:0 4px" @click="openViewTask(row)">查看</el-button>
                      <el-button link style="color:#67c23a;padding:0 4px" :style="{ visibility: row.assigneeId === authStore.userId ? 'visible' : 'hidden', pointerEvents: row.assigneeId === authStore.userId ? 'auto' : 'none' }" @click="openEditTask(row)">编辑</el-button>
                    </template>
                    <template v-else>
                      <el-button link style="color:#409eff;padding:0 4px" :disabled="isTaskLocked" @click="openAddTask(row)">子节点</el-button>
                      <el-button v-if="!listEditMode" link style="color:#67c23a;padding:0 4px" @click="openEditTask(row)">编辑</el-button>
                      <el-button link :disabled="isTaskLocked" :style="isTaskLocked ? { color: '#c0c4cc' } : { color: '#f56c6c' }" @click="handleDeleteTask(row)">删除</el-button>
                    </template>
                  </div>
                </template>
              </template>
            </el-table-column>
          </el-table>
          </div>
        </div>

        <!-- 右键上下文菜单 -->
        <Teleport to="body">
          <div
            v-if="ctxMenuVisible"
            class="task-ctx-menu"
            :style="{ left: ctxMenuX + 'px', top: ctxMenuY + 'px' }"
            @click.stop
          >
            <div class="task-ctx-item" @click="handleCtxInsert(true)">
              <span class="ctx-icon">⬆</span> 插入节点
            </div>
            <div class="task-ctx-item" @click="handleCtxInsert(false)">
              <span class="ctx-icon">⬇</span> 新增节点
            </div>
            <div class="task-ctx-item" @click="handleCtxAddChild">
              <span class="ctx-icon">↳</span> 新增子节点
            </div>
            <div class="task-ctx-divider" />
            <div class="task-ctx-item" @click="handleCtxRenumber">
              <span class="ctx-icon">↻</span> 重新编号
            </div>
            <div class="task-ctx-item" @click="handleCtxEdit">
              <span class="ctx-icon">✎</span> 编辑节点
            </div>
            <div class="task-ctx-item task-ctx-item--danger" @click="handleCtxDelete">
              <span class="ctx-icon">✕</span> 删除节点
            </div>
          </div>
          <div v-if="ctxMenuVisible" class="task-ctx-mask" @click="closeCtxMenu" @contextmenu.prevent="closeCtxMenu" />
        </Teleport>

        <!-- 前置任务悬浮提示 -->
        <Teleport to="body">
          <div
            v-if="pv.show && pv.row"
            class="pover"
            :style="{ left: pv.x + 'px', top: pv.y + 'px' }"
            @mouseenter="pvCancel()"
            @mouseleave="pvHide()"
          >
            <div
              v-for="seg in parsePreTaskCodes(pv.row.preTaskCodes)"
              :key="seg.taskId"
              class="pitem"
              @click="flashRow(seg.taskId)"
            >
              <span class="pno">{{ taskIdMap.get(seg.taskId)?.taskNo ?? seg.taskId }}</span>
              <span class="pnm">
                <span>{{ taskIdMap.get(seg.taskId)?.taskName ?? '（已删除）' }}</span>
                <span class="pprogress">{{ (taskIdMap.get(seg.taskId)?.progressPct ?? 0) + '%' }}</span>
              </span>
            </div>
          </div>
        </Teleport>
      </el-tab-pane>

      <!-- ── Tab 4：里程碑 ── -->
      <el-tab-pane label="里程碑" name="milestones" :disabled="!projectId">
        <el-card shadow="never" class="form-card">
          <template #header><span style="font-weight:600">里程碑列表</span></template>
          <el-table :data="milestoneTasks" border size="small" style="width:100%" max-height="calc(100vh - 350px)" empty-text="暂无里程碑数据">
            <el-table-column type="index" label="序号" width="60" fixed="left" />
            <el-table-column label="任务编号" width="180" prop="taskNo" />
            <el-table-column label="里程碑名称" min-width="200" prop="taskName" show-overflow-tooltip />
            <el-table-column label="状态" width="100" align="center">
              <template #default="{ row }">
                <el-tag :type="row.status === 2 ? 'success' : row.status === 1 ? 'primary' : 'info'" size="small">
                  {{ row.status === 2 ? '已完成' : row.status === 1 ? '进行中' : '未开始' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="计划完成" width="110" align="center">
              <template #default="{ row }">{{ row.planFinishDate?.slice(0, 10) ?? '-' }}</template>
            </el-table-column>
            <el-table-column label="实际完成" width="110" align="center">
              <template #default="{ row }">{{ row.actualFinishDate?.slice(0, 10) ?? '-' }}</template>
            </el-table-column>
            <el-table-column label="进度" width="100" align="center">
              <template #default="{ row }">
                <el-progress :percentage="row.progressPct ?? 0" :status="row.progressPct >= 100 ? 'success' : ''" />
              </template>
            </el-table-column>
            <el-table-column label="责任人" width="120" prop="assigneeName" show-overflow-tooltip />
            <el-table-column label="操作" width="120" fixed="right">
              <template #default="{ row }">
                <el-button link type="primary" size="small" @click="openViewTask(row)">查看</el-button>
                <el-button v-if="!isReadonly" link type="primary" size="small" @click="openEditTask(row)">编辑</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-tab-pane>

      <!-- ── Tab 5：考核任务 ── -->
      <el-tab-pane label="考核任务" name="assessment" :disabled="!projectId">
        <el-card shadow="never" class="form-card">
          <template #header><span style="font-weight:600">考核任务列表</span></template>
          <el-table :data="assessmentTasks" border size="small" style="width:100%" max-height="calc(100vh - 350px)" empty-text="暂无考核任务数据">
            <el-table-column type="index" label="序号" width="55" fixed="left" />
            <el-table-column label="任务名称" min-width="316" prop="taskName" show-overflow-tooltip />
            <el-table-column label="进度" width="106" align="center">
              <template #default="{ row }">
                <el-progress :percentage="row.progressPct ?? 0" :status="row.progressPct >= 100 ? 'success' : ''" />
              </template>
            </el-table-column>
            <el-table-column label="计划开始" width="110" align="center">
              <template #default="{ row }">{{ row.planStartDate?.slice(0, 10) ?? '-' }}</template>
            </el-table-column>
            <el-table-column label="计划完成" width="110" align="center">
              <template #default="{ row }">{{ row.planFinishDate?.slice(0, 10) ?? '-' }}</template>
            </el-table-column>
            <el-table-column label="实际开始" width="110" align="center">
              <template #default="{ row }">{{ row.actualStartDate?.slice(0, 10) ?? '-' }}</template>
            </el-table-column>
            <el-table-column label="实际完成" width="110" align="center">
              <template #default="{ row }">{{ row.actualFinishDate?.slice(0, 10) ?? '-' }}</template>
            </el-table-column>
            <el-table-column label="计划工期" width="69" align="center">
              <template #default="{ row }">{{ row.planDuration }}</template>
            </el-table-column>
            <el-table-column label="前置任务" width="165">
              <template #default="{ row }">
                <span v-if="row.preTaskCodes" class="pcell">{{ formatPreTaskCodes(row.preTaskCodes, taskIdMap) }}</span>
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column label="责任部门" width="130" prop="deptName" show-overflow-tooltip>
              <template #default="{ row }">{{ row.deptName || '-' }}</template>
            </el-table-column>
            <el-table-column label="责任人" width="130" prop="assigneeName" show-overflow-tooltip>
              <template #default="{ row }">{{ row.assigneeName || '-' }}</template>
            </el-table-column>
            <el-table-column label="优先级" width="80" align="center">
              <template #default="{ row }">{{ priorityLabel(row.priority) }}</template>
            </el-table-column>
            <el-table-column label="状态" width="90" align="center">
              <template #default="{ row }">
                <el-tag :type="taskStatusTag(row.status)" size="small">{{ taskStatusLabel(row.status) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="逾期状态" width="88" align="center">
              <template #default="{ row }">
                <el-tag :type="overdueStatus(row) === '已逾期' ? 'danger' : 'info'" size="small">{{ overdueStatus(row) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="任务类别" width="130" align="center">
              <template #default="{ row }">{{ dictMap['task_category']?.find(d => d.dictCode === row.taskCategory)?.dictLabel ?? row.taskCategory }}</template>
            </el-table-column>
            <el-table-column label="实际工期" width="71" align="center">
              <template #default="{ row }">{{ row.actualDuration }}</template>
            </el-table-column>
            <el-table-column label="参考工期" width="80" align="center">
              <template #default="{ row }">{{ row.referenceDuration }}</template>
            </el-table-column>
            <el-table-column label="工序号" width="120" align="center" prop="wbsCode" show-overflow-tooltip>
              <template #default="{ row }">{{ row.wbsCode || '-' }}</template>
            </el-table-column>
            <el-table-column label="操作" width="120" fixed="right">
              <template #default="{ row }">
                <el-button link type="primary" size="small" @click="openViewTask(row)">查看</el-button>
                <el-button v-if="!isReadonly" link type="primary" size="small" @click="openEditTask(row)">编辑</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-tab-pane>

      <!-- ── Tab 6：计划甘特图 ── -->
      <el-tab-pane label="甘特图" name="gantt" :disabled="!projectId" class="tab-pane-fill">
        <div class="gantt-wrapper" v-loading="ganttLoading || cascadeLoading" :element-loading-text="cascadeLoading ? '正在级联更新后续任务日期...' : '加载中...'">
          <template v-if="tasks.length === 0 && !ganttLoading">
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
                  <div v-for="item in ganttFlattenedTasks" :key="item.task.id ?? item.task.taskNo" class="gantt-row" :class="{ 'gantt-row-alt': item.index % 2 === 1, 'gantt-virtual-row': isVirtualTask(item.task.id) }">
                    <span class="gantt-task-no" :class="{ 'gantt-virtual-no': isVirtualTask(item.task.id) }" :style="{ width: ganttSeqWidth + 'px' }" :title="item.task.taskNo">{{ item.task.taskNo }}</span>
                    <span class="gantt-task-name" :class="{ 'gantt-virtual-name': isVirtualTask(item.task.id) }" :style="{ paddingLeft: (item.level * 20 + 8) + 'px' }" :title="item.task.taskName">
                      <template v-if="item.task.id === VIRTUAL_START_ID || item.task.id === VIRTUAL_END_ID">
                        <span class="gantt-task-icon">◆</span>
                        {{ item.task.taskName }}
                      </template>
                      <template v-else>
                        <span class="gantt-task-icon" v-if="item.task.nodeType === 2" style="color:#e74c3c">◆</span>
                        <span class="gantt-task-icon" v-else-if="item.task.children && item.task.children.length" style="color:#409eff">▼</span>
                        <span class="gantt-task-icon" v-else style="color:#909399">·</span>
                        {{ item.task.taskName }}
                      </template>
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
                    <div v-for="item in ganttFlattenedTasks" :key="item.task.id ?? item.task.taskNo" class="gantt-row" :class="{ 'gantt-row-alt': item.index % 2 === 1, 'gantt-virtual-row': isVirtualTask(item.task.id) }">
                      <!-- 虚拟节点虚线 + 圆点 -->
                      <template v-if="item.task.id === VIRTUAL_START_ID">
                        <div class="gantt-virtual-line" :style="{ left: item.barLeft + 'px' }"></div>
                        <div class="gantt-dot gantt-dot-start" :style="{ left: (item.barLeft - 4) + 'px' }" :title="'项目开始: ' + (item.task.planStartDate?.slice(0,10) ?? '')"></div>
                      </template>
                      <template v-else-if="item.task.id === VIRTUAL_END_ID">
                        <div class="gantt-virtual-line" :style="{ left: item.barLeft + 'px' }"></div>
                        <div class="gantt-dot gantt-dot-end" :style="{ left: (item.barLeft - 4) + 'px' }" :title="'项目结束: ' + (item.task.planStartDate?.slice(0,10) ?? '')"></div>
                      </template>
                      <!-- 普通任务 -->
                      <template v-else>
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
                        <!-- 文字层 -->
                        <span v-if="item.task.nodeType === 1 && item.task.planStartDate && item.barWidth > 60" class="gantt-bar-label" :style="{ left: item.barLeft + 'px', width: item.barWidth + 'px' }">{{ item.task.taskName }}</span>
                        <span v-if="item.task.nodeType === 1 && item.task.planStartDate && item.actualBarWidth > 0 && item.task.progressPct > 0" class="gantt-bar-pct" :class="'gantt-pct-status-' + item.actualDisplayStatus" :style="{ left: (item.actualBarLeft - 8) + 'px', top: '30px', transform: 'translateX(-100%)' }">{{ item.task.progressPct }}%</span>
                        <span v-if="item.task.nodeType === 1 && item.task.planStartDate && item.barWidth > 0 && (item.task.deptName || item.task.assigneeName)" class="gantt-bar-info" :style="{ left: (item.barLeft + item.barWidth + 18) + 'px' }"><span v-if="item.task.deptName" class="gantt-info-dept">{{ item.task.deptName }}</span><span v-if="item.task.assigneeName" class="gantt-info-person" style="margin-left:10px">{{ item.task.assigneeName }}</span></span>
                      </template>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </template>
        </div>
      </el-tab-pane>

      <!-- ── Tab 5：任务列表（看板）── -->
      <el-tab-pane label="任务列表" name="board" :disabled="!projectId" class="tab-pane-fill">
        <div class="board-tab-wrapper">
          <div style="display:flex;gap:8px;margin-bottom:12px">
            <span style="line-height:32px;font-size:14px;font-weight:600">分组方式：</span>
            <el-radio-group v-model="boardGroupMode" size="small">
              <el-radio-button value="assignee">负责人</el-radio-button>
              <el-radio-button value="dept">责任部门</el-radio-button>
              <el-radio-button value="category">任务类别</el-radio-button>
            </el-radio-group>
          </div>
          <div class="board-container">
          <div v-for="col in boardData" :key="col.category" class="board-column">
            <div class="board-column-header">
              <span class="board-column-title">{{ getGroupLabel(col.category) }}</span>
              <el-tag size="small" type="info">{{ col.tasks.length }} 条</el-tag>
            </div>
            <div class="board-column-body">
              <div v-for="task in col.tasks" :key="task.id" class="task-card" style="cursor:pointer" @click="openViewTask(task)">
                <div class="task-card-header">
                  <span class="task-card-title">{{ task.taskNo }}<br/>{{ task.taskName }}</span>
                  <el-tag :type="taskStatusTag(task.status)" size="small">{{ taskStatusLabel(task.status) || '未开始' }}</el-tag>
                </div>
                <div class="task-card-meta">
                  <span v-if="task.wbsCode">工序号：{{ task.wbsCode }}</span>
                  <span v-if="task.assigneeName">负责人：{{ task.assigneeName }}</span>
                </div>
                <div class="task-card-footer">
                  <span class="task-card-date" v-if="task.planFinishDate">截止：{{ task.planFinishDate.slice(0,10) }}</span>
                  <span class="task-card-progress">完成进度：{{ task.progressPct }}%</span>
                </div>
                <el-progress :percentage="Number(task.progressPct)" :show-text="false" :stroke-width="4" style="margin-top:6px" />
              </div>
              <div v-if="col.tasks.length === 0" class="board-empty">暂无数据</div>
            </div>
          </div>
        </div>
        </div>
      </el-tab-pane>

      <!-- ── Tab 6：变更记录 ── -->
      <ProjectChangeTab ref="changeTabRef" :project-id="projectId" :is-readonly="isReadonly" />

      <!-- ── Tab 7：财务信息 ── -->
      <ProjectFinanceTab ref="financeTabRef" :project-id="projectId" :is-readonly="isReadonly" />

      <!-- ── Tab 8：操作日志 ── -->
      <ProjectOperationLogTab ref="opLogTabRef" :project-id="projectId" :users="users" :dept-tree-data="deptTreeData" />
    </el-tabs>

    <!-- ── 从模板新增任务对话框 ── -->
    <el-dialog v-model="showTemplateDialog" title="从模板新增任务" width="832px" :close-on-click-modal="false">
      <el-input v-model="templateSearch" placeholder="搜索模板编号或名称..." clearable style="margin-bottom:12px" />
      <el-table
        :data="filteredTemplates"
        border
        size="small"
        style="width:100%"
        max-height="480"
        highlight-current-row
        @current-change="(row: Template | null) => selectedTemplateId = row?.id ?? null"
      >
        <el-table-column width="50" align="center">
          <template #default="{ row }">
            <el-radio :model-value="selectedTemplateId" :value="row.id" @change="selectedTemplateId = row.id">
              <span></span>
            </el-radio>
          </template>
        </el-table-column>
        <el-table-column prop="templateCode" label="模板编号" width="185" />
        <el-table-column prop="templateName" label="模板名称" min-width="180" show-overflow-tooltip />
        <el-table-column prop="description" label="描述" min-width="160" show-overflow-tooltip />
      </el-table>
      <template #footer>
        <el-button @click="showTemplateDialog = false">取消</el-button>
        <el-button type="danger" :loading="templateLoading" @click="handleCreateFromTemplate">确定</el-button>
      </template>
    </el-dialog>

    <!-- ── 新增/编辑任务对话框 ── -->
    <el-dialog v-model="showAddTaskDialog" :title="editingTask?.id ? '编辑任务' : '新增任务'" width="840px" :close-on-click-modal="false" top="5vh">
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
                    <el-date-picker v-model="editingTask.planStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="editingTask?.nodeType === 2 || taskHasChildren(editingTask.id) || form.status !== 0" :placeholder="editingTask?.nodeType === 2 ? '里程碑与计划完成同步' : '有子节点时由子节点自动计算'" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="计划完成">
                    <el-date-picker v-model="editingTask.planFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="taskHasChildren(editingTask.id) || form.status !== 0" placeholder="有子节点时由子节点自动计算" />
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
        <el-button @click="showAddTaskDialog = false">取消</el-button>
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
              <el-input :model-value="viewingTask.wbsCode || ''" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="任务类别">
              <el-select :model-value="viewingTask.taskCategory" placeholder="请选择" style="width:100%">
                <el-option v-for="item in (dictMap['task_category'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="节点类型">
              <el-radio-group :model-value="viewingTask.nodeType">
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
              <el-tree-select
                :model-value="viewingTask.deptId"
                :data="deptTreeData"
                :props="{ label: 'name', children: 'children', value: 'id' }"
                node-key="id"
                check-strictly
                placeholder="请选择部门"
                clearable
                filterable
                style="width:100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="责任人">
              <el-select :model-value="viewingTask.assigneeName" filterable clearable placeholder="请选择人员" style="width:100%">
                <el-option v-for="u in users.filter(u => !viewingTask.deptId || u.departmentId === viewingTask.deptId)" :key="u.id" :label="u.realName" :value="u.realName" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="参考工期">
              <el-input-number :model-value="viewingTask.referenceDuration" :min="0" style="width:100%" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="前置任务">
              <el-input :model-value="formatPreTaskCodes(viewingTask.preTaskCodes, taskIdMap) || '尚未设置前置任务'" disabled />
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
                    <el-date-picker :model-value="viewingTask.planStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="计划完成">
                    <el-date-picker :model-value="viewingTask.planFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="计划工期">
                    <el-input-number :model-value="viewingTask.planDuration" :min="0" style="width:100%" disabled />
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
                    <el-date-picker :model-value="viewingTask.actualStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="实际完成">
                    <el-date-picker :model-value="viewingTask.actualFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item label="实际工期">
                    <el-input-number :model-value="viewingTask.actualDuration" :min="0" style="width:100%" disabled />
                  </el-form-item>
                </el-col>
              </el-row>
            </el-card>
          </el-col>
          <el-col :span="24">
            <el-form-item label="备注" class="remark-item">
              <el-input :model-value="viewingTask.remark || ''" type="textarea" :rows="2" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button type="primary" @click="showViewTaskDialog = false">关闭</el-button>
      </template>
    </el-dialog>

  </div>
</template>

<style scoped>
.project-editor {
  padding: 20px;
  height: calc(100vh - 56px);
  box-sizing: border-box;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid #e4e7ed;
  flex-shrink: 0;
}

/* 任务进度条百分比字体调小 20% */
.task-progress-bar :deep(.el-progress__text) { font-size: 11px !important; }

/* 列表编辑模式：编辑中的行高亮 + 可点击游标 */
.editing-row { background-color: #f0f9ff !important; }
.el-table .el-table__body tr.editing-row:hover > td { background-color: #e6f4ff !important; }

/* 查看模式下字段值显示 */
.field-value { color: #303133; font-size: 14px; line-height: 32px; padding: 0 4px; display: inline-block; min-height: 32px; }
.field-masked { color: #c0c4cc; font-size: 14px; line-height: 32px; letter-spacing: 2px; }
.list-edit-active .el-table__body tr.el-table__row { cursor: pointer; }

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-title {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.header-actions { display: flex; gap: 8px; }

.editor-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.editor-tabs :deep(.el-tabs__header) { flex-shrink: 0; }
.editor-tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.editor-tabs :deep(.el-tab-pane) {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}
.editor-tabs :deep(.tab-pane-fill) {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.form-card { margin-bottom: 0; }

/* ─── 任务计划 fill 模式 ─── */
.tasks-tab-wrapper {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.tasks-toolbar {
  margin-bottom: 12px;
  flex-shrink: 0;
}
.tasks-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

/* ─── 成员列表 fill 模式 ─── */
.member-tab-wrapper {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.member-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  flex-shrink: 0;
}
.member-table-wrap {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

/* ─── 成员拖拽手柄 ─── */
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
.drag-handle:hover { color: #909399; }
.drag-handle:active { cursor: grabbing; }

/* ─── 拖拽行样式 ─── */
:deep(.row-dragging) { opacity: 0.45; }
:deep(.row-drag-over) > td { border-top: 2px solid #409eff !important; }


.node-name-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.node-name-wrap {
  white-space: normal;
  word-break: break-word;
  line-height: 1.4;
  padding: 4px 0;
}

/* ─── 列拖拽样式 ─── */
.task-col-draggable .cell { cursor: grab; user-select: none; }
.task-col-draggable .cell::before {
  content: '⋮⋮'; font-size: 11px; color: #c0c4cc;
  margin-right: 4px; opacity: 0.4; letter-spacing: -2px;
}
.task-col-draggable:hover .cell::before { opacity: 1; color: #909399; }
.task-col-draggable.task-col-dragging { opacity: 0.5; }
.task-col-dragging .cell { cursor: grabbing; }
.task-col-drag-over .cell {
  border-left: 3px solid #409eff;
  padding-left: calc(var(--el-table-cell-padding-left, 12px) - 3px);
}

/* 树表格展开图标替换 */
:deep(.el-table__expand-icon) {
  font-size: 12px !important;
  color: #409eff !important;
}
:deep(.el-table__expand-icon .el-icon) {
  display: none;
}
:deep(.el-table__expand-icon::after) {
  content: none !important;
}
:deep(.el-table__expand-icon::before) {
  content: '\25B6';
  display: inline-block;
  font-size: 10px;
  color: #409eff;
  transition: transform 0.2s;
}
:deep(.el-table__expand-icon--expanded) {
  transform: none !important;
}
:deep(.el-table__expand-icon--expanded::before) {
  content: '\25BC';
  font-size: 12px;
}

.seq-cell {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.seq-icon {
  flex-shrink: 0;
  font-size: 14px;
  line-height: 1;
}

.node-name-icon {
  flex-shrink: 0;
  font-size: 14px;
  line-height: 1;
}
.node-icon {
  flex-shrink: 0;
  color: #409eff;
}

/* ─── 基本信息 fill 模式 ─── */
.basic-tab-wrapper {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.form-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #e4e7ed;
  flex-shrink: 0;
}

.board-tab-wrapper {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.board-container {
  flex: 1;
  display: flex;
  gap: 12px;
  overflow-x: auto;
  overflow-y: auto;
  padding: 4px 0 12px;
  min-height: 0;
}

.board-column {
  flex: 0 0 210px;
  min-width: 180px;
  background: #f5f7fa;
  border-radius: 8px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.board-column-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: #e6ebf5;
  flex-shrink: 0;
}

.board-column-title {
  font-weight: 600;
  font-size: 13px;
  color: #303133;
}

.board-column-body {
  flex: 1;
  padding: 8px;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
  overflow-y: auto;
}

.task-card {
  background: #fff;
  border-radius: 6px;
  padding: 10px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}

.task-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 6px;
  margin-bottom: 6px;
}

.task-card-title {
  font-size: 13px;
  font-weight: 500;
  color: #303133;
  line-height: 1.4;
  flex: 1;
  word-break: break-all;
}

.task-card-meta {
  font-size: 12px;
  color: #909399;
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin-bottom: 6px;
}

.task-card-footer {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: #c0c4cc;
}

.board-empty {
  text-align: center;
  padding: 20px 0;
  color: #c0c4cc;
  font-size: 13px;
}
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

/* 虚拟起止节点圆点 */
.gantt-dot {
  position: absolute;
  top: 23px;
  width: 8px; height: 8px;
  border-radius: 50%;
  background: #333;
  z-index: 20;
}
.gantt-dot-start { background: #333; }
.gantt-dot-end { background: #fff; border: 2px solid #333; }
.gantt-virtual-row { background: #fafafa; }
.gantt-virtual-row .gantt-task-icon { color: #999 !important; }
.gantt-virtual-no { color: #8c8c8c; font-style: italic; }
.gantt-virtual-name { color: #8c8c8c; }
.gantt-virtual-line {
  position: absolute; top: 0; bottom: 0;
  width: 1px;
  border-left: 1px dashed #ccc;
  pointer-events: none; z-index: 0;
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

/* 任务右键上下文菜单 */
.task-ctx-mask {
  position: fixed;
  inset: 0;
  z-index: 1999;
}
.task-ctx-menu {
  position: fixed;
  z-index: 2000;
  background: #fff;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.12);
  padding: 4px 0;
  min-width: 160px;
  user-select: none;
}
.task-ctx-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 16px;
  font-size: 13px;
  color: #303133;
  cursor: pointer;
  transition: background 0.15s;
}
.task-ctx-item:hover {
  background: #f0f7ff;
  color: #409EFF;
}
.task-ctx-item--danger { color: #f56c6c; }
.task-ctx-item--danger:hover { background: #fff0f0; color: #f56c6c; }
.ctx-icon {
  width: 18px;
  text-align: center;
  font-size: 13px;
  flex-shrink: 0;
}
.task-ctx-divider {
  height: 1px;
  background: #ebeef5;
  margin: 4px 0;
}
:deep(.ctx-row-active td) {
  background: #ecf5ff !important;
}

.gantt-critical-path {
  stroke-dashoffset: 13;
  animation: gantt-critical-flow 0.8s linear infinite;
}
@keyframes gantt-critical-flow {
  to { stroke-dashoffset: 0; }
}

/* ─── 前置任务悬浮提示 ─── */
.pcell {
  color: #409eff;
  cursor: pointer;
  padding: 1px 4px;
  white-space: normal;
  word-break: break-word;
  line-height: 1.6;
}
.pcell-off { color: #c0c4cc; }
.pover {
  position: fixed; z-index: 99999;
  min-width: 200px; max-width: 380px; max-height: 300px; overflow-y: auto;
  background: #fff;
  border: 1px solid #d9d9d9; border-radius: 6px;
  box-shadow: 0 6px 20px rgba(0,0,0,0.15);
  padding: 4px 0;
}
.pitem {
  display: flex; align-items: center; gap: 10px;
  padding: 8px 14px; cursor: pointer;
  font-size: 13px;
}
.pitem:hover { background: #e6f4ff; }
.pitem + .pitem { border-top: 1px solid #f0f0f0; }
.pno {
  font-weight: 700; color: #409eff; white-space: nowrap; flex-shrink: 0; min-width: 32px;
}
.pnm {
  color: #303133; overflow: hidden; display: flex; align-items: center; gap: 6px; min-width: 0; flex: 1;
}
.pnm > span:first-child {
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0;
}
.pprogress {
  flex-shrink: 0; font-size: 12px; color: #e74c3c; background: #fff2f0; border-radius: 4px; padding: 1px 6px; font-weight: 600;
}
:deep(.el-table__row.pf td) { background-color: #fce4ec; transition: background-color 0.6s; }

/* 操作列 CSS sticky 右固定 —— 替代 fixed='right' 避免树表格固定层错位 */
:deep(.col-sticky-right) { position: sticky !important; right: 0 !important; z-index: 2 !important; background: #fff !important; }
:deep(.el-table__header-wrapper .col-sticky-right) { z-index: 3 !important; }
:deep(.el-table__body-wrapper tr:hover .col-sticky-right) { background-color: #f5f7fa !important; }

/* ─── 左固定列（CSS sticky 替代 Element fixed，解决树表格固定层宽度不同步问题） ─── */
:deep(td.col-index),
:deep(th.col-index) { position: sticky !important; left: 0 !important; z-index: 2 !important; background: #fff !important; }
:deep(td.col-taskname),
:deep(th.col-taskname) { position: sticky !important; left: var(--index-col-width, 70px) !important; z-index: 2 !important; background: #fff !important; }
:deep(.el-table__header-wrapper .col-index),
:deep(.el-table__header-wrapper .col-taskname) { z-index: 3 !important; }
:deep(.el-table__body-wrapper tr:hover td.col-index),
:deep(.el-table__body-wrapper tr:hover td.col-taskname) { background-color: #f5f7fa !important; }
/* 序号列防止树缩进导致换行 */
:deep(td.col-index .cell) { white-space: nowrap !important; }
</style>
<style>
td[data-flash] .cell { background: transparent !important; }
td[data-flash]::before,
td[data-flash]::after { background: transparent !important; }
</style>
