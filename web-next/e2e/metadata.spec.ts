import { createInvite } from './api';
import { expect, test } from './fixtures';
import { FAMILY } from './people';

test.describe('signed out: what a link-preview bot sees', () => {
  test.use({ person: null });

  test('an invite shows neither the stokvel name nor any amount, in the title, description or og: tags', async ({ request }) => {
    const code = await createInvite('sipho', FAMILY); // a real invite
    const res = await request.get(`/join?code=${code}`, {
      headers: { 'user-agent': 'WhatsApp/2.23.20.0' }, // a known preview bot gets the metadata in <head>
      maxRedirects: 0, // the bot must get the page itself, not a redirect
    });
    expect(res.status()).toBe(200);
    const html = await res.text();
    const head = html.slice(0, html.indexOf('</head>'));

    const tags = [
      ...(head.match(/<title>[^<]*<\/title>/g) ?? []),
      ...(head.match(/<meta[^>]+(name="description"|property="og:[^"]+"|name="twitter:[^"]+")[^>]*>/g) ?? []),
    ].join('\n');

    expect(tags).toMatch(/og:title/);
    expect(tags).toMatch(/invite/i);
    expect(tags).not.toMatch(/E2E Family/i); // not the stokvel's name
    expect(tags).not.toMatch(/Sipho|Thandi|Kagiso/); // not a member
    expect(tags).not.toMatch(/\b500\b|R\s?\d/); // not an amount
  });

  test('robots.txt does not Disallow any page (a blocked page never shows its noindex)', async ({ request }) => {
    const txt = await (await request.get('/robots.txt')).text();
    expect(txt).not.toMatch(/Disallow:\s*\/\S/);
  });
});

test.describe('signed in', () => {
  test.use({ person: 'thandi' });

  test('a signed-in page is noindex', async ({ page }) => {
    await page.goto('/dashboard/member');
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
  });

  test('/bff/ responses carry X-Robots-Tag: noindex', async ({ request }) => {
    const res = await request.get('/bff/activity');
    expect(res.headers()['x-robots-tag']).toContain('noindex');
  });
});