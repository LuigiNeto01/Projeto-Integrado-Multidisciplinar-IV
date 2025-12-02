// AuthContext
// provê sessão (usuário/token) e helpers (login, logout, updateUser, renewSession)
// para toda a aplicação React.
import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import { clearToken, getToken, setToken } from '@/services/api/http'
import { loginApi } from '@/services/auth/api'

const AuthContext = createContext(null)

const EXP_KEY = 'auth_exp'
const USER_KEY = 'auth_user'

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [ready, setReady] = useState(false)

  const setSession = (token, userObj, expiresAtIso) => {
    setToken(token)
    if (userObj) localStorage.setItem(USER_KEY, JSON.stringify(userObj))
    const expMs = expiresAtIso ? Date.parse(expiresAtIso) : Date.now() + 60 * 60 * 1000
    localStorage.setItem(EXP_KEY, String(expMs))
  }

  const clearSession = () => {
    clearToken()
    localStorage.removeItem(USER_KEY)
    localStorage.removeItem(EXP_KEY)
  }

  const login = async (email, password) => {
    const debug = (import.meta.env.VITE_DEBUG_LOGIN || 'false').toString().toLowerCase() === 'true'
    if (debug) {
      const dbgEmail = import.meta.env.VITE_DEBUG_EMAIL || 'admin@demo.com'
      const dbgPass = import.meta.env.VITE_DEBUG_PASSWORD || 'admin'
      if (!email || !password) throw new Error('Informe e-mail e senha.')
      if (email !== dbgEmail || password !== dbgPass) throw new Error('Credenciais inválidas.')
      const token = 'debug-token'
      const fakeUser = { email: dbgEmail, name: 'Admin (Debug)' }
      setSession(token, fakeUser)
      setUser(fakeUser)
      return
    }

    const res = await loginApi(email, password)
    if (res?.token) setSession(res.token, res?.user || { email }, res?.expiresAt)
    setUser(res?.user || { email })
  }

  const logout = () => { setUser(null); clearSession() }

  useEffect(() => {
    const token = getToken()
    const exp = Number(localStorage.getItem(EXP_KEY) || 0)
    if (token && exp && Date.now() < exp) {
      const saved = localStorage.getItem(USER_KEY)
      if (saved) {
        try { setUser(JSON.parse(saved)) } catch { setUser({ email: 'sessao' }) }
      } else {
        setUser({ email: 'sessao' })
      }
    } else {
      clearSession()
      setUser(null)
    }
    setReady(true)
  }, [])

  const isAuthenticated = !!user && !!getToken() && Date.now() < Number(localStorage.getItem(EXP_KEY) || 0)

  const updateUser = (patch) => {
    const next = { ...(user || {}), ...(patch || {}) }
    setUser(next)
    try { localStorage.setItem(USER_KEY, JSON.stringify(next)) } catch {}
  }

  const renewSession = (token, userObj, expiresAtIso) => {
    setSession(token, userObj, expiresAtIso)
    if (userObj) setUser(userObj)
  }

  const value = useMemo(() => ({ user, login, logout, ready, isAuthenticated, updateUser, renewSession }), [user, ready, isAuthenticated])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth deve ser usado dentro de AuthProvider')
  return ctx
}
