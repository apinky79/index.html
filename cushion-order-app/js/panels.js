/**
 * Manages multiple drawing canvases per seat/back section.
 */
const DrawingPanels = (() => {
  const NS = "http://www.w3.org/2000/svg";
  let drawingCounter = 0;
  /** @type {Map<string, { zone: string, cards: Map<string, object> }>} */
  const zones = new Map();

  function uid() {
    drawingCounter += 1;
    return `d${drawingCounter}-${Date.now().toString(36)}`;
  }

  function templateOptions(kind) {
    return CUSHION_TEMPLATES[kind] || [];
  }

  function createCard(zone, kind, { templateId = "blank", svgHtml = null } = {}) {
    const id = uid();
    const card = document.createElement("div");
    card.className = "drawing-card";
    card.dataset.drawingId = id;

    const head = document.createElement("div");
    head.className = "drawing-card-head no-print";
    const title = document.createElement("span");
    title.className = "drawing-card-title";
    const zoneData = zones.get(zone);
    title.textContent = `Drawing ${zoneData.cards.size + 1}`;
    const removeBtn = document.createElement("button");
    removeBtn.type = "button";
    removeBtn.className = "drawing-remove";
    removeBtn.textContent = "Remove";
    removeBtn.addEventListener("click", () => removeCard(zone, id));
    head.append(title, removeBtn);

    const toolbar = document.createElement("div");
    toolbar.className = "panel-toolbar no-print";

    const tplLabel = document.createElement("label");
    tplLabel.textContent = "Style";
    const select = document.createElement("select");
    select.className = "template-select";
    select.setAttribute("aria-label", `${kind} template`);
    for (const t of templateOptions(kind)) {
      select.append(new Option(t.name, t.id));
    }
    select.value = templateId;
    tplLabel.appendChild(select);

    const tools = document.createElement("div");
    tools.className = "draw-tools";
    tools.dataset.drawingId = id;
    for (const [tool, label] of [
      ["add-text", "Text"],
      ["add-line", "Line"],
      ["add-curve", "Curve"],
      ["curve-done", "Done"],
      ["edit-text", "Edit"],
      ["delete", "Delete"],
    ]) {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "tool-btn";
      b.dataset.tool = tool;
      b.textContent = label;
      if (tool === "curve-done") b.hidden = true;
      tools.appendChild(b);
    }

    const clearBtn = document.createElement("button");
    clearBtn.type = "button";
    clearBtn.className = "tool-clear";
    clearBtn.textContent = "Clear";
    clearBtn.addEventListener("click", () => {
      svg.innerHTML = "";
      editor.refresh();
      select.value = "blank";
    });

    toolbar.append(tplLabel, tools, clearBtn);

    const viewport = document.createElement("div");
    viewport.className = "svg-viewport";
    viewport.tabIndex = 0;
    const svg = document.createElementNS(NS, "svg");
    svg.setAttribute("xmlns", NS);
    svg.setAttribute("viewBox", "0 0 520 420");
    viewport.appendChild(svg);

    card.append(head, toolbar, viewport);

    const editor = DrawingEditor.attach(viewport, svg, {
      onEditLabel: (textEl) => window.CushionApp.openEditExisting(textEl),
      onEditLabelAsync: (x, y, cb) => window.CushionApp.promptNewLabel(x, y, cb),
      onToolChange: (tool) => window.CushionApp.setToolButtonsActive(id, tool),
    });

    select.addEventListener("change", async () => {
      const path = templateOptions(kind).find((t) => t.id === select.value)?.path;
      try {
        await DrawingEditor.loadTemplate(svg, path);
      } catch (e) {
        console.error(e);
      }
    });

    const record = { id, card, svg, viewport, editor, select, tools, kind };
    zoneData.cards.set(id, record);
    zoneData.stack.appendChild(card);
    renumberCards(zone);

    (async () => {
      if (svgHtml) {
        DrawingEditor.loadInline(svg, svgHtml);
      } else if (templateId !== "blank") {
        const path = templateOptions(kind).find((t) => t.id === templateId)?.path;
        try {
          await DrawingEditor.loadTemplate(svg, path);
        } catch (e) {
          console.error(e);
        }
      }
    })();

    return record;
  }

  function renumberCards(zone) {
    const zoneData = zones.get(zone);
    let i = 1;
    zoneData.stack.querySelectorAll(".drawing-card").forEach((card) => {
      const t = card.querySelector(".drawing-card-title");
      if (t) t.textContent = `Drawing ${i++}`;
    });
  }

  function removeCard(zone, id) {
    const zoneData = zones.get(zone);
    const record = zoneData.cards.get(id);
    if (!record) return;
    if (zoneData.cards.size <= 1) {
      record.svg.innerHTML = "";
      record.editor.refresh();
      record.select.value = "blank";
      return;
    }
    record.card.remove();
    zoneData.cards.delete(id);
    renumberCards(zone);
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
    zoneData.stack.querySelectorAll(".drawing-card").forEach((card) => {
      const id = card.dataset.drawingId;
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

  return {
    init,
    addDrawing,
    getEditor,
    findEditorForTool,
    serializeZone,
    loadZone,
    refreshAll,
    zones,
  };
})();
