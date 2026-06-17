import request from './request'
import type { ApiResponse, LoginData } from '@/types/template'
import type { CurrentUserData } from '@/types/system'
import { cacheUploadTimeout } from './project'

export function login(username: string, password: string) {
  return request.post<ApiResponse<LoginData>>('/auth/login', { username, password })
}

export function getCurrentUser() {
  return request.get<ApiResponse<CurrentUserData>>('/auth/me')
}

export function changePassword(oldPassword: string, newPassword: string) {
  return request.post<ApiResponse<null>>('/auth/change-password', { oldPassword, newPassword })
}

/** 拉取上传配置参数并缓存到 localStorage */
export async function fetchUploadConfig() {
  try {
    const res = await request.get<ApiResponse<{ paramKey: string; paramValue: string }>>('/sys-params/upload_filemaxtime')
    if (res.data.paramValue) {
      cacheUploadTimeout(res.data.paramValue)
    }
  } catch {
    // 拉取失败用默认值 60s，不阻塞
  }
}
