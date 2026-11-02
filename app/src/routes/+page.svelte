<script lang="ts">
  import { onMount } from "svelte";
  import { fade, fly } from "svelte/transition";
  import ActionBar from "$lib/components/ActionBar.svelte";
  import ModsSheet from "$lib/components/ModsSheet.svelte";
  import ServerCard from "$lib/components/ServerCard.svelte";
  import Steps from "$lib/components/Steps.svelte";
  import TitleBar from "$lib/components/TitleBar.svelte";
  import { launcher } from "$lib/launcher.svelte";

  onMount(() => {
    launcher.start().catch((e: unknown) => {
      launcher.error = String(e);
    });
  });

  const snapshot = $derived(launcher.snapshot);
  const usingBuiltIn = $derived(
    snapshot?.manifestSource.kind === "builtIn" && snapshot.manifestSource.reason !== "checking for updates"
      ? snapshot.manifestSource.reason
      : null,
  );
  let noticeDismissed = $state(false);
</script>

<div class="app">
  <TitleBar />

  <main>
    <!-- Nothing renders until the first state arrives, so the entrance plays on the real step instead of flashing step 1. -->
    {#if snapshot}
      <div in:fly={{ y: 10, duration: 420 }}>
        <ServerCard server={snapshot.manifest.server} status={launcher.server} checking={launcher.checkingServer} />
      </div>
      <Steps {snapshot} step={launcher.step} phase={launcher.phase} progress={launcher.progress} onShowMods={launcher.showMods} />
    {/if}

    <div class="toasts" aria-live="polite">
      {#if launcher.notice}
        <div class="toast accent" transition:fly={{ y: 16, duration: 260 }}>
          <p>{launcher.notice}</p>
          <button type="button" onclick={launcher.dismissNotice}>OK</button>
        </div>
      {/if}
      {#if launcher.error}
        <div class="toast bad" role="alert" transition:fly={{ y: 16, duration: 260 }}>
          <p>{launcher.error}</p>
          <button type="button" onclick={launcher.dismissError}>OK</button>
        </div>
      {/if}
      {#if launcher.launcherUpdate}
        <div class="toast accent" transition:fly={{ y: 16, duration: 260 }}>
          <p>Hyland Link {launcher.launcherUpdate} is out.</p>
          <button type="button" onclick={launcher.openLauncherDownload}>Get it</button>
        </div>
      {/if}
      {#if snapshot?.manifest.notice && !noticeDismissed}
        <div class="toast accent" transition:fly={{ y: 16, duration: 260 }}>
          <p>{snapshot.manifest.notice}</p>
          <button type="button" onclick={() => (noticeDismissed = true)}>OK</button>
        </div>
      {/if}
    </div>

    {#if launcher.modsOpen && snapshot?.install}
      <ModsSheet install={snapshot.install} installing={launcher.installing} progress={launcher.progress} onClose={launcher.hideMods} />
    {/if}
  </main>

  <ActionBar
    phase={launcher.phase}
    {snapshot}
    progress={launcher.progress}
    {usingBuiltIn}
    onInstall={launcher.install}
    onPlay={launcher.play}
    onChooseFolder={launcher.chooseGameFolder}
    onOpenFolder={launcher.openGameFolder}
    onShowMods={launcher.showMods}
    onUninstall={launcher.uninstall}
  />

  {#if launcher.selfUpdate}
    {@const pct = launcher.selfUpdate.percent}
    <div class="self-update" role="status" aria-live="polite" transition:fade={{ duration: 260 }}>
      <div class="beacon" aria-hidden="true"><span></span></div>
      <h1>Updating Hyland Link</h1>
      <p>Version <b>{launcher.selfUpdate.version}</b> is on its way. The launcher closes and reopens by itself.</p>
      <div class="bar" class:waiting={pct === null}><span style:width="{pct ?? 0}%"></span></div>
      <span class="pct mono">{pct === null ? "Starting…" : pct < 100 ? `${pct}%` : "Installing…"}</span>
    </div>
  {/if}
</div>

<style>
  .app {
    position: relative;
    height: 100%;
    display: flex;
    flex-direction: column;
  }
  /* Covers everything under the 38px title bar, so the window can still be moved or closed. */
  .self-update {
    position: absolute;
    inset: 38px 0 0;
    z-index: 20;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 12px;
    padding: 0 44px 40px;
    text-align: center;
    background: var(--asphalt);
  }
  .beacon {
    position: relative;
    display: grid;
    place-items: center;
    width: 76px;
    height: 76px;
    margin-bottom: 14px;
  }
  .beacon span {
    width: 18px;
    height: 18px;
    border-radius: 50%;
    background: var(--sodium-hot);
    box-shadow: 0 0 0 6px var(--sodium-glow), 0 0 30px 8px rgb(242 163 58 / 0.45);
  }
  .beacon::before,
  .beacon::after {
    content: "";
    position: absolute;
    inset: 0;
    border-radius: 50%;
    border: 1px solid rgb(242 163 58 / 0.55);
    animation: ripple 2.4s ease-out infinite;
  }
  .beacon::after {
    animation-delay: 1.2s;
  }
  @keyframes ripple {
    from {
      transform: scale(0.25);
      opacity: 1;
    }
    to {
      transform: scale(1.35);
      opacity: 0;
    }
  }
  .self-update h1 {
    margin: 0;
    font: 700 22px/1.2 var(--font-display);
    color: var(--ink);
  }
  .self-update p {
    margin: 0;
    font-size: 13px;
    line-height: 1.5;
    color: var(--muted);
  }
  .self-update b {
    color: var(--ink);
  }
  .bar {
    width: 100%;
    height: 6px;
    margin-top: 12px;
    border-radius: 3px;
    background: var(--raised);
    overflow: hidden;
  }
  .bar span {
    display: block;
    height: 100%;
    border-radius: inherit;
    background: linear-gradient(90deg, var(--sodium), var(--sodium-hot));
    transition: width 0.25s ease-out;
  }
  /* No size known yet: a short bar sweeps instead of sitting at 0%. */
  .bar.waiting span {
    width: 35% !important;
    animation: sweep-bar 1.2s ease-in-out infinite;
  }
  @keyframes sweep-bar {
    from {
      transform: translateX(-100%);
    }
    to {
      transform: translateX(290%);
    }
  }
  .pct {
    font-size: 12px;
    color: var(--faint);
  }
  /* Fixed-size window: everything fits, nothing scrolls. Only the current step opens up. */
  main {
    position: relative;
    flex: 1;
    min-height: 0;
    overflow: hidden;
    padding: 4px 20px 0;
    display: flex;
    flex-direction: column;
    gap: 14px;
  }
  .toasts {
    position: absolute;
    left: 20px;
    right: 20px;
    bottom: 12px;
    display: flex;
    flex-direction: column;
    gap: 8px;
    pointer-events: none;
    z-index: 10;
  }
  .toast {
    pointer-events: auto;
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 11px 14px;
    border-radius: var(--radius);
    border: 1px solid var(--line);
    background: var(--raised);
    box-shadow: 0 12px 32px -12px rgb(0 0 0 / 0.8);
    font-size: 12.5px;
  }
  .toast p {
    margin: 0;
    flex: 1;
  }
  .toast.accent {
    border-color: rgb(242 163 58 / 0.45);
  }
  .toast.bad {
    border-color: rgb(239 109 106 / 0.55);
    background: color-mix(in srgb, var(--raised) 88%, var(--bad));
  }
  .toast button {
    flex: none;
    border: 0;
    background: transparent;
    color: var(--sodium);
    font-size: 12px;
    font-weight: 700;
    padding: 0;
  }
  .toast.bad button {
    color: var(--bad);
  }
</style>
