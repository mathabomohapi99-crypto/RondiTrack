import type { components } from "./schema";

type Home = components["schemas"]["HomeResponse"];

// Total still owed across all stokvels, in integer cents (exact in JavaScript)
export function totalOwedCents(home: Home): number {
  return home.stokvels.reduce((sum, s) => sum + s.owedCents, 0);
}