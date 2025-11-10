// App.jsx — define rotas e guarda global (RequireAuth)
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import LoginDesktop from '@/screens/LoginDesktop'
import LoginMobile from '@/screens/LoginMobile'
import Home from '@/pages/Home'
import { useAuth } from '@/services/auth/AuthContext'
import Toast from '@/components/Toast'

// RequireAuth: protege rotas privadas usando o AuthContext
function RequireAuth({ children }) {
  const { isAuthenticated, ready } = useAuth()
  if (!ready) return null
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return children
}

// LoginScreen: decide entre versão Desktop/Mobile e redireciona se já logado
function LoginScreen() {
  const { isAuthenticated, ready } = useAuth()
  if (!ready) return null
  if (isAuthenticated) return <Navigate to="/" replace />
  return (
    <div className="min-h-screen w-full">
      <div className="hidden md:block">
        <LoginDesktop />
      </div>
      <div className="block md:hidden">
        <LoginMobile />
      </div>
    </div>
  )
}

// App: ponto de entrada de rotas + Toast global
export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginScreen />} />
        <Route
          path="/"
          element={(
            <RequireAuth>
              <Home />
            </RequireAuth>
          )}
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
      <Toast />
    </BrowserRouter>
  )
}
