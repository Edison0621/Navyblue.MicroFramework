import { createContext, useContext, useMemo, useState } from 'react'
import { clearAccessToken, getAccessToken, setAccessToken } from '../lib/session'

interface AuthContextValue {
  token: string | null
  isAuthed: boolean
  signIn: (token: string) => void
  signOut: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => getAccessToken())

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      isAuthed: Boolean(token),
      signIn: (next) => {
        setAccessToken(next)
        setToken(next)
      },
      signOut: () => {
        clearAccessToken()
        setToken(null)
      },
    }),
    [token],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return ctx
}
