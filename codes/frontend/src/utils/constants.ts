export const TEMPLATE_TYPES = [
  { value: 1, label: '计划' },
  { value: 2, label: '里程碑' },
  { value: 3, label: '项目成员' }
] as const

export const NODE_TYPES = [
  { value: 1, label: '计划节点' },
  { value: 2, label: '任务节点' }
] as const

export const PAGE_SIZES = [10, 20, 50] as const
