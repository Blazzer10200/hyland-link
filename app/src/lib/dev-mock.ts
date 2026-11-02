// Dev-only: lets the UI run in a plain browser (vite dev) with fake backend answers, so every screen
// state can be previewed. Pick one with
// ?s=setup|update|installing|closegame|firstlaunch|firstrun|ready|playing|offline|branch|nogame|selfupdate.
// Loaded from hooks.client.ts only when import.meta.env.DEV and not inside Tauri.
import { emit } from "@tauri-apps/api/event";
import { mockIPC, mockWindows } from "@tauri-apps/api/mocks";
import type { InstallProgress, ServerStatus, Snapshot } from "./api";
import manifest from "../../../manifest/manifest.json";

let scenario = new URLSearchParams(location.search).get("s") ?? "setup";
const gamePath = String.raw`C:\Program Files (x86)\Steam\steamapps\common\Schedule I`;

function snapshot(): Snapshot {
  const fresh = { installed: false, version: null, upToDate: false };
  const current = (version: string) => ({ installed: true, version, upToDate: true });
  const base: Snapshot = {
    manifest: manifest as Snapshot["manifest"],
    manifestSource: { kind: "online" },
    launcherVersion: "0.1.0",
    game: { found: true, path: gamePath, branch: null, running: false, pickedManually: false },
    install: { melonloader: fresh, serverMod: fresh, firstLaunchDone: false },
    favoriteAdded: false,
  };
  const installed = {
    melonloader: current(manifest.melonloader.version),
    serverMod: current(manifest.mod.version),
    firstLaunchDone: true,
  };
  switch (scenario) {
    case "nogame":
      return { ...base, game: { ...base.game, found: false, path: null }, install: null };
    case "branch":
      return { ...base, game: { ...base.game, branch: "alternate" } };
    case "update":
      return {
        ...base,
        install: { ...installed, serverMod: { installed: true, version: "1.1.5", upToDate: false } },
        favoriteAdded: true,
      };
    case "ready":
    case "offline":
    case "selfupdate":
      return { ...base, install: installed, favoriteAdded: true };
    case "firstlaunch":
      return { ...base, install: { ...installed, firstLaunchDone: false }, favoriteAdded: true };
    case "firstrun":
      return { ...base, game: { ...base.game, running: true }, install: { ...installed, firstLaunchDone: false }, favoriteAdded: true };
    case "closegame":
      return { ...base, game: { ...base.game, running: true } };
    case "playing":
      return { ...base, game: { ...base.game, running: true }, install: installed, favoriteAdded: true };
    default:
      return base;
  }
}

const online: ServerStatus = {
  online: true,
  name: manifest.server.name,
  description: "Blazzer and friends",
  players: 2,
  maxPlayers: 8,
  pingMs: 38,
  error: null,
};
const offline: ServerStatus = { online: false, name: null, description: null, players: null, maxPlayers: null, pingMs: null, error: "no answer" };

async function fakeInstall() {
  const steps: Omit<InstallProgress, "done">[] = [
    { step: "melonloader", label: `MelonLoader ${manifest.melonloader.version}`, total: 24_000_000 },
    { step: "mod", label: `server mod ${manifest.mod.version}`, total: 1_460_352 },
  ];
  for (const step of steps) {
    for (let i = 0; i <= 10; i++) {
      await emit("install-progress", { ...step, done: (step.total * i) / 10 });
      await new Promise((r) => setTimeout(r, 180));
    }
  }
}

export function installMock() {
  mockWindows("main");
  mockIPC(
    async (cmd) => {
      switch (cmd) {
        case "get_state":
          return snapshot();
        case "query_server":
          await new Promise((r) => setTimeout(r, 400));
          return scenario === "offline" ? offline : online;
        case "run_install":
          await fakeInstall();
          scenario = "firstlaunch";
          return null;
        case "play":
          if (scenario === "firstlaunch") scenario = "firstrun";
          return null;
        case "check_update":
          return scenario === "selfupdate" ? "0.2.0" : null;
        case "install_update":
          // The real installer closes the app at the end, so this never returns.
          await new Promise((r) => setTimeout(r, 1500));
          for (let pct = 0; pct <= 100; pct += 5) {
            await emit("update-progress", pct);
            await new Promise((r) => setTimeout(r, 150));
          }
          return new Promise(() => {});
        default:
          return null;
      }
    },
    { shouldMockEvents: true },
  );
  if (scenario === "installing") {
    // Start the fake install as soon as the page wires its listener.
    setTimeout(() => document.querySelector<HTMLButtonElement>("button.primary")?.click(), 600);
  }
}
