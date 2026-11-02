// Dev-only: lets the UI run in a plain browser (vite dev) with fake backend answers, so every screen
// state can be previewed. Pick one with
// ?s=setup|update|mods|nomods|installing|closegame|firstlaunch|firstrun|ready|playing|offline|branch|nogame|selfupdate.
// Every state except nomods pretends the server runs three extra mods; mods = one new, one update, one dropped.
// Loaded from hooks.client.ts only when import.meta.env.DEV and not inside Tauri.
import { emit } from "@tauri-apps/api/event";
import { mockIPC, mockWindows } from "@tauri-apps/api/mocks";
import type { InstallProgress, Manifest, ModStatus, ServerStatus, Snapshot } from "./api";
import shipped from "../../../manifest/manifest.json";

let scenario = new URLSearchParams(location.search).get("s") ?? "setup";
const gamePath = String.raw`C:\Program Files (x86)\Steam\steamapps\common\Schedule I`;

const fakeMod = (id: string, name: string, version: string, blurb: string): Manifest["mods"][number] => ({
  id,
  name,
  version,
  blurb,
  side: "both",
  url: `https://thunderstore.io/package/download/Example/${id}/${version}/`,
  sha256: "0".repeat(64),
  files: [{ source: `Mods/${id}.dll`, path: `Mods/${id}.dll`, sha256: "0".repeat(64) }],
});
const extraMods = [
  fakeMod("example-a", "Example mod A", "1.4.0", "A one-line blurb from the manifest"),
  fakeMod("example-b", "Example mod B", "1.3.0", "What it adds, in a few words"),
  fakeMod("example-c", "Example mod C", "2.0.1", "Newly added to the server"),
];

function snapshot(): Snapshot {
  const fresh = { installed: false, version: null, upToDate: false };
  const current = (version: string) => ({ installed: true, version, upToDate: true });
  const extras = scenario === "nomods" ? [] : extraMods;
  const modStatus = (m: (typeof extraMods)[number], installedVersion: string | null): ModStatus => ({
    id: m.id,
    name: m.name,
    blurb: m.blurb,
    version: m.version,
    installed: installedVersion !== null,
    installedVersion,
    upToDate: installedVersion === m.version,
  });
  const base: Snapshot = {
    manifest: { ...(shipped as Manifest), mods: extras },
    manifestSource: { kind: "online" },
    launcherVersion: "0.1.0",
    game: { found: true, path: gamePath, branch: null, running: false },
    install: { melonloader: fresh, serverMod: fresh, mods: extras.map((m) => modStatus(m, null)), staleMods: [], firstLaunchDone: false },
    favoriteAdded: false,
  };
  const installed = {
    melonloader: current(shipped.melonloader.version),
    serverMod: current(shipped.mod.version),
    mods: extras.map((m) => modStatus(m, m.version)),
    staleMods: [],
    firstLaunchDone: true,
  };
  switch (scenario) {
    case "mods":
      return {
        ...base,
        install: {
          ...installed,
          mods: [modStatus(extras[0], extras[0].version), modStatus(extras[1], "1.2.0"), modStatus(extras[2], null)],
          staleMods: [{ id: "old-d", name: "Old mod D", version: "0.9.0" }],
        },
        favoriteAdded: true,
      };
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
    case "nomods":
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
  name: shipped.server.name,
  description: "Blazzer and friends",
  players: 2,
  maxPlayers: 8,
  pingMs: 38,
  error: null,
};
const offline: ServerStatus = { online: false, name: null, description: null, players: null, maxPlayers: null, pingMs: null, error: "no answer" };

async function fakeInstall() {
  // Like the backend: only what's out of date gets downloaded.
  const install = snapshot().install;
  const pending = (install?.mods ?? []).filter((m) => !m.upToDate);
  const steps: Omit<InstallProgress, "done">[] = [
    ...(install?.melonloader.upToDate
      ? []
      : [{ step: "melonloader" as const, label: `MelonLoader ${shipped.melonloader.version}`, item: 1, items: 1, total: 24_000_000 }]),
    ...(install?.serverMod.upToDate ? [] : [{ step: "mod" as const, label: `server mod ${shipped.mod.version}`, item: 1, items: 1, total: 1_460_352 }]),
    ...pending.map((m, i) => ({ step: "mods" as const, label: `${m.name} ${m.version}`, item: i + 1, items: pending.length, total: 380_000 })),
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
