// Toast — componente de notificações simples baseado em eventos do window
// para mostrar toasts de qualquer lugar sem prop drilling, dispare:
// window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'success'|'error'|'info', message: 'texto', duration: 3000 }}))
import { useEffect, useRef, useState } from 'react'

// Simple global toast: listen to window event 'toast:show'
// Usage: window.dispatchEvent(new CustomEvent('toast:show', { detail: { type: 'success'|'error'|'info', message: 'texto' }}))
export default function Toast() {
  const [toasts, setToasts] = useState([])
  const idRef = useRef(0)

  // Inscreve handler global e remove no unmount
  useEffect(() => {
    const onShow = (e) => {
      const { message = '', type = 'info', duration = 3000 } = e.detail || {}
      const id = ++idRef.current
      setToasts((list) => [...list, { id, message, type }])
      setTimeout(() => {
        setToasts((list) => list.filter((t) => t.id !== id))
      }, duration)
    }
    window.addEventListener('toast:show', onShow)
    return () => window.removeEventListener('toast:show', onShow)
  }, [])

  // Define cor por tipo de toast
  const color = (type) => {
    switch (type) {
      case 'success':
        return 'border-green-200 bg-green-50 text-green-800'
      case 'error':
        return 'border-red-200 bg-red-50 text-red-800'
      default:
        return 'border-neutral-200 bg-white text-neutral-900'
    }
  }

  return (
    <div className="pointer-events-none fixed bottom-4 right-4 z-[9999] flex w-full max-w-sm flex-col gap-2">
      {toasts.map((t) => (
        <div key={t.id} className={`pointer-events-auto rounded-xl border px-4 py-3 shadow-lg ${color(t.type)}`}>
          <p className="text-sm font-medium">{t.message}</p>
        </div>
      ))}
    </div>
  )
}
