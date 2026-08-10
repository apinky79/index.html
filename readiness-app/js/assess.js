import {
  computeBaselines,
  hrvVsBaseline,
  rhrVsBaseline,
  cvStability,
  trendDirection,
  isCalibrating,
} from './baselines.js';

const FEEL_SCORES = {
  great: 9,
  good: 7,
  ok: 6,
  tired: 4,
  exhausted: 2,
  unwell: 1,
};

const FEEL_LABELS = Object.keys(FEEL_SCORES);

export function parseTemplate(text) {
  const fields = {
    sleep: ['sleep'],
    hrv: ['hrv'],
    hrv7: ['7-day hrv', '7 day hrv', 'hrv7'],
    hrvCv: ['hrv cv', 'cv'],
    recovery: ['recovery'],
    restingHr: ['resting hr', 'rhr', 'resting heart rate'],
    stress: ['stress'],
    strain: ["yesterday's strain", 'yesterday strain', 'strain'],
    feel: ['how i feel', 'feel', 'how you feel'],
  };

  const result = {};
  const lines = text.split(/\r?\n/);
  for (const line of lines) {
    const match = line.match(/^([^:]+):\s*(.+)$/i);
    if (!match) continue;
    const key = match[1].trim().toLowerCase();
    let value = match[2].trim();
    for (const [field, aliases] of Object.entries(fields)) {
      if (aliases.some((a) => key === a || key.includes(a))) {
        if (field === 'feel') {
          result.feel = normalizeFeel(value);
        } else {
          const num = parseFloat(value.replace(/[^\d.-]/g, ''));
          if (!Number.isNaN(num)) result[field] = num;
        }
        break;
      }
    }
  }
  return result;
}

function normalizeFeel(text) {
  const lower = text.toLowerCase().trim();
  for (const label of FEEL_LABELS) {
    if (lower.includes(label)) return label;
  }
  return lower || 'ok';
}

function clamp(n, min, max) {
  return Math.max(min, Math.min(max, n));
}

function scoreHrv(hrvComp, calibrating) {
  if (hrvComp.pct == null) return 6;
  if (calibrating) return 6;
  if (hrvComp.pct >= 10) return 9;
  if (hrvComp.pct >= 3) return 7.5;
  if (hrvComp.pct >= -3) return 6.5;
  if (hrvComp.pct >= -10) return 5;
  return 3.5;
}

function scoreSleep(sleep) {
  if (sleep == null) return 6;
  if (sleep >= 85) return 9;
  if (sleep >= 70) return 7;
  if (sleep >= 55) return 5;
  return 3;
}

function scoreRecovery(recovery) {
  if (recovery == null) return 6;
  if (recovery >= 80) return 8;
  if (recovery >= 65) return 6.5;
  if (recovery >= 50) return 5;
  return 3;
}

function scoreRhr(rhrComp) {
  if (rhrComp.delta == null) return 6;
  if (rhrComp.delta <= -3) return 8;
  if (rhrComp.delta <= 3) return 6.5;
  return 4;
}

function scoreStress(stress) {
  if (stress == null) return 6;
  if (stress <= 15) return 8;
  if (stress <= 35) return 6;
  if (stress <= 60) return 4;
  return 2;
}

function scoreStrain(strain) {
  if (strain == null) return 6;
  // Yesterday's moderate strain is fine; very high may warrant easier day
  if (strain >= 80) return 4;
  if (strain >= 50) return 6;
  return 7;
}

function scoreCv(cvInfo) {
  if (cvInfo.level === 'stable') return 8;
  if (cvInfo.level === 'moderate') return 6;
  if (cvInfo.level === 'high') return 4;
  return 6;
}

function scoreFeel(feel) {
  return FEEL_SCORES[feel] ?? 6;
}

function readinessLabel(score) {
  if (score >= 8.5) return 'EXCELLENT';
  if (score >= 7) return 'GOOD';
  if (score >= 5.5) return 'MODERATE';
  if (score >= 4) return 'LOW';
  return 'POOR';
}

