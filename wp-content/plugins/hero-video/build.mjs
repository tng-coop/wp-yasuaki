import { build } from 'esbuild';

await build({
  entryPoints: ['src/view.js'],
  bundle: true,
  format: 'esm',
  target: ['es2020'],
  sourcemap: true,
  minify: true,
  outdir: 'build',
  entryNames: '[name]',
  legalComments: 'none'
});
