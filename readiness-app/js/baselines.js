/**
 * Personal baseline and trend calculations.
 * Uses stored entries — never generic population norms.
 */

export function rollingMean(values, window) {
  if (!values.length) return null;
  const slice = values.slice(-window);
  if (!slice.length) return null;
  return slice.reduce((a, b) => a + b, 0) / slice.length;
}

export function rollingValues(entries, field, beforeDate, window) {
  const nums = entries
    .filter((e) => e.date < beforeDate && e[field] != null && !Number.isNaN(Number(e[field])))
    .map((e) => Number(e[field]))
    .slice(-window);
  return nums;
}

export function computeBaselines(entries, beforeDate) {
  const hrv7 = rollingMean(rollingValues(entries, 'hrv', beforeDate, 7), 7);
  const hrv14 = rollingMean(rollingValues(entries, 'hrv', beforeDate, 14), 14);
  const hrv30 = rollingMean(rollingValues(entries, 'hrv', beforeDate, 30), 30);
  const rhr7 = rollingMean(rollingValues(entries, 'restingHr', beforeDate, 7), 7);
  const rhr14 = rollingMean(rollingValues(entries, 'restingHr', beforeDate, 14), 14);
  const rhr30 = rollingMean(rollingValues(entries, 'restingHr', beforeDate, 30), 30);
  const cv7 = rollingMean(rollingValues(entries, 'hrvCv', beforeDate, 7), 7);
  const sleep7 = rollingMean(rollingValues(entries, 'sleep', beforeDate, 7), 7);
  const recovery7 = rollingMean(rollingValues(entries, 'recovery', beforeDate, 7), 7);
  const strain7 = rollingMean(rollingValues(entries, 'strain', beforeDate, 7), 7);

  return {
    hrv: { d7: hrv7, d14: hrv14, d30: hrv30 },
    restingHr: { d7: rhr7, d14: rhr14, d30: rhr30 },
    hrvCv: { d7: cv7 },
    sleep: { d7: sleep7 },
    recovery: { d7: recovery7 },
    strain: { d7: strain7 },
    entryCount: entries.filter((e) => e.date < beforeDate).length,
  };
}

export function hrvVsBaseline(todayHrv, baselines) {
  const ref = baselines.hrv.d7 ?? baselines.hrv.d14 ?? baselines.hrv.d30;
  if (ref == null || todayHrv == null) {
    return { delta: null, pct: null, ref, label: 'insufficient baseline data' };
  }
  const delta = todayHrv - ref;
  const pct = (delta / ref) * 100;
  let label;
  if (pct >= 10) label = 'well above your baseline';
  else if (pct >= 3) label = 'slightly above your baseline';
  else if (pct >= -3) label = 'in line with your baseline';
  else if (pct >= -10) label = 'slightly below your baseline';
  else label = 'below your baseline';
  return { delta, pct, ref, label };
}

export function rhrVsBaseline(todayRhr, baselines) {
  const ref = baselines.restingHr.d7 ?? baselines.restingHr.d14 ?? baselines.restingHr.d30;
  if (ref == null || todayRhr == null) {
    return { delta: null, ref, label: 'insufficient baseline data' };
  }
  const delta = todayRhr - ref;
  let label;
  if (delta <= -3) label = 'encouraging — below your recent average';
  else if (delta <= 3) label = 'in line with your recent average';
  else label = 'elevated vs your recent average';
  return { delta, ref, label };
}

export function cvStability(todayCv, baselines) {
  if (todayCv == null) return { label: 'no data', level: 'unknown' };
  const ref = baselines.hrvCv.d7;
  let level;
  if (todayCv <= 20) level = 'stable';
  else if (todayCv <= 28) level = 'moderate';
  else level = 'high';

  let label;
  if (level === 'stable') label = 'relatively stable';
  else if (level === 'moderate') label = 'moderate variability — monitor trend';
  else label = 'relatively high variability — monitor as a trend';

  if (ref != null && todayCv > ref + 5) {
    label += ' (above your recent average)';
  }
  return { label, level, ref };
}

export function trendDirection(entries, field, window = 7) {
  const vals = entries
    .filter((e) => e[field] != null)
    .slice(-window)
    .map((e) => Number(e[field]));
  if (vals.length < 4) return 'insufficient data';
  const firstHalf = vals.slice(0, Math.floor(vals.length / 2));
  const secondHalf = vals.slice(Math.floor(vals.length / 2));
  const avgFirst = firstHalf.reduce((a, b) => a + b, 0) / firstHalf.length;
  const avgSecond = secondHalf.reduce((a, b) => a + b, 0) / secondHalf.length;
  const diff = avgSecond - avgFirst;
  const threshold = Math.abs(avgFirst) * 0.05 || 1;
  if (Math.abs(diff) < threshold) return 'stable';
  return diff > 0 ? 'rising' : 'falling';
}

export function isCalibrating(entryCount, settings) {
  if (settings.calibrationStart) {
    const start = new Date(settings.calibrationStart);
    const days = Math.floor((Date.now() - start.getTime()) / 86400000);
    if (days < 14) return { active: true, daysLeft: 14 - days, reason: 'calibration period active' };
  }
  if (entryCount < 7) {
    return { active: true, daysLeft: 7 - entryCount, reason: 'building baseline — need more mornings' };
  }
  return { active: false, daysLeft: 0, reason: null };
}
