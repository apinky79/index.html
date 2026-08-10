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
import { assessEntry, formatAssessment, parseTemplate, getMindBodyScores } from './assess.js';
import { computeBaselines, trendDirection } from './baselines.js';
import { extractFromScreenshots, metricsToEntry } from './extract.js';
import { parseHealthExport, healthToEntry } from './health-import.js';
import { encryptExport, decryptExport, downloadEncrypted } from './crypto-share.js';

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => document.querySelectorAll(sel);

let currentDate = todayISO();
let screenshotFiles = [];
let lastExtracted = null;

function init() {
  bindForm();
  bindTabs();
  bindImportExport();
  bindPaste();
  bindHistory();
  bindSettings();
  bindScan();
  bindHealthImport();
  bindEncryptedShare();
  loadFormForDate(currentDate);
  renderHistory();
  renderTrends();
  loadSettingsIntoUI();
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

  const mb = getMindBodyScores(entry, assessment);
  $('#body-state-label').textContent = mb.body.label;
  $('#body-state-detail').textContent = mb.body.detail;
  $('#mind-state-label').textContent = mb.mind.label;
  $('#mind-state-detail').textContent = mb.mind.detail;

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
  if (settings.notes) $('#settings-notes').value = settings.notes;
  if (settings.apiKey) $('#api-key').value = settings.apiKey;

  $('#btn-save-settings').addEventListener('click', () => {
    const start = $('#calibration-start').value;
    saveSettings({
      calibrationStart: start ? new Date(start).toISOString() : null,
      notes: $('#settings-notes').value,
      apiKey: $('#api-key').value.trim(),
    });
    alert('Settings saved.');
  });
}

function loadSettingsIntoUI() {
  const settings = loadSettings();
  if (settings.apiKey) $('#api-key').value = settings.apiKey;
}

function bindScan() {
  const input = $('#screenshot-input');
  const zone = $('#upload-zone');
  const pasteZone = $('#paste-zone');

  $('#btn-add-photos').addEventListener('click', () => input.click());

  input.addEventListener('change', () => {
    addScreenshots([...input.files]);
    input.value = '';
  });

  $('#btn-paste-clipboard').addEventListener('click', () => pasteFromClipboard());

  pasteZone.addEventListener('paste', (e) => {
    e.preventDefault();
    const files = filesFromDataTransfer(e.clipboardData);
    if (files.length) {
      addScreenshots(files);
      setExtractStatus(`Added ${files.length} image(s) from paste.`);
    } else {
      setExtractStatus('Nothing to paste — use Add from Photos instead.');
    }
  });

  document.addEventListener('paste', (e) => {
    if (!$('#tab-scan').classList.contains('active')) return;
    const files = filesFromDataTransfer(e.clipboardData);
    if (!files.length) return;
    e.preventDefault();
    addScreenshots(files);
    setExtractStatus(`Added ${files.length} image(s) from paste.`);
  });

  zone.addEventListener('dragover', (e) => { e.preventDefault(); zone.classList.remove('hidden'); zone.classList.add('dragover'); });
  zone.addEventListener('dragleave', () => zone.classList.remove('dragover'));
  zone.addEventListener('drop', (e) => {
    e.preventDefault();
    zone.classList.remove('dragover');
    addScreenshots([...e.dataTransfer.files]);
  });

  $('#btn-clear-screenshots').addEventListener('click', () => {
    screenshotFiles = [];
    renderScreenshotPreview();
    $('#extract-review').hidden = true;
    setExtractStatus('');
  });

  $('#btn-extract').addEventListener('click', async () => {
    if (!screenshotFiles.length) return;
    const status = $('#extract-status');
    status.hidden = false;
    status.textContent = 'Starting analysis…';
    $('#btn-extract').disabled = true;

    try {
      const settings = loadSettings();
      const apiKey = $('#api-key').value.trim() || settings.apiKey || null;
      if ($('#api-key').value.trim()) {
        saveSettings({ ...settings, apiKey: $('#api-key').value.trim() });
      }
      const result = await extractFromScreenshots(screenshotFiles, {
        apiKey,
        onProgress: (msg) => { status.textContent = msg; },
      });
      lastExtracted = result.metrics;
      renderExtractReview(result.metrics, result.method);
      status.textContent = result.method === 'vision+ocr'
        ? 'Extracted with AI — please review below.'
        : 'Extracted with OCR — please verify numbers (AI key in Settings improves accuracy).';
    } catch (e) {
      status.textContent = `Error: ${e.message}`;
    } finally {
      $('#btn-extract').disabled = false;
    }
  });

  $('#btn-confirm-extract').addEventListener('click', () => {
    const metrics = readExtractFields();
    const feel = $('#scan-feel').value;
    applyMetricsToForm(metricsToEntry(metrics, feel));
    $('#feel').value = feel;

    const entry = collectForm();
    const entries = addEntry(entry);
    const settings = loadSettings();
    const assessment = assessEntry(entry, entries, settings);
    renderAssessment(entry, assessment);
    renderHistory();
    renderTrends();
    showTab('check');
  });
}

function addScreenshots(files) {
  const images = [...files].filter(isImageFile);
  if (!images.length) {
    setExtractStatus('No images found. On iPhone, use Add from Photos.');
    return;
  }
  screenshotFiles.push(...images);
  renderScreenshotPreview();
  setExtractStatus(`${screenshotFiles.length} screenshot(s) ready.`);
}

function isImageFile(file) {
  if (file.type && file.type.startsWith('image/')) return true;
  return /\.(jpe?g|png|heic|heif|webp|gif)$/i.test(file.name || '');
}

