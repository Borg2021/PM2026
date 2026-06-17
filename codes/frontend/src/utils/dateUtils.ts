/** 日期加减天数（时区安全，返回 YYYY-MM-DD） */
export function dateAddDays(dateStr: string, days: number): string {
  const [y, m, d] = dateStr.slice(0, 10).split('-').map(Number)
  const dt = new Date(y, m - 1, d)
  dt.setDate(dt.getDate() + days)
  return dt.toISOString().slice(0, 10)
}
