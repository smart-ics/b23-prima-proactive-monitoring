import { readdirSync, readFileSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, extname, relative, resolve } from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const FRONTEND_ROOT = resolve(__dirname, '..');
const SRC_ROOT = resolve(FRONTEND_ROOT, 'src');
const FEATURES_ROOT = resolve(SRC_ROOT, 'features');

const ALLOWED_EXTENSIONS = new Set(['.vue', '.ts', '.tsx', '.js', '.jsx', '.mjs']);
const FEATURE_SLICES = new Set(['auth', 'dashboard', 'issues', 'detectors', 'history']);
const NO_PAGINATOR_SLICES = new Set(['dashboard', 'detectors', 'history']);

const FORBIDDEN_CHART_PACKAGES = [
  'chart.js',
  'echarts',
  'apexcharts',
  'vue-chartjs',
  'vue-echarts',
  'vue3-apexcharts',
  'recharts',
  'highcharts',
  'd3',
  'plotly.js',
  'chartist',
  'frappe-charts',
];

function toPosix(p) {
  return p.replaceAll('\\', '/');
}

function walkFiles(dir) {
  const results = [];
  const entries = readdirSync(dir);
  for (const entry of entries) {
    const fullPath = resolve(dir, entry);
    const stat = statSync(fullPath);
    if (stat.isDirectory()) {
      results.push(...walkFiles(fullPath));
    } else if (ALLOWED_EXTENSIONS.has(extname(entry))) {
      results.push(fullPath);
    }
  }
  return results;
}

function getSliceName(filePath) {
  const rel = toPosix(relative(FEATURES_ROOT, filePath));
  if (rel.startsWith('..')) return null;
  const [slice] = rel.split('/');
  return FEATURE_SLICES.has(slice) ? slice : null;
}

function extractModuleSpecifiers(line) {
  const specifiers = [];
  const patterns = [
    /(?:^|[\s}*])from\s+['"]([^'"]+)['"]/g,
    /\bimport\s+['"]([^'"]+)['"]/g,
    /\bimport\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
    /\brequire\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
  ];
  for (const regex of patterns) {
    let match;
    while ((match = regex.exec(line)) !== null) {
      specifiers.push(match[1]);
    }
  }
  return specifiers;
}

function isChartLibraryImport(specifier) {
  return FORBIDDEN_CHART_PACKAGES.some(
    (pkg) => specifier === pkg || specifier.startsWith(`${pkg}/`),
  );
}

function resolveCrossSliceTarget(filePath, currentSlice, specifier) {
  if (specifier.startsWith('@/features/')) {
    const targetSlice = specifier.slice('@/features/'.length).split('/')[0];
    if (FEATURE_SLICES.has(targetSlice) && targetSlice !== currentSlice) {
      return targetSlice;
    }
    return null;
  }

  if (specifier.startsWith('.')) {
    const resolved = resolve(dirname(filePath), specifier);
    const targetSlice = getSliceName(resolved);
    if (targetSlice && targetSlice !== currentSlice) {
      return targetSlice;
    }
  }

  return null;
}

function checkPaginatorInTemplate(filePath, lines, violations) {
  let inTemplate = false;
  let inHtmlComment = false;
  let templateDepth = 0;

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];
    const trimmed = line.trim();

    if (!inTemplate) {
      if (/^<template(?:\s|>|$)/.test(trimmed)) {
        inTemplate = true;
        templateDepth = 1;
      }
      continue;
    }

    // Track nested <template> tags inside SFC root <template>
    const openMatches = line.match(/<template(?:\s|>|$)/g);
    if (openMatches) {
      templateDepth += openMatches.length;
    }
    const closeMatches = line.match(/<\/template>/g);
    if (closeMatches) {
      templateDepth -= closeMatches.length;
      if (templateDepth <= 0) {
        inTemplate = false;
        templateDepth = 0;
        continue;
      }
    }

    if (inHtmlComment) {
      if (line.includes('-->')) {
        inHtmlComment = false;
      }
      continue;
    }
    if (trimmed.startsWith('<!--')) {
      if (!line.includes('-->')) {
        inHtmlComment = true;
      }
      continue;
    }

    const sanitized = line.replace(/<!--[\s\S]*?-->/g, '');
    if (/(?:^|\s)(?::|v-bind:)?paginator(?:\s*=|\s|>|\/|$)/.test(sanitized)) {
      violations.push({
        rule: 'no-paginator',
        file: toPosix(relative(FRONTEND_ROOT, filePath)),
        line: i + 1,
        message: 'Forbidden `paginator` attribute/prop in S-01/S-03/S-06/S-07 Vue template (use virtual scrolling).',
        snippet: trimmed,
      });
    }
  }
}

