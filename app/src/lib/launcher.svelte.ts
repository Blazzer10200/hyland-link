import { listen } from "@tauri-apps/api/event";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { open } from "@tauri-apps/plugin-dialog";
import { api, type InstallProgress, type ServerStatus, type Snapshot } from "./api";

/** What the big button does right now. */
export type Phase =
  | "loading"
  | "noGame"
  | "branch"
  | "setup"
  | "update"
  | "installing"
  | "closeGame"
  | "firstLaunch"
  | "firstLaunchRunning"
  | "ready"
  | "playing";

/** The guided steps: 1 find the game, 2 install, 3 first launch, 4 join. */
export type StepNumber = 1 | 2 | 3 | 4;

const SERVER_POLL_MS = 15_000;
const GAME_POLL_MS = 4_000;
/** How often the server's mod list is re-read from GitHub while the launcher is open. */
const MANIFEST_POLL_MS = 5 * 60_000;
/** A focus or Play reuses a mod-list check younger than this instead of asking GitHub again. */
const MANIFEST_FRESH_MS = 60_000;

function message(error: unknown): string {
  return typeof error === "string" ? error : error instanceof Error ? error.message : String(error);
}

class Launcher {
  snapshot = $state<Snapshot | null>(null);
  server = $state<ServerStatus | null>(null);
  checkingServer = $state(false);
  installing = $state(false);
  progress = $state<InstallProgress | null>(null);
  error = $state<string | null>(null);
  step = $derived.by<StepNumber>(() => {
    const s = this.snapshot;
    if (!s || !s.game.found || !s.install || s.game.branch) return 1;
    const { melonloader, serverMod, mods, staleMods, firstLaunchDone } = s.install;
    const modsCurrent = mods.every((m) => m.upToDate) && staleMods.length === 0;
    if (this.installing || !(melonloader.upToDate && serverMod.upToDate && modsCurrent && s.favoriteAdded)) return 2;
    return firstLaunchDone ? 4 : 3;
  });

  phase = $derived.by<Phase>(() => {
    const s = this.snapshot;
    if (!s) return "loading";
    if (this.installing) return "installing";
    if (!s.game.found || !s.install) return "noGame";
    if (s.game.branch) return "branch";
    const running = s.game.running;
    switch (this.step) {
      case 2: {
        if (running) return "closeGame";
        const { melonloader, serverMod } = s.install;
        return melonloader.installed || serverMod.installed ? "update" : "setup";
      }
      case 3:
        return running ? "firstLaunchRunning" : "firstLaunch";
      default:
        return running ? "playing" : "ready";
    }
  });

  /** Set while the launcher replaces itself; the window shows the update screen until the installer closes it. */
  selfUpdate = $state<{ version: string; percent: number | null } | null>(null);
  /** A launcher version that failed to auto-install, offered as a manual download instead. */
  launcherUpdate = $state<string | null>(null);
  /** The "Mods on this server" sheet. */
  modsOpen = $state(false);
  /** Says why a ready player is suddenly back at "Update". */
  notice = $state<string | null>(null);
  private manifestCheckedAt = 0;

  async load() {
    try {
      this.snapshot = await api.getState();
    } catch (e) {
      this.error = message(e);
    }
  }

  async checkServer() {
    this.checkingServer = true;
    try {
      this.server = await api.queryServer();
    } catch (e) {
      this.server = { online: false, name: null, description: null, players: null, maxPlayers: null, pingMs: null, error: message(e) };
    } finally {
      this.checkingServer = false;
    }
  }

  /** Runs a user action, surfacing its error in the banner and refreshing state afterwards. */
  private async act(action: () => Promise<void>) {
    this.error = null;
    try {
      await action();
    } catch (e) {
      this.error = message(e);
    } finally {
      await this.load();
    }
  }

  install = () =>
    this.act(async () => {
      this.installing = true;
      this.progress = null;
      try {
        await api.runInstall();
      } finally {
        this.installing = false;
        this.progress = null;
      }
    });

  /** Re-reads the server's mod list, then the game folder. Skipped while installing or when checked < maxAgeMs ago. */
  private async syncManifest(maxAgeMs = 0) {
    if (this.installing || Date.now() - this.manifestCheckedAt < maxAgeMs) return;
    this.manifestCheckedAt = Date.now();
    const wasReady = this.step >= 3;
    const revision = this.snapshot?.manifest.revision;
    try {
      await api.refreshManifest();
      await this.load();
    } catch (e) {
      this.error = message(e);
      return;
    }
    if (wasReady && this.step === 2 && this.snapshot?.manifest.revision !== revision) {
      this.notice = "The server's mods just changed. Update to keep playing.";
    }
  }

  play = () =>
    this.act(async () => {
      // Never launch on mods the server just replaced: the button turns into Update instead.
      await this.syncManifest(MANIFEST_FRESH_MS);
      if (this.step < 3) {
        this.notice ??= "The server's mods just changed. Update first, then play.";
        return;
      }
      await api.play();
    });
  openGameFolder = () => this.act(() => api.openGameFolder());
  openLauncherDownload = () => this.act(() => api.openLauncherDownload());
  uninstall = (melonloader: boolean) => this.act(() => api.uninstall(melonloader));

  chooseGameFolder = () =>
    this.act(async () => {
      const picked = await open({ directory: true, title: "Choose the folder Schedule I is installed in" });
      if (typeof picked === "string") await api.setGamePath(picked);
    });

  dismissError = () => {
    this.error = null;
  };

  dismissNotice = () => {
    this.notice = null;
  };

  showMods = () => {
    this.modsOpen = true;
  };

  hideMods = () => {
    this.modsOpen = false;
  };

  /** Friends never update the launcher by hand: a newer signed build on GitHub installs itself on start. */
  private async updateLauncher() {
    let version: string | null;
    try {
      version = await api.checkUpdate();
    } catch (e) {
      this.error = `Couldn't check for a Hyland Link update: ${message(e)}`;
      return;
    }
    // Never swap the launcher out from under a mod install; the next start picks it up.
    if (!version || this.installing) return;
    this.selfUpdate = { version, percent: null };
    try {
      await api.installUpdate();
    } catch (e) {
      this.selfUpdate = null;
      this.launcherUpdate = version;
      this.error = `Hyland Link ${version} didn't install by itself: ${message(e)}`;
    }
  }

  async start() {
    await listen<InstallProgress>("install-progress", (event) => {
      this.progress = event.payload;
    });
    await listen<number>("update-progress", (event) => {
      if (this.selfUpdate) this.selfUpdate.percent = event.payload;
    });
    await this.load();
    void this.checkServer();
    void this.updateLauncher();
    // Pick up the online version file without holding up the first paint, then keep following it:
    // whatever the server runs, every open launcher matches within minutes.
    void this.syncManifest();
    setInterval(() => void this.syncManifest(), MANIFEST_POLL_MS);

    setInterval(() => void this.checkServer(), SERVER_POLL_MS);
    setInterval(() => {
      if (!this.installing) void this.load();
    }, GAME_POLL_MS);
    await getCurrentWindow().onFocusChanged(({ payload: focused }) => {
      if (!focused || this.installing) return;
      void this.load();
      void this.syncManifest(MANIFEST_FRESH_MS);
    });
  }
}

export const launcher = new Launcher();
