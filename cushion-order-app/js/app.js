(function () {
  "use strict";

  const seatSvg = document.getElementById("svg-seat");
  const backSvg = document.getElementById("svg-back");
  const seatSelect = document.getElementById("seat-template");
  const backSelect = document.getElementById("back-template");
  const loadDialog = document.getElementById("load-dialog");
  const editDialog = document.getElementById("edit-label-dialog");
  const editForm = document.getElementById("edit-label-form");
  const editTitle = editForm.querySelector("h3");
  const editInput = document.getElementById("edit-label-input");

  let currentDraftId = Storage.newId();
  let editingTextEl = null;
  let pendingPlacement = null;

  const seatEditor = DrawingEditor.attach(document.getElementById("viewport-seat"), seatSvg, {
    onEditLabel: openEditExisting,
    onEditLabelAsync: promptNewLabel,
    onToolChange: (tool) => setToolButtonsActive("seat", tool),
  });

  const backEditor = DrawingEditor.attach(document.getElementById("viewport-back"), backSvg, {
    onEditLabel: openEditExisting,
    onEditLabelAsync: promptNewLabel,
    onToolChange: (tool) => setToolButtonsActive("back", tool),
  });

  function editorForPanel(panel) {
    return panel === "seat" ? seatEditor : backEditor;
  }

  function openEditExisting(textEl) {
    editingTextEl = textEl;
    pendingPlacement = null;
    editTitle.textContent = "Edit measurement or note";
    editInput.value = textEl.textContent.trim();
    editDialog.showModal();
    editInput.focus();
    editInput.select();
  }

  function promptNewLabel(x, y, cb) {
    editTitle.textContent = "New measurement or note";
    editInput.value = '0"';
    editDialog.showModal();
    editInput.focus();
    editInput.select();
  }

  editDialog.addEventListener("close", () => {
    if (editDialog.returnValue !== "ok") {
      pendingPlacement = null;
      editingTextEl = null;
      return;
    }
    if (pendingPlacement) {
      pendingPlacement.cb(editInput.value.trim() || '0"');
      pendingPlacement = null;
      seatEditor.refresh();
      backEditor.refresh();
      return;
    }
    if (editingTextEl) {
      editingTextEl.textContent = editInput.value.trim() || editingTextEl.textContent;
      seatEditor.refresh();
      backEditor.refresh();
    }
    editingTextEl = null;
  });

  function setToolButtonsActive(panel, tool) {
    document.querySelectorAll(`.draw-tools[data-panel="${panel}"] [data-tool]`).forEach((btn) => {
      const active = btn.dataset.tool === tool || (tool === "select" && btn.dataset.tool === "select");
      btn.classList.toggle("active", active);
    });
  }

  function populateSelects() {
    for (const t of CUSHION_TEMPLATES.seat) {
      seatSelect.append(new Option(t.name, t.id));
    }
    for (const t of CUSHION_TEMPLATES.back) {
      backSelect.append(new Option(t.name, t.id));
    }
    seatSelect.value = "chair/t-cushion";
    backSelect.value = "chair/t-back";
  }

  function templatePath(kind, id) {
    const list = CUSHION_TEMPLATES[kind];
    return (list.find((t) => t.id === id) || list[0]).path;
  }

  async function loadPanel(kind, id) {
    const svg = kind === "seat" ? seatSvg : backSvg;
    const path = templatePath(kind, id);
    try {
      await DrawingEditor.loadTemplate(svg, path);
    } catch (err) {
      console.error(err);
      alert("Could not load template. Open the app from the same website folder as cushion-order-kit.");
    }
  }

  function collectForm() {
    const filling = [...document.querySelectorAll('input[name="filling"]:checked')].map((c) => c.value);
    const border = [...document.querySelectorAll('input[name="border"]:checked')].map((c) => c.value);
    const item = document.querySelector('input[name="item"]:checked');
    const sizeType = document.querySelector('input[name="sizeType"]:checked');
    return {
      id: currentDraftId,
      orderNo: document.getElementById("order-no").value,
      to: document.getElementById("to").value,
      from: document.getElementById("from").value,
      design: document.getElementById("design").value,
      qty: document.getElementById("qty").value,
      item: item ? item.value : "",
      sizeType: sizeType ? sizeType.value : "",
      filling,
      border,
      notes: document.getElementById("notes").value,
      seatTemplate: seatSelect.value,
      backTemplate: backSelect.value,
      seatSvg: DrawingEditor.serializeSvg(seatSvg),
      backSvg: DrawingEditor.serializeSvg(backSvg),
    };
  }

  function applyForm(data) {
    currentDraftId = data.id || Storage.newId();
    document.getElementById("order-no").value = data.orderNo || "";
    document.getElementById("to").value = data.to || "";
    document.getElementById("from").value = data.from || "";
    document.getElementById("design").value = data.design || "";
    document.getElementById("qty").value = data.qty || "";
    document.getElementById("notes").value = data.notes || "";
    seatSelect.value = data.seatTemplate || "blank";
    backSelect.value = data.backTemplate || "blank";

    document.querySelectorAll('input[name="item"]').forEach((r) => {
      r.checked = r.value === data.item;
    });
    document.querySelectorAll('input[name="sizeType"]').forEach((r) => {
      r.checked = r.value === data.sizeType;
    });
    document.querySelectorAll('input[name="filling"]').forEach((c) => {
      c.checked = (data.filling || []).includes(c.value);
    });
    document.querySelectorAll('input[name="border"]').forEach((c) => {
      c.checked = (data.border || []).includes(c.value);
    });

    DrawingEditor.loadInline(seatSvg, data.seatSvg || "");
    DrawingEditor.loadInline(backSvg, data.backSvg || "");
  }

  function saveDraft() {
    Storage.save(collectForm());
    flash("Draft saved on this device");
  }

  function flash(msg) {
    const el = document.createElement("div");
    el.className = "toast";
    el.textContent = msg;
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 2200);
  }

  function showLoadDialog() {
    const list = document.getElementById("draft-list");
    const empty = document.getElementById("no-drafts");
    list.innerHTML = "";
    const drafts = Storage.readAll();
    empty.hidden = drafts.length > 0;
    for (const d of drafts) {
      const li = document.createElement("li");
      const label = document.createElement("span");
      label.textContent = `${d.orderNo || "No order #"} · ${d.design || "Untitled"} · ${new Date(d.updatedAt).toLocaleString()}`;
      const openBtn = document.createElement("button");
      openBtn.type = "button";
      openBtn.className = "btn btn-sm btn-primary";
      openBtn.textContent = "Open";
      openBtn.addEventListener("click", () => {
        applyForm(d);
        loadDialog.close();
      });
      const delBtn = document.createElement("button");
      delBtn.type = "button";
      delBtn.className = "btn btn-sm btn-ghost";
      delBtn.textContent = "Delete";
      delBtn.addEventListener("click", () => {
        Storage.remove(d.id);
        li.remove();
        empty.hidden = Storage.readAll().length > 0;
      });
      li.append(label, openBtn, delBtn);
      list.appendChild(li);
    }
    loadDialog.showModal();
  }

  function newOrder() {
    if (!confirm("Start a new blank order? Unsaved changes will be lost unless you saved a draft.")) return;
    currentDraftId = Storage.newId();
    document.getElementById("order-no").value = "";
    document.getElementById("to").value = "";
    document.getElementById("from").value = "";
    document.getElementById("design").value = "";
    document.getElementById("qty").value = "";
    document.getElementById("notes").value = "";
    document.querySelector('input[name="item"][value="chair"]').checked = true;
    document.querySelector('input[name="sizeType"][value="outer-barrier"]').checked = true;
    document.querySelectorAll('input[name="filling"], input[name="border"]').forEach((c) => { c.checked = false; });
    seatSelect.value = "chair/t-cushion";
    backSelect.value = "chair/t-back";
    loadPanel("seat", seatSelect.value);
    loadPanel("back", backSelect.value);
  }

  function exportPdf() {
    window.print();
  }

  let autosaveTimer;
  function scheduleAutosave() {
    clearTimeout(autosaveTimer);
    autosaveTimer = setTimeout(saveDraft, 3000);
  }

  document.getElementById("print-area").addEventListener("input", scheduleAutosave);
  document.getElementById("print-area").addEventListener("change", scheduleAutosave);

  document.getElementById("btn-save").addEventListener("click", saveDraft);
  document.getElementById("btn-load").addEventListener("click", showLoadDialog);
  document.getElementById("btn-new").addEventListener("click", newOrder);
  document.getElementById("btn-export").addEventListener("click", exportPdf);

  seatSelect.addEventListener("change", () => loadPanel("seat", seatSelect.value));
  backSelect.addEventListener("change", () => loadPanel("back", backSelect.value));

  document.querySelectorAll('[data-action="clear"]').forEach((btn) => {
    btn.addEventListener("click", () => {
      const panel = btn.dataset.panel;
      const svg = panel === "seat" ? seatSvg : backSvg;
      svg.innerHTML = "";
      editorForPanel(panel).refresh();
      if (panel === "seat") seatSelect.value = "blank";
      else backSelect.value = "blank";
    });
  });

  document.querySelectorAll(".draw-tools").forEach((bar) => {
    const panel = bar.dataset.panel;
    const ed = editorForPanel(panel);
    bar.querySelectorAll("[data-tool]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const tool = btn.dataset.tool;
        if (tool === "add-text") {
          ed.setTool("add-text");
          flash("Tap on the drawing where you want the label");
          setToolButtonsActive(panel, "add-text");
        } else if (tool === "add-line") {
          ed.setTool("add-line");
          flash("Tap start point, then end point for the dimension line");
          setToolButtonsActive(panel, "add-line");
        } else if (tool === "edit-text") {
          if (!ed.editSelected()) flash("Tap a label first, then Edit");
        } else if (tool === "delete") {
          if (!ed.deleteSelected()) flash("Tap a line or label to select, then Delete");
        }
      });
    });
  });

  populateSelects();
  loadPanel("seat", seatSelect.value);
  loadPanel("back", backSelect.value);

  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("sw.js").catch(() => {});
  }
})();
