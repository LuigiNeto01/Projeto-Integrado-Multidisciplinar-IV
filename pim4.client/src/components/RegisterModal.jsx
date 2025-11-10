import { useEffect, useState } from 'react'
import { HiOutlineXMark } from 'react-icons/hi2'

export default function RegisterModal({ open, onClose, onSubmit }) {
  const [cpf, setCpf] = useState('')
  const [nome, setNome] = useState('')
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    if (open) { setCpf(''); setNome(''); setEmail(''); setSenha(''); setError('') }
  }, [open])

  if (!open) return null

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    if (!cpf || !nome.trim() || !email.includes('@') || !senha) {
      setError('Preencha CPF, Nome, E-mail e Senha.')
      return
    }
    const payload = {
      cpf: cpf.replace(/\D/g, ''),
      nome: nome.trim(),
      email: email.trim(),
      password: senha,
    }
    await onSubmit?.(payload)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-neutral-900">Criar conta</h2>
          <button onClick={onClose} className="rounded-lg p-2 text-neutral-500 hover:bg-neutral-100"><HiOutlineXMark className="h-5 w-5"/></button>
        </div>
        {error && <div className="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{error}</div>}
        <form onSubmit={handleSubmit} className="mt-4 grid grid-cols-1 gap-3">
          <div>
            <label className="mb-1 block text-sm font-medium">CPF</label>
            <input className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={cpf} onChange={(e)=>setCpf(e.target.value)} placeholder="somente números" />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">Nome</label>
            <input className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={nome} onChange={(e)=>setNome(e.target.value)} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">E-mail</label>
            <input type="email" className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={email} onChange={(e)=>setEmail(e.target.value)} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">Senha</label>
            <input type="password" className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={senha} onChange={(e)=>setSenha(e.target.value)} />
          </div>
          <p className="text-xs text-neutral-500">Cargo: usuário (padrão)</p>
          <div className="mt-2 flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-full border border-neutral-200 px-4 py-2 text-sm">Cancelar</button>
            <button type="submit" className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white">Registrar</button>
          </div>
        </form>
      </div>
    </div>
  )
}

