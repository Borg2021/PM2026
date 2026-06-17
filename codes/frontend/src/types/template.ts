export type TemplateType = 1 | 2 | 3 | 4
export type NodeType = 1 | 2

export interface PredecessorInfo {
  predecessorId: number
  predecessorCode: string
  dependencyType: string
  lagDays: number
}

export interface Template {
  id: number
  templateCode: string
  templateName: string
  templateType: TemplateType
  templateTypeName: string
  description: string
  createdByName: string
  createdAt: string
  status: number
}

export interface PlanNode {
  id?: number
  parentId?: number | null
  nodeCode: string
  nodeName: string
  nodeType: NodeType
  nodeTypeName?: string
  sortOrder: number
  stdDuration?: number | null
  predecessors: PredecessorInfo[]
  deliverableCnt: number
  assigneeId?: number | null
  assigneeName?: string
  deptId?: number | null
  deptName?: string
  remark: string
  taskCategory?: string
  children: PlanNode[]
}

export interface Milestone {
  id?: number
  milestoneCode: string
  milestoneName: string
  sortOrder: number
  remark: string
}

export interface TemplateMember {
  id?: number
  sortOrder: number
  roleId?: number | null
  roleName?: string
  memberId?: number | null
  memberName?: string
  deptId?: number | null
  deptName?: string
  remark: string
}

export interface PlanBundle {
  id: number
  name: string
  description: string
  templateCount: number
  createdByName: string
  createdAt: string
}

export interface PlanBundleDetail extends PlanBundle {
  items: PlanBundleItem[]
}

export interface PlanBundleItem {
  id: number
  templateId: number
  templateCode: string
  templateName: string
  sortOrder: number
}

export type FileViewRole = 'pm' | 'member' | 'assignee'

export interface FileTemplateItem {
  id?: number
  sortOrder: number
  fileName: string
  required: boolean
  isPublic: boolean
  viewRoles?: string
  deptId?: number | null
  deptName?: string
  remark?: string
}

export interface TemplateDetail {
  id: number
  templateCode: string
  templateName: string
  templateType: TemplateType
  templateTypeName: string
  description: string
  createdByName: string
  createdAt: string
  planNodes: PlanNode[]
  milestones: Milestone[]
  members: TemplateMember[]
  fileItems?: FileTemplateItem[]
}

export interface PagedResult<T> {
  total: number
  items: T[]
}

export interface ApiResponse<T> {
  code: number
  message: string
  data: T
}

export interface LoginData {
  token: string
  realName: string
  role: string
  userId: number
}

export interface Department {
  id: number
  name: string
  parentId?: number | null
}

export interface RoleDict {
  id: number
  name: string
}

export interface UserInfo {
  id: number
  realName: string
  username: string
  departmentId?: number
  role?: string
  functionIds?: number[]
  functionNames?: string
}
