import { spawnSync } from 'node:child_process';

const arg = (name, fallback) => {
  const i = process.argv.indexOf(`--${name}`);
  return i > -1 ? process.argv[i + 1] : fallback;
};
const times = Number(arg('times', '1'));
const zones = arg('zones', 'UTC,Africa/Johannesburg').split(',');
let failed = 0;
for (const tz of zones) {
  for (let i = 1; i <= times; i++) {
    const r = spawnSync('npx', ['vitest', 'run'], { stdio: 'inherit', shell: true, env: { ...process.env, TZ: tz } });
    console.log(`>>> TZ=${tz} run ${i}/${times}: ${r.status === 0 ? 'PASS' : 'FAIL'}`);
    if (r.status !== 0) failed++;
  }
}
console.log(failed === 0 ? '>>> ALL RUNS PASSED' : `>>> ${failed} RUN(S) FAILED`);
process.exit(failed === 0 ? 0 : 1);