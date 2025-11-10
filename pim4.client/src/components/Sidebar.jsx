// TODO: Corrigir como componentes reagem ao abrir a sidebar no mobile

import { forwardRef, useEffect, useState } from 'react'
import {
  HiOutlineChevronDoubleLeft,
  HiOutlineChevronDoubleRight,
  HiOutlineBriefcase,
  HiOutlineMagnifyingGlass,
  HiOutlineDocumentText,
  HiOutlinePhoto,
  HiOutlineBell,
  HiOutlineCog6Tooth,
  HiOutlineUserCircle,
} from 'react-icons/hi2'
import { useAuth } from '@/services/auth/AuthContext'
import ProfileModal from '@/components/ProfileModal'
import { updateMe } from '@/services/profile/api'
import logo from '@/assets/logo.png'

// Sidebar: responsável por navegação lateral, itens de menu e ações do usuário.
// Observação: os itens exibidos vêm do componente pai (Home) conforme a role.

const ICONS = {
  dashboard: HiOutlineDocumentText,
  chamados: HiOutlineBell,
  vagas: HiOutlineMagnifyingGlass,
  curriculos: HiOutlineDocumentText,
  usuarios: HiOutlineBriefcase,
}

function LogoArea({ open, onToggle }) {
  return (
    <div className="flex min-h-[72px] items-center justify-between px-2 py-2">
      <div className="flex flex-1 items-center justify-center">
        {open ? (
          <>
            <img
              src={logo}
              alt="ORACULO"
              className="h-40 w-40 rounded-lg object-contain"
            />
      
          </>
        ) : null}
      </div>
      <button
        type="button"
        aria-label={open ? 'Recolher sidebar' : 'Expandir sidebar'}
        onClick={onToggle}
        className="inline-flex h-10 w-10 items-center justify-center rounded-lg text-neutral-600 transition-colors hover:bg-neutral-100 hover:text-neutral-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-600"
      >
        {open ? (
          <HiOutlineChevronDoubleLeft className="h-5 w-5 shrink-0" aria-hidden="true" />
        ) : (
          <HiOutlineChevronDoubleRight className="h-5 w-5 shrink-0" aria-hidden="true" />
        )}
      </button>
    </div>
  )
}

function NavItem({ item, open, active, onSelect }) {
  const Icon = ICONS[item.key]
  const layoutClasses = open ? 'justify-start gap-4 px-4 py-3' : 'justify-center py-3'
  return (
    <button
      type="button"
      onClick={() => onSelect?.(item)}
      title={!open ? item.label : undefined}
      aria-current={active ? 'page' : undefined}
      className={[
        'group my-3 flex w-full items-center rounded-lg text-base font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-600',
        layoutClasses,
        active ? 'bg-neutral-100 text-neutral-900' : 'text-neutral-700 hover:bg-neutral-100 hover:text-neutral-900',
      ].join(' ')}
    >
      <Icon className="h-6 w-6 shrink-0 text-neutral-500 group-hover:text-neutral-700" aria-hidden="true" />
      <span className={open ? 'truncate' : 'sr-only'}>{item.label}</span>
    </button>
  )
}

function UserDock({ open, onLogout }) {
  const [showActions, setShowActions] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)

  useEffect(() => {
    if (!open) setShowActions(false)
  }, [open])

  const toggleMenu = () => setShowActions((value) => !value)
  const handleLogout = () => {
    setShowActions(false)
    onLogout?.()
  }

  let { user, updateUser, renewSession } = useAuth()

  const handleProfileSubmit = async (payload) => {
    try {
      const res = await updateMe(payload)
      const updated = res?.user || {}
      if (res?.token) {
        // Renova a sessão com novo token e usuário
        renewSession(res.token, updated, res.expiresAt)
      } else {
        updateUser({ nome: updated.nome ?? payload.nome, email: updated.email ?? payload.email })
      }
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'success', message: 'Perfil atualizado!' } }))
      setProfileOpen(false)
    } catch (err) {
      window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'error', message: err?.message || 'Falha ao atualizar perfil' } }))
    }
  }

  const displayName = user?.nome || user?.name || 'Usuário'
  const displayEmail = user?.email || ''

  return (
    <div className="relative flex min-h-[72px] items-center justify-between gap-2 border-t border-neutral-200 px-3 py-3">
      <div className="flex items-center gap-2">
        <HiOutlineUserCircle className="h-8 w-8 shrink-0 text-neutral-500" aria-hidden="true" />
        {open && (
          <div className="min-w-0">
            <p className="truncate text-sm font-medium text-neutral-900">{displayName}</p>
            <p className="truncate text-xs text-neutral-500">{displayEmail}</p>
          </div>
        )}
      </div>
      {open ? (
        <>
          <button
            type="button"
            aria-label="Configurações"
            onClick={toggleMenu}
            className="inline-flex items-center justify-center rounded-lg p-2 text-neutral-600 transition-colors hover:bg-neutral-100 hover:text-neutral-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-600"
          >
            <HiOutlineCog6Tooth className="h-5 w-5 shrink-0" aria-hidden="true" />
          </button>
          {showActions ? (
            <div className="absolute bottom-20 right-3 w-36 rounded-xl border border-neutral-200 bg-white/95 p-2 shadow-lg backdrop-blur-sm">
              <button
                type="button"
                onClick={() => { setShowActions(false); setProfileOpen(true) }}
                className="mb-1 flex w-full items-center justify-between rounded-lg px-3 py-2 text-sm font-medium text-neutral-700 transition-colors hover:bg-neutral-100"
              >
                <span>Editar perfil</span>
                <HiOutlineChevronDoubleRight className="h-4 w-4 text-neutral-400" aria-hidden="true" />
              </button>
              <button
                type="button"
                onClick={handleLogout}
                className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-sm font-medium text-neutral-700 transition-colors hover:bg-neutral-100"
              >
                <span>Sair</span>
                <HiOutlineChevronDoubleRight className="h-4 w-4 rotate-180 text-neutral-400" aria-hidden="true" />
              </button>
            </div>
          ) : null}
        </>
      ) : null}
      <ProfileModal open={profileOpen} onClose={() => setProfileOpen(false)} initial={user} onSubmit={handleProfileSubmit} />
    </div>
  )
}

// Componente principal exportado
const Sidebar = forwardRef(function Sidebar(
  { open, onToggle, items = [], activeKey, onSelect, onLogout },
  ref,
) {
  return (
    <aside
      ref={ref}
      className={[
        'fixed inset-y-0 left-0 z-50 flex h-screen flex-col border-r border-neutral-200 bg-white shadow-lg transition-all duration-200',
        'pointer-events-auto',
        open ? 'w-72' : 'w-16',
      ].join(' ')}
    >
      <LogoArea open={open} onToggle={onToggle} />

      <nav aria-label="Principal" className="mt-2 flex-1 overflow-y-auto px-2">
        <div className="flex min-h-full flex-col justify-center">
          {items.map((item) => (
            <NavItem
              key={item.key}
              item={item}
              open={open}
              active={item.key === activeKey}
              onSelect={onSelect}
            />
          ))}
        </div>
      </nav>

      <UserDock open={open} onLogout={onLogout} />
    </aside>
  )
})

export default Sidebar



