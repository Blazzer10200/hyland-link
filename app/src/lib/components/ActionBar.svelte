<script lang="ts">
  import { fly } from "svelte/transition";
  import type { InstallProgress, Snapshot } from "$lib/api";
  import type { Phase } from "$lib/launcher.svelte";

  let {
    phase,
    snapshot,
    progress,
    usingBuiltIn,
    onInstall,
    onPlay,
    onChooseFolder,
    onOpenFolder,
    onShowMods,
    onUninstall,
  }: {
    phase: Phase;
    snapshot: Snapshot | null;
    progress: InstallProgress | null;
    usingBuiltIn: string | null;
    onInstall: () => void;
    onPlay: () => void;
    onChooseFolder: () => void;
    onOpenFolder: () => void;
    onShowMods: () => void;
    onUninstall: (melonloader: boolean) => void;
  } = $props();

  const hasExtras = $derived(!!snapshot?.install && snapshot.install.mods.length + snapshot.install.staleMods.length > 0);

  let menuOpen = $state(false);
  let confirming = $state<"mod" | "all" | null>(null);

  const percent = $derived(progress && progress.total > 0 ? Math.round((progress.done / progress.total) * 100) : null);

  type Button = { label: string; disabled: boolean; run: () => void; kind: "go" | "play" | "wait" | "work" };
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
      case "closeGame":
        return { label: "Close Schedule I to install", disabled: true, run: idle, kind: "wait" };
      case "firstLaunch":
        return { label: "Launch Schedule I", disabled: false, run: onPlay, kind: "go" };
      case "firstLaunchRunning":
        return { label: "Setting up… leave the game open", disabled: true, run: idle, kind: "work" };
      case "ready":
        return { label: "Play", disabled: false, run: onPlay, kind: "play" };
      case "playing":
        return { label: "In game", disabled: true, run: idle, kind: "wait" };
      default:
        return { label: "Looking for Schedule I…", disabled: true, run: idle, kind: "wait" };
    }
  });

  function choose(action: () => void) {
    menuOpen = false;
    confirming = null;
    action();
  }

  function uninstall(kind: "mod" | "all") {
    if (confirming !== kind) {
      confirming = kind;
      return;
    }
    choose(() => onUninstall(kind === "all"));
  }
</script>

<svelte:window
  onkeydown={(e) => {
    if (e.key === "Escape") menuOpen = false;
  }}
/>

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
    {#key button.label === "Play" ? "play" : phase}
      <span class="label" in:fly={{ y: 8, duration: 240 }}>
        {#if button.kind === "play"}
          <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M5 3.5v9l7.5-4.5z" /></svg>
        {/if}
        {button.label}
      </span>
    {/key}
  </button>

  <div class="foot">
    <span class="versions mono">
      {#if snapshot}
        v{snapshot.launcherVersion} · ML {snapshot.manifest.melonloader.version} · mod {snapshot.manifest.mod.version}
      {/if}
      {#if usingBuiltIn}
        <span class="offline" title="Couldn't check online for newer mod versions ({usingBuiltIn}). Using the versions built into this launcher."
          >· built-in list</span
        >
      {/if}
    </span>
    <div class="more">
      <button
        type="button"
        class="more-toggle"
        aria-expanded={menuOpen}
        onclick={() => {
          menuOpen = !menuOpen;
          confirming = null;
        }}>More</button
      >
      {#if menuOpen}
        <div class="menu" role="menu" transition:fly={{ y: 8, duration: 180 }}>
          <button type="button" role="menuitem" disabled={!snapshot?.game.found} onclick={() => choose(onOpenFolder)}
            >Open game folder</button
          >
          <button type="button" role="menuitem" onclick={() => choose(onChooseFolder)}>Choose game folder…</button>
          {#if hasExtras}
            <button type="button" role="menuitem" onclick={() => choose(onShowMods)}>Server's mods</button>
          {/if}
          <hr />
          <button
            type="button"
            role="menuitem"
            class="danger"
            disabled={!snapshot?.install?.serverMod.installed || snapshot?.game.running}
            onclick={() => uninstall("mod")}
            >{confirming === "mod"
              ? `Click again to remove the server mod${hasExtras ? "s" : ""}`
              : `Remove server mod${hasExtras ? "s" : ""}`}</button
          >
          <button
            type="button"
            role="menuitem"
            class="danger"
            disabled={!snapshot?.install?.melonloader.installed || snapshot?.game.running}
            onclick={() => uninstall("all")}
            >{confirming === "all" ? "Click again to remove both" : `Remove server mod${hasExtras ? "s" : ""} + MelonLoader`}</button
          >
        </div>
      {/if}
    </div>
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

  .foot {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }
  .versions {
    font-size: 10.5px;
    color: var(--faint);
  }
  .offline {
    color: var(--warn);
    opacity: 0.8;
    cursor: help;
  }
  .more {
    position: relative;
  }
  .more-toggle {
    border: 0;
    background: transparent;
    color: var(--muted);
    font-size: 12px;
    font-weight: 600;
    padding: 4px 8px;
    border-radius: 6px;
    transition: background 0.15s, color 0.15s;
  }
  .more-toggle:hover,
  .more-toggle[aria-expanded="true"] {
    color: var(--ink);
    background: var(--raised);
  }
  .menu {
    position: absolute;
    right: 0;
    bottom: calc(100% + 8px);
    width: 270px;
    padding: 6px;
    border: 1px solid var(--line);
    border-radius: 10px;
    background: var(--raised);
    box-shadow: 0 14px 40px -12px rgb(0 0 0 / 0.75);
    display: flex;
    flex-direction: column;
    z-index: 20;
  }
  .menu button {
    text-align: left;
    border: 0;
    background: transparent;
    padding: 8px 10px;
    border-radius: 6px;
    font-size: 13px;
  }
  .menu button:hover:not(:disabled) {
    background: var(--line);
  }
  .menu button:disabled {
    color: var(--faint);
    cursor: default;
  }
  .menu .danger:not(:disabled) {
    color: var(--bad);
  }
  hr {
    border: 0;
    border-top: 1px solid var(--line);
    margin: 4px 2px;
  }
</style>
