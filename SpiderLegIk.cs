using Godot;
using System.Collections.Generic;

[Tool]
public partial class SpiderLegIk : Node
{
	[Export] public Skeleton3D TargetSkeleton { get; set; }
	[Export] public Node3D FootTarget { get; set; }

	[ExportGroup("Bone Endpoints")]
	[Export] public string RootBoneName { get; set; } = "Hip_L";
	[Export] public string EndBoneName { get; set; } = "FootTip_L";

	[ExportGroup("IK Settings")]
	[Export] public bool InvertKneeBend { get; set; } = false;
	[Export] public Curve ArchProfileCurve { get; set; }

	private readonly List<int> _boneIndices = new();
	private readonly List<float> _restLengths = new();
	private readonly List<Vector3> _restDirectionsSkel = new();
	private readonly List<Transform3D> _restGlobalPoses = new();

	private float _totalChainLength = 0f;
	private float _virtualUpperLength = 0f;
	private float _virtualLowerLength = 0f;

	public override void _Ready()
	{
		CacheChainFromEndpoints();
	}

	public override void _Process(double delta)
	{
		if (!IsInsideTree() || !GodotObject.IsInstanceValid(TargetSkeleton) || !GodotObject.IsInstanceValid(FootTarget))
			return;

		if (!TargetSkeleton.IsInsideTree() || !FootTarget.IsInsideTree())
			return;

		if (_boneIndices.Count < 2)
		{
			CacheChainFromEndpoints();
			if (_boneIndices.Count < 2) return;
		}

		SolveMultiBoneIK();
	}

	private void CacheChainFromEndpoints()
	{
		_boneIndices.Clear();
		_restLengths.Clear();
		_restDirectionsSkel.Clear();
		_restGlobalPoses.Clear();
		_totalChainLength = 0f;
		_virtualUpperLength = 0f;
		_virtualLowerLength = 0f;

		if (!GodotObject.IsInstanceValid(TargetSkeleton)) return;

		int rootIdx = TargetSkeleton.FindBone(RootBoneName);
		int endIdx = TargetSkeleton.FindBone(EndBoneName);
		if (rootIdx == -1 || endIdx == -1) return;

		List<int> chain = new();
		int currentIdx = endIdx;

		while (currentIdx != -1)
		{
			chain.Add(currentIdx);
			if (currentIdx == rootIdx) break;
			currentIdx = TargetSkeleton.GetBoneParent(currentIdx);
		}

		if (chain.Count == 0 || chain[^1] != rootIdx) return;

		chain.Reverse();
		_boneIndices.AddRange(chain);

		// Pre-compute immutable Rest Transforms in Skeleton Space to prevent infinite rotation feedback
		for (int i = 0; i < _boneIndices.Count; i++)
		{
			int boneIdx = _boneIndices[i];
			Transform3D restSkel = GetRestPoseInSkeletonSpace(boneIdx);
			_restGlobalPoses.Add(restSkel);
		}

		// Cache rest lengths and directions between adjacent bones
		for (int i = 0; i < _boneIndices.Count - 1; i++)
		{
			Vector3 posCurr = _restGlobalPoses[i].Origin;
			Vector3 posNext = _restGlobalPoses[i + 1].Origin;

			Vector3 delta = posNext - posCurr;
			float length = delta.Length();
			if (length < 0.001f) length = 0.2f;

			_restLengths.Add(length);
			_restDirectionsSkel.Add(delta.Normalized());
			_totalChainLength += length;
		}

		// Split chain into Virtual Upper & Lower lengths
		int segmentCount = _restLengths.Count;
		if (segmentCount > 0)
		{
			int midIndex = segmentCount / 2;
			for (int i = 0; i < segmentCount; i++)
			{
				if (i < midIndex || midIndex == 0)
					_virtualUpperLength += _restLengths[i];
				else
					_virtualLowerLength += _restLengths[i];
			}

			if (_virtualLowerLength <= 0.001f)
			{
				_virtualUpperLength = _totalChainLength * 0.5f;
				_virtualLowerLength = _totalChainLength * 0.5f;
			}
		}
	}

