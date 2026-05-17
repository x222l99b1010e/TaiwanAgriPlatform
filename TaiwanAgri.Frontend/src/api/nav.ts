import axios from 'axios'

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
  const res = await axios.get<NavModule[]>('/api/nav/modules')
  return res.data
}