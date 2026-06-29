import request from './request'
import type { ApiResponse, PagedResult } from '@/types/template'
import type {
  Project, ProjectDetail, ProductItem, ProjectMemberItem, ProjectMilestoneItem,
  ProjectTaskItem, ProjectChangeItem, ProjectFinanceInfo,
  PlanReceiptItem, ReceiptItem, InvoiceItem, ProjectFileItem, ProjectFileVersion
} from '@/types/project'

/** 读取上传超时配置（秒→毫秒），默认 60 秒 */
function getUploadTimeout(): number {
  const cached = localStorage.getItem('upload_filemaxtime')
  if (cached) {
    const sec = parseInt(cached)
    if (sec > 0) return sec * 1000
  }
  return 60000
}

/** 缓存上传超时配置（由系统参数接口调用后更新） */
export function cacheUploadTimeout(seconds: string | number) {
  localStorage.setItem('upload_filemaxtime', String(seconds))
}

export function getProjectList(params: Record<string, any>) {
  return request.get<ApiResponse<PagedResult<Project>>>('/projects', { params })
}

export function getProjectDetail(id: number) {
  return request.get<ApiResponse<ProjectDetail>>(`/projects/${id}`)
}

export function createProject(data: Record<string, any>) {
  return request.post<ApiResponse<{ id: number }>>('/projects', data)
}

export function updateProject(id: number, data: Record<string, any>) {
  return request.put<ApiResponse<null>>(`/projects/${id}`, data)
}

export function activateProject(id: number) {
  return request.post<ApiResponse<null>>(`/projects/${id}/activate`)
}

export function completeProject(id: number) {
  return request.post<ApiResponse<null>>(`/projects/${id}/complete`)
}

export function suspendProject(id: number) {
  return request.post<ApiResponse<null>>(`/projects/${id}/suspend`)
}

export function resumeProject(id: number) {
  return request.post<ApiResponse<null>>(`/projects/${id}/resume`)
}

export function deactivateProject(id: number) {
  return request.post<ApiResponse<null>>(`/projects/${id}/deactivate`)
}

export function deleteProject(id: number) {
  return request.delete<ApiResponse<null>>(`/projects/${id}`)
}

export function saveProjectMembers(projectId: number, members: ProjectMemberItem[]) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/members`, { members })
}

export function saveProjectMilestones(projectId: number, milestones: ProjectMilestoneItem[]) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/milestones`, { milestones })
}

// 任务计划
export function getProjectTasks(projectId: number) {
  return request.get<ApiResponse<ProjectTaskItem[]>>(`/projects/${projectId}/tasks`)
}
export function createProjectTask(projectId: number, data: Omit<ProjectTaskItem, 'id'>) {
  return request.post<ApiResponse<{ id: number }>>(`/projects/${projectId}/tasks`, data)
}
export function updateProjectTask(projectId: number, taskId: number, data: ProjectTaskItem) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/tasks/${taskId}`, data)
}
export function deleteProjectTask(projectId: number, taskId: number) {
  return request.delete<ApiResponse<null>>(`/projects/${projectId}/tasks/${taskId}`)
}
export function createTasksFromTemplate(projectId: number, templateId: number) {
  return request.post<ApiResponse<{ count: number }>>(`/projects/${projectId}/tasks/from-template`, { templateId })
}

// 变更记录
export function getProjectChanges(projectId: number) {
  return request.get<ApiResponse<ProjectChangeItem[]>>(`/projects/${projectId}/changes`)
}
export function createProjectChange(projectId: number, data: ProjectChangeItem) {
  return request.post<ApiResponse<{ id: number }>>(`/projects/${projectId}/changes`, data)
}
export function updateProjectChange(projectId: number, changeId: number, data: ProjectChangeItem) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/changes/${changeId}`, data)
}
export function deleteProjectChange(projectId: number, changeId: number) {
  return request.delete<ApiResponse<null>>(`/projects/${projectId}/changes/${changeId}`)
}

// 财务信息
export function getProjectFinance(projectId: number) {
  return request.get<ApiResponse<ProjectFinanceInfo | null>>(`/projects/${projectId}/finance`)
}
export function saveProjectFinance(projectId: number, data: ProjectFinanceInfo) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/finance`, data)
}
export function savePlanReceipts(projectId: number, records: PlanReceiptItem[]) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/finance/plan-receipts`, { records })
}
export function saveReceipts(projectId: number, records: ReceiptItem[]) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/finance/receipts`, { records })
}
export function saveInvoices(projectId: number, records: InvoiceItem[]) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/finance/invoices`, { records })
}

