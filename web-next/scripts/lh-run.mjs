// usage: node scripts/lh-run.mjs /dashboard/member before [thandi|sipho]
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';

const [path = '/dashboard/member', label = 'run', person = 'thandi'] = process.argv.slice(2);
const base = process.env.BASE_URL ?? 'http://localhost:3000';
const state = JSON.parse(readFileSync(`e2e/.auth/${person}.json`, 'utf8'));
mkdirSync('lighthouse', { recursive: true });
writeFileSync('lighthouse/headers.json', JSON.stringify({ Cookie: state.cookies.map((c) => `${c.name}=${c.value}`).join('; ') }));

const runs = [];
for (let i = 1; i <= 3; i++) {
  const out = `lighthouse/${label}-${i}.json`;
  const r = spawnSync('npx', ['lighthouse', base + path, '--only-categories=performance', '--output=json',
    `--output-path=${out}`, '--extra-headers=lighthouse/headers.json', '--chrome-flags=--headless=new', '--quiet'],
    { stdio: 'inherit', shell: true });
  if (r.status !== 0) process.exit(r.status ?? 1);
  const j = JSON.parse(readFileSync(out, 'utf8'));
  runs.push({
    lcp: j.audits['largest-contentful-paint'].numericValue,
    cls: j.audits['cumulative-layout-shift'].numericValue,
    tbt: j.audits['total-blocking-time'].numericValue,
    score: j.categories.performance.score,
    lcpEl: j.audits['largest-contentful-paint-element']?.details?.items?.[0]?.items?.[0]?.node?.snippet ?? '?',
  });
}
const med = (k) => runs.map((r) => r[k]).sort((a, b) => a - b)[1];
console.log(`\n${label} ${path} MEDIAN of 3: LCP ${(med('lcp') / 1000).toFixed(2)} s | CLS ${med('cls').toFixed(3)} | TBT ${Math.round(med('tbt'))} ms | score ${Math.round(med('score') * 100)}`);
console.log(`LCP element: ${runs[0].lcpEl}`);