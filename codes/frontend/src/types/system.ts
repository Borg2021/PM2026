export interface SystemUser {
  id: number
  username: string
  realName: string
  role: string
  status: number
  createdAt: string
  departmentId: number | null
  departmentName: string | null
  functionIds: number[]
  functionNames: string | null
  rbacRoleNames: string[]
}

export interface DepartmentItem {
  id: number
  name: string
  parentId: number | null
  sortOrder: number
}

export interface DepartmentTreeNode extends DepartmentItem {
  children: DepartmentTreeNode[]
}

export interface SystemRole {
  id: number
  name: string
}

export interface DictItem {
  id: number
  dictType: string
  dictCode: string
  dictLabel: string
  sortOrder: number
  status: number
}

export interface SysParamItem {
  id: number
  paramKey: string
  paramValue: string
  description: string | null
}

export interface DictTypeItem {
  id: number
  dictTypeCode: string
  dictTypeName: string
  remark: string | null
  itemCount: number
}

export interface PermissionNode {
  id: number
  code: string
  name: string
  parentId: number | null
  type: number
  sortOrder: number
  icon?: string
  path?: string
  children: PermissionNode[]
}

export interface RbacRole {
  id: number
  name: string
  code: string
  description?: string
  status: number
  dataScope?: number
}

export interface MenuItem {
  id: number
  code: string
  name: string
  path: string
  icon?: string
  children: MenuItem[]
}

export interface CurrentUserData {
  userId: number
  realName: string
  role: string
  permissions: string[]
  menus: MenuItem[]
}

export interface FunctionItem {
  id: number
  code: string
  name: string
  description: string | null
  sortOrder: number
}
