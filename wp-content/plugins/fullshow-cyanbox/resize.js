document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll(".cyanbox").forEach((box) => {
    const parent = box.parentElement;
    if (!parent) return;

    const ro = new ResizeObserver((entries) => {
			console.log("ResizeObserver triggered for:", box);
      for (let entry of entries) {
        const { width, height } = entry.contentRect;
        box.style.width = width + "px";
        box.style.height = height + "px";
      }
    });

    ro.observe(parent);
  });
});
