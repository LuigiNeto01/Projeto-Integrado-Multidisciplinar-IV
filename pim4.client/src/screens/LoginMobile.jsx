// LoginMobile
// Eu explico: layout compacto (mobile) para o LoginForm.
import LoginForm from '@/components/LoginForm'

export default function LoginMobile() {
  return (
    <div className="min-h-screen w-full bg-gray-50 flex flex-col items-center justify-center p-4">
      <LoginForm withCard showHeader />
    </div>
  )
}
