import type { ProjectTaskItem } from '@/types/project'

export interface PredSegment {
  taskId: number
  dependencyType: string
  lagDays: number
}

export function parsePreTaskCodes(raw: string | undefined): PredSegment[] {
  if (!raw) return []
  const segments: PredSegment[] = []
  const regex = /(\d+)\((\w+)([+-]\d+)?\)/g
  let m: RegExpExecArray | null
  while ((m = regex.exec(raw)) !== null) {
    segments.push({
      taskId: parseInt(m[1], 10),
      dependencyType: m[2],
      lagDays: parseInt(m[3] || '0', 10)
    })
  }
  return segments
}

export function formatPreTaskCodes(
  preTaskCodes: string | undefined,
  taskMap: Map<number, ProjectTaskItem>
): string {
  if (!preTaskCodes) return ''
  const segments = parsePreTaskCodes(preTaskCodes)
  return segments
    .map(seg => {
      const task = taskMap.get(seg.taskId)
      const displayNo = task?.taskNo ?? String(seg.taskId)
      const sign = seg.lagDays > 0 ? '+' : ''
      const lag = seg.lagDays !== 0 ? `${sign}${seg.lagDays}` : ''
      return `${displayNo}(${seg.dependencyType}${lag})`
    })
    .join(',')
}

/** PredRow[] / PredSegment[] → "001(FS+5),002(SS)" */
export function serializePreTaskCodes(rows: { taskId: number; dependencyType: string; lagDays: number }[]): string {
  return rows
    .filter(r => r.taskId)
    .map(r => {
      const sign = r.lagDays > 0 ? '+' : ''
      const lag = r.lagDays !== 0 ? `${sign}${r.lagDays}` : ''
      return `${r.taskId}(${r.dependencyType}${lag})`
    })
    .join(',')
}
