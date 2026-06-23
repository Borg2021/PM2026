<script setup lang="ts">
import { reactive, ref, computed, watch, onMounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useAuthStore } from '@/store/auth'
import {
  getProjectDetail, createProject, updateProject,
  activateProject, completeProject, suspendProject, resumeProject, deactivateProject,
  saveProjectMembers,
  getProjectTasks, createProjectTask, updateProjectTask, deleteProjectTask,
} from '@/api/project'
import { getDepartments, getRoles, searchUsers, getDictByType, getTemplateList, getTemplateDetail } from '@/api/template'
import { getSysParamByKey, getFunctionList } from '@/api/system'
import { formatPreTaskCodes, parsePreTaskCodes, serializePreTaskCodes } from '@/utils/preTaskHelpers'
import { buildDeptTree } from '@/utils/deptTree'
import { taskStatusOptions, taskPriorityOptions, overdueStatus, statusLabel as taskStatusLabel, priorityLabel } from '@/utils/taskConstants'
import ProjectFileTab from './components/ProjectFileTab.vue'
import ProjectChangeTab from './components/ProjectChangeTab.vue'
import ProjectFinanceTab from './components/ProjectFinanceTab.vue'
import ProjectOperationLogTab from './components/ProjectOperationLogTab.vue'
import ProductListTable from './components/ProductListTable.vue'
import ProjectScopeTable from './components/ProjectScopeTable.vue'
import MilestoneTable from './components/MilestoneTable.vue'
import ProjectKanban from './components/ProjectKanban.vue'
import PreTaskTooltip from './components/PreTaskTooltip.vue'
import TaskTemplateDialog from './components/TaskTemplateDialog.vue'
import TaskViewDialog from './components/TaskViewDialog.vue'
import TaskEditDialog from './components/TaskEditDialog.vue'
import ProjectGantt from './components/ProjectGantt.vue'
import type {
  ProjectDetail, ProductItem, ProjectMemberItem,
  ProjectTaskItem
} from '@/types/project'
import type { Department, RoleDict, UserInfo, Template } from '@/types/template'
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
/** 有效表单只读态（查看模式 或 已激活/暂停/完成的项目不允许编辑基本信息） */
const formDisabled = computed(() => isReadonly.value || form.status !== 0)
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
const filesTabMounted = ref(false)  // 首次切到文件 Tab 后设为 true，避免组件销毁重建
watch(activeTab, (tab) => { if (tab === 'files') filesTabMounted.value = true })

/* ───────── 辅助数据 ───────── */
const departments = ref<Department[]>([])
const roles = ref<RoleDict[]>([])
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
const templateImported = ref(false)

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
const fileList = ref<any[]>([])
const uploadRef = ref()
const tempUploadKey = ref('temp_' + Date.now() + '_' + Math.random().toString(36).slice(2, 8))
const uploadedFileIds = ref<number[]>([])
const getToken = () => localStorage.getItem('token')

function handleUploadSuccess(res: any) {
  if (res.code === 0 && res.data?.id) uploadedFileIds.value.push(res.data.id)
}

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

/* ───────── 项目范围列表 ───────── */
const projectScopes = ref<{ sortOrder: number; scopeName: string; scopeDesc: string }[]>([])

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
    // 标记模板已导入
    templateImported.value = true
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

  // 高亮闪 3 下
  if (_ft) clearTimeout(_ft)
  flashId.value = taskId
  _ft = window.setTimeout(() => { flashId.value = null }, 3000)
}
/* ─────────────────────────────────────── */
const taskEditDialogVisible = ref(false)
const taskEditDialogMode = ref<'create' | 'edit'>('create')
const taskEditDialogTask = ref<ProjectTaskItem | null>(null)
const taskEditDialogParentTask = ref<ProjectTaskItem | null>(null)
const showViewTaskDialog = ref(false)
const viewingTask = ref<ProjectTaskItem | null>(null)
const templateDialogRef = ref<InstanceType<typeof TaskTemplateDialog> | null>(null)

function openViewTask(task: ProjectTaskItem) {
  viewingTask.value = task
  showViewTaskDialog.value = true
}

function taskStatusTag(s: number): '' | 'success' | 'warning' | 'info' | 'danger' {
  return ['info', 'primary', 'success'][s] as any ?? 'info'
}

function openAddTask(parent?: ProjectTaskItem) {
  if (parent) {
    taskEditDialogParentTask.value = parent
  } else {
    taskEditDialogParentTask.value = null
  }
  taskEditDialogMode.value = 'create'
  taskEditDialogTask.value = null
  taskEditDialogVisible.value = true
}

function openEditTask(row: ProjectTaskItem) {
  taskEditDialogMode.value = 'edit'
  taskEditDialogTask.value = row
  taskEditDialogParentTask.value = null
  taskEditDialogVisible.value = true
}

