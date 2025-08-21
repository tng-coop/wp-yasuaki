// fit-image.js
import { LitElement, html } from "https://esm.sh/lit@3";
import { fitImageStyles } from "./fit-image-styles.css.js";
import { metrics } from "./fit-image-math.js";
import { PanZoomEngine } from "./fit-image-panzoom-engine.js";
import { drawPointsToNearestGrid, clearOverlay } from "./fit-image-draw.js";

class FitImage extends LitElement {
  static styles = fitImageStyles;
  static properties = {
    src: { type: String },
    alt: { type: String },
    mode: { type: String, reflect: true },

    // Geo bounds + projection
    lonMin: { type: Number, attribute: "lon-min", reflect: true },
    lonMax: { type: Number, attribute: "lon-max", reflect: true },
    latMin: { type: Number, attribute: "lat-min", reflect: true },
    latMax: { type: Number, attribute: "lat-max", reflect: true },
    projection: { type: String, reflect: true } // "mercator" | "linear"
  };

  constructor() {
    super();
    this.src = "";
    this.alt = "";
    this.mode = "cover";

    // Defaults (override via attributes)
    this.lonMin = 139.0045;
    this.lonMax = 139.7762;
    this.latMin =  35.1189;
    this.latMax =  35.6518;
    this.projection = "mercator";

    // state
    this._posX = 50; this._posY = 50; this._t = 0;
    this._nW = 0; this._nH = 0;
    this._imgEl = null;
    this._svgEl = null;
    this._contentEls = [];
    this._hasInteracted = false;
    this._ro = null;

    this._overlayVisible = true;
    this._overlayNeedsRedraw = true;
    this._overlayPoints = null; // pixel [{x,y, datum?}]
    this._overlayOpts = null;   // draw options (grid/marker/colors/tooltip)

    this._pz = new PanZoomEngine({
      getBoxRect: () => this.getBoundingClientRect(),
      getNaturalSize: () => ({ nW: this._nW, nH: this._nH }),
      getState: () => ({ t: this._t, posX: this._posX, posY: this._posY, mode: this.mode }),
      setState: (patch) => {
        if ("hasInteracted" in patch) this._hasInteracted = patch.hasInteracted;
        if ("t" in patch)     this._t = patch.t;
        if ("posX" in patch)  this._posX = patch.posX;
        if ("posY" in patch)  this._posY = patch.posY;
      },
      applyLayout: () => this._clampAndApply(),
    });
  }

  // Public helpers -----------------------------------------------------------
  toggle() { this._pz.toggleMode(); }
  setMode(mode) {
    if (mode === "cover" || mode === "contain") {
      this._hasInteracted = true;
      this.mode = mode;
      this._t = (mode === "contain" ? 0 : 1);
      this._clampAndApply();
    }
  }

  // Accept pixel points (natural image coords) + optional draw opts
  setOverlayPoints(points, opts) {
    this._overlayPoints = Array.isArray(points) ? points : null;
    this._overlayOpts = opts || null;
    this._overlayNeedsRedraw = true;
    this._applyLayout();
  }

  // Accept lon/lat points; uses element bbox + chosen projection
  setOverlayPointsLonLat(pointsLL, opts) {
    if (!Array.isArray(pointsLL) || pointsLL.length === 0) {
      this.setOverlayPoints(null, opts);
      return;
    }
    if (!(this._nW > 0 && this._nH > 0)) {
      const once = () => {
        this.removeEventListener("fit-image:viewport-transform", once);
        this.setOverlayPointsLonLat(pointsLL, opts);
      };
      this.addEventListener("fit-image:viewport-transform", once, { once: true });
      return;
    }

    const { lonMin, lonMax, latMin, latMax } = this;
    const mode = (this.projection || "mercator").toLowerCase();
    if (![lonMin, lonMax, latMin, latMax].every(Number.isFinite)) {
      console.warn("fit-image: lon/lat bounds not set.");
      return;
    }

    // Mercator helper
    const mercY = (latDeg) => {
      const MAX = 85.05112878;
      const φ = (Math.max(-MAX, Math.min(MAX, latDeg)) * Math.PI) / 180;
      return Math.log(Math.tan(Math.PI/4 + φ/2));
    };
    const myMax = mercY(latMax), myMin = mercY(latMin);

    // Project lon/lat -> natural pixel coords
    const px = pointsLL.map((p) => {
      const { lon, lat, ...rest } = p;
      const x = ((lon - lonMin) / (lonMax - lonMin)) * this._nW;
      const yNorm = (mode === "linear")
        ? (latMax - lat) / (latMax - latMin)
        : (myMax - mercY(lat)) / (myMax - myMin);
      return { x, y: yNorm * this._nH, datum: { lon, lat, ...rest } };
    });

    this.setOverlayPoints(px, opts);
  }

