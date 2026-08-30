import apiClient from './apiClient'

export interface NavChild {
  name: string
  route: string
  icon: string
  sortOrder: number
}

export interface NavModule {
  name: string
  route: string
  icon: string
  sortOrder: number
  children: NavChild[]
}

export async function fetchNavModules(): Promise<NavModule[]> {
  const res = await apiClient.get<NavModule[]>('/api/nav/modules')
  return res.data
}