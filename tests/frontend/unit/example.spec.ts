// Vitest 单元测试示例
// 运行: npx vitest run

import { describe, it, expect, vi } from 'vitest'

// 示例：日期工具函数测试
// 实际路径：import { formatDate } from '@/utils/dateUtils'

describe('dateUtils', () => {
  it('formatDate 格式化日期为 yyyy-MM-dd', () => {
    // 示例断言 — 替换为实际函数
    const formatDate = (date: Date): string => {
      const y = date.getFullYear()
      const m = String(date.getMonth() + 1).padStart(2, '0')
      const d = String(date.getDate()).padStart(2, '0')
      return `${y}-${m}-${d}`
    }

    const result = formatDate(new Date(2026, 5, 17))
    expect(result).toBe('2026-06-17')
  })

  it('formatDate 处理跨年日期', () => {
    const formatDate = (date: Date): string => {
      const y = date.getFullYear()
      const m = String(date.getMonth() + 1).padStart(2, '0')
      const d = String(date.getDate()).padStart(2, '0')
      return `${y}-${m}-${d}`
    }

    const result = formatDate(new Date(2025, 11, 31))
    expect(result).toBe('2025-12-31')
  })
})

describe('deptTree', () => {
  it('buildTree 从扁平数组构建部门树', () => {
    // 示例：部门树构建逻辑
    interface DeptNode {
      id: number
      name: string
      parentId: number | null
      children?: DeptNode[]
    }

    const buildTree = (list: DeptNode[]): DeptNode[] => {
      const map = new Map<number, DeptNode>()
      const roots: DeptNode[] = []

      list.forEach(item => {
        map.set(item.id, { ...item, children: [] })
      })

      list.forEach(item => {
        const node = map.get(item.id)!
        if (item.parentId === null) {
          roots.push(node)
        } else {
          const parent = map.get(item.parentId)
          if (parent) {
            parent.children!.push(node)
          }
        }
      })

      return roots
    }

    const input: DeptNode[] = [
      { id: 1, name: '总部', parentId: null },
      { id: 2, name: '研发部', parentId: 1 },
      { id: 3, name: '销售部', parentId: 1 },
      { id: 4, name: '前端组', parentId: 2 },
    ]

    const result = buildTree(input)

    expect(result).toHaveLength(1)
    expect(result[0].name).toBe('总部')
    expect(result[0].children).toHaveLength(2)
    expect(result[0].children![0].children).toHaveLength(1)
  })
})
