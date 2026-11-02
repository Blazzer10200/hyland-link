<script lang="ts">
  import { fly } from "svelte/transition";
  import type { InstallProgress, Snapshot } from "$lib/api";
  import type { Phase } from "$lib/launcher.svelte";

  let {
    phase,
    snapshot,
    progress,
    usingBuiltIn,
    closingGame,
    onInstall,
    onPlay,
    onChooseFolder,
    onQuit,
  }: {
    phase: Phase;
    snapshot: Snapshot | null;
    progress: InstallProgress | null;
    usingBuiltIn: string | null;
    closingGame: boolean;
    onInstall: () => void;
    onPlay: () => void;
    onChooseFolder: () => void;
    onQuit: () => void;
  } = $props();

  const percent = $derived(progress && progress.total > 0 ? Math.round((progress.done / progress.total) * 100) : null);

  /** "Leave & close" takes two clicks, so a stray click never ends a session. */
  let quitArmed = $state(false);
  let quitTimer: ReturnType<typeof setTimeout> | undefined;
  function quit() {
    clearTimeout(quitTimer);
    if (!quitArmed) {
      quitArmed = true;
      quitTimer = setTimeout(() => (quitArmed = false), 4000);
      return;
    }
    quitArmed = false;
    onQuit();
  }

  type Button = { label: string; disabled: boolean; run: () => void; kind: "go" | "play" | "wait" | "work" | "leave" | "armed" };
  const idle = () => {};

  const button = $derived.by((): Button => {
    switch (phase) {
      case "noGame":
        return { label: "Find Schedule I", disabled: false, run: onChooseFolder, kind: "go" };
      case "branch":
        return { label: "Switch Steam to the normal version first", disabled: true, run: idle, kind: "wait" };
      case "setup":
        return { label: "Install", disabled: false, run: onInstall, kind: "go" };
      case "update":
        return { label: "Update", disabled: false, run: onInstall, kind: "go" };
      case "installing":
        return {
          label: progress ? `Installing ${progress.label}${percent !== null ? ` · ${percent}%` : "…"}` : "Getting ready…",
          disabled: true,
          run: idle,
          kind: "work",
        };
      case "removing":
        return { label: "Removing mods…", disabled: true, run: idle, kind: "work" };
      // Same two-click close as "playing": a player on the server when the mods change can leave from here.
      case "closeGame":
        return quitArmed
          ? { label: "Click again to close the game", disabled: false, run: quit, kind: "armed" }
          : { label: "Close Schedule I to update", disabled: closingGame, run: quit, kind: "leave" };
      case "firstLaunch":
        return { label: "Launch Schedule I", disabled: false, run: onPlay, kind: "go" };
      case "firstLaunchRunning":
        return { label: "Setting up… leave the game open", disabled: true, run: idle, kind: "work" };
      case "ready":
        return { label: "Play", disabled: false, run: onPlay, kind: "play" };
      case "launching":
        return { label: "Starting Schedule I…", disabled: true, run: idle, kind: "work" };
      case "playing":
        if (closingGame) return { label: "Closing Schedule I…", disabled: true, run: idle, kind: "work" };
        return quitArmed
          ? { label: "Click again to close the game", disabled: false, run: quit, kind: "armed" }
          : { label: "Leave server & close game", disabled: false, run: quit, kind: "leave" };
      default:
        return { label: "Looking for Schedule I…", disabled: true, run: idle, kind: "wait" };
    }
  });

</script>

