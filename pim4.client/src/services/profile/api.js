// profile/api — chamada para atualizar perfil do usuário logado
import { apiFetch } from '@/services/api/http'

export function updateMe(payload) {
  return apiFetch('/users/me', { method: 'PUT', body: payload })
}
