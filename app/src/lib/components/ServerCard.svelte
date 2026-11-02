<script lang="ts">
  import { fly } from "svelte/transition";
  import type { Manifest, ServerStatus } from "$lib/api";
  import SignalBars from "./SignalBars.svelte";

  let { server, status, checking }: { server: Manifest["server"]; status: ServerStatus | null; checking: boolean } =
    $props();

  const address = $derived(`${server.host}:${server.port}`);
  const online = $derived(status?.online ?? false);
  const players = $derived(online ? (status?.players ?? null) : null);
  let copied = $state(false);

  const headline = $derived.by(() => {
    if (!status) return "Checking";
    if (status.online) return status.pingMs !== null ? `Online · ${status.pingMs} ms` : "Online";
    return "Offline · retrying";
  });

  async function copyAddress() {
    try {
      await navigator.clipboard.writeText(address);
      copied = true;
      setTimeout(() => (copied = false), 1600);
    } catch {
      // Clipboard refused: the address text stays selectable, so nothing is lost.
      copied = false;
    }
  }
</script>

<section class="card" class:online aria-label="Server">
  <div class="top">
    <div class="status">
      <SignalBars pingMs={status?.pingMs ?? null} {online} {checking} />
      {#if online}<span class="live" aria-hidden="true"></span>{/if}
      <span class="headline" class:good={online}>{headline}</span>
    </div>
    <button type="button" class="address" onclick={copyAddress} title="Copy the server address">
      <span class="mono selectable">{address}</span>
      <span class="copy">
        {#key copied}<span in:fly={{ y: 6, duration: 180 }}>{copied ? "Copied" : "Copy"}</span>{/key}
      </span>
    </button>
  </div>

  <div class="bottom">
    <h1>{status?.name ?? server.name}</h1>
    <div class="players" title={online ? "Players online right now" : "Server offline"}>
      <span class="count mono" class:dim={players === null}>
        {#key players}<span in:fly={{ y: -10, duration: 260 }}>{players ?? "–"}</span>{/key}
      </span>
      <span class="of">{online && status?.maxPlayers ? `/ ${status.maxPlayers} online` : "players"}</span>
    </div>
  </div>
</section>

<style>
  .card {
    position: relative;
    border: 1px solid var(--line);
    border-radius: 14px;
    padding: 14px 16px 16px;
    display: flex;
    flex-direction: column;
    gap: 12px;
    /* Streetlamp spill from the top-left corner: the one loud surface on the screen. */
    background:
      radial-gradient(110% 120% at 0% 0%, var(--sodium-glow), transparent 62%),
      var(--panel);
    transition: border-color 0.4s;
  }
  .card.online {
    border-color: #2f3a37;
  }
  .top {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
  }
  .status {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
  }
  .live {
    position: relative;
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--ok);
  }
  .live::after {
    content: "";
    position: absolute;
    inset: -4px;
    border-radius: 50%;
    border: 1.5px solid var(--ok);
    animation: ping 2.2s ease-out infinite;
  }
  @keyframes ping {
    0% { transform: scale(0.5); opacity: 0.9; }
    80%, 100% { transform: scale(1.6); opacity: 0; }
  }
  .headline {
    font-size: 11.5px;
    font-weight: 650;
    letter-spacing: 0.03em;
    color: var(--muted);
    white-space: nowrap;
  }
  .headline.good {
    color: var(--ok);
  }
  .address {
    display: flex;
    align-items: center;
    gap: 9px;
    border: 1px solid var(--line);
    background: rgb(16 19 23 / 0.7);
    border-radius: 8px;
    padding: 5px 7px 5px 10px;
    font-size: 11.5px;
    transition: border-color 0.15s;
  }
  .address:hover {
    border-color: var(--faint);
  }
  .copy {
    display: inline-grid;
    min-width: 44px;
    text-align: right;
    font-size: 10.5px;
    font-weight: 700;
    letter-spacing: 0.07em;
    text-transform: uppercase;
    color: var(--sodium);
    overflow: hidden;
  }
  .bottom {
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    gap: 14px;
  }
  h1 {
    margin: 0;
    font: 750 23px/1.05 var(--font-display);
    font-stretch: 112%;
    letter-spacing: -0.01em;
    text-wrap: balance;
    min-width: 0;
  }
  .players {
    flex: none;
    display: flex;
    align-items: baseline;
    gap: 5px;
  }
  .count {
    display: inline-grid;
    overflow: hidden;
    font-size: 26px;
    line-height: 1;
  }
  .count.dim {
    color: var(--faint);
  }
  .of {
    color: var(--muted);
    font-size: 12px;
    white-space: nowrap;
  }
</style>
