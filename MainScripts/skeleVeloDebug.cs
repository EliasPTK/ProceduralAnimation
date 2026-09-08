using Godot;
using System.Collections.Generic;

[Tool]
public partial class skeleVeloDebug : skeleDebug
{
	// The velocity at which the color reaches maximum Red or Blue
	[Export] public float MaxVelocityTarget { get; set; } = 2.0f;

	private Dictionary<int, float> _prevDisplacements = new Dictionary<int, float>();
	private double _currentDelta = 0.016; // Safe fallback

	public override void _Process(double delta)
	{
		base._Process(delta);
		_currentDelta = delta;
	}

	protected override void RedrawSkeletonBones()
	{
		InitializeResources();

		if (_targetSkeleton == null) return;
		int boneCount = _targetSkeleton.GetBoneCount();
		if (boneCount == 0) return;

		SurfaceTool st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		Aabb customBounds = new Aabb();

		// Map hierarchy
		Dictionary<int, List<int>> boneChildrenMap = new Dictionary<int, List<int>>();
		for (int i = 0; i < boneCount; i++)
		{
			int parentIdx = _targetSkeleton.GetBoneParent(i);
			if (parentIdx != -1)
			{
				if (!boneChildrenMap.TryGetValue(parentIdx, out List<int> children))
				{
					children = new List<int>();
					boneChildrenMap[parentIdx] = children;
				}
				children.Add(i);
			}
		}

		for (int i = 0; i < boneCount; i++)
		{
			Transform3D globalPose = _targetSkeleton.GetBoneGlobalPose(i);
			int parentIndex = _targetSkeleton.GetBoneParent(i);
			
			Transform3D currentPose = parentIndex != -1 
				? _targetSkeleton.GetBoneGlobalPose(parentIndex).AffineInverse() * globalPose 
				: globalPose;

			Transform3D restPose = _targetSkeleton.GetBoneRest(i);

			// Calculate overall displacement
			float posOffset = currentPose.Origin.DistanceTo(restPose.Origin);
			float rotOffset = currentPose.Basis.GetRotationQuaternion().AngleTo(restPose.Basis.GetRotationQuaternion());
			
			float posFactor = Mathf.Clamp(posOffset / Mathf.Max(MaxPositionDistance, 0.001f), 0.0f, 1.0f);
			float rotFactor = Mathf.Clamp(rotOffset / Mathf.Max(MaxRotationAngle, 0.001f), 0.0f, 1.0f);
			float currentDisplacement = Mathf.Max(posFactor, rotFactor);

			// Calculate Velocity
			float prevDisplacement = _prevDisplacements.GetValueOrDefault(i, currentDisplacement);
			float velocity = (currentDisplacement - prevDisplacement) / Mathf.Max((float)_currentDelta, 0.001f);
			_prevDisplacements[i] = currentDisplacement;

			// Map velocity to Red/Gray/Blue
			float normalizedVel = Mathf.Clamp(velocity / MaxVelocityTarget, -1.0f, 1.0f);
			
			Color greyColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
			Color redColor = new Color(1.0f, 0.1f, 0.1f, 1.0f);
			Color blueColor = new Color(0.1f, 0.3f, 1.0f, 1.0f);
			
			Color boneColor = normalizedVel > 0 
				? greyColor.Lerp(redColor, normalizedVel) 
				: greyColor.Lerp(blueColor, Mathf.Abs(normalizedVel));

			// Draw meshes
			if (boneChildrenMap.TryGetValue(i, out List<int> childIndices))
			{
				Vector3 startPos = ToLocal(_targetSkeleton.ToGlobal(globalPose.Origin));
				foreach (int childIdx in childIndices)
				{
					Vector3 endPos = ToLocal(_targetSkeleton.ToGlobal(_targetSkeleton.GetBoneGlobalPose(childIdx).Origin));
					if (startPos.DistanceTo(endPos) < 0.001f) continue;

					AddConeBone(st, startPos, endPos, BoneBaseWidth, boneColor);
					customBounds = customBounds.Expand(startPos).Expand(endPos);
				}
			}
		}

		_arrayMesh.ClearSurfaces();
		st.Commit(_arrayMesh);
		CustomAabb = customBounds.Grow(2.0f);
	}
}
