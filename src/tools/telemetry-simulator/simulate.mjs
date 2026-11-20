#!/usr/bin/env node

/**
 * ==============================================================================
 * simulate.mjs
 * Zero-dependency Node.js CLI Telemetry Simulator for PrIMA
 * Stage 1 Operational Telemetry & Scenario Exerciser
 * ==============================================================================
 */

const DEFAULT_URL = process.env.PRIMA_TELEMETRY_URL || 'http://localhost:5005/api/ingestion/telemetry';
const DEFAULT_KEY = 'prima-dev-key-alpha-99';

// Deterministic Detector Identifiers matching DevelopmentDemoDataSeeder
const DETECTORS = {
  billing: '3fa85f64-5717-4562-b3fc-2c963f66afa6',       // Billing Journal Balance Checker (Active, DataIntegrity)
  admission: 'e2c04068-8314-469b-8326-880bebbff71d',     // Patient Admission Discharge Sync (Active, ProcessIntegrity)
  prescription: 'a5f8b912-3c4d-4e5f-6a7b-8c9d0e1f2a3b',  // Prescription Stock Deductor (Active, DataSync)
  replication: 'b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c',   // EMR Database Replication Monitor (Active, Infrastructure)
  laboratory: 'c7b0d134-5e6f-6a7b-8c9d-0e1f2a3b4c5d'     // Laboratory Order Sync Prober (Paused, DataSync)
};

const SEEDED_ISSUES = {
  resolvedEntityRef: 'INV-2026-RESOLVED-001',
  dismissedEntityRef: 'ADM-2026-DISMISSED-001'
};

function parseArgs() {
  const args = process.argv.slice(2);
  let scenario = null;
  let url = DEFAULT_URL;
  let key = DEFAULT_KEY;

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '--help' || arg === '-h') {
      printHelp();
      process.exit(0);
    } else if (arg === '--url') {
      url = args[++i];
    } else if (arg.startsWith('--url=')) {
      url = arg.substring(6);
    } else if (arg === '--key') {
      key = args[++i];
    } else if (arg.startsWith('--key=')) {
      key = arg.substring(6);
    } else if (!arg.startsWith('-') && !scenario) {
      scenario = arg;
    }
  }

  return { scenario, url, key };
}

function printHelp() {
  console.log(`
PrIMA Telemetry Simulator CLI Utility
Usage:
  node simulate.mjs <scenario> [options]

Supported Scenarios:
  passed                 Submits inspection run with 0 findings (outcome = "Passed")
  new-finding            Submits finding with unique targetEntityReference creating an Open issue
  duplicate-finding      Submits same finding twice verifying OccurrenceCount increment and occurrence appending
  recurrence-resolved    Submits finding matching seeded Resolved issue to verify BR-PRIMA-002 reopening
  recurrence-dismissed   Submits finding matching seeded Dismissed issue to verify BR-PRIMA-002 reopening
  batch-findings         Submits multiple distinct findings in a single inspection run
  paused-detector        Submits telemetry to paused detector verifying 409 Conflict guardrail
  all                    Runs all scenarios sequentially

Options:
  --url <endpoint>       Ingestion telemetry endpoint URL (default: ${DEFAULT_URL})
  --key <api_key>        Detector Ingestion API Key (default: ${DEFAULT_KEY})
  -h, --help             Show this help message
`);
}

async function sendTelemetry(url, key, payload, scenarioLabel) {
  console.log(`\n==============================================================================`);
  console.log(`[Scenario: ${scenarioLabel}] Submitting Telemetry to: ${url}`);
  console.log(`Detector: ${payload.detectorId} | Outcome: ${payload.outcome} | Trigger: ${payload.triggerSource}`);
  if (payload.findings && payload.findings.length > 0) {
    console.log(`Findings count: ${payload.findings.length}`);
    payload.findings.forEach((f, idx) => {
      console.log(`  [${idx + 1}] Entity: ${f.targetEntityReference} | Title: "${f.title}" | Severity: ${f.baseSeverity}`);
    });
  } else {
    console.log(`Findings: 0 (Clean Passed run)`);
  }
  console.log(`------------------------------------------------------------------------------`);

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Detector-API-Key': key
      },
      body: JSON.stringify(payload)
    });

    console.log(`HTTP Status: ${response.status} ${response.statusText}`);

    const responseText = await response.text();
    let responseData = null;
    try {
      responseData = JSON.parse(responseText);
      console.log('Response Body:');
      console.log(JSON.stringify(responseData, null, 2));
    } catch {
      console.log('Raw Response:');
      console.log(responseText);
    }

    return { status: response.status, data: responseData };
  } catch (err) {
    console.error(`\n[ERROR] Connection failed: ${err.message}`);
    console.error(`Ensure PrIMA API backend is running at ${url}`);
    return { status: 0, error: err };
  }
}

