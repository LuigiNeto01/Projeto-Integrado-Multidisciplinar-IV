// ChamadosGrid
// lista os chamados do usuário (ou todos, se admin) com:
// - Cards de indicadores (abertos, críticos, fechados)
// - Filtros (quem abriu, prioridade, busca)
// - Grade de abertos e, abaixo, divisória com fechados
// - Botão "Resolver/Ver conversa" que abre o ChatModal
import { useCallback, useEffect, useMemo, useState } from 'react'
import { listarChamados, listarChamadosPorUsuario } from '@/services/chamados/api'
import { HiOutlineExclamationTriangle, HiOutlineCheckCircle } from 'react-icons/hi2'
import ChatModal from '@/components/ChatModal'
import { useAuth } from '@/services/auth/AuthContext'

// Converte nível numérico em label/cores de prioridade
function prioridadeInfo(n) {
  switch (Number(n)) {
    case 1:
      return { label: 'Critica', color: 'bg-red-100 text-red-800 border-red-200' }
    case 2:
      return { label: 'Alta', color: 'bg-orange-100 text-orange-800 border-orange-200' }
    case 3:
      return { label: 'Media', color: 'bg-yellow-100 text-yellow-800 border-yellow-200' }
    case 4:
      return { label: 'Baixa', color: 'bg-green-100 text-green-800 border-green-200' }
    default:
      return { label: 'Desconhecida', color: 'bg-neutral-100 text-neutral-700 border-neutral-200' }
  }
}

