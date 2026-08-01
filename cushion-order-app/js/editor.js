/**
 * SVG editor — anchor points, curves (path Q/C), edge bulge, lines, labels.
 */
const DrawingEditor = (() => {
  const NS = "http://www.w3.org/2000/svg";
  const HANDLE_R = 6;
  const TAP_MAX_PX = 12;

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
    return {
      x: vb[0] + ((clientX - rect.left) / rect.width) * vb[2],
      y: vb[1] + ((clientY - rect.top) / rect.height) * vb[3],
    };
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
    svg.querySelectorAll("polygon, line, path").forEach((sh) => {
      sh.classList.add("shape-stroke");
    });
  }

  function stripHandles(svg) {
    svg.querySelectorAll(".editor-handles, .curve-preview").forEach((n) => n.remove());
  }

  // --- Path parse / build ---

  function parsePathD(d) {
    const seg = [];
    const re = /([MmLlHhVvQqCcZz])|(-?\d*\.?\d+(?:e[-+]?\d+)?)/g;
    const tokens = [];
    let m;
    while ((m = re.exec(d)) !== null) tokens.push(m[1] || parseFloat(m[2]));
    let i = 0;
    let cx = 0;
    let cy = 0;
    let sx = 0;
    let sy = 0;

    function read() {
      return tokens[i++];
    }

    while (i < tokens.length) {
      const cmd = tokens[i++];
      if (typeof cmd !== "string") break;
      const rel = cmd === cmd.toLowerCase();
      const c = cmd.toUpperCase();
      if (c === "M") {
        cx = read();
        cy = read();
        if (rel) {
          cx += sx;
          cy += sy;
        }
        sx = cx;
        sy = cy;
        seg.push({ t: "M", x: cx, y: cy });
      } else if (c === "L") {
        cx = read();
        cy = read();
        if (rel) {
          cx += sx;
          cy += sy;
        }
        seg.push({ t: "L", x: cx, y: cy });
      } else if (c === "Q") {
        const x1 = read();
        const y1 = read();
        cx = read();
        cy = read();
        if (rel) {
          seg.push({ t: "Q", x1: sx + x1, y1: sy + y1, x: sx + cx, y: sy + cy });
          cx = sx + cx;
          cy = sy + cy;
        } else {
          seg.push({ t: "Q", x1, y1, x: cx, y: cy });
        }
      } else if (c === "C") {
        const x1 = read();
        const y1 = read();
        const x2 = read();
        const y2 = read();
        cx = read();
        cy = read();
        if (rel) {
          seg.push({
            t: "C", x1: sx + x1, y1: sy + y1, x2: sx + x2, y2: sy + y2, x: sx + cx, y: sy + cy,
          });
          cx = sx + cx;
          cy = sy + cy;
        } else {
          seg.push({ t: "C", x1, y1, x2, y2, x: cx, y: cy });
        }
      } else if (c === "Z") {
        seg.push({ t: "Z" });
        cx = sx;
        cy = sy;
      }
      sx = cx;
      sy = cy;
    }
    return seg;
  }

  function buildPathD(segments) {
    let d = "";
    let px = 0;
    let py = 0;
    for (const s of segments) {
      if (s.t === "M") {
        d += `M ${s.x} ${s.y} `;
        px = s.x;
        py = s.y;
      } else if (s.t === "L") {
        d += `L ${s.x} ${s.y} `;
        px = s.x;
        py = s.y;
      } else if (s.t === "Q") {
        d += `Q ${s.x1} ${s.y1} ${s.x} ${s.y} `;
        px = s.x;
        py = s.y;
      } else if (s.t === "C") {
        d += `C ${s.x1} ${s.y1} ${s.x2} ${s.y2} ${s.x} ${s.y} `;
        px = s.x;
        py = s.y;
      } else if (s.t === "Z") d += "Z ";
    }
    return d.trim();
  }

  function getPathSegments(pathEl) {
    if (!pathEl._segments) pathEl._segments = parsePathD(pathEl.getAttribute("d") || "");
    return pathEl._segments;
  }

  function syncPath(pathEl) {
    pathEl.setAttribute("d", buildPathD(getPathSegments(pathEl)));
    pathEl._segments = parsePathD(pathEl.getAttribute("d"));
  }

  function polygonToPath(poly) {
    const pts = parsePoints(poly.getAttribute("points"));
    if (pts.length < 2) return poly;
    const segs = [{ t: "M", x: pts[0][0], y: pts[0][1] }];
    for (let i = 1; i < pts.length; i++) segs.push({ t: "L", x: pts[i][0], y: pts[i][1] });
    segs.push({ t: "Z" });
    const path = svgEl("path", {
      d: buildPathD(segs),
      fill: "none",
      stroke: "#111",
      "stroke-width": poly.getAttribute("stroke-width") || 2.5,
      class: "shape-stroke",
    });
    path._segments = segs;
    if (poly.id) path.id = poly.id;
    poly.replaceWith(path);
    return path;
  }

  function smoothPathThroughPoints(pts, closed = false) {
    if (pts.length < 2) return "";
    const segs = [{ t: "M", x: pts[0].x, y: pts[0].y }];
    const n = pts.length;
    for (let i = 0; i < n - 1; i++) {
      const p0 = pts[Math.max(0, i - 1)];
      const p1 = pts[i];
      const p2 = pts[i + 1];
      const p3 = pts[Math.min(n - 1, i + 2)];
      const cp1x = p1.x + (p2.x - p0.x) / 6;
      const cp1y = p1.y + (p2.y - p0.y) / 6;
      const cp2x = p2.x - (p3.x - p1.x) / 6;
      const cp2y = p2.y - (p3.y - p1.y) / 6;
      segs.push({ t: "C", x1: cp1x, y1: cp1y, x2: cp2x, y2: cp2y, x: p2.x, y: p2.y });
    }
    if (closed && n > 2) segs.push({ t: "Z" });
    return buildPathD(segs);
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
    svg.querySelectorAll("path").forEach((p) => {
      p._segments = parsePathD(p.getAttribute("d") || "");
    });
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
    svg.innerHTML = doc.documentElement.innerHTML;
    if (doc.documentElement.getAttribute("viewBox")) {
      svg.setAttribute("viewBox", doc.documentElement.getAttribute("viewBox"));
    }
    svg.querySelectorAll("path").forEach((p) => {
      p._segments = parsePathD(p.getAttribute("d") || "");
    });
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

  function makeHandle(state, cx, cy, meta, cls = "anchor-dot") {
    const g = svgEl("g", { class: `anchor-handle ${meta.handleClass || ""}` });
    const hit = svgEl("circle", { cx, cy, r: HANDLE_R + 8, class: "anchor-hit" });
    const dot = svgEl("circle", { cx, cy, r: HANDLE_R, class: cls });
    g.append(hit, dot);
    g._meta = meta;
    state.handlesLayer.appendChild(g);
    return g;
  }

  function isOutlineShape(el) {
    if (!el || el.closest(".editor-handles")) return false;
    if (el.closest("[id^='dimension']") || el.closest(".user-dimension") || el.closest("#title")) return false;
    if (el.closest("#outline") || el.id === "outline") return true;
    if (el.classList.contains("user-curve")) return true;
    return el.tagName === "polygon";
  }

  function applyPathEdgeBulge(path, segIdx, ctrlX, ctrlY) {
    const segs = getPathSegments(path);
    const seg = segs[segIdx];
    if (seg?.t !== "L") return path;
    segs[segIdx] = { t: "Q", x1: ctrlX, y1: ctrlY, x: seg.x, y: seg.y };
    syncPath(path);
    return path;
  }

  function addPolygonEdgeHandles(state, poly) {
    const pts = parsePoints(poly.getAttribute("points"));
    for (let i = 0; i < pts.length; i++) {
      const mx = (pts[i][0] + pts[(i + 1) % pts.length][0]) / 2;
      const my = (pts[i][1] + pts[(i + 1) % pts.length][1]) / 2;
      makeHandle(state, mx, my, { kind: "edge", poly, i }, "anchor-curve");
    }
  }

  function addPathCurveHandles(state, path) {
    const segs = getPathSegments(path);
    let prev = null;
    for (let i = 0; i < segs.length; i++) {
      const s = segs[i];
      if (s.t === "L" && prev && prev.t !== "Z") {
        const px = prev.x;
        const py = prev.y;
        makeHandle(state, (px + s.x) / 2, (py + s.y) / 2, {
          kind: "path-edge", path, segIdx: i,
        }, "anchor-curve");
      } else if (s.t === "Q") {
        makeHandle(state, s.x1, s.y1, { kind: "path-q1", path, segIdx: i }, "anchor-curve");
      }
      if (s.t === "M" || s.t === "L" || s.t === "Q" || s.t === "C") prev = s;
    }
  }

  function rebuildHandles(state) {
    if (!state) return;
    stripHandles(state.svg);
    const layer = svgEl("g", { class: "editor-handles" });
    state.handlesLayer = layer;
    state.svg.appendChild(layer);

    state.svg.querySelectorAll("polygon").forEach((poly) => {
      if (isOutlineShape(poly)) addPolygonEdgeHandles(state, poly);
    });

    state.svg.querySelectorAll("path").forEach((path) => {
      if (!isOutlineShape(path)) return;
      if (path.closest("polygon")) return;
      addPathCurveHandles(state, path);
    });

    updateCurvePreview(state);
  }

  function updateCurvePreview(state) {
    state.svg.querySelectorAll(".curve-preview").forEach((n) => n.remove());
    if (!state.curvePoints?.length) return;
    const g = svgEl("g", { class: "curve-preview" });
    const pts = state.curvePoints;
    let d = `M ${pts[0].x} ${pts[0].y}`;
    for (let i = 1; i < pts.length; i++) d += ` L ${pts[i].x} ${pts[i].y}`;
    g.appendChild(svgEl("path", {
      d, fill: "none", stroke: "#1a4d8c", "stroke-width": 2, "stroke-dasharray": "6 4",
    }));
    pts.forEach((p) => {
      g.appendChild(svgEl("circle", { cx: p.x, cy: p.y, r: 6, fill: "#1a4d8c" }));
    });
    state.svg.appendChild(g);
  }

  function applyEdgeBulge(poly, edgeIndex, ctrlX, ctrlY) {
    const path = poly.tagName === "polygon" ? polygonToPath(poly) : poly;
    let verts = parsePoints(poly.getAttribute?.("points") || "");
    if (!verts.length) {
      for (const s of getPathSegments(path)) {
        if (s.t === "M" || s.t === "L") verts.push([s.x, s.y]);
      }
    }
    const n = verts.length;
    if (n < 2) return path;
    const newSegs = [{ t: "M", x: verts[0][0], y: verts[0][1] }];
    for (let e = 0; e < n; e++) {
      const j = (e + 1) % n;
      if (e === edgeIndex) {
        newSegs.push({ t: "Q", x1: ctrlX, y1: ctrlY, x: verts[j][0], y: verts[j][1] });
      } else {
        newSegs.push({ t: "L", x: verts[j][0], y: verts[j][1] });
      }
    }
    newSegs.push({ t: "Z" });
    path._segments = newSegs;
    syncPath(path);
    return path;
  }

  function updateMetaLive(meta, x, y, handleG) {
    if (meta.kind === "edge") {
      meta.poly = applyEdgeBulge(meta.poly, meta.i, x, y);
    } else if (meta.kind === "path-edge") {
      applyPathEdgeBulge(meta.path, meta.segIdx, x, y);
    } else if (meta.kind === "path-q1") {
      const segs = getPathSegments(meta.path);
      const s = segs[meta.segIdx];
      s.x1 = x;
      s.y1 = y;
      syncPath(meta.path);
    }
    if (handleG) {
      handleG.querySelectorAll("circle").forEach((c) => {
        c.setAttribute("cx", x);
        c.setAttribute("cy", y);
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
  }

  function addDimensionLine(state, x1, y1, x2, y2, label) {
    const g = svgEl("g", { id: nextCustomId(state.svg, "dim"), class: "user-dimension" });
    g.appendChild(svgEl("line", {
      x1, y1, x2, y2, stroke: "#111", "stroke-width": 1.5,
      "marker-start": "url(#arrow)", "marker-end": "url(#arrow)",
    }));
    const t = svgEl("text", {
      x: (x1 + x2) / 2, y: (y1 + y2) / 2 - 14, class: "editable-label",
      "text-anchor": "middle", "font-size": 14, "font-weight": "bold", fill: "#111",
    });
    t.textContent = label || '0"';
    g.appendChild(t);
    state.svg.appendChild(g);
    prepareContent(state.svg);
    rebuildHandles(state);
    select(state, g);
  }

  function finishCurve(state, closed = false) {
    if (!state.curvePoints || state.curvePoints.length < 2) {
      state.curvePoints = [];
      updateCurvePreview(state);
      return;
    }
    const d = smoothPathThroughPoints(state.curvePoints, closed);
    const path = svgEl("path", {
      id: nextCustomId(state.svg, "curve"),
      d,
      fill: "none",
      stroke: "#111",
      "stroke-width": 2.5,
      class: "shape-stroke user-curve",
    });
    path._segments = parsePathD(d);
    state.svg.appendChild(path);
    state.curvePoints = [];
    state.tool = "select";
    if (state.onToolChange) state.onToolChange("select");
    prepareContent(state.svg);
    rebuildHandles(state);
    select(state, path);
  }

  function deleteSelected(state) {
    if (!state.selected) return false;
    const el = state.selected;
    const target = el.closest ? (el.closest("g[id]") || el) : el;
    if (target.classList?.contains("editor-handles")) return false;
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
      curvePoints: [],
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
        if (n.classList?.contains("anchor-handle")) return n;
        n = n.parentNode;
      }
      return null;
    }

    function hitShape(target) {
      let n = target;
      while (n && n !== svg) {
        if (n.tagName === "text" && n.classList.contains("editable-label")) return n;
        if (["line", "polygon", "path"].includes(n.tagName)) return n;
        if (n.tagName === "g" && n.id && !n.classList.contains("editor-handles")) return n;
        n = n.parentNode;
      }
      return null;
    }

    viewport.addEventListener("pointerdown", (e) => {
      if (state.tool === "add-curve" && !hitHandle(e.target)) {
        const pt = clientToSvg(svg, viewport, e.clientX, e.clientY);
        state.curvePoints.push(pt);
        updateCurvePreview(state);
        flashViewport(viewport, `Point ${state.curvePoints.length} — tap Finish curve when done`);
        e.preventDefault();
        return;
      }

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
        state.onToolChange("select");
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
        state.drag = {
          kind: "anchor",
          meta: handle._meta,
          handleG: handle,
          pid: e.pointerId,
          moved: false,
          sx: e.clientX,
          sy: e.clientY,
        };
        viewport.setPointerCapture(e.pointerId);
        e.preventDefault();
        return;
      }

      const shape = hitShape(e.target);
      if (shape) {
        select(state, shape.tagName === "g" ? shape : (shape.closest("g[id]") || shape));
        if (shape.tagName === "text") {
          state.drag = { kind: "text-tap", text: shape, pid: e.pointerId, moved: false, sx: e.clientX, sy: e.clientY };
          viewport.setPointerCapture(e.pointerId);
        }
        e.preventDefault();
        return;
      }
      select(state, null);
    });

    viewport.addEventListener("pointermove", (e) => {
      if (!state.drag || state.drag.pid !== e.pointerId) return;
      if (Math.hypot(e.clientX - state.drag.sx, e.clientY - state.drag.sy) > TAP_MAX_PX) state.drag.moved = true;
      if (state.drag.kind === "anchor") {
        const pt = clientToSvg(svg, viewport, e.clientX, e.clientY);
        updateMetaLive(state.drag.meta, pt.x, pt.y, state.drag.handleG);
        e.preventDefault();
      }
    });

    function endPointer(e) {
      if (!state.drag || state.drag.pid !== e.pointerId) return;
      if (state.drag.kind === "anchor") rebuildHandles(state);
      if (state.drag.kind === "text-tap" && !state.drag.moved) state.onEditLabel(state.drag.text);
      state.drag = null;
      try { viewport.releasePointerCapture(e.pointerId); } catch { /* ignore */ }
    }
    viewport.addEventListener("pointerup", endPointer);
    viewport.addEventListener("pointercancel", endPointer);

    let pinch = null;
    viewport.addEventListener("touchstart", (e) => {
      if (e.touches.length !== 2) return;
      const vb = (svg.getAttribute("viewBox") || "0 0 520 420").split(/\s+/).map(Number);
      pinch = { d0: Math.hypot(e.touches[0].clientX - e.touches[1].clientX, e.touches[0].clientY - e.touches[1].clientY), vb: [...vb] };
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
        if (tool !== "add-curve") state.curvePoints = [];
        updateCurvePreview(state);
      },
      finishCurve: (closed) => finishCurve(state, closed),
      editSelected() {
        const el = state.selected;
        if (!el) return false;
        const text = el.tagName === "text" ? el : el.querySelector?.("text");
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
    tip._t = setTimeout(() => tip.remove(), 2800);
  }

  function detach(svg) {
    if (!svg) return;
    stripHandles(svg);
    states.delete(svg);
  }

  return { attach, detach, loadTemplate, loadInline, serializeSvg };
})();
