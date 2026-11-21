import { describe, it, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { getWibDateRange } from './dateUtils.ts';

describe('getWibDateRange', () => {
  afterEach((t) => {
    t.mock.timers.reset();
  });

  it('Today: handles early morning WIB after midnight WIB but before UTC midnight', (t) => {
    // 2026-11-22 18:00:00.000Z === 2026-11-23 01:00:00.000 WIB
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 22, 18, 0, 0, 0) });

    assert.deepEqual(getWibDateRange('Today'), {
      fromUtc: '2026-11-22T17:00:00.000Z',
      toUtc: '2026-11-23T16:59:59.999Z',
    });
  });

  it('Today: handles exact WIB midnight and 1ms before WIB midnight', (t) => {
    // Exact WIB midnight: 2026-11-23 00:00:00.000 WIB
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 22, 17, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('Today'), {
      fromUtc: '2026-11-22T17:00:00.000Z',
      toUtc: '2026-11-23T16:59:59.999Z',
    });

    // 1ms before WIB midnight: 2026-11-22 23:59:59.999 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 22, 16, 59, 59, 999) });
    assert.deepEqual(getWibDateRange('Today'), {
      fromUtc: '2026-11-21T17:00:00.000Z',
      toUtc: '2026-11-22T16:59:59.999Z',
    });
  });

  it('This Week: returns Monday 00:00 WIB to today 23:59:59.999 WIB on Monday and Sunday', (t) => {
    // Monday 2026-11-23 08:00:00 WIB === 2026-11-23 01:00:00 UTC
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 23, 1, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('This Week'), {
      fromUtc: '2026-11-22T17:00:00.000Z',
      toUtc: '2026-11-23T16:59:59.999Z',
    });

    // Sunday 2026-11-22 23:30:00 WIB === 2026-11-22 16:30:00 UTC -> Monday is 2026-11-16 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 22, 16, 30, 0, 0) });
    assert.deepEqual(getWibDateRange('This Week'), {
      fromUtc: '2026-11-15T17:00:00.000Z',
      toUtc: '2026-11-22T16:59:59.999Z',
    });
  });

  it('This Week: accurately crosses month and year boundaries', (t) => {
    // Wednesday 2026-04-01 09:00:00 WIB -> Monday is 2026-03-30 WIB
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 3, 1, 2, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('This Week'), {
      fromUtc: '2026-03-29T17:00:00.000Z',
      toUtc: '2026-04-01T16:59:59.999Z',
    });

    // Thursday 2026-01-01 02:00:00 WIB (2025-12-31 19:00 UTC) -> Monday is 2025-12-29 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2025, 11, 31, 19, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('This Week'), {
      fromUtc: '2025-12-28T17:00:00.000Z',
      toUtc: '2026-01-01T16:59:59.999Z',
    });
  });

  it('This Month: anchors 1st calendar day in WIB across UTC month boundary', (t) => {
    // 2026-12-01 01:00:00 WIB === 2026-11-30 18:00:00 UTC
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 30, 18, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('This Month'), {
      fromUtc: '2026-11-30T17:00:00.000Z',
      toUtc: '2026-12-01T16:59:59.999Z',
    });

    // Mid-month: 2026-11-22 10:00:00 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 22, 3, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('This Month'), {
      fromUtc: '2026-10-31T17:00:00.000Z',
      toUtc: '2026-11-22T16:59:59.999Z',
    });
  });

  it('Last Month: handles 30/31-day months, January year rollover, and leap vs non-leap February', (t) => {
    // Nov 2026 -> Oct 1..31 2026 WIB
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 15, 3, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('Last Month'), {
      fromUtc: '2026-09-30T17:00:00.000Z',
      toUtc: '2026-10-31T16:59:59.999Z',
    });

    // May 2026 -> Apr 1..30 2026 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 4, 10, 3, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('Last Month'), {
      fromUtc: '2026-03-31T17:00:00.000Z',
      toUtc: '2026-04-30T16:59:59.999Z',
    });

    // Jan 1 2026 01:00 WIB (2025-12-31 18:00 UTC) -> Dec 1..31 2025 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2025, 11, 31, 18, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('Last Month'), {
      fromUtc: '2025-11-30T17:00:00.000Z',
      toUtc: '2025-12-31T16:59:59.999Z',
    });

    // March 2024 (leap year) -> Feb 1..29 2024 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2024, 2, 1, 5, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('Last Month'), {
      fromUtc: '2024-01-31T17:00:00.000Z',
      toUtc: '2024-02-29T16:59:59.999Z',
    });

    // March 2026 (non-leap year) -> Feb 1..28 2026 WIB
    t.mock.timers.reset();
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 2, 1, 5, 0, 0, 0) });
    assert.deepEqual(getWibDateRange('Last Month'), {
      fromUtc: '2026-01-31T17:00:00.000Z',
      toUtc: '2026-02-28T16:59:59.999Z',
    });
  });

  it('Custom: converts start/end dates without mutating inputs and falls back to Today WIB when incomplete', (t) => {
    t.mock.timers.enable({ apis: ['Date'], now: Date.UTC(2026, 10, 22, 5, 0, 0, 0) });

    const startInput = new Date(2026, 10, 10, 14, 30, 15, 500);
    const endInput = new Date(2026, 10, 12, 8, 15, 0, 0);
    const expectedStart = new Date(startInput.getTime());
    expectedStart.setHours(0, 0, 0, 0);
    const expectedEnd = new Date(endInput.getTime());
    expectedEnd.setHours(23, 59, 59, 999);

    assert.deepEqual(getWibDateRange('Custom', [startInput, endInput]), {
      fromUtc: expectedStart.toISOString(),
      toUtc: expectedEnd.toISOString(),
    });

    // Verify input Date objects were not mutated
    assert.equal(startInput.getHours(), 14);
    assert.equal(startInput.getMinutes(), 30);
    assert.equal(endInput.getHours(), 8);
    assert.equal(endInput.getMinutes(), 15);

    // Partial/missing/invalid ranges fall back to Today WIB
    const todayRange = {
      fromUtc: '2026-11-21T17:00:00.000Z',
      toUtc: '2026-11-22T16:59:59.999Z',
    };
    assert.deepEqual(getWibDateRange('Custom'), todayRange);
    assert.deepEqual(getWibDateRange('Custom', [startInput, null]), todayRange);
    assert.deepEqual(getWibDateRange('Custom', [null, endInput]), todayRange);
    assert.deepEqual(getWibDateRange('Custom', [new Date('invalid'), endInput]), todayRange);
  });
});
