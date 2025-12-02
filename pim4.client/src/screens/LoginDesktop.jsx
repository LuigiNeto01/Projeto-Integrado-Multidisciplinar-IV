// LoginDesktop
// layout desktop para a tela de login (duas colunas),
// renderiza o LoginForm na coluna da direita.
import ilustration from '@/assets/login_one.jpg'
import LoginForm from '@/components/LoginForm'

export default function LoginDesktop() {
  return (
    <div className="min-h-screen w-full bg-gray-50 grid grid-cols-3">
      {/* Coluna esquerda */}
      <div className="col-span-2 relative overflow-hidden bg-gradient-to-br from-indigo-50 via-white to-indigo-100">
        <img
          src={ilustration}
          alt="Ilustração"
          className="absolute inset-0 h-full w-full object-cover opacity-90"
        />
      </div>

      {/* Coluna direita */}
      <div className="col-span-1 flex flex-col min-h-screen p-6">
        <div className="w-full max-w-md mx-auto pt-10 pb-6">
          <div className="text-center">
            <h1 className="text-3xl font-semibold tracking-tight text-neutral-900 transition-colors duration-200">
              Bem vindo!
            </h1>
            <p className="mt-2 text-sm font-medium text-neutral-500/90">Entre com suas credenciais</p>
          </div>
        </div>

        <div className="flex-1 flex items-center justify-center">
          <LoginForm withCard={false} showHeader={false} />
        </div>
      </div>
    </div>
  )
}