async function runPassed(url, key) {
  const payload = {
    detectorId: DETECTORS.billing,
    executionTimestampUtc: new Date().toISOString(),
    triggerSource: 'Scheduled',
    outcome: 'Passed',
    durationMs: 145,
    failureReason: null,
    findings: []
  };

  return await sendTelemetry(url, key, payload, 'passed');
}

async function runNewFinding(url, key) {
  const uniqueSuffix = Date.now().toString().slice(-6);
  const entityRef = `INV-${uniqueSuffix}`;

  const payload = {
    detectorId: DETECTORS.billing,
    executionTimestampUtc: new Date().toISOString(),
    triggerSource: 'Scheduled',
    outcome: 'FindingDiscovered',
    durationMs: 380,
    failureReason: null,
    findings: [
      {
        targetEntityReference: entityRef,
        title: `Unbalanced Journal: Billing Invoice ${entityRef}`,
        baseSeverity: 'High',
        evidence: {
          module: 'Billing',
          sourceTable: 'acc_journal_entries',
          recordKeys: `invoice_id=${Math.floor(100000 + Math.random() * 900000)}, batch_no=B-${uniqueSuffix}`,
          debitTotal: 2500000.00,
          creditTotal: 2400000.00,
          varianceDelta: 100000.00,
          trace: 'Debit lines sum to 2,500,000 but credit lines sum to 2,400,000.'
        }
      }
    ]
  };

  return await sendTelemetry(url, key, payload, 'new-finding');
}

async function runDuplicateFinding(url, key) {
  const uniqueSuffix = Date.now().toString().slice(-6);
  const dupRef = `DUP-${uniqueSuffix}`;

  const payload = {
    detectorId: DETECTORS.billing,
    executionTimestampUtc: new Date().toISOString(),
    triggerSource: 'Scheduled',
    outcome: 'FindingDiscovered',
    durationMs: 310,
    failureReason: null,
    findings: [
      {
        targetEntityReference: dupRef,
        title: `Unbalanced Journal: Billing Invoice ${dupRef}`,
        baseSeverity: 'High',
        evidence: {
          module: 'Billing',
          sourceTable: 'acc_journal_entries',
          recordKeys: `invoice_id=883100, batch_no=B-${uniqueSuffix}`,
          debitTotal: 5000000.00,
          creditTotal: 4800000.00,
          varianceDelta: 200000.00,
          trace: 'Initial observation of unbalanced invoice lines.'
        }
      }
    ]
  };

  console.log(`\n[Step 1/2] Submitting initial finding for: ${dupRef}...`);
  const first = await sendTelemetry(url, key, payload, 'duplicate-finding (Initial)');

  console.log(`\n[Step 2/2] Submitting duplicate finding for: ${dupRef} (Expect Appended & Occurrence Increment)...`);
  const second = await sendTelemetry(url, key, {
    ...payload,
    executionTimestampUtc: new Date().toISOString(),
    durationMs: 290
  }, 'duplicate-finding (Duplicate)');

  return { first, second };
}

async function runRecurrenceResolved(url, key) {
  const payload = {
    detectorId: DETECTORS.billing,
    executionTimestampUtc: new Date().toISOString(),
    triggerSource: 'Scheduled',
    outcome: 'FindingDiscovered',
    durationMs: 410,
    failureReason: null,
    findings: [
      {
        targetEntityReference: SEEDED_ISSUES.resolvedEntityRef,
        title: `Unbalanced Journal: Billing Invoice ${SEEDED_ISSUES.resolvedEntityRef}`,
        baseSeverity: 'High',
        evidence: {
          module: 'Billing',
          sourceTable: 'acc_journal_entries',
          recordKeys: 'invoice_id=984321, batch_no=B20261120-1',
          debitTotal: 1750000.00,
          creditTotal: 1600000.00,
          varianceDelta: 150000.00,
          trace: 'Recurrent imbalance detected on invoice INV-2026-RESOLVED-001 after resolution.'
        }
      }
    ]
  };

  return await sendTelemetry(url, key, payload, 'recurrence-resolved (BR-PRIMA-002)');
}

