import type { NextConfig } from 'next';

// React Compiler on, cacheComponents off (Day 2 turns it on), agentRules off.
// "as NextConfig" stops TypeScript complaining if agentRules is not in its types.
const nextConfig = {
  reactCompiler: true,
  cacheComponents: false,
  agentRules: false,
} as NextConfig;

export default nextConfig;