<script lang="ts">
  import { onMount, type Snippet } from "svelte";
  import { fade, fly } from "svelte/transition";

  let {
    id,
    title,
    subtitle,
    tall = false,
    onClose,
    actions,
    children,
    footer,
  }: {
    id: string;
    title: string;
    subtitle: string;
    /** Takes the full height instead of fitting its content (for a list that keeps growing). */
    tall?: boolean;
    onClose: () => void;
    actions?: Snippet;
    children: Snippet;
    footer?: Snippet;
  } = $props();

  // Keyboard users land in the sheet, and go back to whatever opened it when it closes.
  let closeButton = $state<HTMLButtonElement>();
  let sheet = $state<HTMLDivElement>();
  onMount(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    closeButton?.focus({ preventScroll: true });
    // Only when focus is still in this sheet (or lost): a sheet opened in its place keeps its own focus.
    return () => {
      const active = document.activeElement;
      const lost = !active || active === document.body || !!sheet?.contains(active);
      if (lost && opener?.isConnected) opener.focus({ preventScroll: true });
    };
  });
</script>

<svelte:window
  onkeydown={(e) => {
    if (e.key === "Escape") onClose();
  }}
/>

<div class="scrim" role="presentation" onclick={onClose} transition:fade={{ duration: 200 }}></div>
<!-- Not aria-modal: the action bar stays usable while a sheet is open (Update can run with the mod list showing). -->
<div class="sheet" class:tall role="dialog" aria-labelledby={id} bind:this={sheet} transition:fly={{ y: 40, duration: 260 }}>
  <header>
    <div class="titles">
      <h2 {id}>{title}</h2>
      <p>{subtitle}</p>
    </div>
    {@render actions?.()}
    <button type="button" class="close" aria-label="Close" bind:this={closeButton} onclick={onClose}>
      <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 4l8 8M12 4l-8 8" /></svg>
    </button>
  </header>

  {@render children()}

  {#if footer}
    <footer>{@render footer()}</footer>
  {/if}
</div>

<style>
  /* Lives inside <main>, so the title bar and the action bar stay usable: Update can run with a sheet open. */
  .scrim {
    position: absolute;
    inset: 0;
    z-index: 15;
    background: rgb(8 10 12 / 0.62);
  }
  .sheet {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    z-index: 16;
    max-height: calc(100% - 28px);
    display: flex;
    flex-direction: column;
    border: 1px solid var(--line);
    border-bottom: 0;
    border-radius: 16px 16px 0 0;
    background: var(--panel);
    box-shadow: 0 -18px 50px -20px rgb(0 0 0 / 0.85);
  }
  .sheet.tall {
    height: calc(100% - 28px);
  }
  header {
    display: flex;
    align-items: flex-start;
    gap: 10px;
    padding: 18px 20px 12px;
  }
  .titles {
    flex: 1;
    min-width: 0;
  }
  h2 {
    margin: 0 0 3px;
    font: 700 17px/1.25 var(--font-display);
    font-stretch: 108%;
  }
  header p {
    margin: 0;
    font-size: 12px;
    line-height: 1.45;
    color: var(--muted);
  }
  .close {
    flex: none;
    width: 28px;
    height: 28px;
    display: grid;
    place-items: center;
    border: 0;
    border-radius: 7px;
    background: transparent;
    color: var(--muted);
  }
  .close:hover {
    background: var(--raised);
    color: var(--ink);
  }
  .close svg {
    width: 14px;
    height: 14px;
    fill: none;
    stroke: currentColor;
    stroke-width: 1.8;
    stroke-linecap: round;
  }
  footer {
    display: flex;
    flex-direction: column;
    gap: 3px;
    padding: 12px 20px 18px;
    border-top: 1px solid var(--line);
    font-size: 11.5px;
    color: var(--faint);
  }
</style>
