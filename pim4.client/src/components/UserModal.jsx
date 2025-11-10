// UserModal: modal do painel admin para criar/editar usuários.
// Eu explico: senha é opcional quando em edição; para cargo "Suporte" o nível é obrigatório.
import { useEffect, useState } from 'react'
import { HiOutlineXMark } from 'react-icons/hi2'

// Modal para criar/editar usuário (controlado por props "open" e dados em "initial").
export default function UserModal({ open, onClose, onSubmit, initial }) {
  const [nome, setNome] = useState('')
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [cpf, setCpf] = useState('')
  const [cargo, setCargo] = useState('usuario')
  const [nível, setnível] = useState('')
  const [error, setError] = useState('')
  const isEdit = !!initial

  // Ao abrir, preenche com valores iniciais e limpa mensagens de erro
  useEffect(() => {
    if (open) {
      setError('')
      setNome(initial?.nome || '')
      setEmail(initial?.email || '')
      setSenha('')
      setCpf(initial?.cpf || '')
      setCargo(initial?.cargo || 'usuario')
      setnível(initial?.nível != null ? String(initial.nível) : '')
    }
  }, [open, initial])

  if (!open) return null

  // Validação e preparo do payload para submit (create/update)
  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    if (!nome.trim() || !email.includes('@') || !email.includes('.')) {
      setError('Informe nome e email vÃ¡lidos.')
      return
    }
    if (!isEdit && !senha) {
      setError('Informe a senha para criar.')
      return
    }
    if (cargo.toLowerCase() === 'suporte' && !nível) {
      setError('Informe o nível para Suporte.')
      return
    }

    const payload = {
      nome: nome.trim(),
      email: email.trim(),
      cpf: cpf.replace(/\D/g, ''), // envio somente dígitos
      cargo,
      nível: cargo.toLowerCase() === 'suporte' ? Number(nível) : null,
    }
    if (senha) payload.senha = senha
    await onSubmit(payload)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-neutral-900">{isEdit ? 'Editar usuÃ¡rio' : 'Criar usuÃ¡rio'}</h2>
          <button onClick={onClose} className="rounded-lg p-2 text-neutral-500 hover:bg-neutral-100"><HiOutlineXMark className="h-5 w-5"/></button>
        </div>

        {error ? (
          <div className="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{error}</div>
        ) : null}

        <form onSubmit={handleSubmit} className="mt-4 grid grid-cols-1 gap-3">
          <div>
            <label className="mb-1 block text-sm font-medium">Nome</label>
            <input className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={nome} onChange={(e)=>setNome(e.target.value)} required />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">Email</label>
            <input type="email" className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={email} onChange={(e)=>setEmail(e.target.value)} required />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">Senha {isEdit ? '(opcional)' : ''}</label>
            <input type="password" className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={senha} onChange={(e)=>setSenha(e.target.value)} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">CPF</label>
            <input className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={cpf} onChange={(e)=>setCpf(e.target.value)} placeholder="somente números" />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">Cargo</label>
            <select className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={cargo} onChange={(e)=>setCargo(e.target.value)}>
              <option value="usuario">usuario</option>
              <option value="Suporte">Suporte</option>
              <option value="admin">admin</option>
            </select>
          </div>
          {cargo === 'Suporte' ? (
            <div>
              <label className="mb-1 block text-sm font-medium">nível</label>
              <input className="w-full rounded-xl border border-neutral-200 px-4 py-2" value={nível} onChange={(e)=>/^[0-9]*$/.test(e.target.value) && setnível(e.target.value)} placeholder="apenas números" />
            </div>
          ) : null}
          <div className="mt-2 flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-full border border-neutral-200 px-4 py-2 text-sm">Cancelar</button>
            <button type="submit" className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white">{isEdit ? 'Salvar' : 'Criar'}</button>
          </div>
        </form>
      </div>
    </div>
  )
}