async function runRecurrenceDismissed(url, key) {
  const payload = {
    detectorId: DETECTORS.admission,
    executionTimestampUtc: new Date().toISOString(),
    triggerSource: 'Scheduled',
    outcome: 'FindingDiscovered',
    durationMs: 275,
    failureReason: null,
    findings: [
      {
        targetEntityReference: SEEDED_ISSUES.dismissedEntityRef,
        title: `Stale Bed Occupancy: Admission ${SEEDED_ISSUES.dismissedEntityRef}`,
        baseSeverity: 'Medium',
        evidence: {
          module: 'Inpatient',
          sourceTable: 'bed_occupancies',
          recordKeys: 'bed_id=B-302, patient_id=P-8831',
          trace: 'Recurrent stale bed occupancy observed after prior dismissal.'
        }
      }
    ]
  };

  return await sendTelemetry(url, key, payload, 'recurrence-dismissed (BR-PRIMA-002)');
}

async function runBatchFindings(url, key) {
  const ts = Date.now().toString().slice(-5);
  const payload = {
    detectorId: DETECTORS.prescription,
    executionTimestampUtc: new Date().toISOString(),
    triggerSource: 'Scheduled',
    outcome: 'FindingDiscovered',
    durationMs: 520,
    failureReason: null,
    findings: [
      {
        targetEntityReference: `RX-${ts}-01`,
        title: 'Prescription Stock Mismatch: Drug Amoxicillin 500mg',
        baseSeverity: 'High',
        evidence: {
          module: 'Pharmacy',
          sourceTable: 'pharma_dispense_log',
          recordKeys: `prescription_id=PRX-${ts}-A`,
          trace: 'Dispensed 30 capsules but stock ledger deducted 20 capsules.'
        }
      },
      {
        targetEntityReference: `RX-${ts}-02`,
        title: 'Prescription Stock Mismatch: Drug Paracetamol 500mg',
        baseSeverity: 'Medium',
        evidence: {
          module: 'Pharmacy',
          sourceTable: 'pharma_dispense_log',
          recordKeys: `prescription_id=PRX-${ts}-B`,
          trace: 'Dispensed 10 tablets but stock ledger deducted 0 tablets.'
        }
      },
      {
        targetEntityReference: `RX-${ts}-03`,
        title: 'Prescription Stock Mismatch: Drug Omeprazole 20mg',
        baseSeverity: 'Low',
        evidence: {
          module: 'Pharmacy',
          sourceTable: 'pharma_dispense_log',
          recordKeys: `prescription_id=PRX-${ts}-C`,
          trace: 'Batch number discrepancy between prescription slip and inventory ledger.'
        }
      }
    ]
  };

  return await sendTelemetry(url, key, payload, 'batch-findings');
}

async function runPausedDetector(url, key) {
  const payload = {
    detectorId: DETECTORS.laboratory,
    executionTimestampUtc: new Date().toISOString(),
    triggerSource: 'Manual',
    outcome: 'Passed',
    durationMs: 80,
    failureReason: null,
    findings: []
  };

  return await sendTelemetry(url, key, payload, 'paused-detector (Guardrail 409)');
}

async function main() {
  const { scenario, url, key } = parseArgs();

  if (!scenario) {
    printHelp();
    process.exit(1);
  }

  console.log(`==============================================================================`);
  console.log(`PrIMA Telemetry Simulator`);
  console.log(`Scenario: ${scenario}`);
  console.log(`Endpoint: ${url}`);
  console.log(`API Key:  ${key}`);
  console.log(`Time:     ${new Date().toISOString()}`);

  switch (scenario.toLowerCase()) {
    case 'passed':
      await runPassed(url, key);
      break;

    case 'new-finding':
      await runNewFinding(url, key);
      break;

    case 'duplicate-finding':
      await runDuplicateFinding(url, key);
      break;

    case 'recurrence-resolved':
      await runRecurrenceResolved(url, key);
      break;

    case 'recurrence-dismissed':
      await runRecurrenceDismissed(url, key);
      break;

    case 'batch-findings':
      await runBatchFindings(url, key);
      break;

    case 'paused-detector':
      await runPausedDetector(url, key);
      break;

    case 'all':
      console.log('\n>>> Running all simulator scenarios sequentially...\n');
      await runPassed(url, key);
      await runNewFinding(url, key);
      await runDuplicateFinding(url, key);
      await runRecurrenceResolved(url, key);
      await runRecurrenceDismissed(url, key);
      await runBatchFindings(url, key);
      await runPausedDetector(url, key);
      console.log('\n>>> All scenarios completed.');
      break;

    default:
      console.error(`\n[ERROR] Unknown scenario: "${scenario}".`);
      printHelp();
      process.exit(1);
  }
}

main().catch(err => {
  console.error('[FATAL]', err);
  process.exit(1);
});
