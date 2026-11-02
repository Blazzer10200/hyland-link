<script lang="ts">
  import { fade, fly, slide } from "svelte/transition";
  import type { Component, InstallProgress, Snapshot } from "$lib/api";
  import { shortPath, type GameStage } from "$lib/game-log";
  import type { Phase, StepNumber } from "$lib/launcher.svelte";
  import GameStatus from "./GameStatus.svelte";

  let {
    snapshot,
    step,
    phase,
    progress,
    stage,
    logMessage,
    logErrors,
    onShowMods,
    onShowLog,
  }: {
    snapshot: Snapshot | null;
    step: StepNumber;
    phase: Phase;
    progress: InstallProgress | null;
    stage: GameStage | null;
    logMessage: string | null;
    logErrors: number;
    onShowMods: () => void;
    onShowLog: () => void;
  } = $props();

  type ItemState = "done" | "active" | "pending";

  const steps = [
    { n: 1, title: "Find Schedule I", doneTitle: "Schedule I found", preview: "Looks through your Steam libraries" },
    { n: 2, title: "Install the server mod", doneTitle: "Server mod installed", preview: "MelonLoader + the server mod, one click" },
    { n: 3, title: "Launch once to finish setup", doneTitle: "Setup run finished", preview: "A one-time setup run" },
    { n: 4, title: "Join the server", doneTitle: "Join the server", preview: "Servers → Favorites → Join" },
  ] as const;

  const installing = $derived(phase === "installing");
  const percent = $derived(progress && progress.total > 0 ? Math.round((progress.done / progress.total) * 100) : null);

  /** The server's extra mods, summed up for one line: "3 mods, all set", "1 new · 1 update · 1 to remove". */
  const extras = $derived.by(() => {
    const install = snapshot?.install;
    if (!install || (install.mods.length === 0 && install.staleMods.length === 0)) return null;
    const fresh = install.mods.filter((m) => !m.installed).length;
    const updates = install.mods.filter((m) => m.installed && !m.upToDate).length;
    const removals = install.staleMods.length;
    const parts = [fresh && `${fresh} new`, updates && `${updates} update${updates > 1 ? "s" : ""}`, removals && `${removals} to remove`];
    const changes = parts.filter(Boolean).join(" · ");
    return { count: install.mods.length, current: changes === "", note: changes || `${install.mods.length} mod${install.mods.length === 1 ? "" : "s"}, all set` };
  });

  const items = $derived.by(() => {
    if (!snapshot?.install) return [];
    const { melonloader, serverMod } = snapshot.install;
    const downloading = progress?.step ?? "melonloader";
    // The snapshot only refreshes after the whole install, so a later download step means the earlier ones are done.
    const order = ["melonloader", "mod", "mods"];
    const passed = (step: string) => installing && order.indexOf(downloading) > order.indexOf(step);
    const itemState = (upToDate: boolean, step: string): ItemState =>
      upToDate || passed(step) ? "done" : installing && downloading === step ? "active" : "pending";
    // An outdated copy says which version it replaces, so an update reads as an update.
    const note = (c: Component, fallback: string) => (c.installed && !c.upToDate && c.version ? `you have ${c.version}` : fallback);
    const list = [
      {
        id: "ml",
        label: `MelonLoader ${snapshot.manifest.melonloader.version}`,
        note: note(melonloader, "lets the game load mods"),
        state: itemState(melonloader.upToDate, "melonloader"),
        showList: false,
      },
      {
        id: "mod",
        label: `Server mod ${snapshot.manifest.mod.version}`,
        note: note(serverMod, "connects you to the server"),
        state: itemState(serverMod.upToDate, "mod"),
        showList: false,
      },
    ];
    if (extras) {
      list.push({
        id: "mods",
        label: "Server's mods",
        note: installing && downloading === "mods" && progress ? `${progress.item} of ${progress.items}` : extras.note,
        state: itemState(extras.current, "mods"),
        showList: true,
      });
    }
    list.push({
      id: "fav",
      label: "Server saved to Favorites",
      note: "so you never type an address",
      state: snapshot.favoriteAdded ? "done" : "pending",
      showList: false,
    });
    return list;
  });

  /** Step 2 says "mods" once the server runs extras besides the server mod. */
  function modTitle(done: boolean) {
    const mods = `server mod${extras ? "s" : ""}`;
    if (done) return `${mods[0].toUpperCase()}${mods.slice(1)} installed`;
    return `${phase === "update" ? "Update" : "Install"} the ${mods}`;
  }

  function summary(n: number) {
    if (!snapshot) return "";
    switch (n) {
      case 1:
        return shortPath(snapshot.game.path ?? "");
      case 2:
        return `MelonLoader ${snapshot.manifest.melonloader.version} · mod ${snapshot.manifest.mod.version}${extras?.count ? ` · +${extras.count} mod${extras.count === 1 ? "" : "s"}` : ""}`;
      case 3:
        return "MelonLoader prepared the game";
      default:
        return "";
    }
  }

  const joinPath = $derived(["Servers", "Favorites", snapshot?.manifest.server.name ?? "the server", "Join"]);