// 我的任务
export interface MyTaskItem {
  id: number
  taskNo: string
  taskName: string
  status: number
  progressPct: number
  planStartDate?: string
  planFinishDate?: string
  actualStartDate?: string
  actualFinishDate?: string
  planDuration?: number
  actualDuration?: number
  projectId: number
  projectCode: string
  projectName: string
  projectStatus?: number
  isOverdue: boolean
}
export function getMyTasks() {
  return request.get<ApiResponse<MyTaskItem[]>>('/projects/tasks/my')
}

/** 当前用户待处理任务数量（未开始+进行中） */
export function getMyPendingTaskCount() {
  return request.get<ApiResponse<{ count: number }>>('/projects/tasks/my/pending-count')
}

/** 当前用户未上传文件统计（总数, 必须数） */
export function getMyPendingFileCount() {
  return request.get<ApiResponse<{ total: number; required: number }>>('/projects/files/my/pending-count')
}

// 跨项目任务列表
export interface TaskListItem {
  id: number
  taskNo: string
  taskName: string
  nodeType: number
  taskCategory?: string
  sortOrder: number
  status: number
  priority: number
  progressPct: number
  planStartDate?: string
  planFinishDate?: string
  actualStartDate?: string
  actualFinishDate?: string
  planDuration?: number
  actualDuration?: number
  referenceDuration?: number
  preTaskCodes?: string
  wbsCode?: string
  assigneeId?: number | null
  assigneeName?: string
  deptId?: number | null
  deptName?: string
  deliverableCnt: number
  remark?: string
  projectId: number
  projectCode: string
  projectName: string
  projectStatus?: number
}
export function getTaskList(params: Record<string, any>) {
  return request.get<ApiResponse<TaskListItem[]>>('/projects/tasks', { params })
}

// 操作日志
export function getProjectOperationLogs(projectId: number, params?: Record<string, any>) {
  return request.get<ApiResponse<OperationLogItem[]>>(`/projects/${projectId}/operation-logs`, { params })
}
export interface OperationLogItem {
  id: number
  userId: number
  userName: string
  operation: string
  content: string
  createdAt: string
}

// 项目文件资料
export function getProjectFileItems(projectId: number) {
  return request.get<ApiResponse<ProjectFileItem[]>>(`/projects/${projectId}/file-items`)
}

export function saveProjectFileItems(projectId: number, items: Partial<ProjectFileItem>[]) {
  return request.put<ApiResponse<null>>(`/projects/${projectId}/file-items`, { items })
}

export function uploadProjectFileItem(projectId: number, itemId: number, files: File[], remark?: string, onProgress?: (pct: number) => void) {
  const formData = new FormData()
  files.forEach(f => formData.append('files', f))
  if (remark) formData.append('remark', remark)
  return request.post<ApiResponse<{
    versionId: number
    versionNumber: number
    files: { id: number; originalFileName: string; fileSize: number; fileExt: string }[]
    uploadedByName: string
    uploadedAt: string
  }>>(`/projects/${projectId}/file-items/${itemId}/upload`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: getUploadTimeout(),
    onUploadProgress: (e) => { if (e.total) onProgress?.(Math.round((e.loaded / e.total) * 100)) }
  })
}

export function deleteProjectFileItem(projectId: number, itemId: number) {
  return request.delete<ApiResponse<null>>(`/projects/${projectId}/file-items/${itemId}`)
}

export function resetProjectFileItem(projectId: number, itemId: number) {
  return request.post<ApiResponse<null>>(`/projects/${projectId}/file-items/${itemId}/reset`)
}

export function getProjectFileVersions(projectId: number, itemId: number) {
  return request.get<ApiResponse<ProjectFileVersion[]>>(`/projects/${projectId}/file-items/${itemId}/versions`)
}

export function getFileDownloadUrl(projectId: number, itemId: number, version?: number, fileId?: number) {
  const token = localStorage.getItem('token') || ''
  let url = `/api/v1/projects/${projectId}/file-items/${itemId}/download?token=${token}`
  if (version) url += `&version=${version}`
  if (fileId) url += `&fileId=${fileId}`
  return url
}

/** 文件管理：获取当前用户在激活项目中的文件资料 */
export function getMyFiles() {
  return request.get<ApiResponse<any[]>>('/projects/files/my')
}
