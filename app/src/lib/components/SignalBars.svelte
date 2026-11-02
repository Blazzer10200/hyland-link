<script lang="ts">
  // Burner-phone style reception: 4 bars from the status-query round trip.
  let { pingMs, online, checking }: { pingMs: number | null; online: boolean; checking: boolean } = $props();

  const level = $derived.by(() => {
    if (!online || pingMs === null) return 0;
    if (pingMs < 60) return 4;
    if (pingMs < 120) return 3;
    if (pingMs < 220) return 2;
    return 1;
  });
</script>

<span class="bars" class:online class:checking aria-hidden="true">
  {#each [1, 2, 3, 4] as bar (bar)}
    <i class:lit={bar <= level} style:height="{3 + bar * 3}px"></i>
  {/each}
</span>

<style>
  .bars {
    display: inline-flex;
    align-items: flex-end;
    gap: 2px;
    height: 15px;
  }
  i {
    width: 3px;
    border-radius: 1px;
    background: var(--line);
    transition: background 0.3s;
  }
  .online i.lit {
    background: var(--ok);
  }
  .checking i {
    animation: scan 1.1s ease-in-out infinite;
  }
  .checking i:nth-child(2) { animation-delay: 0.12s; }
  .checking i:nth-child(3) { animation-delay: 0.24s; }
  .checking i:nth-child(4) { animation-delay: 0.36s; }
  @keyframes scan {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.35; }
  }
</style>
