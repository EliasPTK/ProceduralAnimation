using System;
using System.Collections.Generic;
using Godot;

[Tool]
[GlobalClass]
public partial class FishSpineIK : Node3D
{
	[Export] public NodePath SkeletonPath;
	[Export] public string[] BoneNames = Array.Empty<string>();
	[Export] public NodePath TargetPath;
	[Export] public bool EditorPreviewEnabled = true;

	[ExportGroup("Movement Parameters")]
	[Export] public float MoveSpeed = 3.0f;
	[Export] public float TurnSpeed = 4.0f;
	[Export] public float StoppingDistance = 0.5f;

	[ExportGroup("Ground Adaptation")]
	// Turns
	// the spine from a free-swimming fish into a ground-hugging "snake" that
	// conforms to terrain, arcing over gaps it can't reach.
	[Export] public bool EnableGroundAdaptation = false;
	// Percentage of bones that need to be grounded.
	[Export(PropertyHint.Range, "0,1,0.05")] public float GroundedPercent = 0.6f;
	[Export] public bool GroundAdaptHead = true;
	[Export] public float GroundRayUpOffset = 1.0f;
	[Export] public float GroundRayDownDistance = 3.0f;
	[Export(PropertyHint.Layers3DPhysics)] public uint GroundCollisionMask = 1;
	[Export] public float GroundSurfaceOffset = 0.05f;
	[Export(PropertyHint.Range, "0,30,0.5")] public float GroundSnapSpeed = 10.0f;
	[Export(PropertyHint.Range, "1,5,1")] public int GroundRelaxIterations = 2;

	public float CurrentGroundedRatio { get; private set; } = 0f;

	[ExportToolButton("Reset IK")]
	public Callable ResetButton => Callable.From(ResetIK);

	private Skeleton3D _skeleton;
	private Node3D _target;
	private int[] _boneIndices = Array.Empty<int>();
	private float[] _boneLengths = Array.Empty<float>();
	private Vector3[] _positions = Array.Empty<Vector3>(); // Local skeleton space
	private bool _initialized = false;

	private bool[] _groundHitMask = Array.Empty<bool>();
	private Vector3[] _groundHitPoints = Array.Empty<Vector3>();

	public override void _Ready()
	{
		CallDeferred(nameof(Initialize));
		CallDeferred(nameof(ResetIK));
	}

	public override void _ExitTree()
	{
		ClearOverrides();
		_initialized = false;
	}

	public void ResetIK()
	{
		ClearOverrides();
		_initialized = false;
		Initialize();
	}

	private void Initialize()
	{
		if (_initialized || SkeletonPath == null || SkeletonPath.IsEmpty)
			return;

		_skeleton = GetNodeOrNull<Skeleton3D>(SkeletonPath);
		if (!IsInstanceValid(_skeleton) || !_skeleton.IsInsideTree())
			return;

		if (TargetPath != null && !TargetPath.IsEmpty)
			_target = GetNodeOrNull<Node3D>(TargetPath);

		if (BoneNames == null || BoneNames.Length < 2)
			return;

		var indices = new List<int>();
		var lengths = new List<float>();
		var positions = new List<Vector3>();

		for (int i = 0; i < BoneNames.Length; i++)
		{
			int idx = _skeleton.FindBone(BoneNames[i]);
			if (idx == -1)
			{
				GD.PushError($"FishSpineIK: Bone '{BoneNames[i]}' not found.");
				return;
			}

			indices.Add(idx);
			Vector3 poseOrigin = _skeleton.GetBoneGlobalPoseNoOverride(idx).Origin;
			positions.Add(poseOrigin);

			if (i > 0)
			{
				float dist = positions[i - 1].DistanceTo(poseOrigin);
				lengths.Add(dist);
			}
		}

		_boneIndices = indices.ToArray();
		_boneLengths = lengths.ToArray();
		_positions = positions.ToArray();
		_initialized = true;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() && !EditorPreviewEnabled)
			return;

		if (!_initialized || !IsInstanceValid(_skeleton))
		{
			Initialize();
			if (!_initialized) return;
		}

		// 1. Target Tracking (Move Controller Node)
		if (IsInstanceValid(_target) && _target.IsInsideTree())
		{
			Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
			float dist = toTarget.Length();

			if (dist > StoppingDistance)
			{
				Vector3 dir = toTarget / dist;
				Basis targetBasis = SafeLookingAt(dir, Vector3.Up);

				// Smoothly rotate controller toward target
				Quaternion currentRot = GlobalBasis.Orthonormalized().GetRotationQuaternion();
				Quaternion targetRot = targetBasis.GetRotationQuaternion();
				GlobalBasis = new Basis(currentRot.Slerp(targetRot, (float)delta * TurnSpeed));

				// Move forward (-Z)
				GlobalPosition += -GlobalBasis.Z * Mathf.Min(MoveSpeed * (float)delta, dist - StoppingDistance);
			}
		}

		// 2. FORWARD FABRIK PASS (Strict Rigid Distance Chain)
		// Position Head (Joint 0) at the controller node's local position
		_positions[0] = _skeleton.ToLocal(GlobalPosition);

