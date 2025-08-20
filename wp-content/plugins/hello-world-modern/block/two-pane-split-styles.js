// split-styles.js
import { css } from "https://esm.sh/lit@3.1.2";

export const splitStyles = css`
  :host { display: block; inline-size: 100%; block-size: 100%; }
  .outer { display: flex; inline-size: 100%; block-size: 100%; overflow: hidden; }
  .pane  { overflow: auto; background: #f9fafb; min-block-size: 0; }
  .gutter {
    background: #ddd;
    touch-action: none;
    -webkit-user-select: none;
    user-select: none;
    transition: background-color 0.15s ease;
  }
  .gutter-horizontal { cursor: col-resize; }
  .gutter-vertical   { cursor: row-resize; }
  .gutter.is-dragging { background: #2563eb; }
  .gutter:focus-visible { outline: 2px solid #2563eb; outline-offset: -2px; }
  ::slotted(*) { inline-size: 100%; block-size: 100%; min-block-size: 0; }
  /* Optional: opt-in hook if you prefer CSS over JS setting height */
  ::slotted([data-fit-top]) { block-size: var(--pane-a-px, auto); }
    /* Turn off scroll in Pane A only */
  #paneA {
    overflow: clip;          /* modern: no scrollbars, no scroll */
    /* fallback for older browsers */
    overflow: hidden;
    scrollbar-width: none;   /* Firefox hides scrollbar UI */
  }
  #paneA::-webkit-scrollbar { display: none; } /* Chrome/Safari */
`;
