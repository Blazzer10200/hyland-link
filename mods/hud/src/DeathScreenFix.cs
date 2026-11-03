using System;
using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.UI;
using MelonLoader;

namespace HylandHud;

/// <summary>
/// The game picks the death screen's button by Steam lobby (<c>DeathScreen.CanRespawn</c> is <c>Lobby.IsInLobby</c>), and
/// a dedicated-server client joins by IP, not through a lobby. So it got "Load last save", which exits to the menu and
/// drops the player off the server. A client of someone else's server gets Respawn instead, like a co-op client does;
/// single player and co-op hosts keep the game's choice. The postfix runs after Open sets both buttons and before the
/// screen fades in (it waits 0.55 s), so the wrong button never shows.
/// </summary>
[HarmonyPatch(typeof(DeathScreen), nameof(DeathScreen.Open))]
internal static class DeathScreenFix
{
	private static void Postfix(DeathScreen __instance)
	{
		try
		{
			if (!InstanceFinder.IsClient || InstanceFinder.IsServer)
			{
				return;
			}
			__instance.respawnButton.gameObject.SetActive(true);
			__instance.loadSaveButton.gameObject.SetActive(false);
			Melon<HudMod>.Logger.Msg("Death screen: Respawn instead of Load last save (server client).");
		}
		catch (Exception ex)
		{
			Melon<HudMod>.Logger.Warning("Death screen fix failed, the game's buttons stay: " + ex.GetType().Name + ": " + ex.Message);
		}
	}
}