  // Lifecycle ----------------------------------------------------------------
  render() { return html`<slot @slotchange=${this._onSlotChange}></slot>`; }

  connectedCallback() {
    super.connectedCallback();
    this.addEventListener("wheel", this._pz.onWheel, { passive: false });
    this.addEventListener("fit-image:toggle", this._onToggleEvent);
    this.addEventListener("fit-image:set-mode", this._onSetModeEvent);
    this.addEventListener("fit-image:toggle-overlay", this._onToggleOverlayEvent);
    this.addEventListener("fit-image:redraw-overlay", this._onRedrawOverlayEvent);

    this._ro = new ResizeObserver(() => this._clampAndApply());
    this._ro.observe(this);

    if (!this.hasAttribute("tabindex")) this.setAttribute("tabindex", "0");
  }

  disconnectedCallback() {
    this.removeEventListener("wheel", this._pz.onWheel);
    this.removeEventListener("fit-image:toggle", this._onToggleEvent);
    this.removeEventListener("fit-image:set-mode", this._onSetModeEvent);
    this.removeEventListener("fit-image:toggle-overlay", this._onToggleOverlayEvent);
    this.removeEventListener("fit-image:redraw-overlay", this._onRedrawOverlayEvent);
    this._ro?.disconnect();
    super.disconnectedCallback();
  }

  updated(changed) {
    if (changed.has("mode") && !this._hasInteracted) {
      const nextT = this.mode === "contain" ? 0 : 1;
      if (this._t !== nextT) { this._t = nextT; this._clampAndApply(); }
    }
  }

  // Events -------------------------------------------------------------------
  _onToggleEvent = () => { this._pz.toggleMode(); };
  _onSetModeEvent = (e) => { this.setMode(e?.detail?.mode); };

  _onToggleOverlayEvent = (e) => {
    this._overlayVisible = e?.detail?.visible ?? !this._overlayVisible;
    if (this._svgEl) this._svgEl.style.display = this._overlayVisible ? "" : "none";
    if (this._overlayVisible) {
      this._overlayNeedsRedraw = true;
      this._applyLayout();
    }
  };
  _onRedrawOverlayEvent = () => {
    if (this._svgEl) {
      this._overlayNeedsRedraw = true;
      this._applyLayout();
    }
  };

  _onSlotChange = () => {
    const slot = this.renderRoot?.querySelector("slot");
    const assigned = slot?.assignedElements({ flatten: true }) ?? [];

    // unhook old
    if (this._contentEls.length) {
      for (const el of this._contentEls) {
        el.removeEventListener("pointerdown", this._pz.onPointerDown);
        el.removeEventListener("pointermove", this._pz.onPointerMove, { passive: false });
        el.removeEventListener("pointerup", this._pz.onPointerUp);
        el.removeEventListener("pointercancel", this._pz.onPointerUp);
        el.removeEventListener("pointerleave", this._pz.onPointerUp);
        if (el.tagName === "IMG") el.removeEventListener("load", this._onImgLoad);
      }
    }

    this._contentEls = assigned;
    this._imgEl = assigned.find(el => el.tagName?.toUpperCase() === "IMG") ?? null;
    this._svgEl = assigned.find(el => el.tagName?.toUpperCase() === "SVG") ?? null;

    for (const el of this._contentEls) {
      el.addEventListener("pointerdown", this._pz.onPointerDown);
      el.addEventListener("pointermove", this._pz.onPointerMove, { passive: false });
      el.addEventListener("pointerup", this._pz.onPointerUp);
      el.addEventListener("pointercancel", this._pz.onPointerUp);
      el.addEventListener("pointerleave", this._pz.onPointerUp);

      if (el.tagName === "IMG") {
        el.loading ||= "lazy";
        el.decoding ||= "async";
        el.draggable = false;
        el.addEventListener("load", this._onImgLoad);
      }
    }

    if (this._imgEl && this._imgEl.complete && this._imgEl.naturalWidth) {
      this._onImgLoad({ currentTarget: this._imgEl });
    }

    this._clampAndApply();
  };

