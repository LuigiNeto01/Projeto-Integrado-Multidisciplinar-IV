// Serviço de usuários (frontend)
// aqui concentro as chamadas para o backend relacionadas a usuários.
import { apiFetch } from '@/services/api/http'

export function listUsers() {
  return apiFetch('/users', { method: 'GET' })
}

export function createUser(payload) {
  return apiFetch('/users', { method: 'POST', body: payload })
}

export function updateUser(id, payload) {
  return apiFetch(`/users/${id}`, { method: 'PUT', body: payload })
}

export function deleteUser(id) {
  return apiFetch(`/users/${id}`, { method: 'DELETE' })
}
