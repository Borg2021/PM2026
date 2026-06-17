export const taskStatusOptions = [
  { value: 0, label: '未开始' },
  { value: 1, label: '进行中' },
  { value: 2, label: '已完成' },
]

export const taskPriorityOptions = [
  { value: 1, label: '最高' },
  { value: 2, label: '高' },
  { value: 3, label: '中' },
  { value: 4, label: '低' },
]

export function statusLabel(s: number): string {
  return ['未开始', '进行中', '已完成'][s] ?? ''
}

export function priorityLabel(p: number): string {
  return ['', '最高', '高', '中', '低'][p] ?? ''
}

export function overdueStatus(row: {
  planStartDate?: string
  planFinishDate?: string
  actualFinishDate?: string
  status?: number
}): string {
  if (!row.planStartDate || !row.planFinishDate) return '未逾期'
  const planEnd = new Date(row.planFinishDate)
  if (row.actualFinishDate) {
    return new Date(row.actualFinishDate) > planEnd ? '已逾期' : '未逾期'
  }
  return new Date() > planEnd ? '已逾期' : '未逾期'
}
