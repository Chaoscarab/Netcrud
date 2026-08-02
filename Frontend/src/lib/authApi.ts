import type { AuthFormValues } from '../components/forms/AuthForm'
import { createApiClient } from './apiClient'

export interface UserProfile {
  id: number
  firstName: string
  lastName: string
  email: string
}

type AuthApiOptions = {
  onUnauthorized?: () => void
}

export function createAuthApi(options: AuthApiOptions = {}) {
  const api = createApiClient(options)

  const me = () => api.get('/api/auth/me')

  const signup = (values: AuthFormValues) =>
    api.post('/api/auth/signup', {
      firstName: values.firstName,
      lastName: values.lastName,
      email: values.email,
      password: values.password,
      confirmPassword: values.confirmPassword
    })

  const login = (values: AuthFormValues) =>
    api.post('/api/auth/login', {
      email: values.email,
      password: values.password
    })

  const logout = () => api.post('/api/auth/logout')

  return { me, signup, login, logout }
}