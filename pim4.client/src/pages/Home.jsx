// Home
// Eu explico: shell principal que organiza a sidebar e a área de conteúdo.
// - Alterno entre Dashboard, Chamados e (se admin) Usuários.
import { useState } from 'react'
import Sidebar from '@/components/Sidebar'
import { useAuth } from '@/services/auth/AuthContext'
import { clearToken } from '@/services/api/http'
import { useNavigate } from 'react-router-dom'
import ChamadosGrid from '@/components/ChamadosGrid'
import AbrirChamadoModal from '@/components/AbrirChamadoModal'
import UsersManager from '@/pages/UsersManager'
import Dashboard from '@/pages/Dashboard'

function useNavItems(role) {
  const base = [
    { key: 'dashboard', label: 'Dashboard' },
    { key: 'chamados', label: 'Chamados' },
  ]
  if (String(role || '').toLowerCase() === 'admin') {
    base.push({ key: 'usuarios', label: 'Gerir usuarios' })
  }
  return base
}

export default function Home() {
  const [open, setOpen] = useState(true)
  const [activeKey, setActiveKey] = useState('dashboard')
  const [refreshKey, setRefreshKey] = useState(0)
  const [modalOpen, setModalOpen] = useState(false)
  const { logout, user } = useAuth()
  const navigate = useNavigate()
  const NAV_ITEMS = useNavItems(user?.cargo)

  const handleLogout = () => {
    logout()
    clearToken()
    navigate('/login')
  }

  return (
    <div className="min-h-screen w-full bg-gray-50">
      <div className="relative min-h-screen">
        <Sidebar
          open={open}
          onToggle={() => setOpen((v) => !v)}
          items={NAV_ITEMS}
          activeKey={activeKey}
          onSelect={(item) => setActiveKey(item.key)}
          onLogout={handleLogout}
        />

        <main className={[
          'ml-16 transition-all duration-200 p-6',
          open ? 'md:ml-72' : 'md:ml-16',
        ].join(' ')}>
          <div className="mx-auto max-w-6xl">
            <div className="flex items-center justify-between">
              <h1 className="text-2xl font-semibold text-neutral-900">{activeKey === 'chamados' ? 'Chamados' : 'Página Inicial'}</h1>
              {activeKey === 'chamados' ? (
                <button type="button" onClick={() => setModalOpen(true)} className="rounded-full bg-black px-4 py-2 text-sm font-medium text-white shadow-sm hover:opacity-90">Abrir chamado</button>
              ) : null}
            </div>
            <p className="mt-2 text-neutral-600">{activeKey === 'chamados' ? 'Lista de chamados' : 'Selecione uma seção no menu.'}</p>

            <div className="mt-6">
              {activeKey === 'dashboard' ? (
                <Dashboard />
              ) : activeKey === 'chamados' ? (
                <>
                  <ChamadosGrid refreshKey={refreshKey} />
                  <AbrirChamadoModal
                    open={modalOpen}
                    onClose={() => setModalOpen(false)}
                    onCreated={() => { setModalOpen(false); setRefreshKey((k) => k + 1) }}
                  />
                </>
              ) : activeKey === 'usuarios' ? (
                <UsersManager />
              ) : null}
            </div>
          </div>
        </main>
      </div>
    </div>
  )
}
