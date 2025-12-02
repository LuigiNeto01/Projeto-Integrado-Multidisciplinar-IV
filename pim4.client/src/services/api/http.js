// http.js — camada fina para requisições REST da SPA
// centraliza a montagem de headers, baseURL e token (JWT)
const BASE_URL = import.meta.env.VITE_API_BASE_URL || ''
const TOKEN_KEY = 'auth_token'

export function getToken() {
  return localStorage.getItem(TOKEN_KEY) || ''
}

export function setToken(token) {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

// apiFetch: wrapper leve de fetch com JSON e Authorization opcional
export async function apiFetch(path, { method = 'GET', headers = {}, body, auth = true } = {}) {
  const url = `${BASE_URL}${path}`
  const finalHeaders = { 'Content-Type': 'application/json', ...headers }
  if (auth) {
    const token = getToken()
    if (token) finalHeaders['Authorization'] = `Bearer ${token}`
  }

  // Disparo da requisição
  const res = await fetch(url, {
    method,
    headers: finalHeaders,
    body: body ? (typeof body === 'string' ? body : JSON.stringify(body)) : undefined,
    credentials: 'include',
  })

  // Tento decodificar JSON; se falhar, devolvo texto cru
  const text = await res.text()
  let data
  try { data = text ? JSON.parse(text) : null } catch { data = text }
  if (!res.ok) {
    const message = data?.message || res.statusText || 'Erro de requisição'
    const err = new Error(message)
    err.status = res.status
    err.data = data
    throw err
  }
  return data
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
}
