// Dashboard — visão geral com KPIs, gráfico simples, alertas e export CSV
import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAuth } from '@/services/auth/AuthContext'
import { listarChamados, listarChamadosPorUsuario } from '@/services/chamados/api'
import { listUsers } from '@/services/users/api'

// Cartão de indicador simples (título, valor e dica)
function StatCard({ title, value, hint, color }) {
  return (
    <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
      <p className="text-xs font-medium text-neutral-500">{title}</p>
      <p className={[
        'mt-1 text-2xl font-semibold',
        color || 'text-neutral-900',
      ].join(' ')}>{value}</p>
      {hint ? <p className="mt-1 text-xs text-neutral-500">{hint}</p> : null}
    </div>
  )
}

// Barrinha CSS para ilustrar proporção (sem libs)
function Bar({ label, count, max }) {
  const pct = max > 0 ? Math.round((count / max) * 100) : 0
  return (
    <div>
      <div className="flex items-center justify-between text-xs text-neutral-600">
        <span>{label}</span>
        <span>{count}</span>
      </div>
      <div className="mt-1 h-2 w-full rounded-full bg-neutral-100">
        <div className="h-2 rounded-full bg-indigo-500" style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}

export default function Dashboard() {
  const { user } = useAuth()
  const [items, setItems] = useState([])
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const role = String(user?.cargo || '').toLowerCase()
  const isAdmin = role === 'admin'
  const isUsuario = role === 'usuario'

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const data = isUsuario && user?.id
        ? await listarChamadosPorUsuario(user.id)
        : await listarChamados()
      setItems(Array.isArray(data) ? data : [])

      if (isAdmin) {
        try {
          const us = await listUsers()
          setUsers(Array.isArray(us) ? us : [])
        } catch { setUsers([]) }
      } else {
        setUsers([])
      }
    } catch (err) {
      setError(err?.message || 'Falha ao carregar dashboard')
    } finally {
      setLoading(false)
    }
  }, [isUsuario, isAdmin, user])

  useEffect(() => { load() }, [load])

  const metrics = useMemo(() => {
    const open = items.filter(i => !i.resolvido)
    const closed = items.filter(i => !!i.resolvido)
    const crit = open.filter(i => Number(i.prioridade) === 1)
    const byPriority = [1,2,3,4].map(p => ({ p, count: items.filter(i => Number(i.prioridade) === p).length }))
    const maxBar = Math.max(1, ...byPriority.map(b => b.count))
    const stale = open.filter(i => {
      const d = i.dataCriacao ? new Date(i.dataCriacao) : null
      if (!d) return false
      const diff = (Date.now() - d.getTime()) / (1000*60*60*24)
      return diff >= 7
    })
    const last5 = [...items].sort((a,b)=>{
      const da = a.dataCriacao ? new Date(a.dataCriacao).getTime() : 0
      const db = b.dataCriacao ? new Date(b.dataCriacao).getTime() : 0
      return db - da
    }).slice(0,5)
    const usersTotal = users.length
    const admins = users.filter(u => String(u.cargo||'').toLowerCase()==='admin').length
    const suporte = users.filter(u => String(u.cargo||'').toLowerCase()==='suporte').length
    return { open, closed, crit, byPriority, maxBar, stale, last5, usersTotal, admins, suporte }
  }, [items, users])

  if (loading) return <div className="text-sm text-neutral-500">Carregando dashboard...</div>
  if (error) return <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>

  const exportCsv = () => {
    const rows = [['id','titulo','motivo','prioridade','resolvido','data_criacao','criador']]
    items.forEach(i => {
      rows.push([
        i.id,
        (i.titulo||'').replaceAll('"','""'),
        (i.motivo||'').replaceAll('"','""'),
        i.prioridade,
        i.resolvido ? 'sim' : 'nao',
        i.dataCriacao || '',
        i.nomeCriador || ''
      ])
    })
    const csv = rows.map(r => r.map(x => `"${String(x)}"`).join(',')).join('\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'relatorio_chamados.csv'
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-6">
      {/* KPIs */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard title="Abertos" value={metrics.open.length} hint={isAdmin ? 'Todos os chamados' : 'Visíveis para você'} />
        <StatCard title="Críticos (abertos)" value={metrics.crit.length} color="text-red-600" hint="Prioridade 1" />
        <StatCard title="Fechados" value={metrics.closed.length} hint={isAdmin ? 'Todos os chamados' : 'Visíveis para você'} />
      </div>

      {/* Gráfico de prioridades */}
      <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
        <div className="mb-3 text-sm font-semibold text-neutral-900">Distribuição por prioridade</div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Bar label="Crítica (1)" count={metrics.byPriority[0].count} max={metrics.maxBar} />
          <Bar label="Alta (2)" count={metrics.byPriority[1].count} max={metrics.maxBar} />
          <Bar label="Média (3)" count={metrics.byPriority[2].count} max={metrics.maxBar} />
          <Bar label="Baixa (4)" count={metrics.byPriority[3].count} max={metrics.maxBar} />
        </div>
      </div>

      {/* Alertas */}
      {(metrics.crit.length > 0 || metrics.stale.length > 0) && (
        <div className="space-y-2">
          {metrics.crit.length > 0 && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
              Há {metrics.crit.length} chamado(s) crítico(s) aberto(s).
            </div>
          )}
          {metrics.stale.length > 0 && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
              {metrics.stale.length} chamado(s) aberto(s) com mais de 7 dias.
            </div>
          )}
        </div>
      )}

      {/* Relatórios e lista recente */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between">
            <div className="text-sm font-semibold text-neutral-900">Relatórios</div>
            <button onClick={exportCsv} className="rounded-full border border-neutral-200 px-3 py-1.5 text-sm hover:bg-neutral-50">Exportar CSV</button>
          </div>
          <p className="mt-2 text-sm text-neutral-600">Gere um CSV com os chamados visíveis neste dashboard.</p>
        </div>

        {isAdmin && (
          <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
            <div className="text-sm font-semibold text-neutral-900">Usuários</div>
            <div className="mt-3 grid grid-cols-3 gap-3 text-center">
              <div className="rounded-xl border border-neutral-200 p-3">
                <div className="text-xs text-neutral-500">Total</div>
                <div className="text-lg font-semibold">{metrics.usersTotal}</div>
              </div>
              <div className="rounded-xl border border-neutral-200 p-3">
                <div className="text-xs text-neutral-500">Admins</div>
                <div className="text-lg font-semibold">{metrics.admins}</div>
              </div>
              <div className="rounded-xl border border-neutral-200 p-3">
                <div className="text-xs text-neutral-500">Suporte</div>
                <div className="text-lg font-semibold">{metrics.suporte}</div>
              </div>
            </div>
          </div>
        )}
      </div>

      <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
        <div className="text-sm font-semibold text-neutral-900">Chamados recentes</div>
        {metrics.last5.length === 0 ? (
          <p className="mt-2 text-sm text-neutral-500">Nenhum chamado.</p>
        ) : (
          <div className="mt-3 divide-y divide-neutral-200">
            {metrics.last5.map(c => (
              <div key={c.id} className="flex items-center justify-between py-2 text-sm">
                <div className="min-w-0">
                  <p className="truncate font-medium text-neutral-900">{c.titulo || 'Sem título'} <span className="text-neutral-400">#{c.id}</span></p>
                  <p className="truncate text-neutral-500">{c.motivo || '-'} • {c.nomeCriador || ''}</p>
                </div>
                <div className="shrink-0 text-xs text-neutral-500">{c.dataCriacao ? new Date(c.dataCriacao).toLocaleString() : ''}</div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
