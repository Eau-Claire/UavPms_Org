#!/usr/bin/env node
/**
 * Deterministic PreToolUse Guardrail for UAV-PMS (Backend)
 * Blocks coding agents from editing secrets, SDD specs, constitution,
 * locked Gate 5 / acceptance tests, or reading external Holdout scenarios.
 * Exit 2 = deterministic block (compatible with Claude Code "onFailure": "block").
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HOOK_DIR = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(HOOK_DIR, '..', '..');
const LOCKED_TESTS_FILE = path.join(REPO_ROOT, '.claude', 'locked-tests.txt');
const PHASE = (process.env.SDD_PHASE || 'CODE').toUpperCase();

function failBlock(message) {
  process.stderr.write(`BLOCKED BY HARNESS (${PHASE}): ${message}\n`);
  process.exit(2);
}

function normalizePath(rawPath) {
  return String(rawPath || '').replace(/\\/g, '/').trim();
}

function toRepoRelative(normalizedPath) {
  const repoNorm = REPO_ROOT.replace(/\\/g, '/').replace(/\/+$/, '');
  if (normalizedPath.toLowerCase().startsWith(repoNorm.toLowerCase() + '/')) {
    return normalizedPath.slice(repoNorm.length + 1);
  }
  return normalizedPath.replace(/^\.\//, '');
}

function loadLockedTests() {
  if (!fs.existsSync(LOCKED_TESTS_FILE)) return [];
  const raw = fs.readFileSync(LOCKED_TESTS_FILE, 'utf8');
  return raw
    .split(/\r?\n/)
    .map((line) => line.split('#')[0].trim().replace(/\\/g, '/').replace(/^\.\//, ''))
    .filter(Boolean);
}

let rawInput = '';
try {
  rawInput = fs.readFileSync(0, 'utf8');
} catch {
  failBlock('Khong doc duoc payload JSON tu stdin.');
}

let payload;
try {
  payload = JSON.parse(rawInput.replace(/^\uFEFF/, '') || '{}');
} catch {
  failBlock('Payload stdin khong phai JSON hop le — mac dinh chan an toan.');
}

const toolName = String(payload?.tool_name || '');
const toolInput = payload?.tool_input || {};
const filePath = normalizePath(toolInput.file_path ?? toolInput.path ?? '');
const relPath = toRepoRelative(filePath);
const bashCmd = String(toolInput.command || '');

if (toolName === 'Edit' || toolName === 'Write' || toolName === 'MultiEdit') {
  if (!filePath) {
    failBlock('Thieu file_path trong lenh sua file.');
  }

  // 1. Always blocked regardless of SDD_PHASE
  const isEnvFile = /(^|\/)\.env($|\.)/i.test(relPath) && !/(^|\/)\.env\.example$/i.test(relPath);
  const alwaysBlocked = [
    { match: isEnvFile, label: '.env secret file' },
    { match: /(^|\/)\.git(\/|$)/i.test(relPath), label: '.git internals' },
    { match: /(^|\/)secrets(\/|$)/i.test(relPath), label: 'secrets directory' },
    { match: /(^|\/)(package-lock\.json|pnpm-lock\.yaml)$/i.test(relPath), label: 'dependency lockfile' },
    { match: /(^|\/)\.claude\/hooks(\/|$)/i.test(relPath), label: 'harness hook script' },
    { match: /(^|\/)\.claude\/settings\.json$/i.test(relPath), label: 'harness settings.json' },
    { match: /uavpms_holdout/i.test(filePath), label: 'external sealed UavPms_Holdout' },
  ];

  for (const rule of alwaysBlocked) {
    if (rule.match) {
      failBlock(`Tuyet doi khong duoc sua ${rule.label} ('${filePath}').`);
    }
  }

  // 2. Spec & Constitution layer (only editable when SDD_PHASE=SPEC)
  const specProtected = [
    /(^|\/)\.specify\/memory\/constitution\.md$/i.test(relPath),
    /(^|\/)specs(\/|$)/i.test(relPath),
    /(^|\/)docs\/MASTER_SYSTEM_SPEC\.md$/i.test(relPath),
    /(^|\/)docs\/AI_SECURITY_AND_GOVERNANCE\.md$/i.test(relPath),
  ];

  if (PHASE !== 'SPEC' && specProtected.some(Boolean)) {
    failBlock(`Khong duoc sua tai lieu Spec/Constitution '${relPath}' trong pha ${PHASE}. Chi sua ma nguon dap ung Spec.`);
  }

  // 3. Locked Acceptance / Gate 5 Tests (only editable when SDD_PHASE=RED)
  const lockedList = loadLockedTests();
  const isInLockedList = lockedList.some((entry) => relPath.toLowerCase() === entry.toLowerCase());
  const testProtected =
    /(^|\/)tests\/acceptance(\/|$)/i.test(relPath) ||
    /Gate5.*Tests\.cs$/i.test(relPath) ||
    isInLockedList;

  if (PHASE !== 'RED' && testProtected) {
    failBlock(`File test Gate 5 / Acceptance '${relPath}' da khoa (pha ${PHASE}). Khong duoc sua test de ep xanh.`);
  }
}

if (toolName === 'Bash') {
  const normalizedCmd = bashCmd.replace(/\\/g, '/');

  if (/uavpms_holdout/i.test(normalizedCmd)) {
    failBlock('Cam doc/ghi thu muc nghiem thu bi mat UavPms_Holdout.');
  }

  const dangerousPatterns = [
    /\brm\s+-[^\n]*r[^\n]*f[^\n]*\s+(?:\/|~|\.\.)/i,
    /\bgit\s+push\b[^\n]*(?:\s--force\b|\s-f\b)/i,
    /\bgit\s+commit\b[^\n]*(?:\s--no-verify\b|\s-n\b)/i,
    /\bDROP\s+(?:TABLE|DATABASE)\b/i,
  ];

  for (const pattern of dangerousPatterns) {
    if (pattern.test(normalizedCmd)) {
      failBlock(`Lenh Bash bi cam vi rui ro pha hoai hoac vuot rao git/DB hook: ${bashCmd.slice(0, 120)}`);
    }
  }

  const writeVerbPattern = /(?:sed\s+-i|>>?|tee\s+|mv\s+|rm\s+|cp\s+|Set-Content|Add-Content|Out-File|Remove-Item)/i;
  const protectedShellTargets = /(?:^|[\s"'/])(?:\.env(?:\.|\b)|specs\/|\.specify\/|tests\/acceptance\/|\.claude\/hooks\/|\.claude\/settings\.json)/i;
  if (writeVerbPattern.test(normalizedCmd) && protectedShellTargets.test(normalizedCmd)) {
    const allowSpecShell = PHASE === 'SPEC' && /specs\//i.test(normalizedCmd) && !/\.env|\.specify|\.claude|tests\/acceptance/i.test(normalizedCmd);
    const allowRedShell = PHASE === 'RED' && /tests\/acceptance\//i.test(normalizedCmd) && !/\.env|\.specify|\.claude|specs\//i.test(normalizedCmd);
    if (!allowSpecShell && !allowRedShell) {
      failBlock(`Cam dung Bash sua/xoa truc tiep tep duoc bao ve trong pha ${PHASE}.`);
    }
  }
}

process.exit(0);