function trainingRecommendation(score, feel, strain, hrvComp, calibrating) {
  const feelScore = scoreFeel(feel);

  if (feel === 'unwell' || feel === 'exhausted') {
    return { level: 'Rest', reason: 'Subjective report suggests you need recovery.' };
  }

  if (score < 4.5) {
    return { level: 'Rest', reason: 'Multiple recovery signals are low today.' };
  }

  if (feelScore <= 4 && score < 6.5) {
    return {
      level: 'Easy',
      reason: 'You reported feeling tired — honour that even when some scores look fine.',
    };
  }

  if (feelScore <= 4 && score >= 6.5) {
    return {
      level: 'Moderate',
      reason: 'Objective scores look solid, but you feel tired — moderate training is reasonable; skip max efforts.',
    };
  }

  if (hrvComp.pct != null && hrvComp.pct < -10 && !calibrating) {
    return { level: 'Easy', reason: 'HRV is notably below your personal baseline.' };
  }

  if (strain != null && strain >= 85 && score < 7) {
    return { level: 'Easy', reason: 'High strain yesterday plus moderate recovery today.' };
  }

  if (score >= 8.5 && feelScore >= 8 && (hrvComp.pct == null || hrvComp.pct >= 0)) {
    return { level: 'Hard', reason: 'Strong recovery across metrics and you feel good.' };
  }

  if (score >= 7 && feelScore >= 6) {
    return {
      level: 'Moderate',
      reason: 'Solid recovery — training is reasonable, but skip max efforts if energy dips.',
    };
  }

  if (score >= 5.5) {
    return { level: 'Easy', reason: 'Mixed signals — keep intensity controlled.' };
  }

  return { level: 'Rest', reason: 'Recovery markers suggest prioritising rest.' };
}

function buildPriorities(entry, assessment, hrvComp, cvInfo, calibrating) {
  const priorities = [];

  if (calibrating.active) {
    priorities.push('Keep logging each morning — your personal baseline is still forming.');
  }

  if (entry.feel === 'tired' || entry.feel === 'exhausted') {
    priorities.push('Prioritise sleep and hydration; match training to how you actually feel.');
  }

  if (cvInfo.level === 'high') {
    priorities.push('Watch HRV stability over the next week — high CV often settles with consistent sleep.');
  }

  if (hrvComp.pct != null && hrvComp.pct < -5 && !calibrating.active) {
    priorities.push('HRV is below baseline — consider an easier day or active recovery.');
  }

  if (entry.sleep != null && entry.sleep < 65) {
    priorities.push('Sleep was short or poor — an earlier bedtime tonight will help tomorrow\'s readiness.');
  }

  if (entry.strain != null && entry.strain >= 75) {
    priorities.push('Yesterday was a high-strain day — allow adequate recovery before pushing hard again.');
  }

  if (priorities.length === 0) {
    if (assessment.training.level === 'Moderate' || assessment.training.level === 'Hard') {
      priorities.push('Good day for planned training — stay attuned to energy as the day goes on.');
    } else {
      priorities.push('Focus on consistency: movement, nutrition, and sleep hygiene.');
    }
  }

  return priorities.slice(0, 2);
}

export function assessEntry(entry, allEntries, settings = {}) {
  const date = entry.date;
  const priorEntries = allEntries.filter((e) => e.date !== date);
  const baselines = computeBaselines(priorEntries, date);
  const calibrating = isCalibrating(baselines.entryCount, settings);

  const hrvComp = hrvVsBaseline(entry.hrv, baselines);
  const rhrComp = rhrVsBaseline(entry.restingHr, baselines);
  const cvInfo = cvStability(entry.hrvCv, baselines);

  const feel = entry.feel || 'ok';

  const componentScores = {
    hrv: scoreHrv(hrvComp, calibrating.active),
    sleep: scoreSleep(entry.sleep),
    recovery: scoreRecovery(entry.recovery),
    rhr: scoreRhr(rhrComp),
    stress: scoreStress(entry.stress),
    strain: scoreStrain(entry.strain),
    cv: scoreCv(cvInfo),
    feel: scoreFeel(feel),
  };

  // Weight subjective feel and HRV baseline comparison more than proprietary scores
  const weights = {
    feel: 0.22,
    hrv: 0.2,
    sleep: 0.15,
    recovery: 0.12,
    rhr: 0.1,
    stress: 0.08,
    strain: 0.05,
    cv: 0.08,
  };

  let overall =
    Object.entries(weights).reduce((sum, [k, w]) => sum + componentScores[k] * w, 0);

  // Subjective tiredness should cap readiness even when objective scores look good
  if (feel === 'tired' && overall > 7) overall = 7;
  if (feel === 'exhausted' && overall > 5.5) overall = 5.5;

  overall = Math.round(overall * 10) / 10;

  const training = trainingRecommendation(
    overall,
    feel,
    entry.strain,
    hrvComp,
    calibrating.active
  );

  const hrvTrend = trendDirection(priorEntries.concat(entry), 'hrv', 14);
  const rhrTrend = trendDirection(priorEntries.concat(entry), 'restingHr', 14);

  const assessment = {
    overall,
    label: readinessLabel(overall),
    training,
    calibrating,
    baselines,
    hrvComp,
    rhrComp,
    cvInfo,
    trends: { hrv: hrvTrend, restingHr: rhrTrend },
    componentScores,
  };

  assessment.priorities = buildPriorities(entry, assessment, hrvComp, cvInfo, calibrating);

  return assessment;
}

