using Il2CppScheduleOne.Map;
using UnityEngine;

namespace HylandMinimap;

internal readonly struct MapProjection
{
	private readonly MapPositionUtility utility;

	private readonly Transform parent;

	private readonly Vector3 anchorOffset;

	private readonly RectTransform background;

	private readonly Vector2 drawingCenter;

	internal MapProjection(MapPositionUtility utility, Transform parent, Vector3 anchorOffset, RectTransform background, Rect drawingRect)
	{
		this.utility = utility;
		this.parent = parent;
		this.anchorOffset = anchorOffset;
		this.background = background;
		drawingCenter = drawingRect.center;
	}

	internal Vector2 Project(Vector3 world)
	{
		Vector2 mapPosition = utility.GetMapPosition(world);
		Vector3 position = parent.TransformPoint(new Vector3(mapPosition.x, mapPosition.y, 0f) + anchorOffset);
		Vector3 vector = background.InverseTransformPoint(position);
		return new Vector2(vector.x - drawingCenter.x, vector.y - drawingCenter.y);
	}
}
