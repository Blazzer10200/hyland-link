using System;
using System.Collections.Generic;
using Il2CppFishNet.Object;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace HylandMinimap;

internal static class WorldMarkerReader
{
	private const int MaximumScanItems = 4096;

	internal static System.Collections.Generic.List<WorldMarkerSource> ReadDealers()
	{
		System.Collections.Generic.List<WorldMarkerSource> list = new System.Collections.Generic.List<WorldMarkerSource>();
		HashSet<int> hashSet = new HashSet<int>();
		try
		{
			Il2CppSystem.Collections.Generic.List<Dealer> allPlayerDealers = Dealer.AllPlayerDealers;
			if (allPlayerDealers == null)
			{
				return list;
			}
			int num = Math.Min(allPlayerDealers.Count, MaximumScanItems);
			for (int i = 0; i < num; i++)
			{
				try
				{
					Dealer dealer = allPlayerDealers[i];
					if (dealer == null || !dealer.IsRecruited || dealer.gameObject == null || !dealer.gameObject.activeInHierarchy)
					{
						continue;
					}
					NPCHealth health = dealer.Health;
					if (health != null && health.IsDead)
					{
						continue;
					}
					Transform transform = dealer.transform;
					if (dealer.IsInVehicle && dealer.CurrentVehicle != null)
					{
						transform = dealer.CurrentVehicle.transform;
					}
					if (!(transform == null))
					{
						int instanceID = dealer.GetInstanceID();
						string name = KnownName(dealer.FullName, dealer.FirstName, "Dealer");
						Sprite mugshotSprite = dealer.MugshotSprite;
						if (hashSet.Add(instanceID))
						{
							list.Add(new WorldMarkerSource(instanceID, WorldMarkerKind.Dealer, dealer, transform, name, mugshotSprite));
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return list;
	}

	internal static System.Collections.Generic.List<WorldMarkerSource> ReadVehicles()
	{
		System.Collections.Generic.List<WorldMarkerSource> result = new System.Collections.Generic.List<WorldMarkerSource>();
		HashSet<int> seen = new HashSet<int>();
		try
		{
			VehicleManager instance = NetworkSingleton<VehicleManager>.Instance;
			if (instance == null)
			{
				return result;
			}
			try
			{
				AddVehicles(instance.PlayerOwnedVehicles, result, seen);
			}
			catch
			{
			}
			try
			{
				AddVehicles(instance.AllVehicles, result, seen);
			}
			catch
			{
			}
		}
		catch
		{
		}
		return result;
	}

	private static void AddVehicles(Il2CppSystem.Collections.Generic.List<LandVehicle>? vehicles, System.Collections.Generic.List<WorldMarkerSource> result, HashSet<int> seen)
	{
		if (vehicles == null)
		{
			return;
		}
		int num = Math.Min(vehicles.Count, MaximumScanItems);
		for (int i = 0; i < num; i++)
		{
			try
			{
				LandVehicle landVehicle = vehicles[i];
				if (!(landVehicle == null) && landVehicle.IsPlayerOwned && !(landVehicle.gameObject == null) && landVehicle.gameObject.activeInHierarchy && !(landVehicle.transform == null))
				{
					int instanceID = landVehicle.GetInstanceID();
					string vehicleCode = landVehicle.VehicleCode;
					string text = KnownName(landVehicle.VehicleName, vehicleCode, "Vehicle");
					WorldMarkerKind kind = ((!IsBikeName(vehicleCode) && !IsBikeName(text)) ? WorldMarkerKind.Vehicle : WorldMarkerKind.Bike);
					if (seen.Add(instanceID))
					{
						result.Add(new WorldMarkerSource(instanceID, kind, landVehicle, landVehicle.transform, text, null));
					}
				}
			}
			catch
			{
			}
		}
	}

	internal static void FillOccupiedVehicleIds(Player local, bool includePeers, HashSet<int> result)
	{
		result.Clear();
		AddOccupiedVehicle(local, result);
		if (!includePeers)
		{
			return;
		}
		try
		{
			Il2CppSystem.Collections.Generic.List<Player> playerList = Player.PlayerList;
			if (playerList == null)
			{
				return;
			}
			int num = Math.Min(playerList.Count, MaximumScanItems);
			for (int i = 0; i < num; i++)
			{
				try
				{
					AddOccupiedVehicle(playerList[i], result);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static void AddOccupiedVehicle(Player? player, HashSet<int> occupied)
	{
		try
		{
			if (player == null || !player.IsInVehicle)
			{
				return;
			}
			NetworkObject currentVehicle = player.CurrentVehicle;
			if (!(currentVehicle == null))
			{
				LandVehicle landVehicle = currentVehicle.GetComponent<LandVehicle>();
				if (landVehicle == null)
				{
					landVehicle = currentVehicle.GetComponentInParent<LandVehicle>();
				}
				if (landVehicle == null)
				{
					landVehicle = currentVehicle.GetComponentInChildren<LandVehicle>();
				}
				if (landVehicle != null)
				{
					occupied.Add(landVehicle.GetInstanceID());
				}
			}
		}
		catch
		{
		}
	}

	private static string KnownName(string? primary, string? secondary, string fallback)
	{
		if (string.IsNullOrWhiteSpace(primary))
		{
			if (string.IsNullOrWhiteSpace(secondary))
			{
				return fallback;
			}
			return secondary.Trim();
		}
		return primary.Trim();
	}

	private static bool IsBikeName(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			if (!value.Contains("bike", StringComparison.OrdinalIgnoreCase) && !value.Contains("motorcycle", StringComparison.OrdinalIgnoreCase))
			{
				return value.Contains("bicycle", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}
}
