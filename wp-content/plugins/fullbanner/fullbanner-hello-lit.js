// file: fullbanner-hello-lit.js
import { LitElement, html, css } from 'https://esm.sh/lit@3?target=es2020';

class FullbannerHello extends LitElement {
  static properties = {
    borderColor: { type: String, attribute: 'border-color' },
    paneBorderColor: { type: String, attribute: 'pane-border-color' }
  };

  static styles = css`
    :host {
      display: block;
      inline-size: 100%;  /* always full width */
    }

    .box {
      width: 100%;
      height: 100%;       /* fill host’s height */
      display: flex;
      flex-direction: row;
      align-items: stretch;
      justify-content: stretch;
      background: #f0f0f0;
      margin: 0;
      padding: 0;
      box-sizing: border-box;
    }

    .pane {
      flex: 1 1 auto;
      min-width: 0;
      padding: 0;
      box-sizing: border-box;
      background: #fff;
      border: 6px solid var(--fb-pane-border, var(--fb-border, green));
      margin: 0;
      display: flex;
      flex-direction: column;
      position: relative;
      overflow: hidden;
    }

    ::slotted(p) { margin: 0 0 .5rem 0; }
    ::slotted(.fullbanner-text) { font-size: 2rem; font-weight: bold; }
  `;

  constructor() {
    super();
    this.borderColor = 'green';
    this.paneBorderColor = '';
  }

  updated(changed) {
    if (changed.has('borderColor') || changed.has('paneBorderColor')) {
      this.applyVars();
    }
  }

  connectedCallback() {
    super.connectedCallback();
    this.applyVars();
  }

  applyVars() {
    const border = this.borderColor || 'green';
    const pane = this.paneBorderColor || border;
    this.style.setProperty('--fb-border', border);
    this.style.setProperty('--fb-pane-border', pane);
  }

  render() {
    return html`
      <div class="box" role="group" aria-label="Fullbanner single-pane layout">
        <div class="pane a">
          <slot name="a">
            <div>hello</div>
          </slot>
        </div>
      </div>
    `;
  }
}

customElements.define('fullbanner-hello', FullbannerHello);