  _onImgLoad = (e) => {
    const img = e.currentTarget ?? this._imgEl;
    if (!img) return;
    this._nW = img.naturalWidth || 0;
    this._nH = img.naturalHeight || 0;
    if (!this._hasInteracted) this._t = (this.mode === "contain" ? 0 : 1);
    this._applyLayout();
  };

  // Layout + overlay ---------------------------------------------------------
  _applyLayout() {
    if (!this._nW || !this._nH) return;

    const { W, H, cw, ch } = metrics(this._t, this._nW, this._nH, () => this.getBoundingClientRect());
    const x = (W - cw) * (this._posX / 100);
    const y = (H - ch) * (this._posY / 100);
    const s = cw / this._nW || 1;

    if (this._imgEl) {
      this._imgEl.style.width  = `${cw}px`;
      this._imgEl.style.height = `${ch}px`;
      this._imgEl.style.left   = `${x}px`;
      this._imgEl.style.top    = `${y}px`;
    }

    if (this._svgEl) {
      this._svgEl.style.left   = `${x}px`;
      this._svgEl.style.top    = `${y}px`;
      this._svgEl.style.width  = `${cw}px`;
      this._svgEl.style.height = `${ch}px`;
      this._svgEl.setAttribute("width", `${cw}`);
      this._svgEl.setAttribute("height", `${ch}`);
      this._svgEl.setAttribute("viewBox", `0 0 ${this._nW} ${this._nH}`);
      this._svgEl.setAttribute("preserveAspectRatio", "none");

      if (this._overlayVisible && this._overlayNeedsRedraw) {
        if (this._overlayPoints?.length) {
          const payload = drawPointsToNearestGrid(this._svgEl, {
            nW: this._nW,
            nH: this._nH,
            points: this._overlayPoints,
            ...(this._overlayOpts || {}) // grid/marker/fill/stroke/tooltip...
          });
          this.dispatchEvent(new CustomEvent("fit-image:overlay-updated", {
            detail: payload, bubbles: true, composed: true
          }));
        } else {
          clearOverlay(this._svgEl);
          this.dispatchEvent(new CustomEvent("fit-image:overlay-updated", {
            detail: { snapped: [] }, bubbles: true, composed: true
          }));
        }
        this._overlayNeedsRedraw = false;
      }
    }

    this.dispatchEvent(new CustomEvent("fit-image:viewport-transform", {
      detail: { W, H, nW: this._nW, nH: this._nH, s, tx: x, ty: y },
      bubbles: true, composed: true
    }));

    const isContain = cw <= W + 0.5 && ch <= H + 0.5;
    const newMode = isContain ? "contain" : "cover";
    if (this._hasInteracted && this.mode !== newMode) this.mode = newMode;

    const hasSlackX = Math.abs(W - cw) > 0.5;
    const hasSlackY = Math.abs(H - ch) > 0.5;
    this.toggleAttribute("pannable", hasSlackX || hasSlackY);
  }

  _clampAndApply() {
    this._posX = Math.max(0, Math.min(100, this._posX));
    this._posY = Math.max(0, Math.min(100, this._posY));
    this._applyLayout();
  }
}

customElements.define("fit-image", FitImage);
export { FitImage };

