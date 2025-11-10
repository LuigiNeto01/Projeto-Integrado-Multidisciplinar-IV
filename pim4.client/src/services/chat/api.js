import { apiFetch } from '@/services/api/http'

export function fetchMessages(chamadoId) {
  return apiFetch(`/chat/${chamadoId}/messages`, { method: 'GET' })
}

export function sendMessage(chamadoId, mensagem) {
  return apiFetch(`/chat/${chamadoId}/messages`, { method: 'POST', body: { mensagem } })
}

