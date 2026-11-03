using System;

namespace HylandMinimap;

internal static class CustomerStatusPolicy
{
	internal const string RecentPurchaseReason = "Customer recently completed a deal";

	internal static bool? ClassifyRegionLock(bool? relationUnlocked, bool? regionUnlocked)
	{
		if (relationUnlocked == true || regionUnlocked == true)
		{
			return false;
		}
		if (!relationUnlocked.HasValue || !regionUnlocked.HasValue)
		{
			return null;
		}
		return true;
	}

	internal static CustomerStatus Classify(bool offerValid, string? invalidReason, bool activeContract)
	{
		CustomerStatus customerStatus = (activeContract ? CustomerStatus.ActiveDeal : CustomerStatus.None);
		if (!offerValid && string.Equals(invalidReason, RecentPurchaseReason, StringComparison.Ordinal))
		{
			customerStatus |= CustomerStatus.AlreadyPurchased;
		}
		return customerStatus;
	}
}
