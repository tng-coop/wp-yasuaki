/* view.js — runs on the front‑end only, loads THREE once
 * and animates every <canvas.my-esm-cube> it finds.
 */
import * as THREE from 'https://esm.sh/three@0.164.0';

function initCube( canvas ) {
    const renderer = new THREE.WebGLRenderer( { canvas, antialias: true } );
    renderer.setSize( canvas.width, canvas.height );

    const scene    = new THREE.Scene();
    const camera   = new THREE.PerspectiveCamera( 75, 1, 0.1, 1000 );
    camera.position.z = 2;

    const geometry = new THREE.BoxGeometry();
    const material = new THREE.MeshNormalMaterial();
    const cube     = new THREE.Mesh( geometry, material );
    scene.add( cube );

    (function animate() {
        cube.rotation.x += 0.01;
        cube.rotation.y += 0.01;
        renderer.render( scene, camera );
        requestAnimationFrame( animate );
    })();
}

/* kick‑off once DOM is ready */
document.addEventListener( 'DOMContentLoaded', () => {
    document
        .querySelectorAll( 'canvas.my-esm-cube' )
        .forEach( initCube );
} );
