<script lang="ts">
  import { fly } from "svelte/transition";
  import type { Snapshot } from "$lib/api";
  import { shortPath } from "$lib/game-log";
  import Sheet from "./Sheet.svelte";

  let {
    snapshot,
    logLines,
    logErrors,
    onOpenFolder,
    onChooseFolder,
    onShowLog,
    onShowMods,
    onOpenLauncherLogs,
    onUninstall,
    onUninstallLauncher,
    onClose,
  }: {
    snapshot: Snapshot;
    logLines: number;
    logErrors: number;
    onOpenFolder: () => void;
    onChooseFolder: () => void;
    onShowLog: () => void;
    onShowMods: () => void;
    onOpenLauncherLogs: () => void;
    onUninstall: (melonloader: boolean) => void;
    onUninstallLauncher: () => void;
    onClose: () => void;
  } = $props();

  type Removal = "mods" | "all" | "launcher";

  const install = $derived(snapshot.install);
  const running = $derived(snapshot.game.running);
  const extras = $derived(install ? install.mods.length + install.staleMods.length : 0);
  const modsWord = $derived(extras ? "Server mods" : "Server mod");

  /** The removal waiting for its second click; it gives up after a few seconds. */
  let confirming = $state<Removal | null>(null);
  let confirmTimer: ReturnType<typeof setTimeout> | undefined;

  function remove(kind: Removal, run: () => void) {
    clearTimeout(confirmTimer);
    if (confirming !== kind) {
      confirming = kind;
      confirmTimer = setTimeout(() => (confirming = null), 4000);
      return;
    }
    confirming = null;
    run();
  }

  const removals = $derived([
    {
      kind: "mods" as const,
      title: modsWord,
      note: "Takes out what Hyland Link installed. Your own mods stay.",
      action: "Remove",
      disabled: running || !install?.serverMod.installed,
      run: () => onUninstall(false),
    },
    {
      kind: "all" as const,
      title: `${modsWord} + MelonLoader`,
      note: "Puts the game back the way Steam installed it.",
      action: "Remove",
      disabled: running || !install?.melonloader.installed,
      run: () => onUninstall(true),
    },
    {
      kind: "launcher" as const,
      title: "Uninstall Hyland Link",
      note: "Removes everything above, then asks before removing the launcher.",
      action: "Uninstall",
      disabled: running,
      run: onUninstallLauncher,
    },
  ]);
</script>

<Sheet id="settings-title" title="Settings" subtitle="Hyland Link {snapshot.launcherVersion}. It keeps itself up to date." {onClose}>
  <div class="scroll">
    <section in:fly={{ y: 8, duration: 260, delay: 80 }}>
      <h3>Game</h3>
      <div class="group">
        <div class="row">
          <div class="text">
            <span class="title">Game folder</span>
            <span class="note mono" title={snapshot.game.path ?? ""}
              >{snapshot.game.path ? shortPath(snapshot.game.path) : "Not found yet"}</span
            >
          </div>
          <div class="buttons">
            <button type="button" class="btn" disabled={!snapshot.game.found} onclick={onOpenFolder}>Open</button>
            <button type="button" class="btn" onclick={onChooseFolder}>Change…</button>
          </div>
        </div>
        <div class="row">
          <div class="text">
            <span class="title">Game log</span>
            <span class="note">
              {#if logLines === 0}Nothing yet{:else}{running ? "This run" : "Last run"} · {logLines} lines{#if logErrors}{" · "}<span
                    class="bad">{logErrors} error{logErrors === 1 ? "" : "s"}</span
                  >{/if}{/if}
            </span>
          </div>
          <button type="button" class="btn" onclick={onShowLog}>View</button>
        </div>
        {#if extras}
          <div class="row">
            <div class="text">
              <span class="title">Server's mods</span>
              <span class="note">{install?.mods.length} mod{install?.mods.length === 1 ? "" : "s"}, kept in sync with the server</span>
            </div>
            <button type="button" class="btn" onclick={onShowMods}>View</button>
          </div>
        {/if}
      </div>
    </section>

    <section in:fly={{ y: 8, duration: 260, delay: 140 }}>
      <h3>Help</h3>
      <div class="group">
        <div class="row">
          <div class="text">
            <span class="title">Launcher log</span>
            <span class="note">If something breaks, send this file to Blazzer.</span>
          </div>
          <button type="button" class="btn" onclick={onOpenLauncherLogs}>Open folder</button>
        </div>
      </div>
    </section>

    <section in:fly={{ y: 8, duration: 260, delay: 200 }}>
      <h3>Remove</h3>
      <div class="group danger">
        {#each removals as r (r.kind)}
          <div class="row">
            <div class="text">
              <span class="title">{r.title}</span>
              <span class="note">{r.note}</span>
            </div>
            <button
              type="button"
              class="btn remove"
              class:armed={confirming === r.kind}
              disabled={r.disabled}
              onclick={() => remove(r.kind, r.run)}
            >
              {confirming === r.kind ? "Click again" : r.action}
            </button>
          </div>
        {/each}
      </div>
      {#if running}
        <p class="hint" transition:fly={{ y: 4, duration: 200 }}>Close Schedule I to remove anything.</p>
      {/if}
    </section>
  </div>
</Sheet>

<style>
  .scroll {
    min-height: 0;
    overflow-y: auto;
    padding: 0 20px 18px;
    display: flex;
    flex-direction: column;
    gap: 14px;
  }
  h3 {
    margin: 0 0 7px 2px;
    font: 700 10.5px/1 var(--font-display);
    font-stretch: 118%;
    letter-spacing: 0.16em;
    text-transform: uppercase;
    color: var(--faint);
  }
  .group {
    border: 1px solid var(--line);
    border-radius: var(--radius);
    background: var(--asphalt);
  }
  .group.danger {
    border-color: rgb(239 109 106 / 0.28);
  }
  .row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 9px 12px;
  }
  .row + .row {
    border-top: 1px solid var(--line);
  }
  .danger .row + .row {
    border-top-color: rgb(239 109 106 / 0.16);
  }
  .text {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 1px;
  }
  .title {
    font-size: 13px;
    font-weight: 650;
    color: var(--ink);
  }
  .note {
    font-size: 11.5px;
    color: var(--muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .note.mono {
    font-size: 11px;
  }
  .bad {
    color: var(--bad);
  }
  .buttons {
    display: flex;
    gap: 6px;
  }
  .btn {
    flex: none;
    min-width: 58px;
    border: 1px solid var(--line);
    border-radius: 7px;
    background: var(--raised);
    padding: 5px 10px;
    font-size: 11.5px;
    font-weight: 650;
    color: var(--ink);
    transition: border-color 0.15s, color 0.15s, background 0.15s;
  }
  .btn:hover:not(:disabled) {
    border-color: rgb(242 163 58 / 0.55);
    color: var(--sodium-hot);
  }
  .btn:disabled {
    color: var(--faint);
    cursor: default;
  }
  .remove:not(:disabled) {
    color: var(--bad);
  }
  .remove:hover:not(:disabled) {
    border-color: rgb(239 109 106 / 0.6);
    color: var(--bad);
  }
  .remove.armed,
  .remove.armed:hover:not(:disabled) {
    border-color: var(--bad);
    background: var(--bad);
    color: var(--asphalt);
  }
  .remove.armed {
    animation: nudge 0.3s ease-out;
  }
  @keyframes nudge {
    30% { transform: translateX(-2px); }
    60% { transform: translateX(2px); }
  }
  .hint {
    margin: 8px 2px 0;
    font-size: 11.5px;
    color: var(--warn);
  }
</style>
