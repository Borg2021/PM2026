import request from './request'
import type { ApiResponse, LoginData } from '@/types/template'
import type { CurrentUserData } from '@/types/system'

export function login(username: string, password: string) {
  return request.post<ApiResponse<LoginData>>('/auth/login', { username, password })
}

export function getCurrentUser() {
  return request.get<ApiResponse<CurrentUserData>>('/auth/me')
}

export function changePassword(oldPassword: string, newPassword: string) {
  return request.post<ApiResponse<null>>('/auth/change-password', { oldPassword, newPassword })
}
