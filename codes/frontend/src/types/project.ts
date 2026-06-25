export interface Project {
  id: number
  projectCode: string
  projectName: string
  projectType?: string
  contractCode?: string
  status: number
  statusName: string
  engineeringCenter?: string
  customerName?: string
  progressDesc?: string
  firstTaskProgress?: number
  plannedProgress?: number
  rootPlanStartDate?: string
  rootPlanFinishDate?: string
  rootPlanDuration?: number
  projectManagerName?: string
  pmCenter?: string
  salesManagerName?: string
  salesRegion?: string
  requiredDelivery?: string
  acceptedDelivery?: string
  planStartDate?: string
  createdByName?: string
  createdAt: string
}

export interface ProductItem {
  id?: number
  sortOrder: number
  productType?: string
  quantity: number
  plannedDelivery?: string
  remark?: string
}

export interface ProjectMemberItem {
  id?: number
  sortOrder: number
  roleId?: number | null
  roleName?: string
  memberId?: number | null
  memberName?: string
  deptId?: number | null
  deptName?: string
  functionId?: number | null
  functionName?: string
  remark?: string
}

export interface ProjectMilestoneItem {
  id?: number
  sortOrder: number
  milestoneCode: string
  milestoneName: string
  status: number
  statusName?: string
  planFinishDate?: string
  actualFinishDate?: string
  nodeReference?: string
  remark?: string
}

export interface ProjectTaskItem {
  id?: number
  projectId?: number
  parentId?: number | null
  taskNo: string
  wbsCode: string
  taskName: string
  nodeType: number
  taskCategory?: string
  sortOrder: number
  status: number
  priority: number
  planStartDate?: string
  planFinishDate?: string
  actualStartDate?: string
  actualFinishDate?: string
  planDuration?: number
  actualDuration?: number
  referenceDuration?: number
  preTaskCodes?: string
  deliverableCnt: number
  progressPct: number
  assigneeId?: number | null
  assigneeName?: string
  deptId?: number | null
  deptName?: string
  milestoneId?: number | null
  remark?: string
  children?: ProjectTaskItem[]
}

export interface ProjectChangeItem {
  id?: number
  changeType?: string
  changeParty?: string
  changeContent?: string
  attachmentUrl?: string
  approverId?: number | null
  approverName?: string
  effectEndDate?: string
  createdByName?: string
  createdAt?: string
}

export interface PlanReceiptItem {
  id?: number
  sortOrder: number
  planAmount: number
  receiptType?: string
  planDate?: string
  remark?: string
}

export interface ReceiptItem {
  id?: number
  sortOrder: number
  actualAmount: number
  receiptType?: string
  receiptTime?: string
  remark?: string
}

export interface InvoiceItem {
  id?: number
  sortOrder: number
  invoiceAmount: number
  invoiceRate?: number
  invoiceTime?: string
  invoiceNo?: string
  remark?: string
}

export interface ProjectFinanceInfo {
  taxContractAmount?: number
  taxRate?: number
  currencyType?: string
  paymentMethod?: string
  currencyAmount?: number
  contributionRate?: number
  invoiceRate?: number
  remark?: string
  planReceipts?: PlanReceiptItem[]
  receipts?: ReceiptItem[]
  invoices?: InvoiceItem[]
}

export interface ProjectDetail extends Project {
  specialTerms?: string
  categoryCode?: string
  regionalManagerId?: number | null
  regionalManagerName?: string
  customerContactPhone?: string
  customerContactEmail?: string
  salesManagerId?: number | null
  salesManagerName?: string
  preSalesManagerId?: number | null
  preSalesManagerName?: string
  projectManagerId?: number | null
  actualFinishDate?: string
  deliveryLocation?: string
  finalCustomer?: string
  ownerContactPhone?: string
  businessContactEmail?: string
  qualityStrategy?: string
  projectDelivery?: string
  reportContent?: string
  riskStatus?: string
  currentPhaseDate?: string
  nextStatus?: string
  progressDesc?: string
  canManageStatus?: boolean
  canDeactivate?: boolean

  products: ProductItem[]
  members: ProjectMemberItem[]
  milestones: ProjectMilestoneItem[]
}

/** ViewRoles 可选值 */
export type FileViewRole = 'pm' | 'member' | 'assignee'

/** 版本下的单个文件 */
export interface ProjectFileVersionFile {
  id: number
  originalFileName: string
  fileSize: number
  fileExt?: string
}

/** 文件版本信息 */
export interface ProjectFileVersion {
  id: number
  versionNumber: number
  uploadedBy: number
  uploadedByName: string
  uploadedAt: string
  remark?: string
  files?: ProjectFileVersionFile[]
}

/** 文件清单项 */
export interface ProjectFileItem {
  id?: number
  templateItemId?: number | null
  sortOrder: number
  fileName: string
  required: boolean
  isPublic: boolean
  viewRoles: FileViewRole[]
  assigneeId?: number | null
  assigneeName?: string
  deptId?: number | null
  deptName?: string
  planFinishDate?: string
  planFinishStatus?: 'normal' | 'expiring' | 'overdue' | null
  latestVersion?: Pick<ProjectFileVersion, 'id' | 'versionNumber' | 'files' | 'uploadedByName' | 'uploadedAt'>
  versionCount: number
  remark?: string
}
