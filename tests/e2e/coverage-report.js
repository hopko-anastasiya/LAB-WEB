import { readdirSync, readFileSync } from 'node:fs';
import { COVERAGE_DIR } from './coverage-setup.js';

function markCovered(entry) {
  const covered = new Uint8Array(entry.source.length);
  const functions = [...entry.functions].sort((a, b) => a.ranges[0].startOffset - b.ranges[0].startOffset);
  for (const fn of functions) {
    for (const range of fn.ranges) covered.fill(range.count > 0 ? 1 : 0, range.startOffset, range.endOffset);
  }
  return covered;
}

export default function report() {
  const scripts = new Map();
  for (const file of readdirSync(COVERAGE_DIR)) {
    for (const entry of JSON.parse(readFileSync(new URL(file, COVERAGE_DIR), 'utf8'))) {
      const name = new URL(entry.url).pathname;
      const covered = markCovered(entry);
      const known = scripts.get(name);
      if (known) known.forEach((value, i) => { known[i] = value | covered[i]; });
      else scripts.set(name, covered);
    }
  }

  let used = 0;
  let total = 0;
  console.log('\nПокриття JavaScript сценаріями E2E:');
  for (const [name, covered] of [...scripts].sort()) {
    const count = covered.reduce((sum, value) => sum + value, 0);
    used += count;
    total += covered.length;
    console.log(`  ${String(Math.round(100 * count / covered.length)).padStart(3)}%  ${name}`);
  }
  if (total) console.log(`  ${String(Math.round(100 * used / total)).padStart(3)}%  усього\n`);
}
