import { mkdirSync, rmSync } from 'node:fs';

export const COVERAGE_DIR = new URL('./test-results-coverage/', import.meta.url);

export default function setup() {
  rmSync(COVERAGE_DIR, { recursive: true, force: true });
  mkdirSync(COVERAGE_DIR, { recursive: true });
}
