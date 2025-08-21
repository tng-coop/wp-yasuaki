// file: fullshow-hello-lit.js
import { LitElement, html, css } from 'https://esm.sh/lit@3?target=es2020';
import Split from 'https://esm.sh/split.js@1.6.0?target=es2020&bundle';

class FullshowHello extends LitElement {
  static properties = {
    borderColor: { type: String, attribute: 'border-color' },
    paneBorderColor: { type: String, attribute: 'pane-border-color' },
    /**
     * Split mode: "auto" | "horizontal" | "vertical"
     * - horizontal: left/right panes (desktop/landscape)
     * - vertical:   top/bottom panes (mobile/portrait)
     * - auto:       picks based on longer side
     */
    split: { type: String, reflect: true }
  };

  static styles = css`
    :host { display: block; }

    .box {
      width: 100%;
      height: calc(
        100dvh
        - var(--wp-admin--admin-bar--height, 0px)
        - var(--fs-header-height, 0px)
      );
      display: flex;
      align-items: stretch;
      justify-content: stretch;
      background: #f0f0f0;
      margin: 0;
      padding: 0;
      box-sizing: border-box;
      overflow: hidden;
    }
    /* Direction-aware layout */
    .box[data-dir="horizontal"] { flex-direction: row; }
    .box[data-dir="vertical"]   { flex-direction: column; }

    .pane {
      flex: 0 0 auto;
      min-width: 0;
      min-height: 0;
      padding: 0;
      box-sizing: border-box;
      background: #fff;
      border: 6px solid var(--fs-pane-border, var(--fs-border, green));
      margin: 0;
      border-radius: 0;
      display: flex;
      flex-direction: column;
      position: relative;   /* anchor for abs-pos tenants if any */
      overflow: hidden;     /* avoid double scrollbars in pane A */
    }

    /* Gutters */
    .gutter.gutter-horizontal {
      background: #ccc;
      cursor: col-resize;
      position: relative;
      z-index: 10;
      pointer-events: auto;
      touch-action: none;
      inline-size: 12px;
      block-size: auto;
    }
    .gutter.gutter-vertical {
      background: #ccc;
      cursor: row-resize;
      position: relative;
      z-index: 10;
      pointer-events: auto;
      touch-action: none;
      block-size: 12px;
      inline-size: auto;
    }

    /* Keep centering; Slot A stretches via .slotbox-a */
    .a { align-items: center; justify-content: center; }
    .b { overflow: auto; }

    /* 🔑 Slot A: real flex child that fills the pane */
    .slotbox-a {
      display: block;
      flex: 1 1 auto;       /* fill available space */
      align-self: stretch;
      inline-size: 100%;
      block-size: 100%;
      min-block-size: 0;
      box-sizing: border-box;
    }

    /* 🔑 Only Slot A tenants fill the available area */
    .slot-a::slotted(*) {
      display: block;
      inline-size: 100%;
      block-size: 100%;
      min-block-size: 0;
      margin: 0;
    }

    /* Nice defaults for common children (global) */
    ::slotted(p) { margin: 0 0 .5rem 0; }
    ::slotted(.fullshow-text) { font-size: 2rem; font-weight: bold; }

    /* Optional helpers for SVG/canvas tenants in Slot A */
    .slot-a::slotted(*) > svg,
    .slot-a::slotted(*) > canvas {
      display: block;
      inline-size: 100%;
      block-size: 100%;
    }
  `;

  constructor() {
    super();
    this.borderColor = 'green';
    this.paneBorderColor = '';
    this.split = 'auto';          // ← default responsive mode

    this.__splitInstance = null;
    this.__splitDirection = null;
    this.__splitInit = false;

    this._headerRO = null;
    this._boundMeasure = () => this.measureAndSetHeaderHeight();
    this._boundResize = () => this._maybeRebuildSplit();
    this._enforceFillSlotA = this._enforceFillSlotA.bind(this);
  }