		int count = _positions.Length;
		for (int i = 1; i < count; i++)
		{
			Vector3 prev = _positions[i - 1];
			Vector3 curr = _positions[i];

			// Get pointing direction from parent to child
			Vector3 dir = (curr - prev).Normalized();
			if (dir.IsZeroApprox())
			{
				dir = Vector3.Back;
			}

			// Lock position to exact rest bone length from parent
			_positions[i] = prev + (dir * _boneLengths[i - 1]);
		}

		// 2.5 GROUND ADAPTATION
		if (EnableGroundAdaptation && !Engine.IsEditorHint())
		{
			ApplyGroundAdaptation((float)delta);
		}

		// 3. Apply positions & rotations to Skeleton
		for (int i = 0; i < _boneIndices.Length - 1; i++)
		{
			Vector3 p1 = _positions[i];
			Vector3 p2 = _positions[i + 1];

			Vector3 lookDir = (p2 - p1).Normalized();
			if (lookDir.IsZeroApprox()) continue;

			Basis boneBasis = SafeLookingAt(lookDir, Vector3.Up);
			Transform3D pose = new Transform3D(boneBasis, p1);

			_skeleton.SetBoneGlobalPoseOverride(_boneIndices[i], pose, 1.0f, true);
		}

		// Tail tip pose
		if (_boneIndices.Length > 0)
		{
			int last = _boneIndices.Length - 1;
			Transform3D lastPose = new Transform3D(Basis.Identity, _positions[last]);
			_skeleton.SetBoneGlobalPoseOverride(_boneIndices[last], lastPose, 1.0f, true);
		}
	}

	// Bends the rigid chain toward the ground wherever ground is reachable,
	// leaving joints over a gap alone so they arc between their grounded
	// neighbors instead of clipping underground or stretching the chain.
	private void ApplyGroundAdaptation(float delta)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		int count = _positions.Length;

		if (_groundHitMask.Length != count)
		{
			_groundHitMask = new bool[count];
			_groundHitPoints = new Vector3[count];
		}

		// 1. Raycast every joint straight down to find candidate ground height.
		int groundableCount = 0;
		int startIndex = GroundAdaptHead ? 0 : 1;
		for (int i = 0; i < count; i++)
		{
			if (i < startIndex)
			{
				_groundHitMask[i] = false;
				continue;
			}

			Vector3 worldPos = _skeleton.GlobalTransform * _positions[i];
			Vector3 rayFrom = worldPos + Vector3.Up * GroundRayUpOffset;
			Vector3 rayTo = worldPos - Vector3.Up * GroundRayDownDistance;

			var query = PhysicsRayQueryParameters3D.Create(rayFrom, rayTo);
			query.CollisionMask = GroundCollisionMask;

			var hit = spaceState.IntersectRay(query);
			if (hit.Count > 0)
			{
				_groundHitMask[i] = true;
				_groundHitPoints[i] = (Vector3)hit["position"];
				groundableCount++;
			}
			else
			{
				_groundHitMask[i] = false;
			}
		}

		CurrentGroundedRatio = count > 0 ? (float)groundableCount / count : 0f;
		

		// 2. Pull every groundable joint's height toward its hit point,
		//    then re-enforce rigid bone lengths so the nudge doesn't stretch
		//    the chain. Kinda like Fabrik again, but not really.
		for (int iter = 0; iter < GroundRelaxIterations; iter++)
		{
			for (int i = 0; i < count; i++)
			{
				if (!_groundHitMask[i]) continue;

				Vector3 worldPos = _skeleton.GlobalTransform * _positions[i];
				float targetY = _groundHitPoints[i].Y + GroundSurfaceOffset;
				float weight = Mathf.Clamp(GroundSnapSpeed * delta, 0f, 1f);
				worldPos.Y = Mathf.Lerp(worldPos.Y, targetY, weight);
				_positions[i] = _skeleton.GlobalTransform.AffineInverse() * worldPos;
			}

			for (int i = 1; i < count; i++)
			{
				Vector3 prev = _positions[i - 1];
				Vector3 curr = _positions[i];
				Vector3 dir = (curr - prev).Normalized();
				if (dir.IsZeroApprox()) dir = Vector3.Back;
				_positions[i] = prev + (dir * _boneLengths[i - 1]);
			}
		}
	}

	private static Basis SafeLookingAt(Vector3 dir, Vector3 defaultUp)
	{
		Vector3 normDir = dir.IsZeroApprox() ? Vector3.Forward : dir.Normalized();
		Vector3 up = defaultUp.Normalized();

		if (Mathf.Abs(normDir.Dot(up)) > 0.98f)
			up = Mathf.Abs(normDir.Dot(Vector3.Right)) < 0.98f ? Vector3.Right : Vector3.Forward;

		return Basis.LookingAt(normDir, up).Orthonormalized();
	}

	private void ClearOverrides()
	{
		if (IsInstanceValid(_skeleton) && _initialized && _boneIndices != null)
		{
			foreach (int idx in _boneIndices)
				_skeleton.SetBoneGlobalPoseOverride(idx, Transform3D.Identity, 0.0f, false);
		}
	}
}
