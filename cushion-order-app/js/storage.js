/**
 * Local-only draft storage. Data never leaves the browser unless you export.
 */
const Storage = (() => {
  const KEY = "cushion-order-drafts-v1";
  const MAX_DRAFTS = 50;

  function readAll() {
    try {
      const raw = localStorage.getItem(KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }

  function writeAll(list) {
    localStorage.setItem(KEY, JSON.stringify(list.slice(0, MAX_DRAFTS)));
  }

  function save(draft) {
    const list = readAll();
    const idx = list.findIndex((d) => d.id === draft.id);
    const entry = { ...draft, updatedAt: new Date().toISOString() };
    if (idx >= 0) list[idx] = entry;
    else list.unshift(entry);
    writeAll(list);
    return entry;
  }

  function remove(id) {
    writeAll(readAll().filter((d) => d.id !== id));
  }

  function get(id) {
    return readAll().find((d) => d.id === id) || null;
  }

  function newId() {
    return `order-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  }

  return { readAll, save, remove, get, newId };
})();
