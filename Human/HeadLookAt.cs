using Godot;
using System;

[Tool]
public partial class HeadLookAt : Node3D
{
	[Export] public Skeleton3D Skeleton;
	[Export] public string BoneName = "Head";
	[Export] public Node3D Target;
	[Export] public bool IsEnabled = true;

	[ExportGroup("Orientation")]
	// The local axis of the bone that should point at the target.
	// Godot's default forward is (0, 0, -1). Modify this based on your rig's bone axes!
	[Export] public Vector3 BoneForwardAxis = new Vector3(0, 0, -1);
	
	[ExportGroup("Limits (Degrees)")]
	// Min angles (Pitch, Yaw, Roll)
	[Export] public Vector3 MinAngles = new Vector3(-45, -60, -20);
	// Max angles (Pitch, Yaw, Roll)
	[Export] public Vector3 MaxAngles = new Vector3(45, 60, 20);

	[ExportGroup("Smoothing")]
	[Export] public float LerpSpeed = 10f;

	private int _boneIdx = -1;
	private Quaternion _currentLocalQuat;
	private bool _isInitialized = false;

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		Initialize();
	}

	private void Initialize()
	{
		if (Skeleton != null && !string.IsNullOrEmpty(BoneName))
		{
			_boneIdx = Skeleton.FindBone(BoneName);
			if (_boneIdx != -1)
			{
				// Start smoothing from the bone's default rest pose
				_currentLocalQuat = new Quaternion(Skeleton.GetBoneRest(_boneIdx).Basis);
				_isInitialized = true;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (!IsEnabled || Target == null || !IsInstanceValid(Target) || Skeleton == null) return;
		
		if (!_isInitialized)
		{
			Initialize();
			if (!_isInitialized) return;
		}

		// 1. Get the bone's position in Global World Space
		Transform3D boneGlobalPose = Skeleton.GetBoneGlobalPose(_boneIdx);
		Vector3 boneWorldPos = Skeleton.ToGlobal(boneGlobalPose.Origin);

		// 2. Get Global Direction
		Vector3 globalDir = boneWorldPos.DirectionTo(Target.GlobalPosition);

		// Prevent alignment crashes if target is directly above/below
		Vector3 up = Vector3.Up;
		if (Mathf.Abs(globalDir.Dot(up)) > 0.99f) 
		{
			up = Vector3.Right;
		}

		// 3. Calculate raw global look rotation (LookingAt points -Z at target)
		Basis worldLookBasis = Basis.LookingAt(globalDir, up);

		// 4. Compensate for custom bone forward axis
		// Vector3.Forward in Godot is -Z. This maps your chosen axis to point at the target.
		Quaternion axisCorrectionQuat = new Quaternion(BoneForwardAxis.Normalized(), Vector3.Forward);
		worldLookBasis = worldLookBasis * new Basis(axisCorrectionQuat);

		// 5. Convert World Basis to the Bone's Parent Space
		// We must apply limits relative to the neck/chest, not global world space!
		int parentIdx = Skeleton.GetBoneParent(_boneIdx);
		Basis parentWorldBasis = Skeleton.GlobalBasis;
		if (parentIdx >= 0)
		{
			parentWorldBasis = Skeleton.GlobalBasis * Skeleton.GetBoneGlobalPose(parentIdx).Basis;
		}
		
		// Inverse parent basis shifts our target rotation into local bone space
		Basis targetLocalBasis = parentWorldBasis.Inverse() * worldLookBasis;

		// 6. Extract Euler angles (Pitch, Yaw, Roll)
		Vector3 targetEuler = targetLocalBasis.GetEuler();
		Vector3 restEuler = Skeleton.GetBoneRest(_boneIdx).Basis.GetEuler();

		// 7. Clamp angles relative to the rest pose
		targetEuler.X = ClampEulerAxis(targetEuler.X, restEuler.X, MinAngles.X, MaxAngles.X);
		targetEuler.Y = ClampEulerAxis(targetEuler.Y, restEuler.Y, MinAngles.Y, MaxAngles.Y);
		targetEuler.Z = ClampEulerAxis(targetEuler.Z, restEuler.Z, MinAngles.Z, MaxAngles.Z);

		// 8. Apply smoothing (Slerp)
		Quaternion clampedTargetQuat = Quaternion.FromEuler(targetEuler);
		
		if (Engine.IsEditorHint())
		{
			_currentLocalQuat = clampedTargetQuat;
		}
		else
		{
			_currentLocalQuat = _currentLocalQuat.Slerp(clampedTargetQuat, (float)(LerpSpeed * delta));
		}

		// 9. Apply the final rotation override to the skeleton bone
		Skeleton.SetBonePoseRotation(_boneIdx, _currentLocalQuat);
	}

	private float ClampEulerAxis(float targetAngle, float restAngle, float minDeg, float maxDeg)
	{
		float angleDiff = Mathf.AngleDifference(restAngle, targetAngle);
		float clampedDiff = Mathf.Clamp(angleDiff, Mathf.DegToRad(minDeg), Mathf.DegToRad(maxDeg));
		return restAngle + clampedDiff;
	}
}
