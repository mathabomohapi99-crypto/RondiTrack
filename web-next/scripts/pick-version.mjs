// usage: node scripts/pick-versions.mjs vitest@4.1.12 msw@3 jsdom
import { execSync } from 'node:child_process';

const cutoff = Date.now() - 14 * 86_400_000;
const cmp = (a, b) => {
  const x = a.split('.').map(Number), y = b.split('.').map(Number);
  for (let i = 0; i < 3; i++) if (x[i] !== y[i]) return x[i] - y[i];
  return 0;
};
const picks = [];
for (const arg of process.argv.slice(2)) {
  const at = arg.lastIndexOf('@');
  const name = at > 0 ? arg.slice(0, at) : arg;
  const want = at > 0 ? arg.slice(at + 1) : '';
  if (/^\d+\.\d+\.\d+$/.test(want)) { picks.push(`${name}@${want}`); continue; } // exact version given
  const time = JSON.parse(execSync(`npm view ${name} time --json`, { encoding: 'utf8', maxBuffer: 1e8 }));
  const ok = Object.entries(time)
    .filter(([v, t]) => /^\d+\.\d+\.\d+$/.test(v) && Date.parse(t) < cutoff && (!want || v.split('.')[0] === want))
    .map(([v]) => v).sort(cmp);
  if (!ok.length) { console.error(`No ${name}${want ? '@' + want : ''} release older than 14 days`); process.exit(1); }
  picks.push(`${name}@${ok.at(-1)}`);
}
console.log(`npm install -D -E ${picks.join(' ')}`);