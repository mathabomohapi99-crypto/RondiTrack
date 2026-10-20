// CHANGED 7.2: short names for the generated API shapes. No hand-written interfaces here.
import type { components } from './schema'

export type HomeStokvelEntry = components['schemas']['HomeStokvelDto']          // one stokvel from GET /me/home
export type CycleItem = components['schemas']['CycleSummaryResponse']           // one cycle from GET /stokvels/{id}/cycles
export type ContributionItem = components['schemas']['ContributionDetailResponse'] // one item of a contributions list