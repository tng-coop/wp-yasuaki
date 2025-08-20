// file: fullshow-hello-lit.js
import { LitElement, html, css } from 'https://cdn.jsdelivr.net/npm/lit@3/+esm';
import Split from 'https://esm.sh/split.js@1.6.0';

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
      flex: 1 1 0;
      min-width: 0;
      min-height: 0;
      padding: 16px;
      box-sizing: border-box;
      background: #fff;
      border: 6px solid var(--fs-pane-border, var(--fs-border, green));
      margin: 8px;
      border-radius: 4px;
      display: flex;
      flex-direction: column;
    }

    /* Invisible draggable gutter that doesn't change layout:
       - width: 24px (nice hit area)
       - negative margins (-12px each side) => net 0 growth
       - sits on top to catch drags; borders remain fully visible */
    .gutter.gutter-horizontal {
      flex: 0 0 24px;
      margin-left: -12px;
      margin-right: -12px;
      background: transparent;
      cursor: col-resize;
      position: relative;
      z-index: 10;
      border-radius: 0; /* not visible, just to be explicit */
    }

    .a {
      align-items: center;
      justify-content: center;
    }
    .b {
      overflow: auto;
    }

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

  updated() {
    const border = this.borderColor || 'green';
    const pane = this.paneBorderColor || border;
    this.style.setProperty('--fs-border', border);
    this.style.setProperty('--fs-pane-border', pane);

    if (!this.__splitInit) {
      const left = this.renderRoot?.querySelector('.a');
      const right = this.renderRoot?.querySelector('.b');
      if (left && right) {
        Split([left, right], {
          sizes: [50, 50],
          minSize: 0,
          gutterSize: 24,   // wide, easy-to-grab target
          snapOffset: 0,
          cursor: 'col-resize',
          direction: 'horizontal'
        });
        this.__splitInit = true;
      }
    }
  }

  render() {
    return html`
      <div class="box" role="group" aria-label="Fullshow two-pane layout">
        <div class="pane a">
          <slot name="a">
            <!-- Fallback if no "a" slot -->
            <div class="fullshow-text">hello</div>
          </slot>
        </div>
        <div class="pane b">
          <slot name="b">
            <!-- Fallback if no "b" slot -->
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
