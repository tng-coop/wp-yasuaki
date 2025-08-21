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
      height: calc(100dvh - var(--wp-admin--admin-bar--height, 0px));
      display: flex;
      flex-direction: row;
      align-items: stretch;
      justify-content: stretch;
      background: #f0f0f0;
      margin: 0;
      padding: 0;
      border: 12px solid var(--fs-border, green);
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
    }

/* replace your gutter rule */
.gutter.gutter-horizontal {
  background: transparent;
  cursor: col-resize;
  position: relative;
  z-index: 10;
  pointer-events: auto;
  touch-action: none;
}

    .a { align-items: center; justify-content: center; }
    .b { overflow: auto; }

    ::slotted(*) { margin: 0; }
    ::slotted(p) { margin: 0 0 .5rem 0; }
    ::slotted(.fullshow-text) { font-size: 2rem; font-weight: bold; }
  `;

  constructor() {
    super();
    this.borderColor = 'green';
    this.paneBorderColor = '';
    this.__splitInit = false;
  }

  firstUpdated() {
    // apply CSS vars once DOM is ready
    this.#applyVars();

    // init Split once after first render
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
  }

  updated(changed) {
    if (changed.has('borderColor') || changed.has('paneBorderColor')) {
      this.#applyVars();
    }
  }

  #applyVars() {
    const border = this.borderColor || 'green';
    const pane = this.paneBorderColor || border;
    this.style.setProperty('--fs-border', border);
    this.style.setProperty('--fs-pane-border', pane);
  }

  render() {
    return html`
      <div class="box" role="group" aria-label="Fullshow two-pane layout">
        <div class="pane a">
          <slot name="a">
            <!-- Fallback for "a" -->
            <div class="fullshow-text">hello</div>
          </slot>
        </div>
        <div class="pane b">
          <slot name="b">
            <!-- Fallback for "b" -->
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
