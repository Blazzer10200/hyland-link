<script lang="ts">
  import { fade, fly } from "svelte/transition";
  import type { InstallProgress, InstallState } from "$lib/api";

  let {
    install,
    installing,
    progress,
    onClose,
  }: { install: InstallState; installing: boolean; progress: InstallProgress | null; onClose: () => void } = $props();

  type Row = {
    key: string;
    name: string;
    version: string;
    blurb: string;
    status: "ok" | "update" | "new" | "busy" | "remove";
    label: string;
  };

  const rows = $derived.by((): Row[] => {
    const busy = installing && progress?.step === "mods" ? progress.label : null;
    const current = install.mods.map((m): Row => {
      const version = m.installed && !m.upToDate && m.installedVersion ? `${m.installedVersion} → ${m.version}` : m.version;
      const row = { key: m.id, name: m.name, version, blurb: m.blurb };
      if (m.upToDate) return { ...row, status: "ok", label: "Installed" };
      if (busy === `${m.name} ${m.version}`) return { ...row, status: "busy", label: "Installing" };
      return m.installed ? { ...row, status: "update", label: "Update" } : { ...row, status: "new", label: "New" };
    });
    const dropped = install.staleMods.map(
      (m): Row => ({
        key: `stale-${m.id}`,
        name: m.name,
        version: m.version,
        blurb: "The server stopped using this one",
        status: "remove",
        label: installing ? "Removing" : "To remove",
      }),
    );
    return [...current, ...dropped];
  });

  const pending = $derived(rows.some((r) => r.status !== "ok"));
</script>

<svelte:window
  onkeydown={(e) => {
    if (e.key === "Escape") onClose();
  }}
/>

<div class="scrim" role="presentation" onclick={onClose} transition:fade={{ duration: 200 }}></div>
<div class="sheet" role="dialog" aria-modal="true" aria-labelledby="mods-title" transition:fly={{ y: 40, duration: 260 }}>
  <header>
    <div>
      <h2 id="mods-title">Mods on this server</h2>
      <p>Hyland Link keeps these matched to the server. You never pick or update them by hand.</p>
    </div>
    <button type="button" class="close" aria-label="Close" onclick={onClose}>
      <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 4l8 8M12 4l-8 8" /></svg>
    </button>
  </header>

  <ul>
    {#each rows as row (row.key)}
      <li class={row.status}>
        <div class="text">
          <span class="name">{row.name} <span class="version mono">{row.version}</span></span>
          {#if row.blurb}<span class="blurb">{row.blurb}</span>{/if}
        </div>
        <span class="pill">
          {#if row.status === "busy"}<span class="spinner" aria-hidden="true"></span>{/if}
          {row.label}
        </span>
      </li>
    {/each}
  </ul>

  <footer>
    {#if pending && !installing}<span class="hint">Press <b>Update</b> to match the server.</span>{/if}
    <span>Your own mods in Mods/ are left alone.</span>
  </footer>
</div>

<style>
  /* Lives inside <main>, so the title bar and the action bar stay usable: Update can run with the list open. */
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
    max-height: calc(100% - 64px);
    display: flex;
    flex-direction: column;
    border: 1px solid var(--line);
    border-bottom: 0;
    border-radius: 16px 16px 0 0;
    background: var(--panel);
    box-shadow: 0 -18px 50px -20px rgb(0 0 0 / 0.85);
  }
  header {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 18px 20px 12px;
  }
  header div {
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

  /* The one list in the app that may outgrow the window, so it alone scrolls. */
  ul {
    list-style: none;
    margin: 0;
    padding: 0 20px;
    overflow-y: auto;
    min-height: 0;
  }
  li {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 0;
    border-top: 1px solid var(--line);
  }
  .text {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 1px;
  }
  .name {
    font-weight: 650;
    font-size: 13px;
    color: var(--ink);
  }
  .version {
    margin-left: 4px;
    font-size: 11px;
    color: var(--faint);
  }
  .blurb {
    font-size: 11.5px;
    color: var(--muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .remove .name {
    color: var(--muted);
    text-decoration: line-through;
    text-decoration-color: var(--faint);
  }

  .pill {
    flex: none;
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 3px 9px;
    border-radius: 999px;
    border: 1px solid var(--line);
    font-size: 11px;
    font-weight: 650;
    color: var(--muted);
  }
  .ok .pill {
    border-color: rgb(94 196 139 / 0.4);
    background: rgb(94 196 139 / 0.1);
    color: var(--ok);
  }
  .update .pill,
  .busy .pill {
    border-color: rgb(242 163 58 / 0.45);
    background: var(--sodium-glow);
    color: var(--sodium-hot);
  }
  .remove .pill {
    border-color: rgb(239 109 106 / 0.45);
    background: var(--bad-wash);
    color: var(--bad);
  }
  .spinner {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    border: 1.5px solid rgb(242 163 58 / 0.3);
    border-top-color: var(--sodium-hot);
    animation: spin 0.8s linear infinite;
  }
  @keyframes spin {
    to { transform: rotate(360deg); }
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
  .hint {
    color: var(--muted);
  }
  .hint b {
    color: var(--sodium);
  }
</style>
