import axios from 'axios'
import { ElMessage } from 'element-plus'

const service = axios.create({
  baseURL: '/api',
  timeout: 10000
})

// 请求拦截器
service.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers['Authorization'] = `Bearer ${token}`
    }
    return config
  },
  error => Promise.reject(error)
)

// 响应拦截器
service.interceptors.response.use(
  response => response.data,
  error => {
    const status = error.response?.status
    const message = error.response?.data?.message || '请求失败'
    const code = error.response?.data?.code || ''
    
    console.error(`[API Error] Status: ${status}, Code: ${code}, Message: ${message}`, error)
    
    if (status === 401) {
      // 区分二次验证失败和 Token 失效
      if (code === 'SECONDARY_AUTH_FAILED' || code === 'SECONDARY_AUTH_REQUIRED') {
        // 二次验证失败：仅提示错误，不跳转登录页
        console.warn('[Auth] 二次验证失败，保留当前页面')
        ElMessage.error(message)
        return Promise.reject(error)
      }
      
      // Token 无效：清除凭证并跳转登录页
      console.warn('[Auth] Token 无效或已过期，正在清除凭证并重定向到登录页...')
      localStorage.removeItem('token')
      localStorage.removeItem('roles')
      localStorage.removeItem('username')
      window.location.href = '/login'
      return Promise.reject(error)
    }
    
    ElMessage.error(message)
    return Promise.reject(error)
  }
)

export default service
