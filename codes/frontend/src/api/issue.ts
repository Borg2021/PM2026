import request from './request'
import type { IssueListResponse, ProjectIssue, CreateIssueRequest, UpdateIssueStatusRequest, MyIssueItem } from '@/types/issue'

// ── 项目内接口 ──
export function getProjectIssues(projectId: number, params?: Record<string, any>) {
  return request.get<IssueListResponse>(`/projects/${projectId}/issues`, { params })
}

export function getProjectIssue(projectId: number, id: number) {
  return request.get<ProjectIssue>(`/projects/${projectId}/issues/${id}`)
}

export function createProjectIssue(projectId: number, data: CreateIssueRequest) {
  return request.post(`/projects/${projectId}/issues`, data)
}

export function updateProjectIssue(projectId: number, id: number, data: CreateIssueRequest) {
  return request.put(`/projects/${projectId}/issues/${id}`, data)
}

export function deleteProjectIssue(projectId: number, id: number) {
  return request.delete(`/projects/${projectId}/issues/${id}`)
}

export function updateIssueStatus(projectId: number, id: number, data: UpdateIssueStatusRequest) {
  return request.put(`/projects/${projectId}/issues/${id}/status`, data)
}

// ── 全局接口 ──
export function getMyIssues(params?: Record<string, any>) {
  return request.get<{ items: MyIssueItem[]; total: number; pageIndex: number; pageSize: number }>('/issues/my', { params })
}

export function getMyIssueCount() {
  return request.get<{ count: number }>('/issues/my/count')
}

export function createIssue(data: CreateIssueRequest & { projectId: number }) {
  return request.post('/issues', data)
}
