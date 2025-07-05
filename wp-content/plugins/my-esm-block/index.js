/* index.js — loaded via <script type="module"> in the editor
 * Uses THREE directly from esm.sh (no local build)
 */
import * as THREE from 'https://esm.sh/three@0.164.0';

// 1. Grab WordPress helpers from the global `wp.*` registry.
const { registerBlockType }   = window.wp.blocks;
const { createElement,
        useRef,
        useEffect }           = window.wp.element;

/**
 * React‑style functional component (remember WP.element is React under the hood).
 * Renders a <canvas> and boots Three.js when mounted.
 */
function CubeEdit() {
    const canvasRef = useRef( null );

    useEffect( () => {
        const canvas   = canvasRef.current;

        /* ---------- Three.js boilerplate ---------- */
        const renderer = new THREE.WebGLRenderer( { canvas, antialias: true } );
        renderer.setSize( 300, 300 );

        const scene    = new THREE.Scene();
        const camera   = new THREE.PerspectiveCamera( 75, 1, 0.1, 1000 );
        camera.position.z = 2;

        const geometry = new THREE.BoxGeometry();
        const material = new THREE.MeshNormalMaterial();
        const cube     = new THREE.Mesh( geometry, material );
        scene.add( cube );

        function animate() {
            cube.rotation.x += 0.01;
            cube.rotation.y += 0.01;
            renderer.render( scene, camera );
            requestAnimationFrame( animate );
        }
        animate();

        /* cleanup when the block unmounts */
        return () => {
            geometry.dispose();
            material.dispose();
            renderer.dispose();
        };
    }, [] );

    /* canvas element shown inside the editor */
    return createElement(
        'canvas',
        { ref: canvasRef, width: 300, height: 300,
          style: { maxWidth: '100%', height: 'auto', display: 'block' } }
    );
}

/* 2. Register the block */
registerBlockType( 'my-plugin/cube-block', {
    title:     '3‑D Cube',
    icon:      'crop',
    category:  'media',

    edit: CubeEdit,

    /** Front‑end markup: just a canvas placeholder.
     *  It gets “activated” by view.js for visitors.
     */
    save() {
        return createElement( 'canvas',
            { className: 'my-esm-cube', width: 300, height: 300 } );
    }
} );
