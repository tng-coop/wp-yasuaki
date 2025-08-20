// view.js
import { LitElement, html, css } from "https://esm.sh/lit@3.1.2";
import "./two-pane-split-lit.js";

class HelloWorldModern extends LitElement {
  static styles = css`
    :host {
      display: block;
      border: 1px dashed currentColor;
      border-radius: 6px;
      overflow: hidden;
    }
    two-pane-split-lit {
      inline-size: 100%;
      block-size: 100%;
    }
    .fallback {
      display: flex;
      align-items: center;
      justify-content: center;
      font: 500 1rem/1.3 system-ui, sans-serif;
      color: #333;
      block-size: 100%;
    }
  `;

  render() {
    return html`
      <two-pane-split-lit orientation="auto" min-size="100" gutter-desktop="8" gutter-touch="16">
        <!-- Pane A: named slot -->
        <slot name="a" slot="a"><div class="fallback">Pane A</div></slot>
        <!-- Pane B: DEFAULT slot forwarded to Pane B -->
        <slot slot="b"><div class="fallback">Pane B</div></slot>
      </two-pane-split-lit>
    `;
  }
}
customElements.define("hello-world-modern", HelloWorldModern);

/**
 * Upgrade & route editor HTML:
 *  - Move children of .hw-pane-a into a wrapper with slot="a"  → Pane A
 *  - Move children of .hw-pane-b and any leftovers into a single wrapper → Pane B (default slot)
 *  - Remove the emptied group wrappers so they don't become leftovers
 */
document.querySelectorAll(".hello-world-modern").forEach((host) => {
  // already upgraded?
  if (host.tagName.toLowerCase() === "hello-world-modern") return;

  const wrapper = document.createElement("hello-world-modern");

  const groupA = host.querySelector(".hw-pane-a");
  const groupB = host.querySelector(".hw-pane-b");

  const moveChildren = (src, dst) => {
    if (!src) return;
    while (src.firstChild) dst.appendChild(src.firstChild);
  };

  // 1) Pane A → named slot
  if (groupA) {
    const aWrap = document.createElement("div");
    aWrap.setAttribute("slot", "a");   // important: named slot for Pane A
    moveChildren(groupA, aWrap);
    wrapper.appendChild(aWrap);
  }

  // 2) Pane B → single default-slotted wrapper (becomes the scroller content)
  const bWrap = document.createElement("div");
  bWrap.className = "pane-b-host";     // styling hook; NO slot attribute here
  if (groupB) moveChildren(groupB, bWrap);

  // 3) remove emptied groups
  groupA?.remove();
  groupB?.remove();

  // 4) move any meaningful leftovers into Pane B
  const isIgnorable = (n) => n.nodeType === 3 && !/\S/.test(n.nodeValue);
  Array.from(host.childNodes).forEach((n) => {
    if (!isIgnorable(n)) bWrap.appendChild(n);
  });

  // 5) finish
  wrapper.appendChild(bWrap);
  host.replaceWith(wrapper);
});
