using System;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Quests;
using MelonLoader;

namespace HylandMinimap;

internal static class CustomerStatusReader
{
	private static bool purchaseWarningShown;

	private static bool contractWarningShown;

	private static bool regionWarningShown;

	internal static bool? GetRegionLockState(Customer customer)
	{
		try
		{
			NPC nPC = customer.NPC;
			if (nPC == null || nPC.RelationData == null)
			{
				return null;
			}
			if (nPC.RelationData.Unlocked)
			{
				return false;
			}
			Map instance = Singleton<Map>.Instance;
			if (instance == null)
			{
				return null;
			}
			MapRegionData regionData = instance.GetRegionData(nPC.Region);
			return CustomerStatusPolicy.ClassifyRegionLock(false, regionData?.IsUnlocked);
		}
		catch (Exception ex)
		{
			if (!regionWarningShown)
			{
				regionWarningShown = true;
				MelonLogger.Warning("Minimap region lock query failed: " + ex.GetType().Name + ": " + ex.Message);
			}
			return null;
		}
	}

	internal static CustomerStatus Read(Customer customer, bool showAlreadyPurchased, bool showActiveDeals)
	{
		bool offerValid = true;
		string invalidReason = null;
		bool activeContract = false;
		if (showAlreadyPurchased)
		{
			try
			{
				offerValid = customer.OfferDealValid(out var invalidReason2);
				invalidReason = invalidReason2;
			}
			catch (Exception ex)
			{
				if (!purchaseWarningShown)
				{
					purchaseWarningShown = true;
					MelonLogger.Warning("Minimap purchase badge query failed: " + ex.GetType().Name + ": " + ex.Message);
				}
			}
		}
		if (showActiveDeals)
		{
			try
			{
				Contract currentContract = customer.CurrentContract;
				activeContract = currentContract != null && currentContract.State == EQuestState.Active;
			}
			catch (Exception ex2)
			{
				if (!contractWarningShown)
				{
					contractWarningShown = true;
					MelonLogger.Warning("Minimap active deal badge query failed: " + ex2.GetType().Name + ": " + ex2.Message);
				}
			}
		}
		return CustomerStatusPolicy.Classify(offerValid, invalidReason, activeContract);
	}
}
