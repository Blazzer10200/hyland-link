<script lang="ts">
  import type { InstallProgress, InstallState } from "$lib/api";
  import Sheet from "./Sheet.svelte";

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

<Sheet
  id="mods-title"
  title="Mods on this server"
  subtitle="Hyland Link keeps these matched to the server. You never pick or update them by hand."
  {onClose}
>
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

  {#snippet footer()}
    {#if pending && !installing}<span class="hint">Press <b>Update</b> to match the server.</span>{/if}
    <span>Your own mods in Mods/ are left alone.</span>
  {/snippet}
</Sheet>

<style>
  /* The list may outgrow the window, so it scrolls inside the sheet. */
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

  .hint {
    color: var(--muted);
  }
  .hint b {
    color: var(--sodium);
  }
</style>
