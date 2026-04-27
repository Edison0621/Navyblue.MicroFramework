import { AuthProvider } from './app/AuthContext'
import { AppRouter } from './app/router'
import { ToastProvider } from './shared/ui/ToastProvider'

function App() {
  return (
    <AuthProvider>
      <ToastProvider>
        <AppRouter />
      </ToastProvider>
    </AuthProvider>
  )
}

export default App
