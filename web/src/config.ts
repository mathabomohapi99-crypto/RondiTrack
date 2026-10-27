// One place for the API address; falls back so tests work without a .env file
export const API_BASE_URL: string =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5134';