export function formatAssessment(entry, assessment) {
  const lines = [];
  lines.push('HUME-STYLE DAILY CHECK');
  lines.push('');
  lines.push(formatMindBodyReport(entry, assessment));
  lines.push('');
  lines.push(`Overall readiness: ${assessment.overall}/10 — ${assessment.label}`);
  lines.push(`Training: ${assessment.training.level}`);
  lines.push(`Recovery: ${formatRecovery(entry, assessment)}`);
  lines.push(`HRV: ${formatHrv(entry, assessment)}`);
  lines.push(`Sleep: ${formatSleep(entry)}`);
  lines.push(`Resting HR: ${formatRhr(entry, assessment)}`);
  lines.push(`Stress: ${formatStress(entry)}`);
  lines.push(`Yesterday's strain: ${formatStrain(entry)}`);
  lines.push(`How you feel: ${formatFeel(entry, assessment)}`);
  lines.push(`Today's priority: ${assessment.priorities.join(' ')}`);

  if (assessment.calibrating.active) {
    lines.push('');
    lines.push(
      `Note: ${assessment.calibrating.reason} (~${assessment.calibrating.daysLeft} more day(s)). Personal trends matter more than absolute numbers during this period.`
    );
  }

  lines.push('');
  lines.push(
    '— Proprietary app scores (Recovery, Sleep score) are useful for trends within each app, not as cross-app equivalents.'
  );

  return lines.join('\n');
}

export function formatMindBodyReport(entry, assessment) {
  const body = bodyState(entry, assessment);
  const mind = mindState(entry, assessment);
  return [
    'BODY: ' + body.summary,
    'MIND: ' + mind.summary,
    '',
    `Body state: ${body.label} (${body.detail})`,
    `Mind state: ${mind.label} (${mind.detail})`,
  ].join('\n');
}

function bodyState(entry, a) {
  const signals = [];
  let score = 5;

  if (entry.hrv != null && a.hrvComp.pct != null) {
    if (a.hrvComp.pct >= 3) { score += 1; signals.push('HRV at/above baseline'); }
    else if (a.hrvComp.pct < -5) { score -= 1.5; signals.push('HRV below baseline'); }
  }
  if (entry.restingHr != null && a.rhrComp.delta != null) {
    if (a.rhrComp.delta <= -2) { score += 0.5; signals.push('resting HR trending down'); }
    else if (a.rhrComp.delta >= 3) { score -= 1; signals.push('resting HR elevated'); }
  }
  if (entry.sleep != null) {
    if (entry.sleep >= 80) { score += 1; signals.push('strong sleep score'); }
    else if (entry.sleep < 60) { score -= 1; signals.push('poor sleep'); }
  }
  if (entry.recovery != null && entry.recovery >= 75) score += 0.5;
  if (entry.strain != null && entry.strain >= 80) { score -= 0.5; signals.push('high yesterday strain'); }

  score = Math.max(1, Math.min(10, score));
  let label, detail;
  if (score >= 7.5) { label = 'Recovered'; detail = signals.join('; ') || 'physical markers look solid'; }
  else if (score >= 5.5) { label = 'Moderately recovered'; detail = signals.join('; ') || 'mixed physical signals'; }
  else { label = 'Needs recovery'; detail = signals.join('; ') || 'body is still catching up'; }

  return { label, detail, summary: `${label} — ${detail}`, score };
}

