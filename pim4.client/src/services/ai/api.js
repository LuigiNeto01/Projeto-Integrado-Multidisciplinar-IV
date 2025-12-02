// Serviço de IA (frontend)
// encapsulo a chamada ao endpoint que gera a mensagem de confirmação
// para abertura de chamado. O backend decide se usa Gemini ou fallback.
import { apiFetch } from '@/services/api/http'

export function opiniaoChamado(payload) {
  return apiFetch('/ai/chamado/opiniao', { method: 'POST', body: payload, auth: false })
}
