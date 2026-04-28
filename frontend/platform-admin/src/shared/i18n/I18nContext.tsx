import { createContext, useContext, useState, useCallback, type ReactNode } from 'react'
import { zh_CN, en_US, type Language, type TranslationKeys } from './locales'

interface I18nContextValue {
  language: Language
  setLanguage: (lang: Language) => void
  t: (key: string) => string
  translations: TranslationKeys
}

const I18nContext = createContext<I18nContextValue | null>(null)

const translationsMap = {
  'zh-CN': zh_CN,
  'en-US': en_US,
}

function getLanguageFromStorage(): Language {
  const saved = localStorage.getItem('platform-admin-language')
  return (saved === 'en-US' ? 'en-US' : 'zh-CN') as Language
}

function getNestedValue(obj: any, path: string): string {
  const keys = path.split('.')
  let result = obj
  for (const key of keys) {
    if (result && typeof result === 'object' && key in result) {
      result = result[key]
    } else {
      return path
    }
  }
  return typeof result === 'string' ? result : path
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(getLanguageFromStorage)

  const setLanguage = useCallback((lang: Language) => {
    setLanguageState(lang)
    localStorage.setItem('platform-admin-language', lang)
    document.documentElement.lang = lang === 'zh-CN' ? 'zh-CN' : 'en'
  }, [])

  const translations = translationsMap[language]

  const t = useCallback(
    (key: string) => {
      return getNestedValue(translations, key)
    },
    [translations]
  )

  return (
    <I18nContext.Provider value={{ language, setLanguage, t, translations }}>
      {children}
    </I18nContext.Provider>
  )
}

export function useI18n() {
  const context = useContext(I18nContext)
  if (!context) {
    throw new Error('useI18n must be used within I18nProvider')
  }
  return context
}
