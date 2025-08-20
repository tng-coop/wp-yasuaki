// split-styles.js
import { css } from "https://esm.sh/lit@3.1.2";

export const splitStyles = css`
  :host { 
    display: block; 
    inline-size: 100%; 
    block-size: 100%; 
  }

  .outer { 
    display: flex; 
    inline-size: 100%; 
    block-size: 100%; 
    overflow: hidden;   /* outer never scrolls */
  }

  .pane  { 
    overflow: hidden;   /* panes themselves don’t scroll */
    background: #f9fafb; 
    min-block-size: 0; 
  }

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

  /* Default slot children don’t get forced height */
  ::slotted(*) { inline-size: 100%; min-block-size: 0; }

  /* Pane B’s wrapper fills the pane and is the ONLY scroller */
  ::slotted(.pane-b-host) {
    block-size: 100%;
    min-block-size: 0;
    overflow: auto;   /* scrollbar lives here */
  }

  /* Pane A height is driven by --pane-a-px when vertical */
  ::slotted([data-fit-top]) { 
    block-size: var(--pane-a-px, auto); 
  }

  /* Turn off scroll in Pane A completely */
  #paneA {
    overflow: clip;          /* modern: no scrollbars, no scroll */
    overflow: hidden;        /* fallback */
    scrollbar-width: none;   /* Firefox hides scrollbar UI */
  }
  #paneA::-webkit-scrollbar { display: none; } /* Chrome/Safari */
`;