export default function ChamadosGrid({ refreshKey = 0 }) {
  // Estado principal: itens, carregamento, erro
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const { user } = useAuth()
  // Filtros e busca
  const [creatorFilter, setCreatorFilter] = useState('all')
  const [priorityFilter, setPriorityFilter] = useState('all')
  const [search, setSearch] = useState('')
  // Chat modal
  const [chatOpen, setChatOpen] = useState(false)
  const [chatChamado, setChatChamado] = useState(null)

  // Carrego a lista considerando a role (usuário vê os seus, admin/suporte vêem mais)
  const loadData = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const role = String(user?.cargo || '').toLowerCase()
      const uid = user?.id
      const data = role === 'usuario' && uid
        ? await listarChamadosPorUsuario(uid)
        : await listarChamados()
      setItems(Array.isArray(data) ? data : [])
    } catch (err) {
      setError(err?.message || 'Falha ao carregar chamados')
    } finally {
      setLoading(false)
    }
  }, [user])

  // Dispara carregamento inicial e quando o pai pede refresh
  useEffect(() => {
    loadData()
  }, [refreshKey, loadData])

  // Ouvinte global para recarregar quando algum evento do app pedir
  useEffect(() => {
    const handler = () => loadData()
    window.addEventListener('chamados:refresh', handler)
    return () => window.removeEventListener('chamados:refresh', handler)
  }, [loadData])

  const isUsuario = String(user?.cargo || '').toLowerCase() === 'usuario'
  const isAdmin = String(user?.cargo || '').toLowerCase() === 'admin'

  // Opções de "Quem abriu" a partir dos itens carregados
  // Opções do filtro "Quem abriu" geradas dinamicamente a partir dos itens
  const creatorOptions = useMemo(() => {
    const map = new Map()
    items.forEach((c) => {
      const id = c.usuarioCriadorId ?? null
      const nome = c.nomeCriador || (id == null ? null : `#${id}`)
      if (id != null || nome) {
        const key = id != null ? `id:${id}` : `name:${nome}`
        if (!map.has(key)) map.set(key, { key, id, nome })
      }
    })
    return [{ key: 'all', id: null, nome: 'Todos' }, ...Array.from(map.values())]
  }, [items])

  // Filtragem local
  // Aplico filtros locais (criador, prioridade e busca textual)
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    const pr = priorityFilter === 'all' ? null : Number(priorityFilter)
    return items.filter((c) => {
      // por criador
      if (creatorFilter !== 'all') {
        if (creatorFilter.startsWith('id:')) {
          const idSel = Number(creatorFilter.slice(3))
          if (Number(c.usuarioCriadorId) !== idSel) return false
        } else if (creatorFilter.startsWith('name:')) {
          const nm = creatorFilter.slice(5)
          if ((c.nomeCriador || '').toLowerCase() !== nm.toLowerCase()) return false
        }
      }
      // por prioridade
      if (pr != null && Number(c.prioridade) !== pr) return false
      // por busca
      if (q) {
        const hay = `${c.titulo || ''} ${c.motivo || ''} ${c.descricao || ''}`.toLowerCase()
        if (!hay.includes(q)) return false
      }
      return true
    })
  }, [items, creatorFilter, priorityFilter, search])

  // Separo abertos/fechados e computo contadores dos cards
  const { abertos, fechados, totalAbertos, totalFechados, totalCriticos } = useMemo(() => {
    const open = filtered.filter((c) => !c.resolvido)
    const closed = filtered.filter((c) => !!c.resolvido)
    const crit = filtered.filter((c) => !c.resolvido && Number(c.prioridade) === 1)
    return {
      abertos: open,
      fechados: closed,
      totalAbertos: open.length,
      totalFechados: closed.length,
      totalCriticos: crit.length,
    }
  }, [filtered])

  if (loading) return (
    <div className="flex items-center gap-2 text-sm text-neutral-500">
      <svg className="h-4 w-4 animate-spin text-neutral-500" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none"></circle>
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
      </svg>
      Carregando chamados...
    </div>
  )
  if (error) return <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>
  if (!items.length) return <div className="text-sm text-neutral-500">Nenhum chamado encontrado.</div>

  // Abre/fecha o modal de chat do chamado selecionado
  const openChat = (c) => { setChatChamado(c); setChatOpen(true) }
  const closeChat = () => { setChatOpen(false); setChatChamado(null) }

  return (
    <div className="space-y-6">
      {/* Cards de indicadores */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
          <p className="text-xs font-medium text-neutral-500">Abertos</p>
          <p className="mt-1 text-2xl font-semibold text-neutral-900">{totalAbertos}</p>
          <p className="mt-1 text-xs text-neutral-500">{isAdmin ? 'Todos os chamados' : 'Chamados visíveis para você'}</p>
        </div>
        <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
          <p className="text-xs font-medium text-neutral-500">Críticos (abertos)</p>
          <p className="mt-1 text-2xl font-semibold text-red-600">{totalCriticos}</p>
          <p className="mt-1 text-xs text-neutral-500">Prioridade 1</p>
        </div>
        <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
          <p className="text-xs font-medium text-neutral-500">Fechados</p>
          <p className="mt-1 text-2xl font-semibold text-neutral-900">{totalFechados}</p>
          <p className="mt-1 text-xs text-neutral-500">{isAdmin ? 'Todos os chamados' : 'Chamados visíveis para você'}</p>
        </div>
      </div>

      {/* Filtros */}
      <div className="flex flex-col gap-3 rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm sm:flex-row sm:items-end">
        <div className="sm:w-1/3">
          <label className="mb-1 block text-sm font-medium text-neutral-700">Quem abriu</label>
          <select
            className="w-full rounded-xl border border-neutral-200 px-3 py-2"
            value={creatorFilter}
            onChange={(e)=>setCreatorFilter(e.target.value)}
          >
            {creatorOptions.map((o) => (
              <option key={o.key} value={o.key}>{o.nome}</option>
            ))}
          </select>
        </div>
        <div className="sm:w-1/3">
          <label className="mb-1 block text-sm font-medium text-neutral-700">Nível de prioridade</label>
          <select
            className="w-full rounded-xl border border-neutral-200 px-3 py-2"
            value={priorityFilter}
            onChange={(e)=>setPriorityFilter(e.target.value)}
          >
            <option value="all">Todos</option>
            <option value="1">Crítica</option>
            <option value="2">Alta</option>
            <option value="3">Média</option>
            <option value="4">Baixa</option>
          </select>
        </div>
        <div className="sm:w-1/3">
          <label className="mb-1 block text-sm font-medium text-neutral-700">Busca</label>
          <input
            className="w-full rounded-xl border border-neutral-200 px-3 py-2"
            placeholder="título, motivo ou descrição"
            value={search}
            onChange={(e)=>setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* Grid de chamados abertos */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {abertos.map((c) => {
        const p = prioridadeInfo(c.prioridade)
        const statusAberto = !c.resolvido
        return (
          <div key={c.id} className="flex h-full flex-col rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
            <div className="flex items-start justify-between">
              <div>
                <h3 className="text-base font-semibold text-neutral-900">
                  {c.titulo || 'Sem titulo'} <span className="text-neutral-400">#{c.id}</span>
                </h3>
                <p className="mt-1 text-sm text-neutral-600">Motivo: {c.motivo || '-'}</p>
              </div>
              <div className="text-right">
                <div className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${p.color}`}>
                  Prioridade: {p.label}
                </div>
                <div className="mt-2 text-xs font-medium text-neutral-600">
                  {statusAberto ? (
                    <span className="inline-flex items-center gap-1 text-amber-700"><HiOutlineExclamationTriangle className="h-4 w-4"/> Aberto</span>
                  ) : (
                    <span className="inline-flex items-center gap-1 text-green-700"><HiOutlineCheckCircle className="h-4 w-4"/> Fechado</span>
                  )}
                </div>
              </div>
            </div>

            {c.descricao ? (
              <p className="mt-3 line-clamp-3 text-sm text-neutral-700">Descricao: {c.descricao}</p>
            ) : null}

            {isUsuario ? null : (
              <p className="mt-3 text-xs text-neutral-500">Criado por: {c.nomeCriador || '-'}</p>
            )}

            <div className="mt-auto pt-4 flex justify-end">
              <button
                type="button"
                className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:opacity-90"
                onClick={() => openChat(c)}
              >
                {isUsuario ? 'Visualizar' : 'Resolver'}
              </button>
            </div>
          </div>
        )
      })}
      </div>

      {/* Divisória e chamados fechados */}
      <div className="pt-2">
        <div className="flex items-center gap-3">
          <div className="h-px flex-1 bg-neutral-200" />
          <span className="text-xs font-medium uppercase tracking-wide text-neutral-500">Chamados fechados</span>
          <div className="h-px flex-1 bg-neutral-200" />
        </div>
        {fechados.length === 0 ? (
          <p className="mt-3 text-sm text-neutral-500">Nenhum chamado fechado.</p>
        ) : (
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {fechados.map((c) => {
              const p = prioridadeInfo(c.prioridade)
              return (
                <div key={c.id} className="flex h-full flex-col rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm opacity-90">
                  <div className="flex items-start justify-between">
                    <div>
                      <h3 className="text-base font-semibold text-neutral-900">
                        {c.titulo || 'Sem titulo'} <span className="text-neutral-400">#{c.id}</span>
                      </h3>
                      <p className="mt-1 text-sm text-neutral-600">Motivo: {c.motivo || '-'}</p>
                    </div>
                    <div className="text-right">
                      <div className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${p.color}`}>
                        Prioridade: {p.label}
                      </div>
                      <div className="mt-2 text-xs font-medium text-green-700">
                        <span className="inline-flex items-center gap-1"><HiOutlineCheckCircle className="h-4 w-4"/> Fechado</span>
                      </div>
                    </div>
                  </div>
                  {c.descricao ? (
                    <p className="mt-3 line-clamp-3 text-sm text-neutral-700">Descricao: {c.descricao}</p>
                  ) : null}
                  {isUsuario ? null : (
                    <p className="mt-3 text-xs text-neutral-500">Criado por: {c.nomeCriador || '-'}</p>
                  )}
                  <div className="mt-auto pt-4 flex justify-end">
                    <button
                      type="button"
                      className="rounded-full border border-neutral-200 px-4 py-2 text-sm text-neutral-700 hover:bg-neutral-50"
                      onClick={() => openChat(c)}
                    >
                      Ver conversa
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>
      <ChatModal
        open={chatOpen}
        onClose={closeChat}
        chamadoId={chatChamado?.id}
        resolvido={!!chatChamado?.resolvido}
        titulo={chatChamado?.titulo}
        onStatusChange={(closed)=>{ setChatChamado((c)=> c ? { ...c, resolvido: !!closed } : c) }}
      />
    </div>
  )
}
