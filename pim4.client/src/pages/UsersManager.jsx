// UsersManager
// tela de administração de usuários (somente para admin).
// - Carrega a lista do backend, permite criar/editar/excluir.
// - Usa o UserModal para o formulário de criação/edição.
import { useEffect, useState } from 'react'
import { createUser, deleteUser, listUsers, updateUser } from '@/services/users/api'
import UserModal from '@/components/UserModal'

export default function UsersManager() {
  // Estado de lista/UX
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editItem, setEditItem] = useState(null)

  // Busca a lista no backend
  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const data = await listUsers()
      setItems(Array.isArray(data) ? data : [])
    } catch (err) {
      setError(err?.message || 'Falha ao carregar usuários')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  // Cria novo usuário e recarrega lista
  const onCreate = async (payload) => {
    try {
      await createUser(payload)
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'success', message: 'Usuário criado!' } }))
      setModalOpen(false)
      await load()
    } catch (err) {
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'error', message: err?.message || 'Falha ao criar' } }))
    }
  }

  // Atualiza usuário selecionado e recarrega lista
  const onUpdate = async (id, payload) => {
    try {
      await updateUser(id, payload)
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'success', message: 'Usuário atualizado!' } }))
      setEditItem(null)
      await load()
    } catch (err) {
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'error', message: err?.message || 'Falha ao atualizar' } }))
    }
  }

  // Exclui usuário (com confirmação) e recarrega lista
  const onDelete = async (id) => {
    if (!confirm('Excluir este usuário?')) return
    try {
      await deleteUser(id)
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'success', message: 'Usuário excluído!' } }))
      await load()
    } catch (err) {
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'error', message: err?.message || 'Falha ao excluir' } }))
    }
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-neutral-900">Usuários</h2>
        <button onClick={() => setModalOpen(true)} className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white shadow-sm hover:opacity-90">Criar usuário</button>
      </div>

      {loading ? (
        <div className="text-sm text-neutral-500">Carregando usuários...</div>
      ) : error ? (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>
      ) : items.length === 0 ? (
        <div className="text-sm text-neutral-500">Nenhum usuário encontrado.</div>
      ) : (
        <div className="divide-y divide-neutral-200 rounded-2xl border border-neutral-200 bg-white">
          {items.map((u) => (
            <div key={u.id} className="flex items-center justify-between gap-3 p-4">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-neutral-900">{u.nome} <span className="text-neutral-400">#{u.id}</span></p>
                <p className="truncate text-xs text-neutral-500">{u.email} • {u.cargo}{u.nivel != null ? ` (nivel ${u.nivel})` : ''}</p>
              </div>
              <div className="flex gap-2">
                <button onClick={() => setEditItem(u)} className="rounded-full border border-neutral-200 px-3 py-1.5 text-sm hover:bg-neutral-50">Editar</button>
                <button onClick={() => onDelete(u.id)} className="rounded-full border border-red-200 px-3 py-1.5 text-sm text-red-700 hover:bg-red-50">Excluir</button>
              </div>
            </div>
          ))}
        </div>
      )}

      <UserModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onSubmit={onCreate}
      />

      <UserModal
        open={!!editItem}
        onClose={() => setEditItem(null)}
        initial={editItem}
        onSubmit={(data) => onUpdate(editItem.id, data)}
      />
    </div>
  )
}
