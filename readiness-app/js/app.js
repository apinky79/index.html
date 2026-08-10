import {
  loadEntries,
  addEntry,
  getEntry,
  deleteEntry,
  loadSettings,
  saveSettings,
  exportData,
  importData,
  todayISO,
} from './storage.js';
import { assessEntry, formatAssessment, parseTemplate, FEEL_LABELS } from './assess.js';
import { computeBaselines, trendDirection } from './baselines.js';

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => document.querySelectorAll(sel);

let currentDate = todayISO();

function init() {
  bindForm();
  bindTabs();
  bindImportExport();
  bindPaste();
  bindHistory();
  bindSettings();
  loadFormForDate(currentDate);
  renderHistory();
  renderTrends();
}

function bindForm() {
  $('#entry-date').value = currentDate;
  $('#entry-date').addEventListener('change', (e) => {
    currentDate = e.target.value;
    loadFormForDate(currentDate);
  });

  $('#btn-assess').addEventListener('click', () => {
    const entry = collectForm();
    if (!entry.hrv && !entry.sleep && !entry.recovery) {
      alert('Enter at least one metric (HRV, Sleep, or Recovery) to assess.');
      return;
    }
    const entries = addEntry(entry);
    const settings = loadSettings();
    const assessment = assessEntry(entry, entries, settings);
    renderAssessment(entry, assessment);
    renderHistory();
    renderTrends();
    showTab('check');
  });

  $('#btn-clear-form').addEventListener('click', () => {
    clearForm();
  });

  $('#btn-copy').addEventListener('click', () => {
    const text = $('#assessment-text').textContent;
    navigator.clipboard.writeText(text).then(() => {
      $('#btn-copy').textContent = 'Copied!';
      setTimeout(() => { $('#btn-copy').textContent = 'Copy'; }, 1500);
    });
  });
}

function collectForm() {
  const num = (id) => {
    const v = $(`#${id}`).value;
    return v === '' ? null : parseFloat(v);
  };
  return {
    date: $('#entry-date').value || todayISO(),
    sleep: num('sleep'),
    hrv: num('hrv'),
    hrv7: num('hrv7'),
    hrvCv: num('hrv-cv'),
    recovery: num('recovery'),
    restingHr: num('resting-hr'),
    stress: num('stress'),
    strain: num('strain'),
    feel: $('#feel').value,
    notes: $('#notes').value.trim(),
  };
}

function loadFormForDate(date) {
  const entry = getEntry(date);
  const set = (id, val) => { $(`#${id}`).value = val ?? ''; };
  if (entry) {
    set('sleep', entry.sleep);
    set('hrv', entry.hrv);
    set('hrv7', entry.hrv7);
    set('hrv-cv', entry.hrvCv);
    set('recovery', entry.recovery);
    set('resting-hr', entry.restingHr);
    set('stress', entry.stress);
    set('strain', entry.strain);
    $('#feel').value = entry.feel || 'ok';
    set('notes', entry.notes);
    const settings = loadSettings();
    const assessment = assessEntry(entry, loadEntries(), settings);
    renderAssessment(entry, assessment);
  } else {
    clearForm(false);
  }
}

function clearForm(resetDate = true) {
  $$('.metric-input').forEach((el) => { el.value = ''; });
  $('#feel').value = 'ok';
  $('#notes').value = '';
  if (resetDate) {
    currentDate = todayISO();
    $('#entry-date').value = currentDate;
  }
  $('#assessment-output').hidden = true;
  $('#no-assessment').hidden = false;
}

function renderAssessment(entry, assessment) {
  const text = formatAssessment(entry, assessment);
  $('#assessment-text').textContent = text;
  $('#assessment-output').hidden = false;
  $('#no-assessment').hidden = true;

  const badge = $('#readiness-badge');
  badge.textContent = `${assessment.overall}/10 — ${assessment.label}`;
  badge.dataset.level = assessment.label.toLowerCase();

  $('#training-badge').textContent = assessment.training.level;
  $('#training-badge').dataset.level = assessment.training.level.toLowerCase();

  $('#training-reason').textContent = assessment.training.reason;

  const priorities = $('#priorities-list');
  priorities.innerHTML = '';
  assessment.priorities.forEach((p) => {
    const li = document.createElement('li');
    li.textContent = p;
    priorities.appendChild(li);
  });

  if (assessment.calibrating.active) {
    $('#calibration-notice').hidden = false;
    $('#calibration-notice').textContent =
      `Calibration active: ${assessment.calibrating.reason}. ~${assessment.calibrating.daysLeft} more day(s) before baselines are reliable.`;
  } else {
    $('#calibration-notice').hidden = true;
  }
}

