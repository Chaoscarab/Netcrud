import type { AuthMode } from '../header/header'

export interface AuthFormValues {
  firstName: string
  lastName: string
  email: string
  password: string
  confirmPassword: string
}

interface AuthFormProps {
  mode: AuthMode
  onModeChange: (mode: AuthMode) => void
  onClose: () => void
  onSubmit: (values: AuthFormValues) => Promise<void>
  error: string
  isSubmitting: boolean
}

export default function AuthForm({ mode, onModeChange, onClose, onSubmit, error, isSubmitting }: AuthFormProps) {
  const isSignIn = mode === 'signin'

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const form = event.currentTarget
    const formData = new FormData(form)
    await onSubmit({
      firstName: String(formData.get('firstName') ?? '').trim(),
      lastName: String(formData.get('lastName') ?? '').trim(),
      email: String(formData.get('email') ?? '').trim(),
      password: String(formData.get('password') ?? ''),
      confirmPassword: String(formData.get('confirmPassword') ?? '')
    })
  }

  return (
    <section className="auth-card" id="auth">
      <button className="auth-card__close" type="button" onClick={onClose} aria-label="Close auth modal">
        ×
      </button>

      <div className="auth-card__header">
        <p className="eyebrow">{isSignIn ? 'Welcome back' : 'Create your account'}</p>
        <h2>{isSignIn ? 'Sign in to NETCRUD' : 'Get started with NETCRUD'}</h2>
        <p>
          {isSignIn
            ? 'Use your email and password to continue.'
            : 'Create an account to manage your projects and data.'}
        </p>
      </div>

      <form className="auth-form" onSubmit={handleSubmit}>
        {!isSignIn && (
          <div className="auth-grid">
            <label>
              First name
              <input type="text" name="firstName" placeholder="Jane" required />
            </label>
            <label>
              Last name
              <input type="text" name="lastName" placeholder="Doe" required />
            </label>
          </div>
        )}

        <label>
          Email
          <input type="email" name="email" placeholder="name@example.com" required />
        </label>

        <label>
          Password
          <input type="password" name="password" placeholder="••••••••" required />
        </label>

        {!isSignIn && (
          <label>
            Confirm password
            <input type="password" name="confirmPassword" placeholder="••••••••" required />
          </label>
        )}

        {error && <p className="auth-form__error">{error}</p>}

        <button className="primary-button" type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Please wait...' : isSignIn ? 'Sign In' : 'Create Account'}
        </button>

        <p className="auth-form__hint">
          {isSignIn ? 'No account yet?' : 'Already have an account?'}{' '}
          <button
            className="text-button"
            type="button"
            onClick={() => onModeChange(isSignIn ? 'signup' : 'signin')}
          >
            {isSignIn ? 'Get started' : 'Sign in'}
          </button>
        </p>
      </form>
    </section>
  )
}