/**
 * Parse Apple Health export.xml (from Settings → Health → Export All Health Data).
 * Runs entirely in the browser — no data leaves your device.
 */

const METRIC_TYPES = {
  hrv: ['HKQuantityTypeIdentifierHeartRateVariabilitySDNN'],
  restingHr: ['HKQuantityTypeIdentifierRestingHeartRate'],
  vo2max: ['HKQuantityTypeIdentifierVO2Max'],
};

function parseValue(record) {
  const v = parseFloat(record.getAttribute('value'));
  return Number.isNaN(v) ? null : v;
}

function parseDate(record) {
  const start = record.getAttribute('startDate');
  return start ? new Date(start) : null;
}

function latestByType(records, typeIds) {
  const filtered = records.filter((r) => typeIds.includes(r.getAttribute('type')));
  if (!filtered.length) return null;
  filtered.sort((a, b) => {
    const da = parseDate(a)?.getTime() || 0;
    const db = parseDate(b)?.getTime() || 0;
    return db - da;
  });
  return parseValue(filtered[0]);
}

function overnightSleepHours(records, targetDate) {
  const sleepRecords = records.filter((r) => {
    const type = r.getAttribute('type');
    return type === 'HKCategoryTypeIdentifierSleepAnalysis';
  });

  if (!sleepRecords.length) return null;

  const dayStart = new Date(`${targetDate}T00:00:00`);
  const windowStart = new Date(dayStart);
  windowStart.setHours(windowStart.getHours() - 12);
  const windowEnd = new Date(dayStart);
  windowEnd.setHours(windowEnd.getHours() + 12);

  let totalMs = 0;
  for (const r of sleepRecords) {
    const start = parseDate(r);
    const endStr = r.getAttribute('endDate');
    const end = endStr ? new Date(endStr) : null;
    const value = r.getAttribute('value');
    if (!start || !end) continue;
    if (value === 'HKCategoryValueSleepAnalysisAwake') continue;
    if (end < windowStart || start > windowEnd) continue;
    const overlapStart = Math.max(start.getTime(), windowStart.getTime());
    const overlapEnd = Math.min(end.getTime(), windowEnd.getTime());
    if (overlapEnd > overlapStart) totalMs += overlapEnd - overlapStart;
  }

  return totalMs > 0 ? totalMs / 3600000 : null;
}

export async function parseHealthExport(file, targetDate = null) {
  let xmlText;

  if (file.name.endsWith('.zip')) {
    throw new Error(
      'Please unzip the Apple Health export on your iPhone (Files app) and select export.xml inside.'
    );
  }

  xmlText = await file.text();

  const parser = new DOMParser();
  const doc = parser.parseFromString(xmlText, 'text/xml');
  if (doc.querySelector('parsererror')) {
    throw new Error('Invalid Apple Health export file.');
  }

  const records = [...doc.querySelectorAll('Record')];
  const date = targetDate || new Date().toISOString().slice(0, 10);

  const result = {
    source: 'apple-health',
    date,
    hrv: latestByType(records, METRIC_TYPES.hrv),
    restingHr: latestByType(records, METRIC_TYPES.restingHr),
    vo2max: latestByType(records, METRIC_TYPES.vo2max),
    sleepDuration: overnightSleepHours(records, date),
    recordCount: records.length,
  };

  return result;
}

export function healthToEntry(healthData) {
  return {
    hrv: healthData.hrv ?? null,
    restingHr: healthData.restingHr ?? null,
    vo2max: healthData.vo2max ?? null,
    sleepDuration: healthData.sleepDuration ?? null,
    healthImportAt: new Date().toISOString(),
  };
}
