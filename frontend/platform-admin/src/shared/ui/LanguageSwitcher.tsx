import { useI18n } from '../i18n/I18nContext'
import type { Language } from '../i18n/locales'

export function LanguageSwitcher() {
  const { language, setLanguage } = useI18n()

  const toggleLanguage = () => {
    const newLang: Language = language === 'zh-CN' ? 'en-US' : 'zh-CN'
    setLanguage(newLang)
  }

  return (
    <button
      type="button"
      onClick={toggleLanguage}
      className="language-switcher"
      title={language === 'zh-CN' ? 'Switch to English' : '切换到中文'}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '6px 12px',
        border: '1px solid var(--border-color)',
        borderRadius: '6px',
        background: 'transparent',
        color: 'var(--text-primary)',
        cursor: 'pointer',
        fontSize: '14px',
        transition: 'all 0.2s',
      }}
    >
      <span style={{ fontSize: '16px' }}>🌐</span>
      <span>{language === 'zh-CN' ? 'EN' : '中文'}</span>
    </button>
  )
}
