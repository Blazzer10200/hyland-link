using UnityEngine;

namespace HylandMinimap;

internal readonly record struct WorldMarkerSource(int Id, WorldMarkerKind Kind, Object Target, Transform Tracking, string Name, Sprite? Portrait);
