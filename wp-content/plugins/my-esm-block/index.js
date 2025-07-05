// index.js  — still <script type="module"> but no external imports

const { registerBlockType } = window.wp.blocks;
const { createElement     } = window.wp.element;

registerBlockType( 'my-plugin/hello-block', {
    title: 'Hello Block',
    icon:  'smiley',
    category: 'text',

    edit()  { return createElement( 'p', null, 'Hello from the editor!' ); },
    save()  { return createElement( 'p', null, 'Hello from the front‑end!' ); },
} );