  connectedCallback() {
    super.connectedCallback();
    this.setupHeaderObserver();
    window.addEventListener('resize', this._boundMeasure, { passive: true });
    window.addEventListener('resize', this._boundResize, { passive: true });
    window.addEventListener('load', this._boundMeasure, { once: true });
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._headerRO?.disconnect();
    window.removeEventListener('resize', this._boundMeasure);
    window.removeEventListener('resize', this._boundResize);
    this.__splitInstance?.destroy?.();
    this.__splitInstance = null;
  }

  firstUpdated() {
    this.applyVars();
    this.updateComplete.then(() => {
      this._buildSplit(this._computeDesiredDirection());
      this._enforceFillSlotA();
    });
    this.renderRoot.querySelector('slot.slot-a')
      ?.addEventListener('slotchange', this._enforceFillSlotA);

    this.measureAndSetHeaderHeight();
  }

  updated(changed) {
    if (changed.has('borderColor') || changed.has('paneBorderColor')) {
      this.applyVars();
    }
    if (changed.has('split')) {
      this._maybeRebuildSplit(true);
    }
  }

  // ---- helpers ----
  applyVars() {
    const border = this.borderColor || 'green';
    const pane = this.paneBorderColor || border;
    this.style.setProperty('--fs-border', border);
    this.style.setProperty('--fs-pane-border', pane);
  }

  getHeaderEl() {
    return document.querySelector('header.wp-block-template-part');
  }

  setupHeaderObserver() {
    const header = this.getHeaderEl();
    if (!header) {
      this.style.setProperty('--fs-header-height', '0px');
      return;
    }
    this._headerRO = new ResizeObserver(() => this.measureAndSetHeaderHeight());
    this._headerRO.observe(header);
    this.measureAndSetHeaderHeight();
  }

  measureAndSetHeaderHeight() {
    const header = this.getHeaderEl();
    const h = header ? header.getBoundingClientRect().height : 0;
    this.style.setProperty('--fs-header-height', `${Math.max(0, Math.ceil(h))}px`);
  }

  _computeDesiredDirection() {
    if (this.split === 'horizontal' || this.split === 'vertical') return this.split;
    // auto: choose based on longer side
    const w = window.innerWidth, h = window.innerHeight;
    return (w >= h) ? 'horizontal' : 'vertical';
  }

  _maybeRebuildSplit(force = false) {
    const desired = this._computeDesiredDirection();
    if (force || desired !== this.__splitDirection) {
      this._buildSplit(desired);
    }
  }

  _buildSplit(direction) {
    // Tear down any existing split/gutters
    this.__splitInstance?.destroy?.();
    this.__splitInstance = null;

    const left  = this.renderRoot?.querySelector('.a');
    const right = this.renderRoot?.querySelector('.b');
    const box   = this.renderRoot?.querySelector('.box');
    if (!left || !right || !box) return;

    box.setAttribute('data-dir', direction);

    this.__splitInstance = Split([left, right], {
      sizes: [50, 50],
      minSize: 0,
      gutterSize: 12,
      snapOffset: 0,
      cursor: (direction === 'horizontal') ? 'col-resize' : 'row-resize',
      direction
    });
    this.__splitDirection = direction;
    this.__splitInit = true;
  }

  /**
   * Ensure the first wrapper(s) under Slot A get height:100% so
   * grandchildren can resolve height:100% correctly.
   */
  _enforceFillSlotA() {
    const slot = this.renderRoot.querySelector('slot.slot-a');
    if (!slot) return;
    const assigned = slot.assignedElements({ flatten: true });
    for (const el of assigned) {
      el.style.height = '100%';
      let cur = el.firstElementChild;
      for (let depth = 0; depth < 2 && cur; depth++) {
        cur.style.height = '100%';
        cur = cur.firstElementChild;
      }
    }
  }

  render() {
    const dir = this._computeDesiredDirection();
    return html`
      <div class="box" role="group"
           aria-label="Fullshow split layout"
           data-dir="${dir}">
        <div class="pane a">
          <div class="slotbox-a">
            <slot name="a" class="slot-a">
              <div>hello</div>
            </slot>
          </div>
        </div>
        <div class="pane b">
          <slot name="b" class="slot-b">
            <p>hello</p>
            <p>hello</p>
            <p>hello</p>
          </slot>
        </div>
      </div>
    `;
  }
}

customElements.define('fullshow-hello', FullshowHello);
