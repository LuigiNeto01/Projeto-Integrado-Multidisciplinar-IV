// AbrirChamadoModal
// Eu explico: este modal abre um novo chamado.
// Fluxo:
// 1) Usuário preenche título/motivo (e prioridade/descrição se "Outros").
// 2) Antes de criar, peço a opinião/FAQ da IA (endpoint /ai/chamado/opiniao).
// 3) Só permito confirmar o envio se a IA retornar uma mensagem.
import { useEffect, useState } from 'react'
import { HiOutlineXMark } from 'react-icons/hi2'
import { useAuth } from '@/services/auth/AuthContext'
import { criarChamado } from '@/services/chamados/api'
import { opiniaoChamado } from '@/services/ai/api'

// Motivos padrão mostrados no select
const MOTIVOS = [
  'Problemas com o mouse',
  'Problemas com som',
  'Problema com video',
  'Problemas com a internet',
  'Outros',
]

// Mapeio prioridade automaticamente de acordo com o motivo
function mapPrioridadeByMotivo(m) {
  const k = (m || '').toLowerCase()
  if (k === 'problemas com o mouse') return 3
  if (k === 'problemas com som') return 4
  if (k === 'problema com video') return 2
  if (k === 'problemas com a internet') return 1
  return 4
}

// Sugestões simples antigas (mantidas como referência)
const SUG_MOUSE = [
  'Verifique se o cabo do mouse esta bem conectado a uma porta USB.',
  'Limpe a parte inferior do mouse (sensor optico).',
  'Se for sem fio, confira pilhas ou bateria.',
  'Teste outra porta USB ou outro computador.'
]
const SUG_SOM = [
  'Verifique volumes do sistema e dos aplicativos.',
  'Confirme a conexao correta dos fones/dispositivo.',
  'Selecione o dispositivo de saida correto nas configuracoes.',
  'Reinicie o computador para destravar os drivers de audio.'
]
const SUG_VIDEO = [
  'Confira cabos (HDMI/DP/VGA) no monitor e no computador.',
  'Atualize os drivers da placa de video.',
  'Verifique se o monitor esta na entrada correta.',
  'Ajuste a resolucao caso a imagem esteja distorcida.'
]
const SUG_NET = [
  'Verifique se o Wi-Fi esta ativado.',
  'Aproxime-se do roteador para melhorar o sinal.',
  'Reinicie o modem/roteador (10s desligado).',
  'Remova redes antigas que nao funcionam bem.'
]

// Retorna uma sugestão local aleatória para o motivo informado
function obterSugestao(m) {
  const k = (m || '').toLowerCase()
  let arr = null
  if (k === 'problemas com o mouse') arr = SUG_MOUSE
  else if (k === 'problemas com som') arr = SUG_SOM
  else if (k === 'problema com video') arr = SUG_VIDEO
  else if (k === 'problemas com a internet') arr = SUG_NET
  if (!arr) return 'Tipo de problema nao reconhecido.'
  const idx = Math.floor(Math.random() * arr.length)
  return arr[idx]
}

