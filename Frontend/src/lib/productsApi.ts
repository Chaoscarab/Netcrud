import { createApiClient } from './apiClient'

export interface Product {
  id: number
  name: string
  quantity: number
  price: number
}

export interface CreateProductInput {
  name: string
  quantity: number
  price: number
}

export type ProductFilter = 'all' | 'in-stock' | 'out-of-stock'

type ProductsApiOptions = {
  onUnauthorized?: () => void
}

export function createProductsApi(options: ProductsApiOptions = {}) {
  const api = createApiClient(options)

  const list = (filter: ProductFilter = 'all') => {
    const query = filter === 'all' ? '' : `?filter=${encodeURIComponent(filter)}`
    return api.get(`/api/products${query}`)
  }

  const create = (product: CreateProductInput) =>
    api.post('/api/products', {
      name: product.name,
      quantity: product.quantity,
      price: product.price
    })

  const remove = (productId: number) => api.del(`/api/products/${productId}`)

  return { list, create, remove }
}