import type { NextConfig } from 'next';

const nextConfig = {
  reactCompiler: true,
  cacheComponents: true,
  partialPrefetching: true,
  agentRules: false,
  // NEW 9.1: a Server Action takes 1 MB by default. 6 MB lets a 5 MB proof (plus form overhead) through to the action.
  experimental: { serverActions: { bodySizeLimit: '6mb' } },
} as NextConfig;

export default nextConfig;

import type { NextConfig } from 'next';

const nextConfig = {
  reactCompiler: true,
  cacheComponents: true,
  partialPrefetching: true,
  agentRules: false,
  experimental: { serverActions: { bodySizeLimit: '6mb' } },
  // NEW 9.2: route handlers and downloads must never be indexed
  async headers() {
    return [{ source: '/bff/:path*', headers: [{ key: 'X-Robots-Tag', value: 'noindex' }] }];
  },
} as NextConfig;

export default nextConfig;