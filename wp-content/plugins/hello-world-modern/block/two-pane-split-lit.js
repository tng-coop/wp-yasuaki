// two-pane-split-lit.js
import { LitElement, html } from "https://esm.sh/lit@3.1.2";
import { splitStyles as styles } from "./two-pane-split-styles.js";
import {
  parseSizes,
  initOrUpdate,
  applyInitialSizesFromRatioIfNeeded,
  applyTopHeight
} from "./two-pane-split-helpers.js";

export class TwoPaneSplitLit extends LitElement {
  static properties = {
    orientation:    { type: String, reflect: true },
    minSize:        { type: Number, reflect: true, attribute: "min-size" },
    gutterDesktop:  { type: Number, reflect: true, attribute: "gutter-desktop" },
    gutterTouch:    { type: Number, reflect: true, attribute: "gutter-touch" },
    sizes:          { type: String, reflect: true }, // "50,50"
    initialFirstPaneRatio: { type: Number, reflect: true, attribute: "initial-first-pane-ratio" },

    // NEW: function props (not reflected as attributes)
    onOverlaysSuspend: { attribute: false },
    onOverlaysResume:  { attribute: false },
  };

  static styles = styles;

  // defaults
  orientation   = "auto";
  minSize       = 100;
  gutterDesktop = 10;
  gutterTouch   = 22;
  sizes         = "50,50";
  initialFirstPaneRatio = NaN;

  // You assign these from outside:
  onOverlaysSuspend = null; // () => void
  onOverlaysResume  = null; // () => void

  // internals
  _split = null;
  _currDir = null;
  _resizeTimer = null;
  _paneAObserver = null;
  _didApplyInitialSizing = false;

  // de-dupe overlay calls
  _overlaysSuppressed = false;

  render() {
    return html`
      <div class="outer" @pointerdown=${this._onPointerDown}>
        <div id="paneA" class="pane"><slot name="a"></slot></div>
        <div id="paneB" class="pane"><slot name="b"></slot></div>
      </div>
    `;
  }

  firstUpdated() {
    window.addEventListener("pointerup", this._clearDragging, { passive: true });
    window.addEventListener("pointercancel", this._clearDragging, { passive: true });
    window.addEventListener("resize", this._onResize, { passive: true });

    const paneA = this.renderRoot.getElementById("paneA");
    this._paneAObserver = new ResizeObserver(() => this._applyTopHeight());
    this._paneAObserver.observe(paneA);

    this._initOrUpdate();
    this._applyTopHeight(); // initial
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._destroy();
    window.removeEventListener("pointerup", this._clearDragging);
    window.removeEventListener("pointercancel", this._clearDragging);
    window.removeEventListener("resize", this._onResize);
    this._paneAObserver?.disconnect();
    this._paneAObserver = null;
    clearTimeout(this._resizeTimer);
  }

  updated(changed) {
    if ([..."orientation,minSize,gutterDesktop,gutterTouch,sizes,initialFirstPaneRatio".split(",")].some(k => changed.has(k))) {
      this._initOrUpdate();
      this._applyTopHeight();
    }
  }

  getSizes() { return this._split?.getSizes?.() ?? parseSizes(this.sizes); }
  setSizes(a, b) {
    const arr = [Number(a), Number(b)];
    if (arr.every(Number.isFinite)) this._split?.setSizes?.(arr);
  }

  // --- bindings to helpers ---
  _initOrUpdate = () => initOrUpdate(this);
  _destroy      = () => { import("./two-pane-split-helpers.js").then(m => m.destroy(this)); };
  _applyTopHeight = () => applyTopHeight(this);

  // --- overlay control via provided functions ---
  _suppressOverlays() {
    if (this._overlaysSuppressed) return;
    this._overlaysSuppressed = true;
    try { this.onOverlaysSuspend?.(); } catch {}
  }

  _resumeOverlays() {
    if (!this._overlaysSuppressed) return;
    this._overlaysSuppressed = false;
    try { this.onOverlaysResume?.(); } catch {}
  }

  // --- UI interactions ---
  _onPointerDown = (e) => {
    const gutter = e.composedPath().find(el => el?.classList?.contains?.("gutter"));
    if (gutter) {
      gutter.classList.add("is-dragging");
      this._suppressOverlays(); // pause during gutter drag
    }
  };

  _clearDragging = () => {
    const hadDragging = this.renderRoot.querySelector(".gutter.is-dragging");
    this.renderRoot.querySelectorAll(".gutter.is-dragging").forEach(g => g.classList.remove("is-dragging"));
    if (hadDragging) {
      this._resumeOverlays(); // resume after drag completes
    }
  };

  _onResize = () => {
    // Pause overlays for a burst of resizes, resume after debounce
    this._suppressOverlays();
    clearTimeout(this._resizeTimer);
    this._resizeTimer = setTimeout(() => {
      this._initOrUpdate();
      this._applyTopHeight();
      this._resumeOverlays();
    }, 120);
  };
}

customElements.define("two-pane-split-lit", TwoPaneSplitLit);