<footer class="dock">
  <button
    type="button"
    class="primary {button.kind}"
    disabled={button.disabled}
    onclick={button.run}
  >
    {#if phase === "installing"}
      <span class="fill" style:width="{percent ?? 6}%"></span>
    {:else if button.kind === "work"}
      <span class="shimmer" aria-hidden="true"></span>
    {/if}
    {#key button.kind === "play" || button.kind === "leave" || button.kind === "armed" ? button.kind : phase}
      <span class="label" in:fly={{ y: 8, duration: 240 }}>
        {#if button.kind === "play"}
          <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M5 3.5v9l7.5-4.5z" /></svg>
        {:else if button.kind === "leave" || button.kind === "armed"}
          <svg class="stroke" viewBox="0 0 16 16" aria-hidden="true"><path d="M9.5 3H4v10h5.5M7 8h6.5m-2.5-2.5L13.5 8 11 10.5" /></svg>
        {/if}
        {button.label}
      </span>
    {/key}
  </button>

  <div class="foot mono">
    {#if snapshot}
      v{snapshot.launcherVersion} · ML {snapshot.manifest.melonloader.version} · mod {snapshot.manifest.mod.version}
    {/if}
    {#if usingBuiltIn}
      <span class="offline" title="Couldn't check online for newer mod versions ({usingBuiltIn}). Using the versions built into this launcher."
        >· built-in list</span
      >
    {/if}
  </div>
</footer>

<style>
  .dock {
    flex: none;
    padding: 14px 20px;
    border-top: 1px solid var(--line);
    background: var(--asphalt);
    display: flex;
    flex-direction: column;
    gap: 10px;
  }
  .primary {
    position: relative;
    overflow: hidden;
    height: 52px;
    border: 0;
    border-radius: 12px;
    background: var(--sodium);
    color: var(--sodium-ink);
    display: grid;
    place-items: center;
    transition: background 0.25s, color 0.25s, box-shadow 0.35s, transform 0.08s;
  }
  .label {
    position: relative;
    grid-area: 1 / 1;
    display: inline-flex;
    align-items: center;
    gap: 9px;
    font: 750 15px/1 var(--font-display);
    font-stretch: 112%;
    letter-spacing: 0.02em;
  }
  .primary:hover:not(:disabled) {
    background: var(--sodium-hot);
  }
  .primary:active:not(:disabled) {
    transform: translateY(1px);
  }
  .primary.play {
    box-shadow: 0 10px 30px -10px rgb(242 163 58 / 0.65);
  }
  .primary.play .label {
    font-size: 17px;
    letter-spacing: 0.1em;
    text-transform: uppercase;
  }
  /* A pass of light across the button when Play appears, and again on hover. */
  .primary.play::after {
    content: "";
    position: absolute;
    inset: 0;
    background: linear-gradient(100deg, transparent 30%, rgb(255 255 255 / 0.45) 50%, transparent 70%);
    transform: translateX(-120%);
    animation: sheen 1.1s 0.25s ease-out;
  }
  .primary.play:hover::after {
    animation: sheen 0.9s ease-out;
  }
  @keyframes sheen {
    to { transform: translateX(120%); }
  }
  .primary:disabled {
    background: var(--raised);
    color: var(--muted);
    cursor: default;
  }
  .primary:disabled .label {
    font: 650 13.5px/1 var(--font-body);
    letter-spacing: 0;
  }
  .primary.work {
    color: var(--ink);
  }
  .fill {
    position: absolute;
    inset: 0 auto 0 0;
    background: linear-gradient(90deg, rgb(242 163 58 / 0.16), rgb(242 163 58 / 0.38));
    transition: width 0.3s ease-out;
  }
  .shimmer {
    position: absolute;
    inset: 0;
    background: linear-gradient(100deg, transparent 20%, rgb(242 163 58 / 0.22) 50%, transparent 80%);
    animation: shimmer 1.8s ease-in-out infinite;
  }
  @keyframes shimmer {
    from { transform: translateX(-100%); }
    to { transform: translateX(100%); }
  }
  .primary svg {
    width: 15px;
    height: 15px;
    fill: currentColor;
  }
  .primary svg.stroke {
    fill: none;
    stroke: currentColor;
    stroke-width: 1.6;
    stroke-linecap: round;
    stroke-linejoin: round;
  }
  /* While playing: a calm outline button; armed, it turns red and waits for the second click. */
  .primary.leave {
    background: transparent;
    border: 1px solid var(--line);
    color: var(--muted);
  }
  .primary.leave:hover {
    background: var(--raised);
    border-color: rgb(239 109 106 / 0.5);
    color: var(--ink);
  }
  .primary.armed,
  .primary.armed:hover:not(:disabled) {
    background: var(--bad);
    color: var(--asphalt);
    animation: nudge 0.3s ease-out;
  }
  .primary.leave .label,
  .primary.armed .label {
    font: 650 13.5px/1 var(--font-body);
    letter-spacing: 0;
  }
  @keyframes nudge {
    30% { transform: translateX(-3px); }
    60% { transform: translateX(3px); }
  }

  .foot {
    font-size: 10.5px;
    color: var(--faint);
    text-align: center;
  }
  .offline {
    color: var(--warn);
    opacity: 0.8;
    cursor: help;
  }
</style>