</script>

<ol class="steps" aria-label="Setup steps">
  {#each steps as s, i (s.n)}
    {@const state = s.n < step ? "done" : s.n === step ? "current" : "upcoming"}
    <li class={state} in:fly={{ y: 14, duration: 420, delay: 120 + i * 80 }}>
      {#if s.n < steps.length}
        <span class="rail" aria-hidden="true"><span class="rail-fill"></span></span>
      {/if}

      <span class="node" aria-hidden="true">
        {#if state === "done"}
          <svg viewBox="0 0 16 16"><path class="draw" d="M3.8 8.4l2.7 2.7 5.7-5.9" /></svg>
        {:else}
          <span class="num mono">{s.n}</span>
        {/if}
      </span>

      <div class="content">
        <div class="head">
          <span class="title">{s.n === 2 ? modTitle(state === "done") : state === "done" ? s.doneTitle : s.title}</span>
          {#if state === "done"}
            <span class="summary" class:mono={s.n === 1} title={s.n === 1 ? (snapshot?.game.path ?? "") : ""} in:fade={{ duration: 300 }}
              >{summary(s.n)}</span
            >
          {:else if state === "upcoming"}
            <span class="summary">{s.preview}</span>
          {/if}
        </div>

        {#if state === "current"}
          <div class="body" transition:slide={{ duration: 340 }}>
            {#if s.n === 1}
              {#if phase === "loading"}
                <p>Looking through your Steam libraries…</p>
              {:else if phase === "branch"}
                <p class="warn-text">
                  Your game is on the <b>“{snapshot?.game.branch}”</b> beta. This server needs the normal version:
                </p>
                <ol class="howto">
                  <li>In Steam, right-click <b>Schedule I</b> → <b>Properties</b>.</li>
                  <li>Open <b>Betas</b> and pick <b>None</b>.</li>
                  <li>Let Steam finish the download. This updates by itself.</li>
                </ol>
              {:else}
                <p>I couldn't find Schedule I in Steam. Install it from Steam, or show me the folder it's in.</p>
              {/if}
            {:else if s.n === 2}
              {#if phase === "closeGame"}
                <p class="warn-text">Close Schedule I first. Its files can't change while it's running.</p>
              {:else if phase === "update"}
                <p>Your mods don't match the server's. One click brings you up to date, then you're back in.</p>
              {:else}
                <p>One click sets up everything. Nothing outside the game folder changes.</p>
              {/if}
              <ul class="items">
                {#each items as item (item.id)}
                  <li class={item.state}>
                    <span class="dot" aria-hidden="true">
                      {#if item.state === "done"}
                        <svg viewBox="0 0 16 16"><path class="draw" d="M4 8.4l2.5 2.5 5.5-5.6" /></svg>
                      {:else if item.state === "active"}
                        <span class="spinner"></span>
                      {/if}
                    </span>
                    <span class="item-label">{item.label}</span>
                    <span class="item-note">
                      {#if item.state === "active" && item.showList}{item.note}{#if percent !== null}<span class="mono"> · {percent}%</span
                          >{/if}{:else if item.state === "active" && percent !== null}<span class="mono">{percent}%</span
                        >{:else}{item.note}{/if}
                    </span>
                    {#if item.showList}
                      <button type="button" class="see-list" onclick={onShowMods}>See list</button>
                    {/if}
                  </li>
                {/each}
              </ul>
            {:else if s.n === 3}
              {#if phase === "firstLaunchRunning"}
                <p>MelonLoader is preparing the game. It takes a few minutes and finishes by itself, so leave the game open.</p>
                <GameStatus stage={stage ?? "starting"} message={logMessage} errors={logErrors} {onShowLog} />
              {:else}
                <p>
                  Start the game once. The first launch takes a few minutes while MelonLoader gets the game ready, and you
                  can watch it happen right here.
                </p>
              {/if}
            {:else}
              {#if phase === "playing"}
                <GameStatus stage={stage ?? "starting"} message={logMessage} errors={logErrors} {onShowLog} />
              {/if}
              {#if stage !== "joined"}
                <p>{phase === "playing" ? "From the main menu:" : "Press Play, then in the main menu:"}</p>
                <div class="path" transition:slide={{ duration: 260 }}>
                  {#each joinPath as crumb, c (c)}
                    {#if c > 0}<span class="arrow" in:fade={{ delay: 260 + c * 160, duration: 200 }}>→</span>{/if}
                    <span class="crumb" class:final={c === joinPath.length - 1} in:fly={{ x: -8, delay: 200 + c * 160, duration: 260 }}
                      >{crumb}</span
                    >
                  {/each}
                </div>
              {/if}
            {/if}
          </div>
        {/if}
      </div>
    </li>
  {/each}
</ol>

<style>
  .steps {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 6px;
  }
  .steps > li {
    position: relative;
    display: grid;
    grid-template-columns: 30px 1fr;
    gap: 14px;
    padding: 9px 4px 9px 0;
  }

  /* The rail: a line from each node down to the next, filled once that step is done. */
  .rail {
    position: absolute;
    left: 14px;
    top: 41px;
    bottom: -8px;
    width: 2px;
    border-radius: 1px;
    background: var(--line);
    overflow: hidden;
  }
  .rail-fill {
    position: absolute;
    inset: 0;
    background: linear-gradient(var(--ok), color-mix(in srgb, var(--ok) 55%, var(--sodium)));
    transform: scaleY(0);
    transform-origin: top;
    transition: transform 0.6s cubic-bezier(0.2, 0.8, 0.2, 1);
  }
  .done .rail-fill {
    transform: scaleY(1);
  }

  .node {
    position: relative;
    z-index: 1;
    width: 30px;
    height: 30px;
    border-radius: 50%;
    display: grid;
    place-items: center;
    border: 1.5px solid var(--line);
    background: var(--asphalt);
    color: var(--faint);
    transition: background 0.35s, border-color 0.35s, color 0.35s;
  }
  .num {
    font-size: 12.5px;
  }
  .current .node {
    border-color: var(--sodium);
    color: var(--sodium);
    background: #1d1710;
  }
  .current .node::after {
    content: "";
    position: absolute;
    inset: -5px;
    border-radius: 50%;
    border: 1.5px solid var(--sodium);
    opacity: 0;
    animation: halo 2.4s ease-out infinite;
  }
  @keyframes halo {
    0% { transform: scale(0.85); opacity: 0.7; }
    70%, 100% { transform: scale(1.25); opacity: 0; }
  }
  .done .node {
    background: var(--ok);
    border-color: var(--ok);
    color: var(--asphalt);
  }
  svg {
    width: 16px;
    height: 16px;
    fill: none;
    stroke: currentColor;
    stroke-width: 2;
    stroke-linecap: round;
    stroke-linejoin: round;
  }
  .draw {
    stroke-dasharray: 16;
    stroke-dashoffset: 16;
    animation: draw 0.45s 0.1s cubic-bezier(0.3, 0.7, 0.3, 1) forwards;
  }
  @keyframes draw {
    to { stroke-dashoffset: 0; }
  }

  .content {
    min-width: 0;
    padding-top: 5px;
  }
  .head {
    display: flex;
    flex-direction: column;
    gap: 1px;
  }
  .title {
    font: 650 14.5px/1.3 var(--font-body);
    transition: color 0.3s;
  }
  .upcoming .title {
    color: var(--muted);
  }
  .current .title {
    font: 700 15.5px/1.3 var(--font-display);
    font-stretch: 108%;
    color: var(--ink);
  }
  .summary {
    font-size: 12px;
    color: var(--muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .summary.mono {
    font-size: 11px;
  }
  .upcoming .summary {
    color: var(--faint);
  }

  .body {
    padding-top: 8px;
    display: flex;
    flex-direction: column;
    gap: 10px;
    font-size: 12.75px;
    line-height: 1.5;
    color: #c3cad2;
  }
  .body p {
    margin: 0;
  }
  .body b {
    font-weight: 650;
    color: var(--ink);
  }
  .warn-text {
    color: var(--warn);
  }
  .warn-text b {
    color: var(--warn);
  }
  .howto {
    margin: 0;
    padding-left: 18px;
    display: flex;
    flex-direction: column;
    gap: 3px;
  }

  .items {
    list-style: none;
    margin: 0;
    padding: 4px 0;
    display: flex;
    flex-direction: column;
    gap: 7px;
  }
  .items li {
    display: grid;
    grid-template-columns: 18px auto minmax(0, 1fr) auto;
    align-items: center;
    gap: 9px;
  }
  .see-list {
    border: 0;
    background: transparent;
    padding: 2px 0;
    color: var(--sodium);
    font-size: 11.5px;
    font-weight: 650;
  }
  .see-list:hover {
    color: var(--sodium-hot);
    text-decoration: underline;
  }
  .dot {
    width: 18px;
    height: 18px;
    border-radius: 50%;
    border: 1.5px solid var(--line);
    display: grid;
    place-items: center;
    transition: background 0.3s, border-color 0.3s;
  }
  .dot svg {
    width: 12px;
    height: 12px;
    stroke-width: 2.2;
  }
  .items .done .dot {
    background: var(--ok);
    border-color: var(--ok);
    color: var(--asphalt);
  }
  .items .active .dot {
    border-color: transparent;
  }
  .spinner {
    width: 16px;
    height: 16px;
    border-radius: 50%;
    border: 2px solid var(--line);
    border-top-color: var(--sodium);
    animation: spin 0.8s linear infinite;
  }
  @keyframes spin {
    to { transform: rotate(360deg); }
  }
  .item-label {
    font-weight: 600;
    color: var(--ink);
    font-size: 12.75px;
  }
  .pending .item-label {
    color: var(--muted);
  }
  .item-note {
    color: var(--faint);
    font-size: 11.5px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .active .item-note {
    color: var(--sodium);
  }

  .path {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 4px;
  }
  .crumb {
    padding: 4px 7px;
    border-radius: 6px;
    border: 1px solid var(--line);
    background: var(--raised);
    color: var(--ink);
    font-size: 11.5px;
    font-weight: 600;
    white-space: nowrap;
  }
  .crumb.final {
    border-color: rgb(242 163 58 / 0.55);
    background: var(--sodium-glow);
    color: var(--sodium-hot);
  }
  .arrow {
    color: var(--faint);
    font-size: 11px;
  }
</style>
