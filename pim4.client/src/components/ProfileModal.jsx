// ProfileModal
// Eu explico: modal para o usuário editar dados básicos (nome, email, senha).
// Campos de CPF/cargo/nível aparecem como somente leitura.
import { useEffect, useState } from 'react'
import { HiOutlineXMark } from 'react-icons/hi2'

export default function ProfileModal({ open, onClose, onSubmit, initial }) {
  const [nome, setNome] = useState('')
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [error, setError] = useState('')
  const cpf = initial?.cpf || ''
  const cargo = initial?.cargo || ''
  const nivel = initial?.nivel ?? ''

  useEffect(() => {
    if (open) {
      setError('')
      setNome(initial?.nome || initial?.name || '')
      setEmail(initial?.email || '')
      setSenha('')
    }
  }, [open, initial])

  if (!open) return null

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    if (!nome.trim() || !email.includes('@') || !email.includes('.')) {
      setError('Informe nome e email válidos.')
      return
    }
    const payload = { nome: nome.trim(), email: email.trim() }
    if (senha) payload.senha = senha
    await onSubmit(payload)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-neutral-900">Editar perfil</h2>
          <button onClick={onClose} className="rounded-lg p-2 text-neutral-500 hover:bg-neutral-100"><HiOutlineXMark className="h-5 w-5"/></button>
        </div>

        {error ? (
          <div className="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{error}</div>
        ) : null}

        <form onSubmit={handleSubmit} className="mt-4 grid grid-cols-1 gap-3">
          {/* Somente leitura */}
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div>
              <label className="mb-1 block text-sm font-medium">CPF</label>
              <input className="w-full rounded-xl border border-neutral-200 bg-neutral-50 px-4 py-2 text-neutral-600" value={cpf} readOnly disabled />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Cargo</label>
              <input className="w-full rounded-xl border border-neutral-200 bg-neutral-50 px-4 py-2 text-neutral-600" value={cargo} readOnly disabled />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Nível</label>
              <input className="w-full rounded-xl border border-neutral-200 bg-neutral-50 px-4 py-2 text-neutral-600" value={nivel === null ? '' : String(nivel)} readOnly disabled />
            </div>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Nome</label>
            <input className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={nome} onChange={(e)=>setNome(e.target.value)} required />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">Email</label>
            <input type="email" className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={email} onChange={(e)=>setEmail(e.target.value)} required />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">Senha (opcional)</label>
            <input type="password" className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={senha} onChange={(e)=>setSenha(e.target.value)} />
          </div>
          <p className="text-xs text-neutral-500">CPF, cargo e nível não podem ser alterados aqui.</p>
          <div className="mt-2 flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-full border border-neutral-200 px-4 py-2 text-sm">Cancelar</button>
            <button type="submit" className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white">Salvar</button>
          </div>
        </form>
      </div>
    </div>
  )
}
