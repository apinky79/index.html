/**
 * Manages multiple drawing canvases per seat/back section.
 */
const DrawingPanels = (() => {
  const NS = "http://www.w3.org/2000/svg";
  let drawingCounter = 0;
  /** @type {Map<string, { zone: string, kind: string, stack: HTMLElement, cards: Map<string, object> }>} */
  const zones = new Map();

  function uid() {
    drawingCounter += 1;
    return `d${drawingCounter}-${Date.now().toString(36)}`;
  }

  function templateOptions(kind) {
    return CUSHION_TEMPLATES[kind] || [];
  }

  async function applyTemplate(record, { force = false } = {}) {
    const templateId = record.select.value;
    const path = templateOptions(record.kind).find((t) => t.id === templateId)?.path ?? null;
    if (!force && record.loadedTemplateId === templateId && record.svg.childElementCount > 0) {
      return;
    }
    record.loadedTemplateId = templateId;
    try {
      await DrawingEditor.loadTemplate(record.svg, path);
      record.editor.refresh();
    } catch (err) {
      console.error(err);
      record.loadedTemplateId = null;
      if (window.CushionApp?.flash) {
        window.CushionApp.flash("Could not load that cushion style");
      }
    }
  }

  function createCard(zone, kind, { templateId = "blank", svgHtml = null } = {}) {
    const id = uid();
    const card = document.createElement("div");
    card.className = "drawing-card";
    card.dataset.drawingId = id;

    const toolbar = document.createElement("div");
    toolbar.className = "panel-toolbar no-print";

    const styleRow = document.createElement("div");
    styleRow.className = "style-row";

    const tplLabel = document.createElement("span");
    tplLabel.className = "style-label";
    tplLabel.textContent = "Cushion style";

    const select = document.createElement("select");
    select.className = "template-select";
    select.setAttribute("aria-label", `${kind} cushion style`);
    for (const t of templateOptions(kind)) {
      select.append(new Option(t.name, t.id));
    }
    select.value = templateId;

    const removeBtn = document.createElement("button");
    removeBtn.type = "button";
    removeBtn.className = "drawing-remove";
    removeBtn.textContent = "Remove drawing";
    removeBtn.addEventListener("click", () => removeCard(zone, id));

    styleRow.append(tplLabel, select, removeBtn);

    const tools = document.createElement("div");
    tools.className = "draw-tools";
    tools.dataset.drawingId = id;
    for (const [tool, label] of [
      ["add-text", "Text"],
      ["add-line", "Line"],
      ["edit-text", "Edit"],
      ["delete", "Delete"],
    ]) {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "tool-btn";
      b.dataset.tool = tool;
      b.textContent = label;
      tools.appendChild(b);
    }

    const clearBtn = document.createElement("button");
    clearBtn.type = "button";
    clearBtn.className = "tool-clear";
    clearBtn.textContent = "Clear drawing";
    clearBtn.addEventListener("click", async () => {
      select.value = "blank";
      record.loadedTemplateId = null;
      await applyTemplate(record, { force: true });
    });

    toolbar.append(styleRow, tools, clearBtn);

    const viewport = document.createElement("div");
    viewport.className = "svg-viewport";
    viewport.tabIndex = 0;
    const svg = document.createElementNS(NS, "svg");
    svg.setAttribute("xmlns", NS);
    viewport.appendChild(svg);

    card.append(toolbar, viewport);

    const editor = DrawingEditor.attach(viewport, svg, {
      onEditLabel: (textEl) => window.CushionApp.openEditExisting(textEl),
      onEditLabelAsync: (x, y, cb) => window.CushionApp.promptNewLabel(x, y, cb),
      onToolChange: (tool) => window.CushionApp.setToolButtonsActive(id, tool),
    });

    const onStylePick = () => applyTemplate(record, { force: true });
    select.addEventListener("change", onStylePick);
    select.addEventListener("input", onStylePick);

    const record = {
      id,
      card,
      svg,
      viewport,
      editor,
      select,
      tools,
      kind,
      loadedTemplateId: null,
    };

    const zoneData = zones.get(zone);
    zoneData.cards.set(id, record);
    zoneData.stack.appendChild(card);
    updateRemoveButtons(zone);

    (async () => {
      if (svgHtml) {
        DrawingEditor.loadInline(svg, svgHtml);
        record.loadedTemplateId = templateId;
        record.editor.refresh();
      } else {
        await applyTemplate(record, { force: true });
      }
    })();

    return record;
  }

  function updateRemoveButtons(zone) {
    const zoneData = zones.get(zone);
    const showRemove = zoneData.cards.size > 1;
    zoneData.cards.forEach((rec) => {
      rec.card.classList.toggle("drawing-card-multi", showRemove);
      rec.card.querySelector(".drawing-remove").hidden = !showRemove;
    });
  }

  function removeCard(zone, id) {
    const zoneData = zones.get(zone);
    const record = zoneData.cards.get(id);
    if (!record) return;

    if (zoneData.cards.size <= 1) {
      record.select.value = "blank";
      record.loadedTemplateId = null;
      applyTemplate(record, { force: true });
      return;
    }

    DrawingEditor.detach(record.svg);
    record.card.remove();
    zoneData.cards.delete(id);
    updateRemoveButtons(zone);
  }

  function init(zoneId, kind) {
    const stack = document.getElementById(zoneId);
    zones.set(zoneId, { zone: zoneId, kind, stack, cards: new Map() });
    createCard(zoneId, kind, { templateId: kind === "seat" ? "chair/t-cushion" : "chair/t-back" });
    return zones.get(zoneId);
  }

  function addDrawing(zoneId) {
    const zoneData = zones.get(zoneId);
    return createCard(zoneId, zoneData.kind, { templateId: "blank" });
  }

  function getEditor(zoneId, drawingId) {
    return zones.get(zoneId)?.cards.get(drawingId)?.editor;
  }

  function findEditorForTool(drawingId) {
    for (const z of zones.values()) {
      const r = z.cards.get(drawingId);
      if (r) return r.editor;
    }
    return null;
  }

  function serializeZone(zoneId) {
    const zoneData = zones.get(zoneId);
    const out = [];
    zoneData.stack.querySelectorAll(".drawing-card").forEach((cardEl) => {
      const id = cardEl.dataset.drawingId;
      const rec = zoneData.cards.get(id);
      if (!rec) return;
      out.push({
        id,
        templateId: rec.select.value,
        svg: DrawingEditor.serializeSvg(rec.svg),
      });
    });
    return out;
  }

  function loadZone(zoneId, kind, drawings, legacySingleSvg) {
    const zoneData = zones.get(zoneId);
    for (const rec of zoneData.cards.values()) {
      DrawingEditor.detach(rec.svg);
    }
    zoneData.stack.innerHTML = "";
    zoneData.cards.clear();

    let list = drawings;
    if (!list?.length && legacySingleSvg) {
      list = [{ id: uid(), templateId: "blank", svg: legacySingleSvg }];
    }
    if (!list?.length) {
      createCard(zoneId, kind, {
        templateId: kind === "seat" ? "chair/t-cushion" : "chair/t-back",
      });
      return;
    }
    for (const d of list) {
      createCard(zoneId, kind, { templateId: d.templateId || "blank", svgHtml: d.svg || "" });
    }
  }

  function refreshAll() {
    for (const z of zones.values()) {
      for (const rec of z.cards.values()) rec.editor.refresh();
    }
  }

  function forEachEditor(fn) {
    for (const z of zones.values()) {
      for (const rec of z.cards.values()) fn(rec.editor);
    }
  }

  function setAllTools(tool) {
    forEachEditor((ed) => ed.setTool(tool));
  }

  function setAllShowHandles(show) {
    forEachEditor((ed) => ed.setShowHandles(show));
  }

  return {
    init,
    addDrawing,
    getEditor,
    findEditorForTool,
    serializeZone,
    loadZone,
    refreshAll,
    forEachEditor,
    setAllTools,
    setAllShowHandles,
    zones,
  };
})();
