import type { NextConfig } from 'next';

// "as NextConfig" stops TypeScript complaining if agentRules is not in its types.
const nextConfig = {
  reactCompiler: true,
  // Cache Components: every page = a static shell plus parts that stream in at request time.
  cacheComponents: true,
  // Partial Prefetching: links prefetch only the static shell of a route, never its private data.
  partialPrefetching: true,
  agentRules: false,
} as NextConfig;

export default nextConfig;