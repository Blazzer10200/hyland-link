<script lang="ts">
  import { fade, fly } from "svelte/transition";
  import type { LogLine } from "$lib/game-log";
  import Sheet from "./Sheet.svelte";

  let {
    lines,
    running,
    errors,
    problem,
    onClose,
  }: { lines: LogLine[]; running: boolean; errors: number; problem: string | null; onClose: () => void } = $props();

  let box = $state<HTMLDivElement>();
  /** Follow new lines unless the player scrolled up to read something. */
  let stick = $state(true);
  let copyState = $state<"idle" | "copied" | "failed">("idle");

  $effect(() => {
    if (box && stick && lines.length) box.scrollTop = box.scrollHeight;
  });

  function onScroll() {
    if (box) stick = box.scrollHeight - box.scrollTop - box.clientHeight < 32;
  }

  async function copy() {
    try {
      await navigator.clipboard.writeText(lines.map((l) => l.raw).join("\n"));
      copyState = "copied";
    } catch (e) {
      console.error("copy game log:", e);
      copyState = "failed";
    }
    setTimeout(() => (copyState = "idle"), 1600);
  }
</script>

<Sheet
  id="log-title"
  title="Game log"
  subtitle={running ? "Live from MelonLoader while the game runs." : "From the last time the game ran."}
  tall
  {onClose}
>
  {#snippet actions()}
    {#if running}
      <span class="live" transition:fade={{ duration: 200 }}><i></i>Live</span>
    {/if}
    <button type="button" class="copy" disabled={lines.length === 0} onclick={copy}>
      {copyState === "copied" ? "Copied" : copyState === "failed" ? "Couldn't copy" : "Copy"}
    </button>
  {/snippet}

  <div class="frame">
    <div class="log mono selectable" bind:this={box} onscroll={onScroll}>
      {#if lines.length === 0}
        <p class="empty">No game log yet. It shows up here as soon as Schedule I starts.</p>
      {:else}
        {#each lines as line (line.id)}
          {#if line.rule}
            <div class="rule" in:fade={{ duration: 200 }}></div>
          {:else}
            <div class="line {line.level}" in:fly={{ y: 4, duration: 200 }}>
              <span class="t">{line.time ?? ""}</span>
              <span class="msg"
                >{#if line.source}<span class="src" class:dim={line.source.startsWith("Il2Cpp")}>{line.source}</span>
                {/if}{line.text}</span
              >
            </div>
          {/if}
        {/each}
      {/if}
    </div>
    {#if !stick}
      <button type="button" class="jump" transition:fly={{ y: 8, duration: 180 }} onclick={() => (stick = true)}>
        Jump to latest ↓
      </button>
    {/if}
  </div>

  {#snippet footer()}
    {#if problem}
      <span class="problem">{problem}</span>
    {/if}
    <span>
      {lines.length} line{lines.length === 1 ? "" : "s"}{#if errors}{" · "}<span class="errors"
          >{errors} error{errors === 1 ? "" : "s"} this run</span
        >{/if}
      · MelonLoader/Latest.log
    </span>
  {/snippet}
</Sheet>

<style>
  .live {
    flex: none;
    align-self: center;
    display: inline-flex;
    align-items: center;
    gap: 6px;
    font-size: 11px;
    font-weight: 650;
    color: var(--ok);
  }
  .live i {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: var(--ok);
    animation: blink 1.6s ease-in-out infinite;
  }
  @keyframes blink {
    50% { opacity: 0.35; }
  }
  .copy {
    flex: none;
    align-self: center;
    border: 1px solid var(--line);
    background: var(--raised);
    border-radius: 7px;
    padding: 4px 10px;
    font-size: 11.5px;
    font-weight: 650;
    color: var(--ink);
    transition: border-color 0.15s, color 0.15s;
  }
  .copy:hover:not(:disabled) {
    border-color: rgb(242 163 58 / 0.55);
    color: var(--sodium-hot);
  }
  .copy:disabled {
    color: var(--faint);
    cursor: default;
  }

  .frame {
    position: relative;
    flex: 1;
    min-height: 0;
    margin: 0 12px 12px;
    border: 1px solid var(--line);
    border-radius: var(--radius);
    background: var(--asphalt);
    overflow: hidden;
  }
  .log {
    height: 100%;
    overflow-y: auto;
    padding: 8px 10px 10px;
    font-size: 10.75px;
    line-height: 1.55;
  }
  .line {
    display: grid;
    grid-template-columns: 52px minmax(0, 1fr);
    gap: 8px;
    color: #c3cad2;
  }
  .t {
    color: var(--faint);
  }
  .msg {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
  }
  .src {
    color: var(--sodium);
    margin-right: 6px;
  }
  .src.dim {
    color: var(--muted);
  }
  .warn .msg,
  .warn .src {
    color: var(--warn);
  }
  .error {
    margin: 1px -10px;
    padding: 1px 10px;
    background: var(--bad-wash);
  }
  .error .msg,
  .error .src {
    color: var(--bad);
  }
  .rule {
    height: 1px;
    margin: 6px 0;
    background: linear-gradient(90deg, var(--line), transparent);
  }
  .empty {
    margin: 0;
    padding: 24px 8px;
    text-align: center;
    font-family: var(--font-body);
    font-size: 12px;
    color: var(--faint);
  }
  .jump {
    position: absolute;
    left: 50%;
    bottom: 10px;
    translate: -50% 0;
    border: 1px solid rgb(242 163 58 / 0.5);
    border-radius: 999px;
    padding: 5px 12px;
    background: var(--raised);
    box-shadow: 0 8px 20px -8px rgb(0 0 0 / 0.8);
    font-size: 11.5px;
    font-weight: 650;
    color: var(--sodium-hot);
  }
  .problem {
    color: var(--bad);
  }
  .errors {
    color: var(--bad);
  }
</style>
