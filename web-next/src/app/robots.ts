import type { MetadataRoute } from 'next';

// Deliberately no Disallow: a crawler that may not fetch a page never sees its noindex.
export default function robots(): MetadataRoute.Robots {
  return { rules: { userAgent: '*', allow: '/' } };
}