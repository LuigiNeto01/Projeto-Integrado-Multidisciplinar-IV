// LoginForm
// formulário de autenticação com botão para abrir o modal de registro.
// - login(email, password) vem do AuthContext e salva token/usuário.
// - registro chama /auth/register e, com sucesso, navega para a Home.
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { FaArrowRight, FaEye, FaEyeSlash } from 'react-icons/fa'
import { useAuth } from '@/services/auth/AuthContext'
import RegisterModal from '@/components/RegisterModal'
import { registerApi } from '@/services/auth/api'

export default function LoginForm({ withCard = true, showHeader = true, className = '' }) {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [registerOpen, setRegisterOpen] = useState(false)

  // Envia credenciais para a API e navega para a Home ao sucesso
  const handleSubmit = async (event) => {
    event.preventDefault()
    if (loading) return

    setError('')
    setLoading(true)

    try {
      await login(email, password)
      navigate('/')
    } catch (err) {
      const message = err?.message || 'Nao foi possivel entrar.'
      setError(message)
      setLoading(false)
    }
  }

  const content = (
    <>
      {showHeader ? (
        <div className="mb-6 text-center">
          <h1 className="text-2xl font-semibold text-gray-900">Bem-vindo!</h1>
          <p className="mt-1 text-sm text-gray-500">Entre com suas credenciais</p>
        </div>
      ) : null}

      {error ? (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{error}</div>
      ) : null}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="email" className="sr-only">
            E-mail
          </label>
          <input
            id="email"
            type="email"
            placeholder="E-mail"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-gray-900 placeholder-gray-400 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            required
            autoComplete="email"
            disabled={loading}
          />
        </div>

        <div className="relative">
          <label htmlFor="password" className="sr-only">
            Senha
          </label>
          <input
            id="password"
            type={showPassword ? 'text' : 'password'}
            placeholder="Senha"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 pr-12 text-gray-900 placeholder-gray-400 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            required
            autoComplete="current-password"
            disabled={loading}
          />
          <button
            type="button"
            aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}
            onClick={() => setShowPassword((value) => !value)}
            className="absolute inset-y-0 right-3 my-auto inline-flex items-center justify-center rounded-lg p-2 text-gray-500 transition hover:bg-gray-100 hover:text-gray-700"
            disabled={loading}
          >
            {showPassword ? <FaEyeSlash size={16} /> : <FaEye size={16} />}
          </button>
        </div>

        <button
          type="submit"
          disabled={loading}
          className="group inline-flex w-full items-center justify-center gap-2 rounded-full bg-black px-5 py-3 font-medium text-white shadow-lg shadow-black/20 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-xl hover:shadow-black/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-black disabled:cursor-not-allowed disabled:opacity-60 motion-reduce:transition-none"
        >
          {loading ? 'Entrando...' : 'Entrar'}
          {!loading ? <FaArrowRight className="transition-transform duration-200 group-hover:translate-x-1" /> : null}
        </button>
        <div className="text-center">
          <button type="button" onClick={() => setRegisterOpen(true)} className="mt-2 text-sm font-medium text-indigo-700 hover:underline">Registrar</button>
        </div>
      </form>
      <RegisterModal
        open={registerOpen}
        onClose={() => setRegisterOpen(false)}
        onSubmit={async (payload) => {
          try {
            setError('')
            setLoading(true)
            const res = await registerApi(payload)
            if (res?.token) {
              // sessão já foi setada por registerApi; navega para home
              navigate('/')
            }
          } catch (err) {
            const message = err?.message || 'Não foi possível registrar.'
            setError(message)
          } finally {
            setLoading(false)
            setRegisterOpen(false)
          }
        }}
      />
    </>
  )

  if (withCard) {
    return (
      <div className={`w-full max-w-md rounded-2xl bg-white p-6 shadow-xl sm:p-8 ${className}`}>
        {content}
      </div>
    )
  }

  return <div className={`w-full max-w-md ${className}`}>{content}</div>
}
