// Drag-to-resize for the demo's JSON panel. Document-level delegation so it keeps
// working across Blazor re-renders (the panel/splitter are added and removed by the
// toggle). The splitter sets the panel width directly for smooth dragging.
(function () {
    "use strict";
    let dragging = false;

    function panel() {
        return document.querySelector(".demo-panel");
    }

    document.addEventListener("pointerdown", function (e) {
        if (!(e.target instanceof Element) || !e.target.closest(".demo-splitter")) {
            return;
        }
        dragging = true;
        document.body.style.userSelect = "none";
        document.body.style.cursor = "col-resize";
        e.preventDefault();
    });

    document.addEventListener("pointermove", function (e) {
        if (!dragging) {
            return;
        }
        const p = panel();
        if (!p) {
            return;
        }
        const width = window.innerWidth - e.clientX;
        const clamped = Math.max(280, Math.min(width, window.innerWidth * 0.8));
        p.style.width = clamped + "px";
    });

    function stop() {
        if (!dragging) {
            return;
        }
        dragging = false;
        document.body.style.userSelect = "";
        document.body.style.cursor = "";
    }

    document.addEventListener("pointerup", stop);
    document.addEventListener("pointercancel", stop);
})();
