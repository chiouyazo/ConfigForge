// Hash deep-linking helpers for the ConfigForge open-mode host. The active
// category is mirrored to the URL fragment (e.g. /config#advanced) so a link
// opens straight to that category. The fragment never reaches the server, so it
// cannot collide with the host application's own routes.
window.configForgeHost = {
    readHash() {
        return (window.location.hash || "").replace(/^#/, "");
    },
    writeHash(slug) {
        const target = slug ? "#" + slug : "";
        if (target !== window.location.hash) {
            history.replaceState(null, "", window.location.pathname + window.location.search + target);
        }
    },
    onHashChange(dotnetRef) {
        const self = this;
        window.addEventListener("hashchange", () => {
            dotnetRef.invokeMethodAsync("OnHashChanged", self.readHash());
        });
    },
};
