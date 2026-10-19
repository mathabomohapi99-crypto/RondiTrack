// NEW: tests that cover the role comparison so a typo gets caught.
import { describe, expect, it } from 'vitest'
import { isTreasurer, roleLabel } from './roles'

describe('roles', () => {
  it('knows a Treasurer', () => {
    expect(isTreasurer('Treasurer')).toBe(true)
  })

  it('does not treat a Member as Treasurer', () => {
    expect(isTreasurer('Member')).toBe(false)
  })

  it('does not treat a typo as Treasurer', () => {
    expect(isTreasurer('Treasuer')).toBe(false)
  })

  it('shows the role as text', () => {
    expect(roleLabel('Treasurer')).toBe("You're the Treasurer")
    expect(roleLabel('Member')).toBe("You're a Member")
    expect(roleLabel('Admin')).toBe("You're an Admin")
  })
})