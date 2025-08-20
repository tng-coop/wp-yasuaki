// split-helpers.js
import Split from "https://esm.sh/split.js@1.6.0";

export function parseSizes(s) {
  const parts = String(s || "").split(",").map(v => Number(v.trim()));
  return (parts.length === 2 && parts.every(Number.isFinite)) ? parts : [50, 50];
}

export function chooseDir(host) {
  const o = host.orientation;
  if (o === "horizontal" || o === "vertical") return o;
  return (window.innerWidth >= window.innerHeight) ? "horizontal" : "vertical";
}

export function destroy(host) {
  if (host._split?.destroy) host._split.destroy();
  host.renderRoot.querySelectorAll(".gutter").forEach(g => g.remove());
  host._split = null;
}

export function initOrUpdate(host) {
  const dir = chooseDir(host);
  const orientationChanged = dir !== host._currDir;

  if (dir === host._currDir && host._split) return;

  destroy(host);

  const outer = host.renderRoot.querySelector(".outer");
  outer.style.flexDirection = (dir === "horizontal") ? "row" : "column";

  const isTouch  = matchMedia("(pointer: coarse)").matches;
  const gutterSz = isTouch ? host.gutterTouch : host.gutterDesktop;

  const makeGutter = (direction) => {
    const g = document.createElement("div");
    g.className = `gutter gutter-${direction}`;
    g.setAttribute("role", "separator");
    g.setAttribute("tabindex", "0");
    g.setAttribute("aria-orientation", direction === "horizontal" ? "vertical" : "horizontal");
    return g;
  };

  host._split = Split(
    [host.renderRoot.getElementById("paneA"), host.renderRoot.getElementById("paneB")],
    {
      direction: dir,
      sizes: parseSizes(host.sizes),
      minSize: host.minSize,
      gutterSize: gutterSz,
      gutter: (i, d) => makeGutter(d),
      onDrag:    () => host._applyTopHeight(),
      onDragEnd: () => {
        host._applyTopHeight();
        host._didApplyInitialSizing = true;
        const sizes = host._split?.getSizes?.();
        if (sizes) host.dispatchEvent(new CustomEvent("split-resize", { detail: { sizes }, bubbles: true }));
      }
    }
  );

  host._currDir = dir;

  if (orientationChanged) host._didApplyInitialSizing = false;

  applyInitialSizesFromRatioIfNeeded(host, gutterSz);
}

export function applyInitialSizesFromRatioIfNeeded(host, gutterPx) {
  const ratio = host.initialFirstPaneRatio;
  if (!Number.isFinite(ratio)) return;
  if (host._didApplyInitialSizing) return;

  const outer = host.renderRoot.querySelector(".outer");
  if (!outer) return;

  const rect = outer.getBoundingClientRect();
  const totalW = rect.width  || 0;
  const totalH = rect.height || 0;
  if (!totalW || !totalH) return;

  const dir = host._currDir;
  const main = (dir === "horizontal") ? totalW : totalH;

  const availableMain = Math.max(0, main - (Number.isFinite(gutterPx) ? gutterPx : 0));
  if (!availableMain) return;

  let desiredA;
  if (dir === "vertical") desiredA = totalW / ratio;      // height
  else                    desiredA = totalH * ratio;      // width

  const minA = host.minSize, minB = host.minSize;
  desiredA = Math.min(Math.max(desiredA, minA), availableMain - minB);

  const aPct = (desiredA / availableMain) * 100;
  const bPct = 100 - aPct;

  host.setSizes(aPct, bPct);
  host._applyTopHeight();
  host._didApplyInitialSizing = true;
}

export function applyTopHeight(host) {
  const paneA = host.renderRoot.getElementById("paneA");
  if (!paneA) return;

  const rect = paneA.getBoundingClientRect();
  const h = Math.round(rect.height);
  const w = Math.round(rect.width);

  const slotA = host.renderRoot.querySelector('slot[name="a"]');
  const containerEl = slotA?.assignedElements({ flatten: true })[0];
  const targetEl = containerEl?.querySelector(':scope > *') ?? containerEl;

  if (host._currDir === "vertical") {
    const px = `${h}px`;
    // Set the CSS var only if it changed to avoid RO feedback loops
    if (host.style.getPropertyValue("--pane-a-px") !== px) {
      host.style.setProperty("--pane-a-px", px);
    }
    // Rely on CSS var; don't pin inline to avoid jitter loops
    if (targetEl) targetEl.style.removeProperty("block-size");
  } else {
    host.style.removeProperty("--pane-a-px");
    if (targetEl) targetEl.style.removeProperty("block-size");
  }

  const svg = containerEl?.querySelector('svg');
  if (svg) {
    svg.setAttribute('width', w);
    svg.setAttribute('height', h);
    svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  }
}
