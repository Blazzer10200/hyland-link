// In `vite dev` outside Tauri (a plain browser), swap the backend for canned answers so screens can be previewed.
export async function init() {
  if (import.meta.env.DEV && !("__TAURI_INTERNALS__" in window)) {
    const { installMock } = await import("$lib/dev-mock");
    installMock();
  }
}
