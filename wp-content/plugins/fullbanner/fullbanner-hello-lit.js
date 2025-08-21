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
      /* border: 6px solid var(--fb-pane-border, var(--fb-border, green)); */
      position: relative;
      overflow: hidden;
    }

    .overlay {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: left;
      z-index: 1;
      pointer-events: none;
      padding: 0;
    }

    /* This wrapper is what we scale uniformly (1:1) */
    .scaler {
      display: inline-block; /* shrink-wrap content for accurate height */
      transform-origin: center center;
      will-change: transform;
      pointer-events: auto; /* allow clicks inside */
      max-width: 100%;
    }

    /* Helpful defaults for slotted overlay content */
    .scaler ::slotted(*) {
      max-width: 100%;
      height: auto;
      margin: 0;
    }

    .pane ::slotted(*) {
      max-width: 100%;
    }
  `;

  constructor() {
    super();
    this.borderColor = 'green';
    this.paneBorderColor = '';
    this._obs = { item: null, pane: null };
    this._onSlotChange = null;
  }

  connectedCallback() {
    super.connectedCallback();
    this._applyVars();
  }
  updated(c) {
    if (c.has('borderColor') || c.has('paneBorderColor')) this._applyVars();
  }
  _applyVars() {
    const b = this.borderColor || 'green';
    const p = this.paneBorderColor || b;
    this.style.setProperty('--fb-border', b);
    this.style.setProperty('--fb-pane-border', p);
  }

  firstUpdated() {
    const slot = this.renderRoot.querySelector('slot[name="b"]');
    const pane = this.renderRoot.querySelector('.pane');
    const scaler = this.renderRoot.querySelector('.scaler');

    const measureAndScale = (reason = 'measure') => {
      if (!pane || !scaler) return;

      // Temporarily reset transform for accurate measurement
      scaler.style.transform = '';
      const paneH = Math.max(0, pane.getBoundingClientRect().height);
      const itemH = Math.max(0, scaler.getBoundingClientRect().height);

      console.log(`[Fullbanner] overlay ${reason}: pane=${Math.round(paneH)}px, item=${Math.round(itemH)}px`);

      if (paneH > 0 && itemH > paneH) {
        const scale = paneH / itemH;
        scaler.style.transform = `scale(${scale})`;
        console.log(`[Fullbanner] scaled to ${scale.toFixed(3)}x to fit height`);
      }
    };

    // Observe pane resize
    this._obs.pane = new ResizeObserver(() => measureAndScale('pane resize'));
    this._obs.pane.observe(pane);

    // Observe content size changes (images, fonts, etc.)
    this._obs.item = new ResizeObserver(() => measureAndScale('content resize'));
    this._obs.item.observe(scaler);

    // Re-run when slot nodes change and when images load
    const hookImages = () => {
      const assigned = slot.assignedElements({ flatten: true });
      assigned.forEach(node => {
        node.querySelectorAll?.('img').forEach(img => {
          if (!img.complete) {
            img.addEventListener('load', () => measureAndScale('img load'), { once: true });
          }
        });
      });
    };

    this._onSlotChange = () => {
      hookImages();
      // Delay 1 frame to allow DOM to settle before measuring
      requestAnimationFrame(() => measureAndScale('slot change'));
    };
    slot.addEventListener('slotchange', this._onSlotChange);

    // Initial pass
    hookImages();
    requestAnimationFrame(() => measureAndScale('initial'));
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this._obs.item?.disconnect?.();
    this._obs.pane?.disconnect?.();
    this._obs = { item: null, pane: null };
    const slot = this.renderRoot?.querySelector?.('slot[name="b"]');
    if (slot && this._onSlotChange) slot.removeEventListener('slotchange', this._onSlotChange);
    this._onSlotChange = null;
  }

  render() {
    return html`
      <div class="box" role="group" aria-label="Fullbanner with overlay">
        <div class="pane a">
          <slot name="a"><div>hello</div></slot>

          <!-- Overlay; we scale .scaler, not the slotted nodes directly -->
          <div class="overlay" part="overlay">
            <div class="scaler">
              <slot name="b"></slot>
            </div>
          </div>
        </div>
      </div>
    `;
  }
}

customElements.define('fullbanner-hello', FullbannerHello);
