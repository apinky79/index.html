/**
 * SVG editor with visible anchor handles — reshape polygons, move line endpoints,
 * drag labels, add text/lines. Handles are rebuilt after each load (not saved).
 */
const DrawingEditor = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const HANDLE_R = 11;
  const TAP_MAX_PX = 12;

  /** @type {Map<SVGSVGElement, object>} */
  const states = new Map();

  function svgEl(tag, attrs = {}) {
    const e = document.createElementNS(NS, tag);
    for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, String(v));
    return e;
  }

  function parsePoints(str) {
    return (str || "")
      .trim()
      .split(/\s+/)
      .map((p) => p.split(",").map(Number))
      .filter((p) => p.length === 2 && !Number.isNaN(p[0]));
  }

  function pointsToStr(pts) {
    return pts.map(([x, y]) => `${x},${y}`).join(" ");
  }

  function clientToSvg(svg, viewport, clientX, clientY) {
    const rect = viewport.getBoundingClientRect();
    const vb = (svg.getAttribute("viewBox") || "0 0 520 420").split(/\s+/).map(Number);
    const x = vb[0] + ((clientX - rect.left) / rect.width) * vb[2];
    const y = vb[1] + ((clientY - rect.top) / rect.height) * vb[3];
    return { x, y };
  }

  function ensureMarker(svg) {
    let defs = svg.querySelector("defs");
    if (!defs) {
      defs = svgEl("defs");
      svg.insertBefore(defs, svg.firstChild);
    }
    if (!defs.querySelector("#arrow")) {
      const m = svgEl("marker", { id: "arrow", markerWidth: 6, markerHeight: 6, refX: 3, refY: 3, orient: "auto" });
      m.appendChild(svgEl("path", { d: "M0,0 L6,3 L0,6 z", fill: "#111" }));
      defs.appendChild(m);
    }
  }

  function prepareContent(svg) {
    ensureMarker(svg);
    svg.querySelectorAll("text").forEach((t) => {
      t.classList.add("editable-label");
      if (!t.getAttribute("font-size")) t.setAttribute("font-size", "14");
    });
    svg.querySelectorAll("polygon, line").forEach((sh) => {
      sh.classList.add("shape-stroke");
    });
  }

  function stripHandles(svg) {
    svg.querySelectorAll(".editor-handles").forEach((n) => n.remove());
  }

  function serializeSvg(svg) {
    stripHandles(svg);
    svg.querySelectorAll(".selected").forEach((n) => n.classList.remove("selected"));
    const html = svg.innerHTML;
    rebuildHandles(getState(svg));
    return html;
  }

  function loadInline(svg, html) {
    svg.innerHTML = html || "";
    prepareContent(svg);
    rebuildHandles(getState(svg));
  }

  async function loadTemplate(svg, url) {
    if (!url) {
      svg.innerHTML = "";
      rebuildHandles(getState(svg));
      return;
    }
    const res = await fetch(url, { credentials: "same-origin" });
    if (!res.ok) throw new Error(`Could not load template (${res.status})`);
    const doc = new DOMParser().parseFromString(await res.text(), "image/svg+xml");
    const inner = doc.documentElement;
    svg.innerHTML = inner.innerHTML;
    if (inner.getAttribute("viewBox")) svg.setAttribute("viewBox", inner.getAttribute("viewBox"));
    prepareContent(svg);
    rebuildHandles(getState(svg));
  }

  function getState(svg) {
    return states.get(svg) || null;
  }

  function select(state, el) {
    if (!state) return;
    state.svg.querySelectorAll(".selected").forEach((n) => n.classList.remove("selected"));
    state.selected = el;
    if (el) el.classList.add("selected");
    if (state.onSelectionChange) state.onSelectionChange(el);
  }

  function makeHandle(state, cx, cy, meta) {
    const g = svgEl("g", { class: "anchor-handle" });
    const hit = svgEl("circle", { cx, cy, r: HANDLE_R + 6, class: "anchor-hit" });
    const dot = svgEl("circle", { cx, cy, r: HANDLE_R, class: "anchor-dot" });
    g.append(hit, dot);
    g._meta = meta;
    state.handlesLayer.appendChild(g);
    return g;
  }

  function rebuildHandles(state) {
    if (!state) return;
    stripHandles(state.svg);
    const layer = svgEl("g", { class: "editor-handles" });
    state.handlesLayer = layer;
    state.svg.appendChild(layer);

    state.svg.querySelectorAll("polygon").forEach((poly) => {
      parsePoints(poly.getAttribute("points")).forEach(([x, y], idx) => {
        makeHandle(state, x, y, { kind: "poly", poly, idx });
      });
    });

    state.svg.querySelectorAll("line").forEach((line) => {
      const x1 = +line.getAttribute("x1");
      const y1 = +line.getAttribute("y1");
      const x2 = +line.getAttribute("x2");
      const y2 = +line.getAttribute("y2");
      makeHandle(state, x1, y1, { kind: "line", line, end: 1 });
      makeHandle(state, x2, y2, { kind: "line", line, end: 2 });
    });

    state.svg.querySelectorAll("text.editable-label").forEach((text) => {
      const x = +(text.getAttribute("x") || 0);
      const y = +(text.getAttribute("y") || 0);
      makeHandle(state, x, y - 4, { kind: "text", text });
    });
  }

  function updateMetaLive(meta, x, y, handleG) {
    if (meta.kind === "poly") {
      const pts = parsePoints(meta.poly.getAttribute("points"));
      pts[meta.idx] = [x, y];
      meta.poly.setAttribute("points", pointsToStr(pts));
    } else if (meta.kind === "line") {
      if (meta.end === 1) {
        meta.line.setAttribute("x1", x);
        meta.line.setAttribute("y1", y);
      } else {
        meta.line.setAttribute("x2", x);
        meta.line.setAttribute("y2", y);
      }
    } else if (meta.kind === "text") {
      meta.text.setAttribute("x", x);
      meta.text.setAttribute("y", y + 4);
    }
    if (handleG) {
      handleG.querySelectorAll("circle").forEach((c) => {
        c.setAttribute("cx", x);
        c.setAttribute("cy", meta.kind === "text" ? y - 4 : y);
      });
    }
  }

  function nextCustomId(svg, prefix) {
    let n = 1;
    while (svg.querySelector(`#${prefix}-${n}`)) n += 1;
    return `${prefix}-${n}`;
  }

  function addText(state, x, y, label) {
    const g = svgEl("g", { id: nextCustomId(state.svg, "label"), class: "user-label" });
    const t = svgEl("text", {
      x, y, class: "editable-label", "font-size": 16, "font-weight": "bold", fill: "#111",
    });
    t.textContent = label || '0"';
    g.appendChild(t);
    state.svg.appendChild(g);
    prepareContent(state.svg);
    rebuildHandles(state);
    select(state, g);
    return t;
  }

  function addDimensionLine(state, x1, y1, x2, y2, label) {
    const id = nextCustomId(state.svg, "dim");
    const g = svgEl("g", { id, class: "user-dimension" });
    const line = svgEl("line", {
      x1, y1, x2, y2, stroke: "#111", "stroke-width": 1.5,
      "marker-start": "url(#arrow)", "marker-end": "url(#arrow)",
    });
    g.appendChild(line);
    const mx = (x1 + x2) / 2;
    const my = (y1 + y2) / 2 - 14;
    const t = svgEl("text", {
      x: mx, y: my, class: "editable-label", "text-anchor": "middle",
      "font-size": 14, "font-weight": "bold", fill: "#111",
    });
    t.textContent = label || '0"';
    g.appendChild(t);
    state.svg.appendChild(g);
    prepareContent(state.svg);
    rebuildHandles(state);
    select(state, g);
  }

  function deleteSelected(state) {
    if (!state.selected) return false;
    const el = state.selected;
    if (el.classList.contains("anchor-handle")) return false;
    const target = el.closest ? (el.closest("g[id]") || el) : el;
    if (target.classList && target.classList.contains("editor-handles")) return false;
    target.remove();
    select(state, null);
    rebuildHandles(state);
    return true;
  }

  function attach(viewport, svg, callbacks = {}) {
    const state = {
      svg,
      viewport,
      tool: "select",
      selected: null,
      drag: null,
      lineStart: null,
      onEditLabel: callbacks.onEditLabel || (() => {}),
      onEditLabelAsync: callbacks.onEditLabelAsync || (() => {}),
      onSelectionChange: callbacks.onSelectionChange || (() => {}),
      onToolChange: callbacks.onToolChange || (() => {}),
    };
    states.set(svg, state);
    prepareContent(svg);
    rebuildHandles(state);

    function hitHandle(target) {
      let n = target;
      while (n && n !== svg) {
        if (n.classList && n.classList.contains("anchor-handle")) return n;
        n = n.parentNode;
      }
      return null;
    }

    function hitShape(target) {
      let n = target;
      while (n && n !== svg) {
        if (n.tagName === "text" && n.classList.contains("editable-label")) return n;
        if (n.tagName === "line" || n.tagName === "polygon") return n;
        if (n.tagName === "g" && n.id && !n.classList.contains("editor-handles")) return n;
        n = n.parentNode;
      }
      return null;
    }

    viewport.addEventListener("pointerdown", (e) => {
      if (state.tool === "add-line" && !hitHandle(e.target)) {
        const pt = clientToSvg(svg, viewport, e.clientX, e.clientY);
        if (!state.lineStart) {
          state.lineStart = pt;
          flashViewport(viewport, "Tap second point for line end");
          e.preventDefault();
          return;
        }
        addDimensionLine(state, state.lineStart.x, state.lineStart.y, pt.x, pt.y);
        state.lineStart = null;
        state.tool = "select";
        if (state.onToolChange) state.onToolChange("select");
        e.preventDefault();
        return;
      }

      if (state.tool === "add-text" && !hitHandle(e.target)) {
        const pt = clientToSvg(svg, viewport, e.clientX, e.clientY);
        state.onEditLabelAsync(pt.x, pt.y, (label) => {
          if (label != null) addText(state, pt.x, pt.y, label);
          state.tool = "select";
          state.onToolChange("select");
        });
        e.preventDefault();
        return;
      }

      const handle = hitHandle(e.target);
      if (handle) {
        const meta = handle._meta;
        state.drag = {
          kind: "anchor",
          meta,
          handleG: handle,
          pid: e.pointerId,
          moved: false,
          sx: e.clientX,
          sy: e.clientY,
        };
        if (meta.text) select(state, meta.text);
        viewport.setPointerCapture(e.pointerId);
        e.preventDefault();
        return;
      }

      const shape = hitShape(e.target);
      if (shape) {
        select(state, shape.tagName === "g" ? shape : (shape.closest("g[id]") || shape));
        if (shape.tagName === "text") {
          state.drag = {
            kind: "text-tap",
            text: shape,
            pid: e.pointerId,
            moved: false,
            sx: e.clientX,
            sy: e.clientY,
          };
          viewport.setPointerCapture(e.pointerId);
        }
        e.preventDefault();
        return;
      }

      select(state, null);
    });

    viewport.addEventListener("pointermove", (e) => {
      if (!state.drag || state.drag.pid !== e.pointerId) return;
      const dx = e.clientX - state.drag.sx;
      const dy = e.clientY - state.drag.sy;
      if (Math.hypot(dx, dy) > TAP_MAX_PX) state.drag.moved = true;

      if (state.drag.kind === "anchor") {
        const pt = clientToSvg(svg, viewport, e.clientX, e.clientY);
        updateMetaLive(state.drag.meta, pt.x, pt.y, state.drag.handleG);
        e.preventDefault();
      }
    });

    function endPointer(e) {
      if (!state.drag || state.drag.pid !== e.pointerId) return;
      if (state.drag.kind === "anchor") {
        rebuildHandles(state);
      }
      if (state.drag.kind === "text-tap" && !state.drag.moved) {
        state.onEditLabel(state.drag.text);
      }
      state.drag = null;
      try { viewport.releasePointerCapture(e.pointerId); } catch { /* ignore */ }
    }
    viewport.addEventListener("pointerup", endPointer);
    viewport.addEventListener("pointercancel", endPointer);

    // Pinch zoom
    let pinch = null;
    viewport.addEventListener("touchstart", (e) => {
      if (e.touches.length !== 2) return;
      const vb = (svg.getAttribute("viewBox") || "0 0 520 420").split(/\s+/).map(Number);
      pinch = {
        d0: Math.hypot(e.touches[0].clientX - e.touches[1].clientX, e.touches[0].clientY - e.touches[1].clientY),
        vb: [...vb],
      };
    }, { passive: true });
    viewport.addEventListener("touchmove", (e) => {
      if (!pinch || e.touches.length !== 2) return;
      const d1 = Math.hypot(e.touches[0].clientX - e.touches[1].clientX, e.touches[0].clientY - e.touches[1].clientY);
      const scale = pinch.d0 / d1;
      const vb = pinch.vb;
      const w = vb[2] * scale;
      const h = vb[3] * scale;
      const cx = vb[0] + vb[2] / 2;
      const cy = vb[1] + vb[3] / 2;
      svg.setAttribute("viewBox", `${cx - w / 2} ${cy - h / 2} ${w} ${h}`);
    }, { passive: true });
    viewport.addEventListener("touchend", () => { pinch = null; });

    const api = {
      setTool(tool) {
        state.tool = tool;
        state.lineStart = null;
      },
      editSelected() {
        const el = state.selected;
        if (!el) return false;
        const text = el.tagName === "text" ? el : el.querySelector("text");
        if (text) {
          state.onEditLabel(text);
          return true;
        }
        return false;
      },
      deleteSelected: () => deleteSelected(state),
      refresh: () => rebuildHandles(state),
    };
    state._controller = api;
    return api;
  }

  function flashViewport(viewport, msg) {
    let tip = viewport.querySelector(".viewport-tip");
    if (!tip) {
      tip = document.createElement("div");
      tip.className = "viewport-tip no-print";
      viewport.appendChild(tip);
    }
    tip.textContent = msg;
    clearTimeout(tip._t);
    tip._t = setTimeout(() => tip.remove(), 2500);
  }

  return {
    attach,
    loadTemplate,
    loadInline,
    serializeSvg,
    getController: (svg) => states.get(svg)?._controller,
  };
})();
