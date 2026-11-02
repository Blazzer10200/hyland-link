<script lang="ts">
  import { getCurrentWindow } from "@tauri-apps/api/window";

  let { settingsOpen, onSettings }: { settingsOpen: boolean; onSettings: () => void } = $props();

  const appWindow = getCurrentWindow();
</script>

<header class="bar" data-tauri-drag-region>
  <div class="brand" data-tauri-drag-region>
    <span class="lamp" aria-hidden="true"></span>
    <span class="wordmark" data-tauri-drag-region>Hyland Link</span>
  </div>
  <div class="controls">
    <button type="button" class="gear" class:on={settingsOpen} aria-label="Settings" aria-expanded={settingsOpen} onclick={onSettings}>
      <svg viewBox="0 0 16 16" aria-hidden="true">
        <path
          d="M6.7 1.8h2.6l.4 1.8 1.2.7 1.8-.6 1.3 2.3-1.4 1.2v1.6l1.4 1.2-1.3 2.3-1.8-.6-1.2.7-.4 1.8H6.7l-.4-1.8-1.2-.7-1.8.6-1.3-2.3 1.4-1.2V7.2L2 6l1.3-2.3 1.8.6 1.2-.7z"
        />
        <circle cx="8" cy="8" r="2.1" />
      </svg>
    </button>
    <button type="button" aria-label="Minimize" onclick={() => appWindow.minimize()}>
      <svg viewBox="0 0 12 12" aria-hidden="true"><path d="M2 6.5h8" /></svg>
    </button>
    <button type="button" class="close" aria-label="Close" onclick={() => appWindow.close()}>
      <svg viewBox="0 0 12 12" aria-hidden="true"><path d="M2.5 2.5l7 7M9.5 2.5l-7 7" /></svg>
    </button>
  </div>
</header>

<style>
  .bar {
    height: 38px;
    flex: none;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-left: 16px;
  }
  .brand {
    display: flex;
    align-items: center;
    gap: 10px;
  }
  /* A streetlamp seen from below: hot core, soft halo. */
  .lamp {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--sodium-hot);
    box-shadow: 0 0 0 3px var(--sodium-glow), 0 0 12px 2px rgb(242 163 58 / 0.45);
  }
  .wordmark {
    font: 700 11px/1 var(--font-display);
    font-stretch: 125%;
    letter-spacing: 0.18em;
    text-transform: uppercase;
    color: var(--muted);
  }
  .controls {
    display: flex;
    height: 100%;
  }
  .controls button {
    width: 46px;
    height: 100%;
    border: 0;
    background: transparent;
    display: grid;
    place-items: center;
    color: var(--muted);
    transition: background 0.12s, color 0.12s;
  }
  .controls button:hover {
    background: var(--raised);
    color: var(--ink);
  }
  .controls .close:hover {
    background: #c42b1c;
    color: #fff;
  }
  .gear svg {
    width: 15px;
    height: 15px;
    stroke-width: 1.15;
    stroke-linejoin: round;
    transition: transform 0.5s cubic-bezier(0.3, 0.7, 0.3, 1);
  }
  .gear:hover svg {
    transform: rotate(45deg);
  }
  .controls .gear.on {
    color: var(--sodium);
  }
  .gear.on svg {
    transform: rotate(90deg);
  }
  svg {
    width: 12px;
    height: 12px;
    fill: none;
    stroke: currentColor;
    stroke-width: 1.2;
  }
</style>
