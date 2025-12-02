// Serviço de chamados (frontend)
// centralizo as requisições da SPA relacionadas a chamados.
import { apiFetch } from '@/services/api/http'

export async function listarChamados() {
  return apiFetch('/chamados', { method: 'GET' })
}

export async function listarChamadosPorUsuario(userId) {
  return apiFetch('/chamados/by-user', {
    method: 'POST',
    body: { userId },
  })
}

export async function criarChamado(payload) {
  return apiFetch('/chamados', { method: 'POST', body: payload })
}

export function fecharChamado(id) {
  return apiFetch(`/chamados/${id}/close`, { method: 'PUT' })
}

export function reabrirChamado(id) {
  return apiFetch(`/chamados/${id}/reopen`, { method: 'PUT' })
}
