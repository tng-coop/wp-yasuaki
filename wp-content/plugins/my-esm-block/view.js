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

// JavaScript: Adjust internal resolution based on CSS size
function resizeCanvas() {
    const canvas = document.querySelector('canvas');  // Get the canvas element
    const rect = canvas.getBoundingClientRect();  // Get the size from CSS
    const width = rect.width;
    const height = rect.height;
    console.log(`Resizing canvas to: ${width}x${height}`);

    // Set the internal resolution (drawing buffer size)
    canvas.width = window.innerWidth
    canvas.height = window.innerHeight; 


    // Optionally, redraw or reinitialize your canvas content
    // Re-initialize the cube with the new size
    initCube(canvas);
}

// Resize canvas when window is resized
window.addEventListener('resize', resizeCanvas);

// kick‑off once DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    document
        .querySelectorAll('canvas.my-esm-cube')
        .forEach( initCube );
    resizeCanvas();  // Ensure the canvas is correctly sized when the page loads
});
