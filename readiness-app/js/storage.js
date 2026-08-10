const STORAGE_KEY = 'readiness-entries-v1';
const SETTINGS_KEY = 'readiness-settings-v1';

export function loadEntries() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

export function saveEntries(entries) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(entries));
}

export function loadSettings() {
  try {
    const raw = localStorage.getItem(SETTINGS_KEY);
    return raw ? JSON.parse(raw) : { calibrationStart: null, notes: '', apiKey: '' };
  } catch {
    return { calibrationStart: null, notes: '', apiKey: '' };
  }
}

export function saveSettings(settings) {
  localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
}

export function addEntry(entry) {
  const entries = loadEntries();
  const date = entry.date || todayISO();
  const filtered = entries.filter((e) => e.date !== date);
  filtered.push({ ...entry, date, savedAt: new Date().toISOString() });
  filtered.sort((a, b) => a.date.localeCompare(b.date));
  saveEntries(filtered);
  return filtered;
}

export function getEntry(date) {
  return loadEntries().find((e) => e.date === date) || null;
}

export function deleteEntry(date) {
  const entries = loadEntries().filter((e) => e.date !== date);
  saveEntries(entries);
  return entries;
}

export function exportData() {
  return {
    exportedAt: new Date().toISOString(),
    settings: loadSettings(),
    entries: loadEntries(),
  };
}

export function importData(data) {
  if (data.entries && Array.isArray(data.entries)) {
    saveEntries(data.entries);
  }
  if (data.settings) {
    saveSettings(data.settings);
  }
}

export function todayISO() {
  return new Date().toISOString().slice(0, 10);
}
