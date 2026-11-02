import { listen } from "@tauri-apps/api/event";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { open } from "@tauri-apps/plugin-dialog";
import { error as logError, info as logInfo, warn as logWarn } from "@tauri-apps/plugin-log";
import { api, type InstallProgress, type ServerStatus, type Snapshot } from "./api";
import { latestStage, parseLine, type GameStage, type LogLine } from "./game-log";

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
  | "launching"
  | "playing";

/** The guided steps: 1 find the game, 2 install, 3 first launch, 4 join. */
export type StepNumber = 1 | 2 | 3 | 4;

const SERVER_POLL_MS = 15_000;
const GAME_POLL_MS = 4_000;
/** How often the server's mod list is re-read from GitHub while the launcher is open. */
const MANIFEST_POLL_MS = 5 * 60_000;
/** A focus or Play reuses a mod-list check younger than this instead of asking GitHub again. */
const MANIFEST_FRESH_MS = 60_000;
/** The game log is followed this often while the game runs or the log sheet is open. */
const LOG_POLL_MS = 800;
/** The log sheet keeps this many of the newest lines. */
const MAX_LOG_LINES = 600;
/** How long "Leave & close" waits for the game to go before saying it's still open. */
const CLOSE_WAIT_MS = 30_000;
/** How long Play stays locked waiting for Steam to start the game. */
const LAUNCH_WAIT_MS = 45_000;
/** How long the self-update may go without progress before the manual download is offered. */
const SELF_UPDATE_STALL_MS = 120_000;

function message(error: unknown): string {
  return typeof error === "string" ? error : error instanceof Error ? error.message : String(error);
}