function bindTabs() {
  $$('.tab-btn').forEach((btn) => {
    btn.addEventListener('click', () => showTab(btn.dataset.tab));
  });
}

function showTab(name) {
  $$('.tab-btn').forEach((b) => b.classList.toggle('active', b.dataset.tab === name));
  $$('.tab-panel').forEach((p) => p.classList.toggle('active', p.id === `tab-${name}`));
}

function bindPaste() {
  $('#btn-parse-paste').addEventListener('click', () => {
    const text = $('#paste-area').value;
    const parsed = parseTemplate(text);
    if (Object.keys(parsed).length === 0) {
      alert('Could not parse any fields. Use the format:\nSleep:\nHRV:\n...');
      return;
    }
    if (parsed.sleep != null) $('#sleep').value = parsed.sleep;
    if (parsed.hrv != null) $('#hrv').value = parsed.hrv;
    if (parsed.hrv7 != null) $('#hrv7').value = parsed.hrv7;
    if (parsed.hrvCv != null) $('#hrv-cv').value = parsed.hrvCv;
    if (parsed.recovery != null) $('#recovery').value = parsed.recovery;
    if (parsed.restingHr != null) $('#resting-hr').value = parsed.restingHr;
    if (parsed.stress != null) $('#stress').value = parsed.stress;
    if (parsed.strain != null) $('#strain').value = parsed.strain;
    if (parsed.feel) $('#feel').value = parsed.feel;
    showTab('entry');
  });
}

function bindHistory() {
  $('#history-list').addEventListener('click', (e) => {
    const row = e.target.closest('[data-date]');
    if (!row) return;
    if (e.target.classList.contains('btn-delete')) {
      if (confirm(`Delete entry for ${row.dataset.date}?`)) {
        deleteEntry(row.dataset.date);
        renderHistory();
        renderTrends();
        if (row.dataset.date === currentDate) clearForm();
      }
      return;
    }
    currentDate = row.dataset.date;
    $('#entry-date').value = currentDate;
    loadFormForDate(currentDate);
    showTab('entry');
  });
}

function renderHistory() {
  const entries = loadEntries().slice().reverse();
  const list = $('#history-list');
  list.innerHTML = '';
  if (!entries.length) {
    list.innerHTML = '<p class="empty">No entries yet. Log your first morning check-in.</p>';
    return;
  }
  entries.forEach((entry) => {
    const settings = loadSettings();
    const a = assessEntry(entry, loadEntries(), settings);
    const row = document.createElement('div');
    row.className = 'history-row';
    row.dataset.date = entry.date;
    row.innerHTML = `
      <span class="history-date">${formatDate(entry.date)}</span>
      <span class="history-score" data-level="${a.label.toLowerCase()}">${a.overall}</span>
      <span class="history-hrv">${entry.hrv != null ? entry.hrv + ' ms' : '—'}</span>
      <span class="history-training">${a.training.level}</span>
      <span class="history-feel">${entry.feel || '—'}</span>
      <button type="button" class="btn btn-small btn-delete" aria-label="Delete">×</button>
    `;
    list.appendChild(row);
  });
}

