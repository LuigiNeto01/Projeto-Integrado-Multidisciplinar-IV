// ChatModal
// este modal mostra a conversa do chamado selecionado.
// - Busca mensagens a cada 3s (polling simples).
// - Permite enviar quando o chamado está aberto.
// - Para suporte/admin, permite fechar/reabrir o chamado.
import { useEffect, useMemo, useRef, useState } from 'react'
import { HiOutlineXMark, HiPaperAirplane } from 'react-icons/hi2'
import { fetchMessages, sendMessage } from '@/services/chat/api'
import { fecharChamado, reabrirChamado } from '@/services/chamados/api'
import { useAuth } from '@/services/auth/AuthContext'

export default function ChatModal({ open, onClose, chamadoId, resolvido = false, titulo, onStatusChange }) {
  const { user } = useAuth()
  // Estado local de mensagens, carregamento e input
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [text, setText] = useState('')
  const listRef = useRef(null)
  const pollRef = useRef(null)
  const [closed, setClosed] = useState(!!resolvido)

  const canSend = !!text.trim() && !closed

  // Carrega mensagens do chamado
  const load = async () => {
    if (!open || !chamadoId) return
    try {
      setError('')
      const data = await fetchMessages(chamadoId)
      setItems(Array.isArray(data) ? data : [])
    } catch (err) {
      setError(err?.message || 'Falha ao carregar mensagens')
    } finally {
      setLoading(false)
    }
  }

  // Inicia/para o polling quando abre/fecha o modal
  useEffect(() => {
    if (!open) return
    setLoading(true)
    load()
    pollRef.current = setInterval(load, 3000)
    return () => { if (pollRef.current) clearInterval(pollRef.current) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, chamadoId])

  // Mantém a rolagem no final quando chegam mensagens novas
  useEffect(() => {
    // scroll to bottom
    if (listRef.current) listRef.current.scrollTop = listRef.current.scrollHeight
  }, [items])

  // Envia mensagem e recarrega a conversa
  const handleSend = async () => {
    const msg = text.trim()
    if (!msg) return
    try {
      await sendMessage(chamadoId, msg)
      setText('')
      await load()
      window.dispatchEvent(new CustomEvent('chamados:refresh'))
    } catch (err) {
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'error', message: err?.message || 'Falha ao enviar mensagem' } }))
    }
  }

  const header = useMemo(() => `Chamado #${chamadoId}${titulo ? ` · ${titulo}` : ''} — ${closed ? 'Fechado' : 'Aberto'}`, [chamadoId, titulo, closed])

  const isUsuario = String(user?.cargo || '').toLowerCase() === 'usuario'

  // Alterna o status do chamado (fecha/reabre). Apenas não-usuário.
  const toggleStatus = async () => {
    try {
      if (closed) await reabrirChamado(chamadoId)
      else await fecharChamado(chamadoId)
      const next = !closed
      setClosed(next)
      if (typeof onStatusChange === 'function') onStatusChange(next)
      window.dispatchEvent(new CustomEvent('chamados:refresh'))
    } catch (err) {
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'error', message: err?.message || 'Falha ao alterar status' } }))
    }
  }

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="flex w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-neutral-200 px-5 py-3">
          <h2 className="truncate text-base font-semibold text-neutral-900">{header}</h2>
          <div className="flex items-center gap-2">
            {!isUsuario && (
              <button
                type="button"
                onClick={toggleStatus}
                className={["rounded-full px-3 py-1.5 text-sm font-medium", closed ? "border border-green-200 text-green-700 hover:bg-green-50" : "border border-red-200 text-red-700 hover:bg-red-50"].join(' ')}
              >
                {closed ? 'Reabrir Chamado' : 'Fechar Chamado'}
              </button>
            )}
            <button onClick={onClose} className="rounded-lg p-2 text-neutral-500 hover:bg-neutral-100"><HiOutlineXMark className="h-5 w-5"/></button>
          </div>
        </div>

        <div ref={listRef} className="max-h-[60vh] min-h-[40vh] overflow-y-auto bg-neutral-50 px-4 py-3">
          {loading ? (
            <div className="text-sm text-neutral-500">Carregando conversas...</div>
          ) : error ? (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>
          ) : items.length === 0 ? (
            <div className="text-sm text-neutral-500">Nenhuma mensagem ainda.</div>
          ) : (
            <div className="space-y-2">
              {items.map((m) => {
                const self = Number(m.idUsuario) === Number(user?.id)
                return (
                  <div key={m.id} className={["flex", self ? "justify-end" : "justify-start"].join(' ')}>
                    <div className={["max-w-[75%] rounded-2xl border px-3 py-2 text-sm shadow-sm", self ? "bg-indigo-50 border-indigo-100" : "bg-white border-neutral-200"].join(' ')}>
                      <div className="text-[11px] font-medium text-neutral-500">{m.nome || (self ? 'Você' : 'Usuário')} · {new Date(m.dataEnvio).toLocaleString()}</div>
                      <div className="mt-1 whitespace-pre-wrap text-neutral-900">{m.mensagem}</div>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>

        <div className="border-t border-neutral-200 bg-white px-4 py-3">
          <div className="flex items-center gap-2">
            <input
              className="min-w-0 flex-1 rounded-xl border border-neutral-200 px-3 py-2"
              placeholder={closed ? 'Chamado fechado' : 'Escreva uma mensagem'}
              value={text}
              onChange={(e)=>setText(e.target.value)}
              onKeyDown={(e)=>{ if (e.key === 'Enter' && (e.ctrlKey || e.shiftKey)) return; if (e.key === 'Enter') { e.preventDefault(); handleSend() } }}
              disabled={closed}
            />
            <button
              type="button"
              onClick={handleSend}
              disabled={!canSend}
              className={["inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium shadow-sm", canSend ? "bg-black text-white hover:opacity-90" : "bg-neutral-200 text-neutral-500"].join(' ')}
            >
              <HiPaperAirplane className="h-4 w-4"/> Enviar
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
