<script lang="ts">
  import { fade, fly } from "svelte/transition";
  import { stageLabels, type GameStage } from "$lib/game-log";

  let {
    stage,
    message,
    errors,
    onShowLog,
  }: { stage: GameStage; message: string | null; errors: number; onShowLog: () => void } = $props();

  const tone = $derived(stage === "joined" ? "ok" : stage === "closing" ? "quiet" : "busy");
</script>

<!-- The game's state, live from its log: where it is now, and the newest line ticking underneath. -->
<div class="status {tone}">
  <div class="row">
    <span class="beacon" aria-hidden="true"></span>
    <span class="stack" aria-live="polite">
      {#key stage}
        <span class="stage" in:fly={{ y: 8, duration: 280, delay: 60 }} out:fade={{ duration: 120 }}>{stageLabels[stage]}</span>
      {/key}
    </span>
    <button type="button" class="log-link" onclick={onShowLog}>
      Game log
      {#if errors}<span class="badge mono" in:fly={{ y: -4, duration: 200 }}>{errors}</span>{/if}
    </button>
  </div>
  <span class="stack ticker mono" title={message ?? ""}>
    {#key message}
      <span in:fly={{ y: 6, duration: 220 }} out:fade={{ duration: 100 }}>{message ?? "Waiting for the game to write its log…"}</span>
    {/key}
  </span>
</div>

<style>
  .status {
    display: flex;
    flex-direction: column;
    gap: 5px;
    padding: 10px 12px;
    border: 1px solid var(--line);
    border-radius: var(--radius);
    background: var(--panel);
    transition: border-color 0.4s;
  }
  .status.ok {
    border-color: rgb(94 196 139 / 0.4);
  }
  .row {
    display: flex;
    align-items: center;
    gap: 10px;
  }
  /* Old and new text share one cell, so a change crossfades in place instead of shifting the layout. */
  .stack {
    flex: 1;
    min-width: 0;
    display: grid;
  }
  .stack > span {
    grid-area: 1 / 1;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .stage {
    font: 650 13.5px/1.3 var(--font-body);
    color: var(--ink);
  }

  .beacon {
    position: relative;
    flex: none;
    width: 9px;
    height: 9px;
    border-radius: 50%;
    background: var(--sodium);
    transition: background 0.4s;
  }
  .beacon::after {
    content: "";
    position: absolute;
    inset: -4px;
    border-radius: 50%;
    border: 1.5px solid currentColor;
    color: var(--sodium);
    animation: ping 1.6s ease-out infinite;
  }
  .ok .beacon {
    background: var(--ok);
    box-shadow: 0 0 10px 1px rgb(94 196 139 / 0.55);
  }
  .ok .beacon::after {
    color: var(--ok);
    animation-duration: 2.8s;
  }
  .quiet .beacon {
    background: var(--faint);
  }
  .quiet .beacon::after {
    animation: none;
    opacity: 0;
  }
  @keyframes ping {
    0% { transform: scale(0.6); opacity: 0.8; }
    80%, 100% { transform: scale(1.7); opacity: 0; }
  }

  .log-link {
    flex: none;
    display: inline-flex;
    align-items: center;
    gap: 6px;
    border: 0;
    background: transparent;
    padding: 2px 0;
    color: var(--sodium);
    font-size: 11.5px;
    font-weight: 650;
  }
  .log-link:hover {
    color: var(--sodium-hot);
    text-decoration: underline;
  }
  .badge {
    min-width: 17px;
    padding: 1px 5px;
    border-radius: 999px;
    background: var(--bad-wash);
    border: 1px solid rgb(239 109 106 / 0.5);
    color: var(--bad);
    font-size: 10px;
    text-align: center;
  }
  .ticker {
    padding-left: 19px;
    font-size: 10.75px;
    color: var(--faint);
  }
</style>