function renderTrends() {
  const entries = loadEntries();
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const baselines = computeBaselines(entries, tomorrow.toISOString().slice(0, 10));
  const container = $('#trends-grid');
  container.innerHTML = '';

  const metrics = [
    { key: 'hrv', label: 'HRV (ms)', baseline: baselines.hrv.d7, trend: trendDirection(entries, 'hrv') },
    { key: 'restingHr', label: 'Resting HR (bpm)', baseline: baselines.restingHr.d7, trend: trendDirection(entries, 'restingHr') },
    { key: 'hrvCv', label: 'HRV CV (%)', baseline: baselines.hrvCv.d7, trend: trendDirection(entries, 'hrvCv') },
    { key: 'sleep', label: 'Sleep score', baseline: baselines.sleep.d7, trend: trendDirection(entries, 'sleep') },
    { key: 'recovery', label: 'Recovery %', baseline: baselines.recovery.d7, trend: trendDirection(entries, 'recovery') },
  ];

  metrics.forEach((m) => {
    const latest = entries.length ? entries[entries.length - 1][m.key] : null;
    const card = document.createElement('div');
    card.className = 'trend-card';
    card.innerHTML = `
      <div class="trend-label">${m.label}</div>
      <div class="trend-latest">${latest != null ? latest : '—'}</div>
      <div class="trend-baseline">${m.baseline != null ? `7-day avg: ${m.baseline.toFixed(1)}` : 'Building baseline…'}</div>
      <div class="trend-direction">${m.trend !== 'insufficient data' ? `Trend: ${m.trend}` : ''}</div>
    `;
    container.appendChild(card);
  });

  renderSparkline(entries, 'hrv', '#sparkline-hrv');
}

function renderSparkline(entries, field, selector) {
  const canvas = $(selector);
  if (!canvas) return;
  const ctx = canvas.getContext('2d');
  const vals = entries.filter((e) => e[field] != null).slice(-14).map((e) => Number(e[field]));
  const w = canvas.width;
  const h = canvas.height;
  ctx.clearRect(0, 0, w, h);
  if (vals.length < 2) return;

  const min = Math.min(...vals);
  const max = Math.max(...vals);
  const range = max - min || 1;
  const pad = 4;

  ctx.strokeStyle = '#5eead4';
  ctx.lineWidth = 2;
  ctx.beginPath();
  vals.forEach((v, i) => {
    const x = pad + (i / (vals.length - 1)) * (w - pad * 2);
    const y = h - pad - ((v - min) / range) * (h - pad * 2);
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  });
  ctx.stroke();

  ctx.fillStyle = 'rgba(94, 234, 212, 0.15)';
  ctx.lineTo(pad + (w - pad * 2), h - pad);
  ctx.lineTo(pad, h - pad);
  ctx.closePath();
  ctx.fill();
}

function bindSettings() {
  const settings = loadSettings();
  if (settings.calibrationStart) {
    $('#calibration-start').value = settings.calibrationStart.slice(0, 10);
  }

  $('#btn-save-settings').addEventListener('click', () => {
    const start = $('#calibration-start').value;
    saveSettings({
      calibrationStart: start ? new Date(start).toISOString() : null,
      notes: $('#settings-notes').value,
    });
    alert('Settings saved.');
  });
}

function bindImportExport() {
  $('#btn-export').addEventListener('click', () => {
    const blob = new Blob([JSON.stringify(exportData(), null, 2)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `readiness-export-${todayISO()}.json`;
    a.click();
  });

  $('#btn-import').addEventListener('click', () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = () => {
      const file = input.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = () => {
        try {
          importData(JSON.parse(reader.result));
          renderHistory();
          renderTrends();
          loadFormForDate(currentDate);
          alert('Import complete.');
        } catch {
          alert('Invalid JSON file.');
        }
      };
      reader.readAsText(file);
    };
    input.click();
  });
}

function formatDate(iso) {
  const d = new Date(iso + 'T12:00:00');
  return d.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
}

// Seed example entry from handoff if empty
function seedIfEmpty() {
  const entries = loadEntries();
  if (entries.length > 0) return;
  const example = {
    date: todayISO(),
    sleep: 94,
    hrv: 24.3,
    hrv7: 23.4,
    hrvCv: 29.6,
    recovery: 82,
    restingHr: 65.8,
    stress: 1,
    strain: 53,
    feel: 'tired',
  };
  addEntry(example);
  saveSettings({
    calibrationStart: new Date().toISOString(),
    notes: 'Post-holiday calibration period',
  });
}

document.addEventListener('DOMContentLoaded', () => {
  seedIfEmpty();
  init();
});
