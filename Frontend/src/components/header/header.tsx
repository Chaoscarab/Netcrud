export type AuthMode = 'signin' | 'signup'

interface HeaderProps {
	currentMode: AuthMode
	isAuthenticated: boolean
	onOpenAuth: (mode: AuthMode) => void
	onLogout: () => void
}

export default function Header({ currentMode, isAuthenticated, onOpenAuth, onLogout }: HeaderProps) {
	return (
		<header className="site-header">
			<nav className="site-header__nav">
				<a href="/" className="brand">
					<span className="brand__name">NETCRUD</span>
				</a>

				<div className="site-header__actions">
					{isAuthenticated ? (
						<button className="header-button header-button--active" type="button" onClick={onLogout}>
							Log Out
						</button>
					) : (
						<>
							<button
								className={currentMode === 'signin' ? 'header-button header-button--active' : 'header-button'}
								type="button"
								onClick={() => onOpenAuth('signin')}
							>
								Sign In
							</button>
							<button
								className={currentMode === 'signup' ? 'header-button header-button--active' : 'header-button'}
								type="button"
								onClick={() => onOpenAuth('signup')}
							>
								Get Started
							</button>
						</>
					)}
				</div>
			</nav>
		</header>
	)
}