async function onTaskSavedFromDialog(payload: { task: ProjectTaskItem; isNew: boolean; oldParentId: number | null }) {
  const { task: savedTask, isNew, oldParentId } = payload

  // 更新本地 tasks
  if (!isNew && savedTask.id) {
    const idx = tasks.value.findIndex(t => t.id === savedTask.id)
    if (idx >= 0) tasks.value[idx] = { ...savedTask }
  } else {
    tasks.value.push({ ...savedTask })
  }

  // 重新编号
  const newParentId = savedTask.parentId ?? null
  if (!isNew && oldParentId !== null) {
    await renumberSiblings(oldParentId)
  }
  await renumberSiblings(newParentId)
  await saveListEdits()

  ElMessage.success('保存成功')
  // 保持 tasks 数据最新，再次从后端同步
  await loadTasks()
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
    const children = tasks.value.filter(t => t.parentId === pid)
    // 计划工期 = 所有直接子节点计划工期之和
    const totalDuration = children.reduce((sum, c) => sum + (c.planDuration || 0), 0)
    if (parent.planDuration !== totalDuration) {
      parent.planDuration = totalDuration
    }
    // 实际工期 = 所有直接子节点实际工期之和
    const totalActual = children.reduce((sum, c) => sum + (c.actualDuration || 0), 0)
    if (parent.actualDuration !== totalActual) {
      parent.actualDuration = totalActual
    }
    // 进度 = 子节点计划工期加权平均
    const weightedPct = children.reduce((sum, c) => sum + (c.planDuration || 0) * (c.progressPct || 0), 0)
    if (totalDuration > 0) {
      const avg = Math.round(weightedPct / totalDuration)
      if (parent.progressPct !== avg) parent.progressPct = avg
    }
  }
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
    }
    // 同步父节点日期 + 收集级联集合
    const parentCascadeNos = syncParentAndCollectChangedNos(cascadeNos)
    dirtyRowIds.value = new Set()
    // 保存后重新拍快照，保持编辑状态
    const snapshots = new Map<number, ProjectTaskItem>()
    for (const t of tasks.value) {
      if (t.id != null) snapshots.set(t.id, JSON.parse(JSON.stringify(t)))
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
      if (t.id != null) snapshots.set(t.id, JSON.parse(JSON.stringify(t)))
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
function openTemplateDialog() {
  templateDialogRef.value?.open()
}

function onTemplateImported() {
  loadTasks()
}

const changeTabRef = ref<InstanceType<typeof ProjectChangeTab> | null>(null)
const financeTabRef = ref<InstanceType<typeof ProjectFinanceTab> | null>(null)

const ganttRef = ref<InstanceType<typeof ProjectGantt> | null>(null)

function handleGanttNavigate(taskId: number) {
  const task = tasks.value.find(t => t.id === taskId)
  if (!task) return
  if (isReadonly.value) {
    openViewTask(task)
  } else {
    openEditTask(task)
  }
}

const opLogTabRef = ref<InstanceType<typeof ProjectOperationLogTab> | null>(null)

/* Tab 切换时按需加载 */
async function onTabChange(tab: string) {
  if (!projectId.value) return
  if (tab === 'tasks' && tasks.value.length === 0) await loadTasks()
  if (tab === 'milestones' && tasks.value.length === 0) await loadTasks()
  if (tab === 'board' && tasks.value.length === 0) await loadTasks()
  if (tab === 'gantt') await ganttRef.value?.loadGanttData()
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
  const [deptRes, roleRes, userRes, dictRes, prodDictRes, taskCatRes] = await Promise.allSettled([
    getDepartments(), getRoles(), searchUsers(''),
    getDictByType('project_type'), getDictByType('product_type'), getDictByType('task_category')
  ])
  if (deptRes.status === 'fulfilled') {
    departments.value = deptRes.value.data
    resolveEngineeringCenterId()
  }
  if (roleRes.status === 'fulfilled') roles.value = roleRes.value.data
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
          <ProjectScopeTable v-model="projectScopes" :readonly="formDisabled" />

          <!-- 产品列表 -->
          <ProductListTable v-model="products" :readonly="formDisabled" :dict-map="dictMap" />

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
              @dragover.native="onMemberTableDragOver"
              @drop.native="onMemberTableDrop"
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
        <PreTaskTooltip v-model:visible="pv.show" :x="pv.x" :y="pv.y" :task="pv.row" :all-tasks="tasks" @navigate="flashRow" />
      </el-tab-pane>

      <!-- ── Tab 4：里程碑 ── -->
      <el-tab-pane label="里程碑" name="milestones" :disabled="!projectId">
        <MilestoneTable :tasks="tasks" :is-readonly="isReadonly" @view="openViewTask" @edit="openEditTask" />
      </el-tab-pane>

      <!-- ── Tab 6：计划甘特图 ── -->
      <el-tab-pane label="甘特图" name="gantt" :disabled="!projectId" class="tab-pane-fill">
        <ProjectGantt
          ref="ganttRef"
          :tasks="tasks"
          :project-id="projectId"
          :is-readonly="isReadonly"
          :cascade-loading="cascadeLoading"
          @navigate="handleGanttNavigate"
        />
      </el-tab-pane>

      <!-- ── Tab 5：任务列表（看板）── -->
      <el-tab-pane label="任务列表" name="board" :disabled="!projectId" class="tab-pane-fill">
        <div class="board-tab-wrapper">
          <ProjectKanban :tasks="tasks" :dict-map="dictMap" @view="openViewTask" />
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
    <TaskTemplateDialog ref="templateDialogRef" :project-id="projectId" @imported="onTemplateImported" />

    <!-- ── 新增/编辑任务对话框 ── -->
    <TaskEditDialog
      v-model:visible="taskEditDialogVisible"
      :mode="taskEditDialogMode"
      :task="taskEditDialogTask"
      :parent-task="taskEditDialogParentTask"
      :project-id="projectId"
      :task-no-rule="taskNoRule"
      :all-tasks="tasks"
      :task-tree="taskTree"
      :dict-map="dictMap"
      :departments="departments"
      :users="users"
      :form-status="form.status"
      @saved="onTaskSavedFromDialog"
    />

    <!-- ── 查看任务详情对话框 ── -->
    <TaskViewDialog v-model:visible="showViewTaskDialog" :task="viewingTask" :dict-map="dictMap" :users="users" :departments="departments" :all-tasks="tasks" />

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
