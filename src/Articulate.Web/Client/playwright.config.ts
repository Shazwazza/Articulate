import { defineConfig } from '@playwright/test';

const baseURL = process.env.ARTICULATE_E2E_BASE_URL;
const candidateProject = process.env.ARTICULATE_E2E_CANDIDATE_PROJECT;
const candidatePort = process.env.ARTICULATE_E2E_CANDIDATE_PORT;
const canonicalLane = baseURL === 'https://localhost:19443' ? 'v17' : baseURL === 'https://localhost:19444' ? 'v18' : undefined;
const candidateMatch = /^https:\/\/localhost:(\d{1,5})$/.exec(baseURL ?? '');
const candidateLane = candidateProject?.match(/^art_e2e_(?:candidate|native)_(v17|v18)_[a-f0-9]{32}$/)?.[1];
const candidatePortIsValid = candidateMatch !== null && candidatePort === candidateMatch[1]
  && Number(candidatePort) >= 1024 && Number(candidatePort) <= 65535
  && !['18443', '18444', '19443', '19444'].includes(candidatePort);

if (canonicalLane) {
  if (candidateProject || candidatePort) throw new Error('Candidate settings cannot be combined with canonical E2E ports');
} else if (!candidateLane || !candidatePortIsValid || !baseURL || !baseURL.startsWith(`https://localhost:${candidatePort}`)) {
  throw new Error('E2E requires canonical port 19443/19444 or an owned localhost candidate port and project');
}

const lane = canonicalLane ?? candidateLane;
if (!lane || !baseURL) throw new Error('E2E lane and URL are required');
const reportDirectory = process.env.PLAYWRIGHT_HTML_OUTPUT_DIR ?? `e2e-report-${lane}`;
const resultsDirectory = process.env.PLAYWRIGHT_OUTPUT_DIR ?? `e2e-results-${lane}`;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { outputFolder: reportDirectory, open: 'never' }]],
  outputDir: resultsDirectory,
  use: {
    baseURL,
    ignoreHTTPSErrors: true,
    extraHTTPHeaders: { Accept: 'text/html,application/rss+xml,application/xml,*/*' },
  },
});
