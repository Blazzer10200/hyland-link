import { invoke } from "@tauri-apps/api/core";

export type Manifest = {
  schema: number;
  revision: number;
  server: { name: string; host: string; port: number };
  game: { appId: number; version: string };
  melonloader: { version: string; url: string; sha256: string };
  mod: { version: string; url: string; sha256: string; dll: string; dllSha256: string; removeDlls: string[] };
  mods: ExtraMod[];
  launcher: { version: string; downloadUrl: string };
  notice: string;
};

export type ExtraMod = {
  id: string;
  name: string;
  version: string;
  blurb: string;
  side: "both" | "client" | "server";
  url: string;
  sha256: string;
  files: { source: string; path: string; sha256: string }[];
};

export type ManifestSource = { kind: "online" } | { kind: "builtIn"; reason: string };

export type GameInfo = {
  found: boolean;
  path: string | null;
  branch: string | null;
  running: boolean;
};

export type Component = { installed: boolean; version: string | null; upToDate: boolean };

export type ModStatus = {
  id: string;
  name: string;
  blurb: string;
  version: string;
  installed: boolean;
  installedVersion: string | null;
  upToDate: boolean;
};

export type StaleMod = { id: string; name: string; version: string };

export type InstallState = {
  melonloader: Component;
  serverMod: Component;
  mods: ModStatus[];
  staleMods: StaleMod[];
  firstLaunchDone: boolean;
};

export type Snapshot = {
  manifest: Manifest;
  manifestSource: ManifestSource;
  launcherVersion: string;
  game: GameInfo;
  install: InstallState | null;
  favoriteAdded: boolean;
};

export type ServerStatus = {
  online: boolean;
  name: string | null;
  players: number | null;
  maxPlayers: number | null;
  pingMs: number | null;
  error: string | null;
};

export type InstallProgress = {
  step: "melonloader" | "mod" | "mods";
  label: string;
  item: number;
  items: number;
  done: number;
  total: number;
};

/** New lines of MelonLoader/Latest.log. `fresh` means a different run than the one asked about: drop the old lines. */
export type LogChunk = { run: string | null; offset: number; fresh: boolean; lines: string[] };

export const api = {
  getState: () => invoke<Snapshot>("get_state"),
  refreshManifest: () => invoke<void>("refresh_manifest"),
  runInstall: () => invoke<void>("run_install"),
  queryServer: () => invoke<ServerStatus>("query_server"),
  play: () => invoke<void>("play"),
  /** Asks the game to quit normally (never kills it). */
  quitGame: () => invoke<void>("quit_game"),
  openLauncherLogs: () => invoke<void>("open_launcher_logs"),
  /** Cleans the game folder, starts Hyland Link's uninstaller and closes the launcher. */
  uninstallLauncher: () => invoke<void>("uninstall_launcher"),
  readGameLog: (run: string | null, offset: number) => invoke<LogChunk>("read_game_log", { run, offset }),
  setGamePath: (path: string) => invoke<void>("set_game_path", { path }),
  openGameFolder: () => invoke<void>("open_game_folder"),
  openLauncherDownload: () => invoke<void>("open_launcher_download"),
  uninstall: (melonloader: boolean) => invoke<void>("uninstall", { melonloader }),
  /** The newer launcher version on GitHub Releases, or null when this one is the latest. */
  checkUpdate: () => invoke<string | null>("check_update"),
  installUpdate: () => invoke<void>("install_update"),
};
