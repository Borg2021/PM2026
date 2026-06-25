import axios from 'axios'
import { ElMessage } from 'element-plus'

const request = axios.create({
  baseURL: '/api/v1',
  timeout: 15000
})

request.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

request.interceptors.response.use(
  (response) => {
    const data = response.data
    if (data.code !== 0) {
      ElMessage.error(data.message || '请求失败')
      if (data.code === 401) {
        localStorage.removeItem('token')
        window.location.href = '/#/login'
      }
      return Promise.reject(new Error(data.message))
    }
    return data
  },
  (error) => {
    // HTTP 401 — token 过期或无效，直接跳登录
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      window.location.href = '/#/login'
      return Promise.reject(error)
    }
    let msg = '网络错误'
    if (error.response?.data) {
      const data = error.response.data
      // 优先取业务 message，其次取 ProblemDetails 的 title，最后取 errors 中的第一条
      if (data.message) {
        msg = data.message
      } else if (data.title) {
        msg = data.title
        // 如果有详细的 errors 字典，拼上第一条
        const errors = data.errors
        if (errors && typeof errors === 'object') {
          const firstKey = Object.keys(errors)[0]
          if (firstKey) {
            const firstError = Array.isArray(errors[firstKey]) ? errors[firstKey][0] : errors[firstKey]
            msg = `${msg}: ${firstError}`
          }
        }
      }
    } else if (error.code === 'ECONNABORTED' && error.message?.includes('timeout')) {
      const match = error.message.match(/timeout of (\d+)ms/)
      const sec = match ? Math.round(parseInt(match[1]) / 1000) : '?'
      msg = `请求超时（超过 ${sec} 秒未响应），请检查网络或稍后重试`
    } else if (error.message) {
      msg = error.message
    }
    ElMessage.error(msg)
    return Promise.reject(error)
  }
)

export default request
