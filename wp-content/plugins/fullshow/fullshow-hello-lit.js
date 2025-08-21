// file: fullshow-hello-lit.js
import { LitElement, html, css } from 'https://esm.sh/lit@3?target=es2020';
import Split from 'https://esm.sh/split.js@1.6.0?target=es2020&bundle';

class FullshowHello extends LitElement {
  static properties = {
    borderColor: { type: String, attribute: 'border-color' },
    paneBorderColor: { type: String, attribute: 'pane-border-color' }
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
      flex-direction: row;
      align-items: stretch;
      justify-content: stretch;
      background: #f0f0f0;
      margin: 0;
      padding: 0;
      box-sizing: border-box;
      overflow: hidden;
    }

    .pane {
      flex: 0 0 auto;
      min-width: 0;
      min-height: 0;
      padding: 0px;
      box-sizing: border-box;
      background: #fff;
      border: 6px solid var(--fs-pane-border, var(--fs-border, green));
      margin: 0px;
      border-radius: 0px;
      display: flex;
      flex-direction: column;
      position: relative;   /* anchor for abs-pos tenants if any */
      overflow: hidden;     /* avoid double scrollbars in pane A */
    }

    .gutter.gutter-horizontal {
      background: #ccc;
      cursor: col-resize;
      position: relative;
      z-index: 10;
      pointer-events: auto;
      touch-action: none;
    }

    /* Keep centering; Slot A stretches via .slotbox-a */
    .a { align-items: center; justify-content: center; }
    .b { overflow: auto; }

    /* 🔑 Slot A: real flex child that fills the pane */
    .slotbox-a {
      display: block;
      flex: 1 1 auto;       /* fill vertical space in column flex */
      align-self: stretch;  /* ignore .a cross-axis centering */
      inline-size: 100%;
      block-size: 100%;
      min-block-size: 0;    /* avoid min-content clamp */
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
    this.__splitInit = false;

    this._headerRO = null;
    this._boundMeasure = () => this.measureAndSetHeaderHeight();
  }

  connectedCallback() {
    super.connectedCallback();
    this.setupHeaderObserver();
    window.addEventListener('resize', this._boundMeasure, { passive: true });
    window.addEventListener('load', this._boundMeasure, { once: true });
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._headerRO?.disconnect();
    window.removeEventListener('resize', this._boundMeasure);
  }

  firstUpdated() {
    this.applyVars();

    const left  = this.renderRoot?.querySelector('.a');
    const right = this.renderRoot?.querySelector('.b');
    if (left && right && !this.__splitInit) {
      Split([left, right], {
        sizes: [50, 50],
        minSize: 0,
        gutterSize: 12,
        snapOffset: 0,
        cursor: 'col-resize',
        direction: 'horizontal'
      });
      this.__splitInit = true;
    }

    this.measureAndSetHeaderHeight();
  }

  updated(changed) {
    if (changed.has('borderColor') || changed.has('paneBorderColor')) {
      this.applyVars();
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

  render() {
    return html`
      <div class="box" role="group" aria-label="Fullshow two-pane layout">
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