function filesFromDataTransfer(dataTransfer) {
  if (!dataTransfer) return [];
  const files = [];
  if (dataTransfer.files?.length) {
    for (const f of dataTransfer.files) {
      if (isImageFile(f)) files.push(f);
    }
  }
  if (dataTransfer.items) {
    for (const item of dataTransfer.items) {
      if (item.kind === 'file') {
        const f = item.getAsFile();
        if (f && isImageFile(f)) files.push(f);
      }
    }
  }
  return files;
}

async function pasteFromClipboard() {
  setExtractStatus('Reading clipboard…');
  try {
    if (navigator.clipboard?.read) {
      const items = await navigator.clipboard.read();
      const files = [];
      for (const item of items) {
        for (const type of item.types) {
          if (type.startsWith('image/')) {
            const blob = await item.getType(type);
            files.push(new File([blob], `pasted-${Date.now()}.png`, { type }));
          }
        }
      }
      if (files.length) {
        addScreenshots(files);
        return;
      }
    }
    setExtractStatus('Clipboard empty or paste not supported. On iPhone: tap Add from Photos, then pick screenshots from your library.');
  } catch {
    setExtractStatus('Paste blocked by Safari. Use Add from Photos — screenshots are saved there automatically.');
  }
}

function setExtractStatus(msg) {
  const status = $('#extract-status');
  status.hidden = !msg;
  status.textContent = msg;
}

function renderScreenshotPreview() {
  const container = $('#screenshot-preview');
  container.innerHTML = '';
  screenshotFiles.forEach((file, i) => {
    const img = document.createElement('img');
    img.className = 'screenshot-thumb';
    img.src = URL.createObjectURL(file);
    img.alt = `Screenshot ${i + 1}`;
    container.appendChild(img);
  });
  $('#btn-extract').disabled = screenshotFiles.length === 0;
}

const EXTRACT_FIELD_MAP = [
  ['sleep', 'Sleep score'],
  ['hrv', 'HRV (ms)'],
  ['hrv7', '7-day HRV'],
  ['hrvCv', 'HRV CV (%)'],
  ['recovery', 'Recovery (%)'],
  ['readiness', 'Readiness (%)'],
  ['restingHr', 'Resting HR'],
  ['stress', 'Stress'],
  ['strain', 'Strain (%)'],
];

function renderExtractReview(metrics, method) {
  const container = $('#extract-fields');
  container.innerHTML = '';
  for (const [key, label] of EXTRACT_FIELD_MAP) {
    const wrap = document.createElement('label');
    wrap.className = 'extract-field';
    wrap.innerHTML = `<span>${label}</span><input type="text" data-key="${key}" value="${metrics[key] ?? ''}">`;
    container.appendChild(wrap);
  }
  $('#extract-review').hidden = false;
}

function readExtractFields() {
  const metrics = {};
  $$('#extract-fields input').forEach((input) => {
    const v = input.value.trim();
    if (v !== '') {
      const n = parseFloat(v);
      if (!Number.isNaN(n)) metrics[input.dataset.key] = n;
    }
  });
  return metrics;
}

function applyMetricsToForm(partial) {
  const map = {
    sleep: 'sleep', hrv: 'hrv', hrv7: 'hrv7', hrvCv: 'hrv-cv',
    recovery: 'recovery', restingHr: 'resting-hr', stress: 'stress', strain: 'strain',
  };
  for (const [k, id] of Object.entries(map)) {
    if (partial[k] != null) $(`#${id}`).value = partial[k];
  }
}

function bindHealthImport() {
  const zone = $('#health-upload-zone');
  const input = $('#health-input');
  zone.addEventListener('click', () => input.click());
  input.addEventListener('change', async () => {
    const file = input.files[0];
    if (!file) return;
    const status = $('#health-import-status');
    status.hidden = false;
    status.textContent = 'Parsing Apple Health export…';
    try {
      const data = await parseHealthExport(file, currentDate);
      applyMetricsToForm(healthToEntry(data));
      const parts = [];
      if (data.hrv) parts.push(`HRV ${data.hrv} ms`);
      if (data.restingHr) parts.push(`RHR ${data.restingHr} bpm`);
      if (data.vo2max) parts.push(`VO2 max ${data.vo2max}`);
      if (data.sleepDuration) parts.push(`Sleep ${data.sleepDuration.toFixed(1)}h`);
      status.textContent = parts.length
        ? `Imported: ${parts.join(', ')}. Review in Log tab.`
        : 'No recent metrics found in export for today.';
      showTab('entry');
    } catch (e) {
      status.textContent = e.message;
    }
    input.value = '';
  });
}

function bindEncryptedShare() {
  $('#btn-export-encrypted').addEventListener('click', async () => {
    const pass = $('#share-passphrase').value;
    try {
      const encrypted = await encryptExport(exportData(), pass);
      downloadEncrypted(encrypted, `readiness-encrypted-${todayISO()}.json`);
      alert('Encrypted export downloaded. Share the file and passphrase separately.');
    } catch (e) {
      alert(e.message);
    }
  });

  $('#btn-import-encrypted').addEventListener('click', () => {
    const pass = $('#share-passphrase').value;
    if (!pass) { alert('Enter the passphrase used to encrypt the file.'); return; }
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async () => {
      const file = input.files[0];
      if (!file) return;
      try {
        const encrypted = JSON.parse(await file.text());
        const data = await decryptExport(encrypted, pass);
        importData(data);
        renderHistory();
        renderTrends();
        loadFormForDate(currentDate);
        alert('Encrypted import complete.');
      } catch {
        alert('Wrong passphrase or invalid file.');
      }
    };
    input.click();
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

document.addEventListener('DOMContentLoaded', () => {
  init();
});
