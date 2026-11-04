using Il2CppScheduleOne.Map;
using UnityEngine;

namespace HylandMinimap;

internal readonly struct MapProjection
{
	private readonly MapPositionUtility utility;

	private readonly Vector3 anchorOffset;

	/// <summary>parent's local space → background's local space: what parent.TransformPoint then
	/// background.InverseTransformPoint did per marker, as one matrix built once per frame.</summary>
	private readonly Matrix4x4 toBackground;

	private readonly Vector2 drawingCenter;

	internal MapProjection(MapPositionUtility utility, Transform parent, Vector3 anchorOffset, RectTransform background, Rect drawingRect)
	{
		this.utility = utility;
		this.anchorOffset = anchorOffset;
		toBackground = background.worldToLocalMatrix * parent.localToWorldMatrix;
		drawingCenter = drawingRect.center;
	}

	internal Vector2 Project(Vector3 world)
	{
		Vector2 mapPosition = utility.GetMapPosition(world);
		// Only x and y are used; the math is by hand because a Matrix4x4 method is a call into the game here.
		Matrix4x4 m = toBackground;
		float x = mapPosition.x + anchorOffset.x;
		float y = mapPosition.y + anchorOffset.y;
		float z = anchorOffset.z;
		float bx = m.m00 * x + m.m01 * y + m.m02 * z + m.m03;
		float by = m.m10 * x + m.m11 * y + m.m12 * z + m.m13;
		return new Vector2(bx - drawingCenter.x, by - drawingCenter.y);
	}
}
