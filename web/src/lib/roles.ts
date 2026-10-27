// NEW: the ONLY place the role strings are compared, so a typo can't silently hide the section.
const TREASURER = 'Treasurer'
const MEMBER = 'Member'
const ADMIN = 'Admin'

export function isTreasurer(role: string): boolean {
  return role === TREASURER
}

// The role is always shown as words (never colour alone)
export function roleLabel(role: string): string {
  if (role === TREASURER) return "You're the Treasurer"
  if (role === MEMBER) return "You're a Member"
  if (role === ADMIN) return "You're an Admin"
  return `Unknown role: ${role}`
}