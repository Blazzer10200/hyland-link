using System;

namespace HylandMinimap;

[Flags]
internal enum CustomerStatus
{
	None = 0,
	AlreadyPurchased = 1,
	ActiveDeal = 2,
	RegionLocked = 4
}
