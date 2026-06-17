import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getCurrentUser } from '@/api/auth'
import { getMyPendingTaskCount, getMyPendingFileCount } from '@/api/project'
import type { MenuItem } from '@/types/system'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem('token') || '')
  const realName = ref(localStorage.getItem('realName') || '')
  const role = ref(localStorage.getItem('role') || '')
  const storedUserId = localStorage.getItem('userId')
  const userId = ref(storedUserId ? Number(storedUserId) : 0)
  const permissions = ref<string[]>([])
  const menus = ref<MenuItem[]>([])
  const pendingTaskCount = ref(0)
  const pendingFileTotal = ref(0)
  const pendingFileRequired = ref(0)

  function setAuth(t: string, name: string, r: string, uid: number) {
    token.value = t
    realName.value = name
    role.value = r
    userId.value = uid
    localStorage.setItem('token', t)
    localStorage.setItem('realName', name)
    localStorage.setItem('role', r)
    localStorage.setItem('userId', String(uid))
  }

  function logout() {
    token.value = ''
    realName.value = ''
    role.value = ''
    userId.value = 0
    permissions.value = []
    menus.value = []
    pendingTaskCount.value = 0
    pendingFileTotal.value = 0
    pendingFileRequired.value = 0
    localStorage.removeItem('token')
    localStorage.removeItem('realName')
    localStorage.removeItem('role')
    localStorage.removeItem('userId')
  }

  async function fetchPermissionsAndMenus() {
    try {
      const res = await getCurrentUser()
      permissions.value = res.data.permissions ?? []
      menus.value = res.data.menus ?? []
    } catch {
      permissions.value = []
      menus.value = []
    }
  }

  function hasPermission(code: string) {
    return permissions.value.includes(code)
  }

  function hasAnyPermission(codes: string[]) {
    return codes.some(c => permissions.value.includes(c))
  }

  async function ensureUserId(): Promise<number> {
    if (userId.value) return userId.value
    try {
      const res = await getCurrentUser()
      userId.value = res.data.userId
      localStorage.setItem('userId', String(userId.value))
      return userId.value
    } catch {
      return 0
    }
  }

  async function fetchPendingCounts() {
    try {
      const [taskRes, fileRes] = await Promise.all([
        getMyPendingTaskCount(),
        getMyPendingFileCount()
      ])
      if (taskRes.code === 0) pendingTaskCount.value = taskRes.data?.count ?? 0
      if (fileRes.code === 0) {
        pendingFileTotal.value = fileRes.data?.total ?? 0
        pendingFileRequired.value = fileRes.data?.required ?? 0
      }
    } catch { /* 静默失败，不影响菜单显示 */ }
  }

  const isLoggedIn = () => !!token.value

  return { token, realName, role, userId, permissions, menus, pendingTaskCount, pendingFileTotal, pendingFileRequired, setAuth, logout, isLoggedIn, ensureUserId, fetchPermissionsAndMenus, fetchPendingCounts, hasPermission, hasAnyPermission }
})
