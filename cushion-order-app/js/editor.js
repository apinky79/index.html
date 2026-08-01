/**
 * SVG drawing editor — draggable groups, editable dimension labels, pinch zoom.
 */
const DrawingEditor = (() => {
  const DRAGGABLE_IDS = /^(outline|title|dimension-)/;

  function parseTranslate(el) {
    const t = el.getAttribute("transform") || "";
    const m = t.match(/translate\(\s*([-\d.]+)[,\s]+([-\d.]+)\s*\)/);
    return m ? { x: +m[1], y: +m[2] } : { x: 0, y: 0 };
  }

  function setTranslate(el, x, y) {
    el.setAttribute("transform", `translate(${x}, ${y})`);
  }

  function ensureDefs(svg) {
    if (svg.querySelector("defs")) return;
    const defs = document.createElementNS("http://www.w3.org/2000/svg", "defs");
    svg.insertBefore(defs, svg.firstChild);
  }

  function prepareSvgRoot(svg) {
    ensureDefs(svg);
    svg.querySelectorAll("g[id]").forEach((g) => {
      const id = g.getAttribute("id") || "";
      if (!DRAGGABLE_IDS.test(id)) return;
      g.classList.add("draggable");
      if (!g.getAttribute("transform")) setTranslate(g, 0, 0);
      g.querySelectorAll("text").forEach((t) => t.classList.add("editable-label"));
    });
  }

  function serializeSvg(svg) {
    prepareSvgRoot(svg);
    return svg.innerHTML;
  }

  function loadInline(svg, html) {
    svg.innerHTML = html || "";
    prepareSvgRoot(svg);
  }

  async function loadTemplate(svg, url) {
    if (!url) {
      svg.innerHTML = "";
      return;
    }
    const res = await fetch(url, { credentials: "same-origin" });
    if (!res.ok) throw new Error(`Could not load template (${res.status})`);
    const text = await res.text();
    const doc = new DOMParser().parseFromString(text, "image/svg+xml");
    const inner = doc.documentElement;
    svg.innerHTML = inner.innerHTML;
    if (inner.getAttribute("viewBox")) svg.setAttribute("viewBox", inner.getAttribute("viewBox"));
    prepareSvgRoot(svg);
  }

  function attach(viewport, svg, { onEditLabel }) {
    let selected = null;
    let drag = null;
    let pinch = null;

    function select(el) {
      svg.querySelectorAll(".selected").forEach((n) => n.classList.remove("selected"));
      selected = el;
      if (el) el.classList.add("selected");
    }

    function hitGroup(target) {
      let node = target;
      while (node && node !== svg) {
        if (node.classList && node.classList.contains("draggable")) return node;
        node = node.parentNode;
      }
      return null;
    }

    viewport.addEventListener("pointerdown", (e) => {
      if (e.pointerType === "touch" && e.isPrimary === false) return;

      const g = hitGroup(e.target);
      if (g) {
        select(g);
        const pos = parseTranslate(g);
        drag = {
          el: g,
          pid: e.pointerId,
          sx: e.clientX,
          sy: e.clientY,
          ox: pos.x,
          oy: pos.y,
        };
        g.classList.add("dragging");
        viewport.setPointerCapture(e.pointerId);
        e.preventDefault();
        return;
      }

      select(null);
    });

    viewport.addEventListener("pointermove", (e) => {
      if (!drag || e.pointerId !== drag.pid) return;
      const scale = viewportScale(viewport, svg);
      const dx = (e.clientX - drag.sx) / scale;
      const dy = (e.clientY - drag.sy) / scale;
      setTranslate(drag.el, drag.ox + dx, drag.oy + dy);
    });

    function endDrag(e) {
      if (!drag || e.pointerId !== drag.pid) return;
      drag.el.classList.remove("dragging");
      try { viewport.releasePointerCapture(e.pointerId); } catch { /* ignore */ }
      drag = null;
    }
    viewport.addEventListener("pointerup", endDrag);
    viewport.addEventListener("pointercancel", endDrag);

    // Double-tap / double-click to edit dimension text
    let lastTap = 0;
    viewport.addEventListener("click", (e) => {
      const now = Date.now();
      const isDbl = now - lastTap < 350;
      lastTap = now;
      if (!isDbl) return;

      const textEl = e.target.closest ? e.target.closest("text") : null;
      if (!textEl || !textEl.classList.contains("editable-label")) return;
      e.preventDefault();
      onEditLabel(textEl);
    });

    // Pinch zoom (viewBox)
    viewport.addEventListener("touchstart", (e) => {
      if (e.touches.length !== 2) return;
      const vb = viewBoxArray(svg);
      pinch = {
        d0: dist(e.touches[0], e.touches[1]),
        vb: [...vb],
        mid: touchMid(e.touches[0], e.touches[1], viewport, svg),
      };
    }, { passive: true });

    viewport.addEventListener("touchmove", (e) => {
      if (!pinch || e.touches.length !== 2) return;
      const d1 = dist(e.touches[0], e.touches[1]);
      const scale = pinch.d0 / d1;
      const vb = pinch.vb;
      const w = vb[2] * scale;
      const h = vb[3] * scale;
      const cx = vb[0] + vb[2] / 2;
      const cy = vb[1] + vb[3] / 2;
      svg.setAttribute("viewBox", `${cx - w / 2} ${cy - h / 2} ${w} ${h}`);
    }, { passive: true });

    viewport.addEventListener("touchend", () => { pinch = null; });
  }

  function viewBoxArray(svg) {
    const vb = svg.getAttribute("viewBox") || "0 0 520 420";
    return vb.split(/\s+/).map(Number);
  }

  function viewportScale(viewport, svg) {
    const vb = viewBoxArray(svg);
    return vb[2] / viewport.clientWidth;
  }

  function dist(a, b) {
    return Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
  }

  function touchMid(t0, t1, viewport, svg) {
    const rect = viewport.getBoundingClientRect();
    const scale = viewportScale(viewport, svg);
    const vb = viewBoxArray(svg);
    const x = ((t0.clientX + t1.clientX) / 2 - rect.left) * scale + vb[0];
    const y = ((t0.clientY + t1.clientY) / 2 - rect.top) * scale + vb[1];
    return { x, y };
  }

  return { loadTemplate, loadInline, serializeSvg, attach, prepareSvgRoot };
})();
