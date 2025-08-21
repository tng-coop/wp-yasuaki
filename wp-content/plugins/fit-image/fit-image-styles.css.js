// fit-image-styles.css.js
import { css } from "https://esm.sh/lit@3";

export const fitImageStyles = css`
:host {
  display: block;
  position: relative;
  overflow: hidden;
  background: transparent;
  touch-action: none;
}

/* Toggle button */
.toggle {
  position: absolute;
  top: 0.75rem;
  left: 0.75rem;
  z-index: 2;
  padding: 0.5rem 0.75rem;
  background-color: transparent; /* remove blue fill */
  color: white; /* text color stays white */
  font-size: 0.9rem;
  border: 2px solid white; /* white outline */
  border-radius: 0.5rem;
  cursor: pointer;
  transition: background-color 0.2s ease, transform 0.1s ease;
  touch-action: manipulation;
  -webkit-tap-highlight-color: transparent;
}

.toggle:hover { background-color: rgba(255, 255, 255, 0.1); } /* subtle hover */
.toggle:active { transform: scale(0.97); }


::slotted(*) {
  position: absolute;
  left: 0; top: 0;
  width: auto; height: auto;
  display: block;
  background: transparent;
  user-select: none;
  -webkit-user-drag: none;
  pointer-events: auto;
  will-change: left, top, width, height;
  touch-action: none;
}
:host([pannable]) ::slotted(*) { cursor: grab; }
:host([pannable].dragging) ::slotted(*) { cursor: grabbing; }
`;
