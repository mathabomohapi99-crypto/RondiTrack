// NEW: the theme "contract": which values exist and how components read/change them.
import { createContext, useContext } from 'react'

export type Theme = 'light' | 'dark' | 'system'
export const THEME_STORAGE_KEY = 'ronditrack-theme'

export type ThemeContextValue = { theme: Theme; setTheme: (theme: Theme) => void }
export const ThemeContext = createContext<ThemeContextValue | null>(null)

export function useTheme(): ThemeContextValue {
  const value = useContext(ThemeContext)
  if (!value) throw new Error('useTheme must be used inside ThemeProvider')
  return value
}