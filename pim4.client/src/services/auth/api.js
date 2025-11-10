// auth/api — endpoints de autenticação (login/registro)
import { apiFetch, setToken } from '@/services/api/http'

// Login: salva token ao sucesso
export async function loginApi(email, password) {
  const res = await apiFetch('/auth/login', {
    method: 'POST',
    body: { email, password },
    auth: false,
  })
  if (res?.token) setToken(res.token)
  return res
}

// Registro: cria usuário com cargo 'usuario' e faz auto-login
export async function registerApi({ cpf, nome, email, password }) {
  const res = await apiFetch('/auth/register', {
    method: 'POST',
    body: { cpf, nome, email, password },
    auth: false,
  })
  if (res?.token) setToken(res.token)
  return res
}
