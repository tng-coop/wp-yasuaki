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
      inline-size: 100%;
      position: relative;
      /* Allow height to be driven by inline style (render.php) or by content */
    }

    .box {
      width: 100%;
      height: 100%;
      display: block;
      position: relative;
    }

    .pane {
      width: 100%;
      height: 100%;
      background: #fff;
      border: 6px solid var(--fb-pane-border, var(--fb-border, green));
      position: relative; /* key for absolutely-positioned overlay */
      overflow: hidden;   /* clip video & overlay to the same box */
    }

    /* Overlay now lives INSIDE .pane so it shares the same box */
    .overlay {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1;
      pointer-events: none; /* let slotted children opt-in */
      padding: 0;
    }

    /* Make ALL slotted overlay children clickable and nicely constrained */
    .overlay ::slotted(*) {
      pointer-events: auto;
      max-width: 100%;
      height: auto;
      margin: 0;
    }

    /* Optional: make slot A fill the pane when it wants (e.g., video backgrounds) */
    .pane ::slotted(*) {
      max-width: 100%;
    }
  `;

  constructor() {
    super();
    this.borderColor = 'green';
    this.paneBorderColor = '';
  }

  updated(c) {
    if (c.has('borderColor') || c.has('paneBorderColor')) this.applyVars();
  }
  connectedCallback() {
    super.connectedCallback();
    this.applyVars();
  }
  applyVars() {
    const b = this.borderColor || 'green';
    const p = this.paneBorderColor || b;
    this.style.setProperty('--fb-border', b);
    this.style.setProperty('--fb-pane-border', p);
  }

  render() {
    return html`
      <div class="box" role="group" aria-label="Fullbanner with overlay">
        <div class="pane a">
          <slot name="a"><div>hello</div></slot>
          <!-- Overlay moved INSIDE the pane so it shares the same box as slot A -->
          <div class="overlay" part="overlay"><slot name="b"></slot></div>
        </div>
      </div>
    `;
  }
}
customElements.define('fullbanner-hello', FullbannerHello);
