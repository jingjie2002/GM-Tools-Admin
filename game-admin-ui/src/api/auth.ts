import request from '@/utils/request'

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
  username: string
  roles: string[]
}

export const loginApi = (data: LoginRequest): Promise<LoginResponse> => {
  return request.post('/Auth/login', data)
}

export const logoutApi = (): Promise<void> => {
  return request.post('/Auth/logout')
}
