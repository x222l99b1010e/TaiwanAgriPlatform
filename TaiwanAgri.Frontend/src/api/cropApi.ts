// src/api/cropApi.ts
import { marketApi } from './market'
import type { CropItem } from './profile'

export async function getAllCrops(): Promise<CropItem[]> {
  const [veg, fruit, flower] = await Promise.all([
    marketApi.getCrops('Veg'),
    marketApi.getCrops('Fruit'),
    marketApi.getCrops('Flower'),
  ])
  return [...veg, ...fruit, ...flower]
}