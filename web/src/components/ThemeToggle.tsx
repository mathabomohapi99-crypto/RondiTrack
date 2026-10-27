// NEW: Light / Dark / System buttons. aria-pressed tells a screen reader which one is on.
import { Monitor, Moon, Sun } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useTheme, type Theme } from '@/theme/theme-context'

const OPTIONS: { value: Theme; label: string; Icon: typeof Sun }[] = [
  { value: 'light', label: 'Light', Icon: Sun },
  { value: 'dark', label: 'Dark', Icon: Moon },
  { value: 'system', label: 'System', Icon: Monitor },
]

export function ThemeToggle() {
  const { theme, setTheme } = useTheme()
  return (
    <div role="group" aria-label="Colour theme" className="flex flex-wrap gap-1">
      {OPTIONS.map(({ value, label, Icon }) => (
        <Button
          key={value}
          type="button"
          size="sm"
          variant={theme === value ? 'default' : 'outline'}
          aria-pressed={theme === value}
          onClick={() => setTheme(value)}
        >
          <Icon aria-hidden="true" />
          {label}
        </Button>
      ))}
    </div>
  )
}