function auditFile(filePath, violations) {
  const content = readFileSync(filePath, 'utf8');
  const lines = content.split(/\r?\n/);
  const relPath = toPosix(relative(FRONTEND_ROOT, filePath));
  const currentSlice = getSliceName(filePath);
  const ext = extname(filePath);

  if (ext === '.vue' && currentSlice && NO_PAGINATOR_SLICES.has(currentSlice)) {
    checkPaginatorInTemplate(filePath, lines, violations);
  }

  let inBlockComment = false;

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];
    const trimmed = line.trim();

    if (inBlockComment) {
      if (line.includes('*/')) {
        inBlockComment = false;
      }
      continue;
    }
    if (trimmed.startsWith('/*')) {
      if (!line.includes('*/')) {
        inBlockComment = true;
      }
      continue;
    }
    if (trimmed.startsWith('//') || trimmed.startsWith('*')) {
      continue;
    }

    // Rule 2 & Rule 4: Module specifiers (external chart libraries + cross-slice imports)
    const specifiers = extractModuleSpecifiers(line);
    for (const specifier of specifiers) {
      if (isChartLibraryImport(specifier)) {
        violations.push({
          rule: 'no-external-charts',
          file: relPath,
          line: i + 1,
          message: `Forbidden external charting library import '${specifier}' (use pure CSS proportion bars).`,
          snippet: trimmed,
        });
      }

      if (currentSlice) {
        const targetSlice = resolveCrossSliceTarget(filePath, currentSlice, specifier);
        if (targetSlice) {
          violations.push({
            rule: 'no-cross-slice-imports',
            file: relPath,
            line: i + 1,
            message: `Forbidden cross-slice import from '${currentSlice}' to '${targetSlice}' ('${specifier}').`,
            snippet: trimmed,
          });
        }
      }
    }

    // Rule 3: Native window.alert() or window.confirm()
    const withoutStrings = line
      .replace(/'(?:\\.|[^'\\])*'/g, "''")
      .replace(/"(?:\\.|[^"\\])*"/g, '""')
      .replace(/`(?:\\.|[^`\\])*`/g, '``')
      .replace(/\/\/.*$/, '');

    if (/\bwindow\.(?:alert|confirm)\s*\(/.test(withoutStrings) || /(?<![.\w$])(?:alert|confirm)\s*\(/.test(withoutStrings)) {
      violations.push({
        rule: 'no-native-dialogs',
        file: relPath,
        line: i + 1,
        message: 'Forbidden native browser dialog call (`window.alert` / `window.confirm`); use PrimeVue Toast/ConfirmDialog.',
        snippet: trimmed,
      });
    }
  }
}

function main() {
  const files = walkFiles(SRC_ROOT);
  const violations = [];

  for (const file of files) {
    auditFile(file, violations);
  }

  const ruleCounts = {
    'no-paginator': violations.filter((v) => v.rule === 'no-paginator').length,
    'no-external-charts': violations.filter((v) => v.rule === 'no-external-charts').length,
    'no-native-dialogs': violations.filter((v) => v.rule === 'no-native-dialogs').length,
    'no-cross-slice-imports': violations.filter((v) => v.rule === 'no-cross-slice-imports').length,
  };

  console.log('=== PrIMA Frontend Static Compliance Audit (DEC-008) ===');
  console.log(`Scanned files: ${files.length} under src/frontend/src/`);
  console.log(`- [no-paginator]           S-01/S-03/S-06/S-07 zero pagination props : ${ruleCounts['no-paginator']} violations`);
  console.log(`- [no-external-charts]     Zero external charting library imports    : ${ruleCounts['no-external-charts']} violations`);
  console.log(`- [no-native-dialogs]      Zero window.alert() / window.confirm()      : ${ruleCounts['no-native-dialogs']} violations`);
  console.log(`- [no-cross-slice-imports] Zero cross-slice feature imports            : ${ruleCounts['no-cross-slice-imports']} violations`);

  if (violations.length > 0) {
    console.error(`\nFAILED: Found ${violations.length} compliance violation(s):`);
    for (const v of violations) {
      console.error(`  [${v.rule}] ${v.file}:${v.line} — ${v.message}`);
      console.error(`    > ${v.snippet}`);
    }
    process.exit(1);
  }

  console.log('\nPASSED: 0 compliance violations found.');
  process.exit(0);
}

main();