	private void SolveMultiBoneIK()
	{
		int rootIdx = _boneIndices[0];

		// 1. Convert Foot Target position to Skeleton Space relative to Root Rest Position
		Transform3D skelGlobalInverse = TargetSkeleton.GlobalTransform.AffineInverse();
		Vector3 rootRestPosSkel = _restGlobalPoses[0].Origin;
		Vector3 targetPosSkel = skelGlobalInverse * FootTarget.GlobalPosition;

		Vector3 localTarget = targetPosSkel - rootRestPosSkel;

		// 2. Project Target into 2D Leg Plane
		float yaw = Mathf.Atan2(localTarget.X, localTarget.Z);
		float flatDist = new Vector2(localTarget.X, localTarget.Z).Length();
		float height = localTarget.Y;

		// 3. Solve 2D Knee Apex
		Vector2 p0 = Vector2.Zero;
		Vector2 p2 = new Vector2(flatDist, height);
		Vector2 p1 = Solve2DKneeApex(flatDist, height);

		// 4. Sample Joint Positions along Curve in Skeleton Space
		int jointCount = _boneIndices.Count;
		Vector3[] jointPositionsSkel = new Vector3[jointCount];
		float accumulatedLength = 0f;

		for (int i = 0; i < jointCount; i++)
		{
			float t = (i == 0) ? 0f : (accumulatedLength / Mathf.Max(_totalChainLength, 0.001f));
			if (i > 0) accumulatedLength += _restLengths[i - 1];

			float u = 1f - t;
			Vector2 point2D = (u * u * p0) + (2f * u * t * p1) + (t * t * p2);

			if (ArchProfileCurve != null)
				point2D.Y *= ArchProfileCurve.Sample(t);

			Vector3 posRelRoot = new Vector3(
				Mathf.Sin(yaw) * point2D.X,
				point2D.Y,
				Mathf.Cos(yaw) * point2D.X
			);

			jointPositionsSkel[i] = rootRestPosSkel + posRelRoot;
		}

		// 5. Apply Orientations deterministically using cached Rest Pose
		int parentOfRoot = TargetSkeleton.GetBoneParent(rootIdx);
		Basis currentParentSkelBasis = (parentOfRoot != -1)
			? GetRestPoseInSkeletonSpace(parentOfRoot).Basis
			: Basis.Identity;

		for (int i = 0; i < _boneIndices.Count - 1; i++)
		{
			int boneIdx = _boneIndices[i];

			Vector3 desiredDirSkel = (jointPositionsSkel[i + 1] - jointPositionsSkel[i]).Normalized();
			Vector3 restDirSkel = _restDirectionsSkel[i];

			if (desiredDirSkel.LengthSquared() < 0.001f || restDirSkel.LengthSquared() < 0.001f)
				continue;

			// Compute desired global orientation in Skeleton space
			Quaternion skelDelta = GetArcRotation(restDirSkel, desiredDirSkel);
			Basis targetSkelBasis = new Basis(skelDelta) * _restGlobalPoses[i].Basis;

			// Convert to local pose rotation relative to parent
			Basis localBasis = currentParentSkelBasis.Inverse() * targetSkelBasis;
			Basis restLocalBasis = TargetSkeleton.GetBoneRest(boneIdx).Basis;
			Basis poseBasis = restLocalBasis.Inverse() * localBasis;

			TargetSkeleton.SetBonePoseRotation(boneIdx, poseBasis.GetRotationQuaternion());

			// Pass target basis down to child bone
			currentParentSkelBasis = targetSkelBasis;
		}
	}

	private Transform3D GetRestPoseInSkeletonSpace(int boneIdx)
	{
		Transform3D restSkel = Transform3D.Identity;
		int current = boneIdx;

		List<int> ancestors = new();
		while (current != -1)
		{
			ancestors.Add(current);
			current = TargetSkeleton.GetBoneParent(current);
		}

		ancestors.Reverse();
		foreach (int idx in ancestors)
		{
			restSkel *= TargetSkeleton.GetBoneRest(idx);
		}

		return restSkel;
	}

	private Vector2 Solve2DKneeApex(float d, float h)
	{
		float targetDist = Mathf.Sqrt(d * d + h * h);
		float a = _virtualUpperLength;
		float b = _virtualLowerLength;

		float clampedDist = Mathf.Clamp(targetDist, Mathf.Abs(a - b) + 0.001f, (a + b) - 0.001f);
		float cosAlpha = (a * a + clampedDist * clampedDist - b * b) / (2f * a * clampedDist);
		float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f));

		float targetPitch = Mathf.Atan2(h, d);
		float apexPitch = targetPitch + (InvertKneeBend ? -alpha : alpha);

		return new Vector2(Mathf.Cos(apexPitch) * a, Mathf.Sin(apexPitch) * a);
	}

	private static Quaternion GetArcRotation(Vector3 from, Vector3 to)
	{
		from = from.Normalized();
		to = to.Normalized();
		float dot = from.Dot(to);

		if (dot >= 0.9999f) return Quaternion.Identity;
		if (dot <= -0.9999f)
		{
			Vector3 ortho = Mathf.Abs(from.Y) < 0.9f ? Vector3.Up : Vector3.Right;
			return new Quaternion(from.Cross(ortho).Normalized(), Mathf.Pi);
		}

		Vector3 axis = from.Cross(to).Normalized();
		float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
		return new Quaternion(axis, angle);
	}
}
