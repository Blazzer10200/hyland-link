// Dev-only: lets the UI run in a plain browser (vite dev) with fake backend answers, so every screen
// state can be previewed. Pick one with
// ?s=setup|update|mods|nomods|installing|closegame|firstlaunch|firstrun|ready|playing|logerrors|offline|branch|nogame|selfupdate.
// Every state except nomods pretends the server runs three extra mods; mods = one new, one update, one dropped.
// firstrun and playing stream a game log (the one-time setup, a join); logerrors is playing plus a warning and an error.
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
    case "logerrors":
      return { ...base, game: { ...base.game, running: true }, install: installed, favoriteAdded: true };
    default:
      return base;
  }
}

const online: ServerStatus = {
  online: true,
  name: shipped.server.name,
  players: 2,
  maxPlayers: 8,
  pingMs: 38,
  error: null,
};
const offline: ServerStatus = { online: false, name: null, players: null, maxPlayers: null, pingMs: null, error: "no answer" };

// Fake Latest.log lines as [ms after page load, line], shaped like a real MelonLoader 0.7.2 + client mod 1.1.6 run.
const header = (t: string): [number, string][] => [
  [0, `[${t}.352] ------------------------------`],
  [100, `[${t}.355] MelonLoader v0.7.2 Open-Beta`],
  [200, `[${t}.356] Game Type: Il2cpp`],
  [300, `[${t}.432] Game Version: 0.4.7f12`],
  [400, `[${t}.433] ------------------------------`],
  [900, `[${t}.920] Preferences Loaded!`],
  [1300, `[${t}.935] 0 Plugins loaded.`],
  [1700, `[${t}.425] [Il2CppAssemblyGenerator] Contacting RemoteAPI...`],
];
const setupLog: [number, string][] = [
  ...header("02:33:35"),
  ...[
    "Processing Cpp2IL.Plugin.StrippedCodeRegSupport...",
    "Checking GameAssembly...",
    "[Info] [Library] Searching Binary for Required Data...",
    "[Info] [Program] Application model created in 1197.5356ms",
    "[Info] [Program] Invoking processing layers...",
    "Reading unity assemblies...",
    "Creating typedefs...",
    "Filling generic constraints...",
    "Finalizing method declarations...",
    "Unstripping types...",
    "Unstripping methods...",
    "IL unstrip statistics: 7995 successful, 1226 failed",
  ].map((text, i): [number, string] => [2600 + i * 1300, `[02:33:${String(37 + i * 2).padStart(2, "0")}.110] [Il2CppAssemblyGenerator] ${text}`]),
];
const joinLog = (errors: boolean): [number, string][] => [
  ...header(runHeader),
  [2200, "[02:34:54.661] [Il2CppAssemblyGenerator] Assembly is up to date. No Generation Needed."],
  [2900, "[02:34:54.662] Loading Mods..."],
  [3300, "[02:34:54.701] Melon Assembly loaded: '.\\Mods\\DedicatedServerMod_Il2cpp_Client.dll'"],
  [3700, "[02:34:55.042] DedicatedServerClient v1.1.6"],
  [4200, "[02:34:55.043] 1 Mod loaded."],
  [4900, "[02:34:58.123] [DedicatedServerClient] DedicatedServerClient mod initializing..."],
  [5400, "[02:34:58.212] [DedicatedServerClient] DedicatedServerClient Steam runtime check passed."],
  [5900, "[02:34:58.332] [DedicatedServerClient] DedicatedServerClient mod initialized successfully"],
  ...(errors ? [[6500, "[02:35:00.104] [DedicatedServerClient] [WARNING] Example warning from the mock"] as [number, string]] : []),
  [7200, "[02:35:07.045] [DedicatedServerClient] AssetBundle loaded successfully from embedded resources"],
  [10000, "[02:35:19.521] [DedicatedServerClient] Target server updated to 69.50.245.12:38465"],
  [10300, "[02:35:19.523] [DedicatedServerClient] Starting dedicated server connection to 69.50.245.12:38465"],
  ...(errors ? [[11200, "[02:35:20.880] [DedicatedServerClient] [ERROR] Example error from the mock"] as [number, string]] : []),
  [12800, "[02:35:22.527] [DedicatedServerClient] Main scene loaded in dedicated server mode - ensuring quest initialization"],
  [14500, "[02:35:26.534] [DedicatedServerClient] Quest system initialization completed"],
];
// Like MelonLoader, every launch starts a new log whose first line carries a new time.
let runStartedAt = Date.now();
let runHeader = "02:34:53";
function relaunch(next: string) {
  scenario = next;
  runStartedAt = Date.now();
  runHeader = "02:41:42";
}

function readGameLog({ run, offset }: { run: string | null; offset: number }) {
  const streaming = scenario === "firstrun" || scenario === "playing" || scenario === "logerrors";
  const script = scenario === "firstrun" ? setupLog : joinLog(scenario === "logerrors");
  const elapsed = streaming ? Date.now() - runStartedAt : Infinity;
  const written = script.filter(([at]) => at <= elapsed).map(([, line]) => line);
  const current = written[0] ?? null;
  const fresh = current !== run;
  return { run: current, offset: written.length, fresh, lines: written.slice(fresh ? 0 : offset) };
}

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
    async (cmd, payload) => {
      switch (cmd) {
        case "read_game_log":
          return readGameLog(payload as { run: string | null; offset: number });
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
          // Steam takes a moment to start the game.
          setTimeout(() => {
            if (scenario === "firstlaunch") relaunch("firstrun");
            else if (scenario === "ready") relaunch("playing");
          }, 1500);
          return null;
        case "quit_game":
          // The game takes a moment to save and close.
          setTimeout(() => (scenario = "ready"), 2500);
          return null;
        case "uninstall":
          // Removing MelonLoader touches a lot of files; long enough to see the button lock.
          await new Promise((r) => setTimeout(r, 2500));
          return null;
        case "uninstall_launcher":
          throw "The browser preview has no uninstaller. In the real app this closes the launcher and runs it.";
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
