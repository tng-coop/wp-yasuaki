// view.js (minimal)
import { LitElement, html, css } from "https://esm.sh/lit@3.1.2";
import "./two-pane-split-lit.js";

class HelloWorldModern extends LitElement {
  static styles = css`
    :host { display:block; border:1px dashed currentColor; border-radius:6px; overflow:hidden; }
    two-pane-split-lit { inline-size:100%; block-size:240px; }
    .fallback { display:flex; align-items:center; justify-content:center; font:500 1rem/1.3 system-ui,sans-serif; color:#333; block-size:100%; }
  `;

  render() {
    return html`
      <two-pane-split-lit orientation="auto" min-size="100" gutter-desktop="8" gutter-touch="16">
        <!-- Pane A: you said you don't care; give it a harmless fallback -->

      <div class="fallback" slot="a">Pane A</div>
      <!-- DEFAULT (unnamed) slot forwarded to Pane B -->
      <slot slot="b"></slot>
      </two-pane-split-lit>
    `;
  }
}
customElements.define("hello-world-modern", HelloWorldModern);

// Upgrade the saved wrapper so the slots take effect (no template changes needed)
document.querySelectorAll(".hello-world-modern").forEach((el) => {
  if (!(el instanceof HelloWorldModern)) {
    const wrapper = document.createElement("hello-world-modern");
    while (el.firstChild) wrapper.appendChild(el.firstChild);
    el.replaceWith(wrapper);
  }
});
