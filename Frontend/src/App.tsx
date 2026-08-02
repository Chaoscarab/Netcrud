import { useEffect, useMemo, useState } from 'react'
import Header, { type AuthMode } from './components/header/header'
import AuthForm, { type AuthFormValues } from './components/forms/AuthForm'
import { createAuthApi, type UserProfile } from './lib/authApi'
import { createProductsApi, type Product } from './lib/productsApi'

type SortKey = 'name' | 'quantity' | 'price'
type SortDirection = 'neutral' | 'asc' | 'desc'

export default function App() {
  const [mode, setMode] = useState<AuthMode>('signin')
  const [isAuthModalOpen, setIsAuthModalOpen] = useState(false)
  const [authError, setAuthError] = useState('')
  const [isSubmittingAuth, setIsSubmittingAuth] = useState(false)

  const [products, setProducts] = useState<Product[]>([])
  const [productsError, setProductsError] = useState('')
  const [isLoadingProducts, setIsLoadingProducts] = useState(false)
  const [sortKey, setSortKey] = useState<SortKey | null>(null)
  const [sortDirection, setSortDirection] = useState<SortDirection>('neutral')

  const [isAddModalOpen, setIsAddModalOpen] = useState(false)
  const [addProductError, setAddProductError] = useState('')
  const [isSubmittingProduct, setIsSubmittingProduct] = useState(false)

  const [currentUser, setCurrentUser] = useState<UserProfile | null>(null)

  const handleUnauthorized = () => {
    setCurrentUser(null)
    setProducts([])
    setProductsError('')
  }

  const authApi = createAuthApi({
    onUnauthorized: handleUnauthorized
  })

  const productsApi = createProductsApi({
    onUnauthorized: handleUnauthorized
  })

  const openAuthModal = (nextMode: AuthMode) => {
    setMode(nextMode)
    setAuthError('')
    setIsAuthModalOpen(true)
  }

  const closeAuthModal = () => {
    setAuthError('')
    setIsAuthModalOpen(false)
  }

  const loadProducts = async () => {
    if (!currentUser) {
      return
    }

    setIsLoadingProducts(true)
    setProductsError('')

    try {
      const response = await productsApi.list()
      if (!response.ok) {
        setProductsError('Could not load products.')
        return
      }

      const data = (await response.json()) as Product[]
      setProducts(data)
    } catch {
      setProductsError('Could not load products.')
    } finally {
      setIsLoadingProducts(false)
    }
  }

  const onSortHeaderClick = (column: SortKey) => {
    if (sortKey !== column) {
      setSortKey(column)
      setSortDirection('asc')
      return
    }

    if (sortDirection === 'asc') {
      setSortDirection('desc')
      return
    }

    if (sortDirection === 'desc') {
      setSortKey(null)
      setSortDirection('neutral')
      return
    }

    setSortDirection('asc')
  }

  const getSortArrow = (column: SortKey) => {
    if (sortKey !== column || sortDirection === 'neutral') {
      return '↕'
    }

    return sortDirection === 'asc' ? '↑' : '↓'
  }

  const sortedProducts = useMemo(() => {
    if (!sortKey || sortDirection === 'neutral') {
      return products
    }

    const direction = sortDirection === 'asc' ? 1 : -1
    const next = [...products]

    next.sort((a, b) => {
      if (sortKey === 'name') {
        return a.name.localeCompare(b.name) * direction
      }

      if (sortKey === 'quantity') {
        return (a.quantity - b.quantity) * direction
      }

      return (a.price - b.price) * direction
    })

    return next
  }, [products, sortDirection, sortKey])

  const loadCurrentUser = async () => {
    try {
      const response = await authApi.me()
      if (!response.ok || response.status === 204) {
        setCurrentUser(null)
        return
      }

      const user = (await response.json()) as UserProfile
      setCurrentUser(user)
    } catch {
      setCurrentUser(null)
    }
  }

  const submitAuthForm = async (values: AuthFormValues) => {
    setIsSubmittingAuth(true)
    setAuthError('')

    try {
      const response = mode === 'signin' ? await authApi.login(values) : await authApi.signup(values)

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { message?: string } | null
        setAuthError(body?.message ?? 'Authentication failed.')
        return
      }

      const user = (await response.json()) as UserProfile
      setCurrentUser(user)
      closeAuthModal()
    } finally {
      setIsSubmittingAuth(false)
    }
  }

  const logout = async () => {
    await authApi.logout()
    setCurrentUser(null)
    setProducts([])
    setProductsError('')
    setIsAddModalOpen(false)
  }

  const closeAddProductModal = () => {
    setAddProductError('')
    setIsAddModalOpen(false)
  }

  const submitAddProduct = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const formData = new FormData(event.currentTarget)
    const name = String(formData.get('name') ?? '').trim()
    const quantityValue = Number(formData.get('quantity'))
    const priceValue = Number(formData.get('price'))

    setIsSubmittingProduct(true)
    setAddProductError('')

    try {
      const response = await productsApi.create({
        name,
        quantity: Number.isFinite(quantityValue) ? quantityValue : -1,
        price: Number.isFinite(priceValue) ? priceValue : -1
      })

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { message?: string } | null
        setAddProductError(body?.message ?? 'Could not create product.')
        return
      }

      closeAddProductModal()
      await loadProducts()
    } finally {
      setIsSubmittingProduct(false)
    }
  }

  const deleteProduct = async (product: Product) => {
    const confirmed = window.confirm(`Delete ${product.name}? This cannot be undone.`)
    if (!confirmed) {
      return
    }

    const response = await productsApi.remove(product.id)
    if (!response.ok) {
      setProductsError('Could not delete product.')
      return
    }

    await loadProducts()
  }

  useEffect(() => {
    void loadCurrentUser()
  }, [])

  useEffect(() => {
    if (!currentUser) {
      return
    }

    void loadProducts()
  }, [currentUser])

  return (
    <div className="page-shell">
      <Header
        currentMode={mode}
        isAuthenticated={Boolean(currentUser)}
        onOpenAuth={openAuthModal}
        onLogout={logout}
      />

      <main className="page-content">
        {!currentUser ? (
          <section className="hero hero--empty-state">
            <div className="hero__copy">
              <p className="eyebrow">NETCRUD Inventory</p>
              <h1>Sign in to access your products dashboard.</h1>
              <p>Create an account or sign in to manage products and inventory.</p>
              <div className="hero__actions">
                <button className="primary-button" type="button" onClick={() => openAuthModal('signin')}>
                  Sign In
                </button>
                <button className="secondary-button" type="button" onClick={() => openAuthModal('signup')}>
                  Create Account
                </button>
              </div>
            </div>
          </section>
        ) : (
          <section className="dashboard">
            <div className="dashboard__toolbar">
              <div>
                <p className="eyebrow">Welcome</p>
                <h2>
                  {currentUser.firstName} {currentUser.lastName}
                </h2>
                <p className="dashboard__subtitle">Manage products stored in your database.</p>
              </div>

              <div className="dashboard__actions">
                <button className="primary-button" type="button" onClick={() => setIsAddModalOpen(true)}>
                  Add Product
                </button>
              </div>
            </div>

            {productsError && <p className="inline-error">{productsError}</p>}

            <div className="table-wrap" role="region" aria-live="polite">
              <table className="products-table">
                <thead>
                  <tr>
                    <th>
                      <button
                        type="button"
                        className={sortKey === 'name' ? 'sort-header-button sort-header-button--active' : 'sort-header-button'}
                        onClick={() => onSortHeaderClick('name')}
                      >
                        Name <span className="sort-arrow">{getSortArrow('name')}</span>
                      </button>
                    </th>
                    <th>
                      <button
                        type="button"
                        className={sortKey === 'quantity' ? 'sort-header-button sort-header-button--active' : 'sort-header-button'}
                        onClick={() => onSortHeaderClick('quantity')}
                      >
                        Quantity <span className="sort-arrow">{getSortArrow('quantity')}</span>
                      </button>
                    </th>
                    <th>
                      <button
                        type="button"
                        className={sortKey === 'price' ? 'sort-header-button sort-header-button--active' : 'sort-header-button'}
                        onClick={() => onSortHeaderClick('price')}
                      >
                        Price <span className="sort-arrow">{getSortArrow('price')}</span>
                      </button>
                    </th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {products.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="products-table__empty">
                        {isLoadingProducts ? 'Loading products...' : 'No products found.'}
                      </td>
                    </tr>
                  ) : (
                    sortedProducts.map((product) => (
                      <tr key={product.id}>
                        <td>{product.name}</td>
                        <td>{product.quantity}</td>
                        <td>${product.price.toFixed(2)}</td>
                        <td>
                          <button
                            type="button"
                            className="icon-button icon-button--danger"
                            onClick={() => {
                              void deleteProduct(product)
                            }}
                            aria-label={`Delete ${product.name}`}
                            title="Delete product"
                          >
                            <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                              <path d="M9 3h6l1 2h4v2H4V5h4l1-2Zm-2 6h2v9H7V9Zm4 0h2v9h-2V9Zm4 0h2v9h-2V9ZM6 21h12l1-13H5l1 13Z" />
                            </svg>
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </section>
        )}
      </main>

      {isAuthModalOpen && (
        <div className="auth-modal" role="presentation" onClick={closeAuthModal}>
          <div className="auth-modal__dialog" role="dialog" aria-modal="true" onClick={(event) => event.stopPropagation()}>
            <AuthForm
              mode={mode}
              onModeChange={setMode}
              onClose={closeAuthModal}
              onSubmit={submitAuthForm}
              error={authError}
              isSubmitting={isSubmittingAuth}
            />
          </div>
        </div>
      )}

      {isAddModalOpen && (
        <div className="auth-modal" role="presentation" onClick={closeAddProductModal}>
          <div className="auth-modal__dialog" role="dialog" aria-modal="true" onClick={(event) => event.stopPropagation()}>
            <section className="auth-card">
              <button className="auth-card__close" type="button" onClick={closeAddProductModal} aria-label="Close add product modal">
                ×
              </button>

              <div className="auth-card__header">
                <p className="eyebrow">Create Product</p>
                <h2>Add a new item</h2>
                <p>Each product name must be unique.</p>
              </div>

              <form className="auth-form" onSubmit={submitAddProduct}>
                <label>
                  Name
                  <input type="text" name="name" placeholder="Product name" maxLength={120} required />
                </label>

                <label>
                  Quantity
                  <input type="number" name="quantity" min={0} step={1} defaultValue={0} required />
                </label>

                <label>
                  Price
                  <input type="number" name="price" min={0} step="0.01" defaultValue="0.00" required />
                </label>

                {addProductError && <p className="auth-form__error">{addProductError}</p>}

                <button className="primary-button" type="submit" disabled={isSubmittingProduct}>
                  {isSubmittingProduct ? 'Saving...' : 'Save Product'}
                </button>
              </form>
            </section>
          </div>
        </div>
      )}
    </div>
  )
}
