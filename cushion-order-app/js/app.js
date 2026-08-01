(function () {
  "use strict";

  const loadDialog = document.getElementById("load-dialog");
  const editDialog = document.getElementById("edit-label-dialog");
  const editForm = document.getElementById("edit-label-form");
  const editTitle = editForm.querySelector("h3");
  const editInput = document.getElementById("edit-label-input");

  let currentDraftId = Storage.newId();
  let editingTextEl = null;
  let pendingPlacement = null;

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
    pendingPlacement = { x, y, cb };
    editingTextEl = null;
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
      DrawingPanels.refreshAll();
      return;
    }
    if (editingTextEl) {
      editingTextEl.textContent = editInput.value.trim() || editingTextEl.textContent;
      DrawingPanels.refreshAll();
    }
    editingTextEl = null;
  });

  function setToolButtonsActive(drawingId, tool) {
    const bar = document.querySelector(`.draw-tools[data-drawing-id="${drawingId}"]`);
    if (!bar) return;
    bar.querySelectorAll("[data-tool]").forEach((btn) => {
      btn.classList.toggle("active", btn.dataset.tool === tool);
    });
  }

  function wireToolButtons(toolsEl) {
    const drawingId = toolsEl.dataset.drawingId;
    toolsEl.querySelectorAll("[data-tool]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const editor = DrawingPanels.findEditorForTool(drawingId);
        if (!editor) return;
        const tool = btn.dataset.tool;
        if (tool === "add-text") {
          editor.setTool("add-text");
          flash("Tap on the drawing where you want the label");
          setToolButtonsActive(drawingId, "add-text");
        } else if (tool === "add-line") {
          editor.setTool("add-line");
          flash("Tap start point, then end point for the dimension line");
          setToolButtonsActive(drawingId, "add-line");
        } else if (tool === "edit-text") {
          if (!editor.editSelected()) flash("Tap a label first, then Edit");
        } else if (tool === "delete") {
          if (!editor.deleteSelected()) flash("Tap a line or label to select, then Delete");
        }
      });
    });
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
      seatDrawings: DrawingPanels.serializeZone("drawings-seat"),
      backDrawings: DrawingPanels.serializeZone("drawings-back"),
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

    DrawingPanels.loadZone(
      "drawings-seat",
      "seat",
      data.seatDrawings,
      data.seatSvg,
    );
    DrawingPanels.loadZone(
      "drawings-back",
      "back",
      data.backDrawings,
      data.backSvg,
    );
    wireAllToolButtons();
  }

  function wireAllToolButtons() {
    document.querySelectorAll(".draw-tools[data-drawing-id]").forEach((bar) => {
      if (bar.dataset.wired) return;
      bar.dataset.wired = "1";
      wireToolButtons(bar);
    });
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
    DrawingPanels.loadZone("drawings-seat", "seat", null, null);
    DrawingPanels.loadZone("drawings-back", "back", null, null);
    wireAllToolButtons();
  }

  function syncPrintFields() {
    const item = document.querySelector('input[name="item"]:checked');
    const sizeType = document.querySelector('input[name="sizeType"]:checked');
    const filling = [...document.querySelectorAll('input[name="filling"]:checked')].map((c) => c.value);
    const border = [...document.querySelectorAll('input[name="border"]:checked')].map((c) => c.value);
    const sizeLabels = {
      "outer-barrier": "Outer case finished size, With barrier",
      "outer-no-barrier": "Outer case finished size, No barrier",
      "inner-barrier": "Inner case finished size, With barrier",
      "inner-no-barrier": "Inner case finished size, No barrier",
      foam: "Foam size",
    };
    const map = {
      design: document.getElementById("design").value,
      item: item ? item.value : "",
      qty: document.getElementById("qty").value,
      filling: filling.join(", "),
      border: border.join(", "),
      sizeType: sizeType ? sizeLabels[sizeType.value] || sizeType.value : "",
      notes: document.getElementById("notes").value,
    };
    document.querySelectorAll("[data-print]").forEach((el) => {
      el.textContent = map[el.dataset.print] || "";
    });
  }

  function exportPdf() {
    syncPrintFields();
    window.print();
  }

  let autosaveTimer;
  function scheduleAutosave() {
    clearTimeout(autosaveTimer);
    autosaveTimer = setTimeout(saveDraft, 3000);
  }

  window.CushionApp = {
    openEditExisting,
    promptNewLabel,
    setToolButtonsActive,
    flash,
  };

  document.getElementById("print-area").addEventListener("input", scheduleAutosave);
  document.getElementById("print-area").addEventListener("change", scheduleAutosave);

  document.getElementById("btn-save").addEventListener("click", saveDraft);
  document.getElementById("btn-load").addEventListener("click", showLoadDialog);
  document.getElementById("btn-new").addEventListener("click", newOrder);
  document.getElementById("btn-export").addEventListener("click", exportPdf);

  document.querySelectorAll('[data-action="add-drawing"]').forEach((btn) => {
    btn.addEventListener("click", () => {
      const zoneId = btn.dataset.zone;
      DrawingPanels.addDrawing(zoneId);
      wireAllToolButtons();
      syncDrawModes();
      flash("New drawing added");
    });
  });

  DrawingPanels.init("drawings-seat", "seat");
  DrawingPanels.init("drawings-back", "back");
  wireAllToolButtons();

  let penMode = false;
  let curveMode = false;
  const btnPen = document.getElementById("btn-pen");
  const btnCurves = document.getElementById("btn-curves");
  const drawMore = document.getElementById("draw-more");

  function syncDrawModes() {
    document.body.classList.toggle("mode-pen", penMode);
    document.body.classList.toggle("mode-curves", curveMode);
    document.body.classList.toggle("show-draw-tools", drawMore.open);
    btnPen.classList.toggle("active", penMode);
    btnCurves.classList.toggle("active", curveMode);
    DrawingPanels.setAllShowHandles(curveMode);
    if (penMode) {
      DrawingPanels.setAllTools("pen");
    } else {
      DrawingPanels.setAllTools("select");
    }
  }

  btnPen.addEventListener("click", () => {
    penMode = !penMode;
    if (penMode) curveMode = false;
    syncDrawModes();
    flash(penMode ? "Draw with finger or stylus — like pen on paper" : "Drawing off — tap measurements to edit");
  });

  btnCurves.addEventListener("click", () => {
    curveMode = !curveMode;
    if (curveMode) penMode = false;
    syncDrawModes();
    flash(curveMode ? "Drag edge dots to curve T-cushion corners" : "Curve mode off");
  });

  drawMore.addEventListener("toggle", syncDrawModes);

  syncDrawModes();

  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("sw.js").catch(() => {});
  }
})();
