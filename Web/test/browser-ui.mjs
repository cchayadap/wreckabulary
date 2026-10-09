import assert from "node:assert/strict";
export const softwareGpu = process.env.WRECKABULARY_SOFTWARE_GPU === "1";
export const deviceScaleFactor = softwareGpu ? 0.5 : 1;
export async function configureSoftwareRendering(page) {
  if (!softwareGpu) return;
  const profile = await page.evaluate(() => {
    const view = window.wreckabulary.view;
    const shadows = [];
    view.scene.traverse((node) => {
      if (!node.isLight || !node.castShadow || !node.shadow) return;
      node.shadow.map?.dispose();
      node.shadow.map = null;
      node.shadow.mapSize.set(512, 512);
      node.shadow.needsUpdate = true;
      shadows.push([node.shadow.mapSize.x, node.shadow.mapSize.y]);
    });
    view.renderer.shadowMap.needsUpdate = true;
    const gl = view.renderer.getContext();
    const debug = gl.getExtension("WEBGL_debug_renderer_info");
    return {
      devicePixelRatio,
      drawingBuffer: [view.canvas.width, view.canvas.height],
      shadowsEnabled: view.renderer.shadowMap.enabled,
      shadows,
      renderer: debug ? gl.getParameter(debug.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER),
    };
  });
  assert.equal(profile.devicePixelRatio, deviceScaleFactor);
  assert.equal(profile.shadowsEnabled, true);
  assert.ok(profile.shadows.length > 0);
  assert.ok(profile.shadows.every(([width, height]) => width === 512 && height === 512));
  return profile;
}
export async function click(page, selector) {
  const target = page.locator(selector).first();
  await target.scrollIntoViewIfNeeded();
  const accessible = await target.evaluate((button) => {
    const r = button.getBoundingClientRect(),
      hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
    return (
      r.width > 0 &&
      r.height > 0 &&
      r.left >= 0 &&
      r.top >= 0 &&
      r.right <= innerWidth + 1 &&
      r.bottom <= innerHeight + 1 &&
      (hit === button || button.contains(hit))
    );
  });
  assert.equal(accessible, true, `unreachable UI: ${selector}`);
  await target.click({ timeout: softwareGpu ? 120000 : 30000 });
}
export async function screenshot(page, name) {
  await page.screenshot({ path: `playwright-results/${name}.png` });
}
