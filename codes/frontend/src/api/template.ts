import request from './request'
import type {
  ApiResponse, PagedResult, Template, TemplateDetail,
  PlanNode, Milestone, TemplateMember, FileTemplateItem,
  Department, RoleDict, UserInfo,
  PlanBundle, PlanBundleDetail
} from '@/types/template'

export function getTemplateList(params: Record<string, any>) {
  return request.get<ApiResponse<PagedResult<Template>>>('/templates', { params })
}

export function createTemplate(data: { templateCode: string; templateName: string; templateType: number; description?: string }) {
  return request.post<ApiResponse<{ id: number }>>('/templates', data)
}

export function getTemplateDetail(id: number) {
  return request.get<ApiResponse<TemplateDetail>>(`/templates/${id}`)
}

export function updateTemplate(id: number, data: { templateName: string; description?: string }) {
  return request.put<ApiResponse<null>>(`/templates/${id}`, data)
}

export function deleteTemplate(id: number) {
  return request.delete<ApiResponse<null>>(`/templates/${id}`)
}

export function savePlanNodes(templateId: number, nodes: PlanNode[]) {
  return request.put<ApiResponse<null>>(`/templates/${templateId}/plan-nodes`, { nodes })
}

export function saveMilestones(templateId: number, milestones: Milestone[]) {
  return request.put<ApiResponse<null>>(`/templates/${templateId}/milestones`, { milestones })
}

export function saveMembers(templateId: number, members: TemplateMember[]) {
  return request.put<ApiResponse<null>>(`/templates/${templateId}/members`, { members })
}

export function saveTemplateFiles(templateId: number, items: FileTemplateItem[]) {
  return request.put<ApiResponse<null>>(`/templates/${templateId}/files`, { files: items })
}

export function getDepartments() {
  return request.get<ApiResponse<Department[]>>('/departments')
}

export function getRoles() {
  return request.get<ApiResponse<RoleDict[]>>('/roles')
}

export function searchUsers(keyword: string, departmentId?: number, functionId?: number) {
  return request.get<ApiResponse<UserInfo[]>>('/users/search', { params: { keyword, departmentId, functionId } })
}

export function getDictByType(dictType: string) {
  return request.get<ApiResponse<{ id: number; dictCode: string; dictLabel: string }[]>>(`/dicts/${dictType}`)
}

/* 计划模板集 */
export function getPlanBundleList(params: Record<string, any>) {
  return request.get<ApiResponse<PagedResult<PlanBundle>>>('/plan-bundles', { params })
}

export function getPlanBundleDetail(id: number) {
  return request.get<ApiResponse<PlanBundleDetail>>(`/plan-bundles/${id}`)
}

export function createPlanBundle(data: { name: string; description?: string; items: { templateId: number; sortOrder: number }[] }) {
  return request.post<ApiResponse<{ id: number }>>('/plan-bundles', data)
}

export function updatePlanBundle(id: number, data: { name: string; description?: string; items: { templateId: number; sortOrder: number }[] }) {
  return request.put<ApiResponse<null>>(`/plan-bundles/${id}`, data)
}

export function deletePlanBundle(id: number) {
  return request.delete<ApiResponse<null>>(`/plan-bundles/${id}`)
}

export function assemblePlanBundle(id: number, name: string) {
  return request.post<ApiResponse<{ id: number }>>(`/plan-bundles/${id}/assemble`, { name })
}
