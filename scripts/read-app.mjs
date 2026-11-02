// Prints the text on the running Hyland Link window, read over WebView2's DevTools port. Read-only: never clicks.
//
//   $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9333"
//   Start-Process C:\cargo-targets\release\hyland-link.exe
//   node scripts/read-app.mjs 9333
const port = process.argv[2] ?? "9333";
let page;
for (let i = 0; i < 40 && !page; i++) {
  try {
    const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
    page = targets.find((t) => t.type === "page");
  } catch {
    // Not listening yet: the window is still starting.
  }
  if (!page) await new Promise((r) => setTimeout(r, 500));
}
if (!page) throw new Error(`No WebView2 page on port ${port}. Was the app started with the DevTools argument?`);

// Give the launcher time to load state, fetch the online manifest and run the update check.
await new Promise((r) => setTimeout(r, 9000));
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener("open", r, { once: true }));
ws.send(JSON.stringify({ id: 1, method: "Runtime.evaluate", params: { expression: "document.body.innerText", returnByValue: true } }));
const reply = await new Promise((r) => ws.addEventListener("message", (e) => r(JSON.parse(e.data)), { once: true }));
ws.close();
console.log(reply.result.result.value);