function mindState(entry, a) {
  const signals = [];
  let score = 5;
  const feel = entry.feel || 'ok';

  if (entry.stress != null) {
    if (entry.stress <= 15) { score += 1.5; signals.push('very low stress'); }
    else if (entry.stress <= 35) score += 0.5;
    else if (entry.stress >= 60) { score -= 1.5; signals.push('elevated stress'); }
  }

  if (feel === 'great' || feel === 'good') { score += 1.5; signals.push(`feeling ${feel}`); }
  else if (feel === 'tired') { score -= 1.5; signals.push('self-reported tiredness'); }
  else if (feel === 'exhausted' || feel === 'unwell') { score -= 2.5; signals.push(`feeling ${feel}`); }

  if (entry.sleep != null && entry.sleep < 65) {
    score -= 0.5;
    signals.push('sleep may affect focus');
  }

  score = Math.max(1, Math.min(10, score));
  let label, detail;
  if (score >= 7.5) { label = 'Clear & steady'; detail = signals.join('; ') || 'mental energy looks good'; }
  else if (score >= 5.5) { label = 'Functional but flat'; detail = signals.join('; ') || 'manageable but not sharp'; }
  else { label = 'Depleted'; detail = signals.join('; ') || 'prioritise rest and low cognitive load'; }

  return { label, detail, summary: `${label} — ${detail}`, score };
}

export function getMindBodyScores(entry, assessment) {
  return {
    body: bodyState(entry, assessment),
    mind: mindState(entry, assessment),
  };
}

function formatRecovery(entry, a) {
  if (entry.recovery == null) return 'No recovery score entered.';
  let s = `Bevel Recovery ${entry.recovery}% — ${entry.recovery >= 75 ? 'good' : entry.recovery >= 60 ? 'moderate' : 'low'}.`;
  if (a.calibrating.active) s += ' Treat as directional during calibration.';
  return s;
}

function formatHrv(entry, a) {
  if (entry.hrv == null) return 'No HRV entered.';
  let s = `${entry.hrv} ms`;
  if (a.hrvComp.ref != null) {
    s += ` vs your ${a.hrvComp.ref.toFixed(1)} ms baseline — ${a.hrvComp.label}.`;
  } else {
    s += ' — baseline still forming.';
  }
  if (entry.hrvCv != null) {
    s += ` CV ${entry.hrvCv}% (${a.cvInfo.label}).`;
  }
  if (a.trends.hrv !== 'insufficient data') {
    s += ` 14-day trend: ${a.trends.hrv}.`;
  }
  return s;
}

function formatSleep(entry) {
  if (entry.sleep == null) return 'No sleep score entered.';
  const quality = entry.sleep >= 85 ? 'excellent' : entry.sleep >= 70 ? 'good' : entry.sleep >= 55 ? 'fair' : 'poor';
  return `Sleep Cycle score ${entry.sleep} — ${quality}.`;
}

function formatRhr(entry, a) {
  if (entry.restingHr == null) return 'No resting HR entered.';
  let s = `${entry.restingHr} bpm — ${a.rhrComp.label}.`;
  if (a.trends.restingHr !== 'insufficient data') {
    s += ` Trend: ${a.trends.restingHr}.`;
  }
  return s;
}

function formatStress(entry) {
  if (entry.stress == null) return 'No stress score entered.';
  const level = entry.stress <= 15 ? 'very low' : entry.stress <= 35 ? 'low-moderate' : entry.stress <= 60 ? 'elevated' : 'high';
  return `${entry.stress}/100 — ${level}.`;
}

function formatStrain(entry) {
  if (entry.strain == null) return 'No strain entered.';
  const level = entry.strain >= 75 ? 'high' : entry.strain >= 45 ? 'moderate' : 'light';
  return `${entry.strain}% — ${level} load yesterday.`;
}

function formatFeel(entry, a) {
  const feel = entry.feel || 'not reported';
  if (feel === 'tired' || feel === 'exhausted') {
    return `"${feel}" — this matters. Don't ignore how you feel when scores look good.`;
  }
  if (feel === 'great' || feel === 'good') {
    return `"${feel}" — aligns with ${a.overall >= 7 ? 'solid' : 'mixed'} objective data.`;
  }
  return `"${feel}".`;
}

export { FEEL_LABELS, FEEL_SCORES };
