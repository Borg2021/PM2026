/** 部门树节点（最小化接口，兼容所有消费方） */
export interface DeptTreeNode {
  id: number
  name: string
  parentId?: number | null
  children: DeptTreeNode[]
}

/** 扁平部门列表 → 树结构 */
export function buildDeptTree(list: { id: number; parentId?: number | null; name?: string }[]): DeptTreeNode[] {
  const map = new Map<number, DeptTreeNode>()
  const roots: DeptTreeNode[] = []
  for (const item of list) {
    map.set(item.id, { id: item.id, name: item.name ?? '', parentId: item.parentId ?? null, children: [] })
  }
  for (const node of map.values()) {
    if (node.parentId != null && map.has(node.parentId)) {
      map.get(node.parentId)!.children.push(node)
    } else {
      roots.push(node)
    }
  }
  return roots
}
