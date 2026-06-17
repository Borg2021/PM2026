import request from './request'
import type { ApiResponse, PagedResult } from '@/types/template'
import type { SystemUser, DepartmentItem, SystemRole, DictItem, DictTypeItem, SysParamItem, PermissionNode, RbacRole, FunctionItem } from '@/types/system'

/* ---- 用户管理 ---- */

export function getSystemUsers(params: { pageIndex: number; pageSize: number; keyword?: string; departmentIds?: string }) {
  return request.get<ApiResponse<PagedResult<SystemUser>>>('/system/users', { params })
}

export function createSystemUser(data: { username: string; password: string; realName: string; role: string; departmentId?: number | null; functionIds?: number[] }) {
  return request.post<ApiResponse<{ id: number }>>('/system/users', data)
}

export function updateSystemUser(id: number, data: { realName: string; role: string; status: number; departmentId?: number | null; functionIds?: number[] }) {
  return request.put<ApiResponse<null>>(`/system/users/${id}`, data)
}

export function resetUserPassword(id: number, data: { newPassword: string }) {
  return request.put<ApiResponse<null>>(`/system/users/${id}/reset-password`, data)
}

export function deleteSystemUser(id: number) {
  return request.delete<ApiResponse<null>>(`/system/users/${id}`)
}

/** 搜索用户（用于选择部门负责人等场景） */
export function searchUsers(keyword: string) {
  return request.get<ApiResponse<{ id: number; realName: string; username: string; departmentId: number | null; departmentName: string | null }[]>>('/users/search', { params: { keyword } })
}

/* ---- 部门管理 ---- */

export function getDepartmentList() {
  return request.get<ApiResponse<DepartmentItem[]>>('/system/departments')
}

export function createDepartment(data: { name: string; parentId?: number | null; sortOrder: number; leaderIds?: number[] }) {
  return request.post<ApiResponse<{ id: number }>>('/system/departments', data)
}

export function updateDepartment(id: number, data: { name: string; parentId?: number | null; sortOrder: number; leaderIds?: number[] }) {
  return request.put<ApiResponse<null>>(`/system/departments/${id}`, data)
}

export function deleteDepartment(id: number) {
  return request.delete<ApiResponse<null>>(`/system/departments/${id}`)
}

/* ---- 角色管理 ---- */

export function getSystemRoles() {
  return request.get<ApiResponse<SystemRole[]>>('/system/roles')
}

export function createSystemRole(data: { name: string }) {
  return request.post<ApiResponse<{ id: number }>>('/system/roles', data)
}

export function updateSystemRole(id: number, data: { name: string }) {
  return request.put<ApiResponse<null>>(`/system/roles/${id}`, data)
}

export function deleteSystemRole(id: number) {
  return request.delete<ApiResponse<null>>(`/system/roles/${id}`)
}

/* ---- 字典管理 ---- */

export function getDictList(params?: { dictType?: string }) {
  return request.get<ApiResponse<DictItem[]>>('/system/dicts', { params })
}

export function createDict(data: { dictType: string; dictCode: string; dictLabel: string; sortOrder: number }) {
  return request.post<ApiResponse<{ id: number }>>('/system/dicts', data)
}

export function updateDict(id: number, data: { dictType: string; dictCode: string; dictLabel: string; sortOrder: number }) {
  return request.put<ApiResponse<null>>(`/system/dicts/${id}`, data)
}

export function deleteDict(id: number) {
  return request.delete<ApiResponse<null>>(`/system/dicts/${id}`)
}

/* ---- 字典类型管理 ---- */

export function getDictTypeList() {
  return request.get<ApiResponse<DictTypeItem[]>>('/system/dict-types')
}

export function createDictType(data: { dictTypeCode: string; dictTypeName: string; remark?: string }) {
  return request.post<ApiResponse<{ id: number }>>('/system/dict-types', data)
}

export function updateDictType(id: number, data: { dictTypeCode: string; dictTypeName: string; remark?: string }) {
  return request.put<ApiResponse<null>>(`/system/dict-types/${id}`, data)
}

export function deleteDictType(id: number) {
  return request.delete<ApiResponse<null>>(`/system/dict-types/${id}`)
}

/* ---- 系统参数 ---- */

export function getSysParamList() {
  return request.get<ApiResponse<SysParamItem[]>>('/system/sys-params')
}

export function createSysParam(data: { paramKey: string; paramValue: string; description?: string }) {
  return request.post<ApiResponse<{ id: number }>>('/system/sys-params', data)
}

export function updateSysParam(id: number, data: { paramKey: string; paramValue: string; description?: string }) {
  return request.put<ApiResponse<null>>(`/system/sys-params/${id}`, data)
}

export function deleteSysParam(id: number) {
  return request.delete<ApiResponse<null>>(`/system/sys-params/${id}`)
}

export function getSysParamByKey(key: string) {
  return request.get<ApiResponse<{ paramKey: string; paramValue: string }>>(`/sys-params/${encodeURIComponent(key)}`)
}

/* ---- RBAC 权限管理 ---- */

export function getPermissionTree() {
  return request.get<ApiResponse<PermissionNode[]>>('/system/permissions/tree')
}

export function getRbacRoles() {
  return request.get<ApiResponse<RbacRole[]>>('/system/rbac-roles')
}

export function createRbacRole(data: { name: string; code: string; description?: string; dataScope?: number }) {
  return request.post<ApiResponse<{ id: number }>>('/system/rbac-roles', data)
}

export function updateRbacRole(id: number, data: { name: string; code: string; description?: string; dataScope?: number }) {
  return request.put<ApiResponse<null>>(`/system/rbac-roles/${id}`, data)
}

export function deleteRbacRole(id: number) {
  return request.delete<ApiResponse<null>>(`/system/rbac-roles/${id}`)
}

export function getRolePermissions(roleId: number) {
  return request.get<ApiResponse<number[]>>(`/system/roles/${roleId}/permissions`)
}

export function updateRolePermissions(roleId: number, data: { permissionIds: number[] }) {
  return request.put<ApiResponse<null>>(`/system/roles/${roleId}/permissions`, data)
}

export function getUserRoles(userId: number) {
  return request.get<ApiResponse<number[]>>(`/system/users/${userId}/roles`)
}

export function updateUserRoles(userId: number, data: { roleIds: number[] }) {
  return request.put<ApiResponse<null>>(`/system/users/${userId}/roles`, data)
}

/* ---- 职能管理 ---- */

export function getFunctionList() {
  return request.get<ApiResponse<FunctionItem[]>>('/functions')
}

export function createFunction(data: { code: string; name: string; description?: string; sortOrder: number }) {
  return request.post<ApiResponse<{ id: number }>>('/system/functions', data)
}

export function updateFunction(id: number, data: { code: string; name: string; description?: string; sortOrder: number }) {
  return request.put<ApiResponse<null>>(`/system/functions/${id}`, data)
}

export function deleteFunction(id: number) {
  return request.delete<ApiResponse<null>>(`/system/functions/${id}`)
}
