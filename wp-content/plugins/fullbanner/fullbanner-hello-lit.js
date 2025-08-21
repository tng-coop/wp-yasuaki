// file: fullbanner-hello-lit.js
import { LitElement, html, css } from 'https://esm.sh/lit@3?target=es2020';

class FullbannerHello extends LitElement {
  static properties = {
    borderColor: { type: String, attribute: 'border-color' },
    paneBorderColor: { type: String, attribute: 'pane-border-color' }
  };

  static styles = css`
    :host { display:block; inline-size:100%; position:relative; }
    .box { width:100%; height:100%; display:flex; position:relative; }
    .pane { flex:1 1 auto; background:#fff; border:6px solid var(--fb-pane-border, var(--fb-border, green)); position:relative; overflow:hidden; }
    .overlay { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; pointer-events:none; z-index:1; }
    ::slotted(.overlay-content) { pointer-events:auto; }
  `;

  constructor(){ super(); this.borderColor='green'; this.paneBorderColor=''; }
  updated(c){ if (c.has('borderColor') || c.has('paneBorderColor')) this.applyVars(); }
  connectedCallback(){ super.connectedCallback(); this.applyVars(); }
  applyVars(){ const b=this.borderColor||'green'; const p=this.paneBorderColor||b; this.style.setProperty('--fb-border', b); this.style.setProperty('--fb-pane-border', p); }

  render(){
    return html`
      <div class="box" role="group" aria-label="Fullbanner with overlay">
        <div class="pane a"><slot name="a"><div>hello</div></slot></div>
        <div class="overlay" part="overlay"><slot name="b"></slot></div>
      </div>
    `;
  }
}
customElements.define('fullbanner-hello', FullbannerHello);
