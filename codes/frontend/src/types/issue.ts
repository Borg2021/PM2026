export interface ProjectIssue {
  id: number
  projectId: number
  issueCode: string
  title: string
  description?: string
  issueSource: string
  issueType: string
  severity: string
  priority: string
  status: number          // 0=待处理 1=处理中 2=已完成
  causeAnalysis?: string
  discoveredDate: string
  plannedDate?: string
  responsibleDeptId?: number
  responsibleDeptName?: string
  assigneeId: number
  assigneeName?: string
  submitterId: number
  submitterName?: string
  creatorId: number
  creatorName?: string
  verifierId?: number
  verifierName?: string
  verifiedDate?: string
  reopenCount: number
  createdAt: string
  updatedAt?: string
  measures?: IssueMeasure[]
}

export interface IssueMeasure {
  id?: number
  issueId?: number
  sortOrder: number
  measure: string
  measureType?: string
  responsibleDeptId?: number
  responsibleDeptName?: string
  responsibleUserId?: number
  responsibleUserName?: string
  remark?: string
  plannedDate?: string
}

export interface IssueListResponse {
  items: ProjectIssue[]
  total: number
  pageIndex: number
  pageSize: number
}

export interface MyIssueItem extends ProjectIssue {
  projectCode: string
  projectName: string
}

export interface CreateIssueRequest {
  title: string
  description?: string
  issueSource: string
  issueType: string
  severity: string
  priority: string
  causeAnalysis?: string
  discoveredDate: string
  plannedDate?: string
  responsibleDeptId?: number
  responsibleDeptName?: string
  assigneeId: number
  assigneeName?: string
  submitterId: number
  submitterName?: string
  verifierId?: number
  verifierName?: string
  verifiedDate?: string
  measures: IssueMeasure[]
}

export interface UpdateIssueStatusRequest {
  status: number
  causeAnalysis?: string
  verifierId?: number
  verifierName?: string
  verifiedDate?: string
}

export const issueStatusMap: Record<number, string> = {
  0: '待处理',
  1: '处理中',
  2: '已完成'
}

export const issueStatusTagType: Record<number, string> = {
  0: 'warning',
  1: 'primary',
  2: 'success'
}