export default function AbrirChamadoModal({ open, onClose, onCreated }) {
  const { user } = useAuth()
  // Estados do formulário
  const [titulo, setTitulo] = useState('')
  const [motivo, setMotivo] = useState('')
  const [descricao, setDescricao] = useState('')
  const [prioridade, setPrioridade] = useState('4')
  // Estados de UX e confirmação com IA
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [confirmText, setConfirmText] = useState('')

  useEffect(() => {
    if (!open) {
      setTitulo(''); setMotivo(''); setDescricao(''); setPrioridade('4'); setError('')
    }
  }, [open])

  if (!open) return null

  const isOutros = motivo === 'Outros'

  // Primeiro submit: valido dados e busco confirmação da IA
  const handleSubmit = async (e) => {
    e.preventDefault()
    if (loading) return
    setError('')
    if (!titulo.trim() || !motivo.trim()) {
      setError('Preencha Titulo e Motivo.')
      return
    }
    if (isOutros && !prioridade) {
      setError('Selecione... prioridade.')
      return
    }

    // Solicita opinião/FAQ da IA e só permite confirmar com texto retornado.
    try {
      setLoading(true)
      const payload = {
        titulo: titulo.trim(),
        motivo: motivo.trim(),
        descricao: (descricao || '').trim(),
        prioridade: isOutros ? Number(prioridade) : mapPrioridadeByMotivo(motivo),
        nome: user?.nome || user?.name || '',
        email: user?.email || '',
      }
      const res = await opiniaoChamado(payload)
      const text = (res && typeof res.text === 'string') ? res.text : ''
      if (text) {
        setConfirmText(text)
        setConfirmOpen(true)
        setLoading(false)
        return
      }
      setError('Não foi possível gerar a confirmação. Tente novamente.')
      setLoading(false)
      return
    } catch (err) {
      setLoading(false)
      setError(err?.message || 'Falha ao obter confirmação')
      return
    }
  }

  // Segundo passo: confirmação efetiva (criação do chamado)
  const confirmarEnvio = async () => {
    setLoading(true)
    try {
      const payload = {
        titulo: titulo.trim(),
        motivo: motivo.trim(),
        descricao: isOutros ? (descricao.trim() || null) : null,
        prioridade: isOutros ? Number(prioridade) : mapPrioridadeByMotivo(motivo),
      }
      await criarChamado(payload)
      window.dispatchEvent(new Event('chamados:refresh'))
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'success', message: 'Chamado criado com sucesso!' } }))
      setConfirmOpen(false)
      onClose?.()
      onCreated?.()
    } catch (err) {
      setError(err?.message || 'Falha ao criar chamado')
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'error', message: 'Falha ao criar chamado' } }))
    } finally {
      setLoading(false)
    }
  }

  // Cancela a confirmação e mantém o formulário aberto
  const cancelarEnvio = () => {
    setConfirmOpen(false)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-neutral-900">Abrir Chamado</h2>
          <button onClick={onClose} className="rounded-lg p-2 text-neutral-500 hover:bg-neutral-100">
            <HiOutlineXMark className="h-5 w-5" />
          </button>
        </div>

        {error ? (
          <div className="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{error}</div>
        ) : null}

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-neutral-700">Titulo</label>
            <input
              type="text"
              value={titulo}
              onChange={(e) => setTitulo(e.target.value)}
              className="w-full rounded-xl border border-neutral-200 px-4 py-2 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-neutral-700">Motivo</label>
            <select
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              className="w-full rounded-xl border border-neutral-200 px-4 py-2 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            >
              <option value="" disabled>Selecione...</option>
              {MOTIVOS.map((m) => (
                <option key={m} value={m}>{m}</option>
              ))}
            </select>
          </div>

          {isOutros ? (
            <>
              <div>
                <label className="mb-1 block text-sm font-medium text-neutral-700">Prioridade</label>
                <select
                  value={prioridade}
                  onChange={(e) => setPrioridade(e.target.value)}
                  className="w-full rounded-xl border border-neutral-200 px-4 py-2 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
                  <option value="1">Critica</option>
                  <option value="2">Alta</option>
                  <option value="3">Media</option>
                  <option value="4">Baixa</option>
                </select>
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-neutral-700">Descricao</label>
                <textarea
                  rows={4}
                  value={descricao}
                  onChange={(e) => setDescricao(e.target.value)}
                  className="w-full rounded-xl border border-neutral-200 px-4 py-2 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  placeholder="Descreva o problema..."
                />
              </div>
            </>
          ) : null}

          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="rounded-full border border-neutral-200 px-4 py-2 text-sm font-medium text-neutral-700 hover:bg-neutral-50">Cancelar</button>
            <button type="submit" disabled={loading} className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white shadow-sm hover:opacity-90 disabled:opacity-60">
              {loading ? 'Enviando...' : 'Enviar chamado'}
            </button>
          </div>
        </form>
      </div>

      {confirmOpen ? (
        <div className="fixed inset-y-0 right-0 z-50 w-full max-w-md bg-white shadow-2xl">
          <div className="flex items-center justify-between border-b border-neutral-200 p-4">
            <h3 className="text-base font-semibold text-neutral-900">Confirme o envio</h3>
            <button onClick={cancelarEnvio} className="rounded-lg p-2 text-neutral-500 hover:bg-neutral-100">
              <HiOutlineXMark className="h-5 w-5" />
            </button>
          </div>
          <div className="p-5">
            <p className="text-base md:text-lg leading-relaxed text-neutral-800 whitespace-pre-wrap">
              {confirmText}
            </p>
            <p className="mt-4 text-base text-neutral-600">Se concordar com a mensagem e o problema persistir, confirme o envio do chamado.</p>
          </div>
          <div className="flex justify-end gap-3 p-4">
            <button type="button" onClick={cancelarEnvio} className="rounded-full border border-neutral-200 px-4 py-2 text-sm font-medium text-neutral-700 hover:bg-neutral-50">Cancelar chamado</button>
            <button type="button" onClick={confirmarEnvio} disabled={loading || !confirmText} className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white shadow-sm hover:opacity-90 disabled:opacity-60">Confirmar envio</button>
          </div>
        </div>
      ) : null}
    </div>
  )
}