/** Writes to the launcher log (launcher.log); never lets a logging hiccup break the UI. */
function record(write: (m: string) => Promise<void>, text: string) {
  write(text).catch((e: unknown) => console.error("launcher log:", e));
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
    // Favorites isn't required: Play puts the server back if the player removed it in game.
    if (this.installing || !(melonloader.upToDate && serverMod.upToDate && modsCurrent)) return 2;
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
        return running ? "firstLaunchRunning" : this.launching ? "launching" : "firstLaunch";
      default:
        return running ? "playing" : this.launching ? "launching" : "ready";
    }
  });

  /** Set while the launcher replaces itself; the window shows the update screen until the installer closes it. */
  selfUpdate = $state<{ version: string; percent: number | null } | null>(null);
  /** A launcher version that failed to auto-install, offered as a manual download instead. */
  launcherUpdate = $state<string | null>(null);
  /** The one bottom sheet that's open, if any. */
  sheet = $state<"mods" | "log" | "settings" | null>(null);
  /** Set when "Leave & close" asked the game to quit, until it's gone. */
  closingGame = $state(false);
  private closeTimer: ReturnType<typeof setTimeout> | undefined;
  /** Set from a Play click until the game shows up (or a timeout), so a double click can't launch twice. */
  launching = $state(false);
  private launchTimer: ReturnType<typeof setTimeout> | undefined;
  /** Says why a ready player is suddenly back at "Update". */
  notice = $state<string | null>(null);
  private manifestCheckedAt = 0;
  /** The last error a state poll showed, so a failure that repeats every poll isn't shown (or logged) again. */
  private loadError: string | null = null;
  /** When the self-update last reported progress; a download that stalls gives the player the manual link. */
  private selfUpdateProgressAt = 0;

  /** The newest lines of MelonLoader's Latest.log (this run, or the last one once the game closed). */
  gameLog = $state<LogLine[]>([]);
  /** Errors in the whole run, not just the lines still kept. */
  logErrors = $state(0);
  private logStage = $state<GameStage | null>(null);
  private logId = 0;
  /** Why the game log can't be read, shown in the sheet instead of a toast every poll. */
  logProblem = $state<string | null>(null);
  private logRun = $state<string | null>(null);
  private logOffset = 0;
  private logBusy = false;
  /** The log that was on disk when Play was pressed: until a new one replaces it, the game hasn't started writing. */
  private launchedOver = $state<string | null | undefined>(undefined);

  /** Where the running game is, read off its log. Null while the game isn't running. */
  stage = $derived.by<GameStage | null>(() => {
    if (!this.snapshot?.game.running) return null;
    if (this.launchedOver !== undefined && this.logRun === this.launchedOver) return "starting";
    return this.logStage ?? "starting";
  });
  private lastStage: GameStage | null = null;

  /** Shows an error in the banner and writes it to the launcher log. */
  fail(error: unknown) {
    this.error = message(error);
    record(logError, `Shown to the player: ${this.error}`);
  }

  async load() {
    try {
      this.snapshot = await api.getState();
      this.loadError = null;
    } catch (e) {
      if (this.loadError !== message(e)) this.fail(e);
      this.loadError = message(e);
    }
    const running = !!this.snapshot?.game.running;
    if (this.closingGame && !running) {
      this.closingGame = false;
      clearTimeout(this.closeTimer);
    }
    if (this.launching && running) {
      this.launching = false;
      clearTimeout(this.launchTimer);
    }
  }

  async checkServer() {
    const wasOnline = this.server?.online;
    this.checkingServer = true;
    try {
      this.server = await api.queryServer();
    } catch (e) {
      this.server = { online: false, name: null, players: null, maxPlayers: null, pingMs: null, error: message(e) };
    } finally {
      this.checkingServer = false;
    }
    if (this.server.online !== wasOnline) {
      record(logInfo, this.server.online ? `Server online (${this.server.pingMs} ms)` : `Server offline: ${this.server.error ?? "no answer"}`);
    }
  }

  /** Follows the game log: the new lines since the last read, or a clean start when the game relaunched. */
  private async pollLog() {
    if (this.logBusy) return;
    this.logBusy = true;
    try {
      const chunk = await api.readGameLog(this.logRun, this.logOffset);
      if (chunk.fresh) {
        this.gameLog = [];
        this.logErrors = 0;
        this.logStage = null;
      }
      this.logRun = chunk.run;
      this.logOffset = chunk.offset;
      if (chunk.lines.length) {
        const lines = chunk.lines.map((raw) => parseLine(raw, this.logId++));
        this.gameLog = [...this.gameLog, ...lines].slice(-MAX_LOG_LINES);
        this.logErrors += lines.filter((l) => l.level === "error").length;
        this.logStage = latestStage(lines) ?? this.logStage;
      }
      this.logProblem = null;
    } catch (e) {
      if (this.logProblem !== message(e)) record(logWarn, `Game log: ${message(e)}`);
      this.logProblem = message(e);
    } finally {
      this.logBusy = false;
    }
    if (this.stage !== this.lastStage) {
      if (this.stage) record(logInfo, `Game: ${this.stage}`);
      this.lastStage = this.stage;
    }
  }

  /** Runs a user action, surfacing its error in the banner and refreshing state afterwards. */
  private async act(action: () => Promise<void>) {
    this.error = null;
    try {
      await action();
    } catch (e) {
      this.fail(e);
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
        this.notice = null;
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
      this.fail(e);
      return;
    }
    if (wasReady && this.step === 2 && this.snapshot?.manifest.revision !== revision) {
      this.notice = "The server's mods just changed. Update to keep playing.";
      record(logInfo, `Mods changed (revision ${revision} → ${this.snapshot?.manifest.revision}): asked the player to update`);
    }
  }

  play = () => {
    // Locked from the first click, so a double click can't start the game twice.
    if (this.launching) return Promise.resolve();
    this.launching = true;
    let started = false;
    return this.act(async () => {
      try {
        // Never launch on mods the server just replaced: the button turns into Update instead.
        await this.syncManifest(MANIFEST_FRESH_MS);
        if (this.step < 3) {
          this.notice ??= "The server's mods just changed. Update first, then play.";
          return;
        }
        this.launchedOver = this.logRun;
        await api.play();
        started = true;
        // Steam can take a while to start the game; after this long the button comes back so Play can be retried.
        clearTimeout(this.launchTimer);
        this.launchTimer = setTimeout(() => (this.launching = false), LAUNCH_WAIT_MS);
      } finally {
        if (!started) this.launching = false;
      }
    });
  };
  /** Leave & close: the game shuts down the normal way. If it's still open after a while, say what to do. */
  quitGame = () =>
    this.act(async () => {
      await api.quitGame();
      this.closingGame = true;
      clearTimeout(this.closeTimer);
      this.closeTimer = setTimeout(() => {
        this.closingGame = false;
        this.notice = "Schedule I is still open. If it's asking you something, answer it, or quit from its menu.";
      }, CLOSE_WAIT_MS);
    });
  openGameFolder = () => this.act(() => api.openGameFolder());
  openLauncherDownload = () => this.act(() => api.openLauncherDownload());
  openLauncherLogs = () => this.act(() => api.openLauncherLogs());
  uninstall = (melonloader: boolean) => this.act(() => api.uninstall(melonloader));
  uninstallLauncher = () => this.act(() => api.uninstallLauncher());

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
    this.sheet = "mods";
  };

  showLog = () => {
    this.sheet = "log";
    void this.pollLog();
  };

  /** The gear: opens settings, or closes whatever sheet is open. */
  toggleSettings = () => {
    this.sheet = this.sheet === "settings" ? null : "settings";
  };

  closeSheet = () => {
    this.sheet = null;
  };

  /** Friends never update the launcher by hand: a newer signed build on GitHub installs itself on start. */
  private async updateLauncher() {
    let version: string | null;
    try {
      version = await api.checkUpdate();
    } catch (e) {
      // Usually just offline (the server card says so). Logged for Blazzer, not shown on every start.
      record(logWarn, `Couldn't check for a Hyland Link update: ${message(e)}`);
      return;
    }
    // Never swap the launcher out from under a mod install; the next start picks it up.
    if (!version || this.installing) return;
    this.selfUpdate = { version, percent: null };
    this.selfUpdateProgressAt = Date.now();
    const giveUp = (reason: string) => {
      this.selfUpdate = null;
      this.launcherUpdate = version;
      this.fail(`Hyland Link ${version} didn't install by itself: ${reason}`);
    };
    // A download with no progress for this long hands the player the manual download instead of a stuck screen.
    const stall = setInterval(() => {
      if (this.selfUpdate && Date.now() - this.selfUpdateProgressAt > SELF_UPDATE_STALL_MS) {
        clearInterval(stall);
        giveUp("the download stopped.");
      }
    }, 5_000);
    try {
      await api.installUpdate();
    } catch (e) {
      if (this.selfUpdate) giveUp(message(e));
    } finally {
      clearInterval(stall);
    }
  }

  async start() {
    await listen<InstallProgress>("install-progress", (event) => {
      this.progress = event.payload;
    });
    await listen<number>("update-progress", (event) => {
      if (!this.selfUpdate) return;
      if (this.selfUpdate.percent !== event.payload) this.selfUpdateProgressAt = Date.now();
      this.selfUpdate.percent = event.payload;
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
    // One read up front so the sheet can show the last run, then follow it only while there's something to watch.
    void this.pollLog();
    setInterval(() => {
      if (this.snapshot?.game.running || this.sheet === "log") void this.pollLog();
    }, LOG_POLL_MS);
    await getCurrentWindow().onFocusChanged(({ payload: focused }) => {
      if (!focused || this.installing) return;
      void this.load();
      void this.syncManifest(MANIFEST_FRESH_MS);
    });
  }
}

export const launcher = new Launcher();
