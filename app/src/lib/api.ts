import { invoke } from "@tauri-apps/api/core";

export type Manifest = {
  schema: number;
  revision: number;
  server: { name: string; host: string; port: number };
  game: { appId: number; version: string };
  melonloader: { version: string; url: string; sha256: string };
  mod: { version: string; url: string; sha256: string; dll: string; dllSha256: string; removeDlls: string[] };
  launcher: { version: string; downloadUrl: string };
  notice: string;
};

export type ManifestSource = { kind: "online" } | { kind: "builtIn"; reason: string };

export type GameInfo = {
  found: boolean;
  path: string | null;
  branch: string | null;
  running: boolean;
  pickedManually: boolean;
};

export type Component = { installed: boolean; version: string | null; upToDate: boolean };

export type InstallState = {
  melonloader: Component;
  serverMod: Component;
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
  description: string | null;
  players: number | null;
  maxPlayers: number | null;
  pingMs: number | null;
  error: string | null;
};

export type InstallProgress = { step: "melonloader" | "mod"; label: string; done: number; total: number };

export const api = {
  getState: () => invoke<Snapshot>("get_state"),
  refreshManifest: () => invoke<void>("refresh_manifest"),
  runInstall: () => invoke<void>("run_install"),
  queryServer: () => invoke<ServerStatus>("query_server"),
  play: () => invoke<void>("play"),
  setGamePath: (path: string) => invoke<void>("set_game_path", { path }),
  openGameFolder: () => invoke<void>("open_game_folder"),
  openLauncherDownload: () => invoke<void>("open_launcher_download"),
  uninstall: (melonloader: boolean) => invoke<void>("uninstall", { melonloader }),
  /** The newer launcher version on GitHub Releases, or null when this one is the latest. */
  checkUpdate: () => invoke<string | null>("check_update"),
  installUpdate: () => invoke<void>("install_update"),
};
