using System;
using Godot;


/*
Drives a single FABRIK leg chain against a Skeleton3D.

*/

[Tool]
[GlobalClass]
public partial class Fabriklegchain : Node3D
{
	[Export] public NodePath SkeletonPath;
	[Export] public string[] BoneNames = Array.Empty<string>();
	[Export] public NodePath TargetPath;
	[Export] public bool EditorPreviewEnabled = true;

	[Export] public Node3D pole;


	[ExportToolButton("Rerun setup")]
	public Callable RerunSetupButton => Callable.From(ForceResolve);

	private Skeleton3D _skeleton;
	private Node3D _target;
	private int[] _boneIndices;
	private Fabriksolver _solver;
	private bool _resolved;

	public override void _Ready()
	{
		CallDeferred(nameof(TryResolve));
	}

	public override void _ExitTree()
	{
		_resolved = false;
	}

	// Clears the resolved state and immediately re-resolves everything.
	// Public so a parent-level manager script can trigger this on every
	// leg at once, instead of clicking each leg's own button individually.
	public void ForceResolve()
	{
		_resolved = false;
		TryResolve();
	}

	// Attempts to resolve node references and bone indices. Safe to call
	// repeatedly
	private void TryResolve()
	{
		if (_resolved)
			return;

		_skeleton = GetNodeOrNull<Skeleton3D>(SkeletonPath);
		_target = GetNodeOrNull<Node3D>(TargetPath);

		if (!IsInstanceValid(_skeleton))
		{
			GD.Print($"[{Name}] TryResolve: skeleton not valid (path='{SkeletonPath}')");
			return;
		}
		if (!IsInstanceValid(_target))
		{
			GD.Print($"[{Name}] TryResolve: target not valid (path='{TargetPath}')");
			return;
		}
		if (BoneNames.Length < 2)
		{
			GD.Print($"[{Name}] TryResolve: BoneNames.Length = {BoneNames.Length} (need >= 2)");
			return;
		}

		if (!IsInsideTree() || !_skeleton.IsInsideTree() || !_target.IsInsideTree())
		{
			GD.Print($"[{Name}] TryResolve: tree not settled yet (self={IsInsideTree()}, skeleton={_skeleton.IsInsideTree()}, target={_target.IsInsideTree()})");
			return;
		}

		_boneIndices = new int[BoneNames.Length];
		for (int i = 0; i < BoneNames.Length; i++)
		{
			_boneIndices[i] = _skeleton.FindBone(BoneNames[i]);
			if (_boneIndices[i] == -1)
			{
				GD.PushError($"FabrikLegChain: bone '{BoneNames[i]}' not found on {_skeleton.Name}");
				return; // Try again next frame in case the setup is mid-edit.
			}
		}

		_solver = new Fabriksolver();
		_solver.Initialize(GetCurrentJointPositions());
		_resolved = true;
		GD.Print($"[{Name}] TryResolve: succeeded.");
	}

	// Reads each bone's current global position, ignoring IK
	// override from last frame, so the root tracks any animation or
	// movement happening above this chain in the hierarchy.
	private Vector3[] GetCurrentJointPositions()
	{
		Vector3[] positions = new Vector3[_boneIndices.Length];
		for (int i = 0; i < _boneIndices.Length; i++)
			positions[i] = _skeleton.GetBoneGlobalPoseNoOverride(_boneIndices[i]).Origin;
		return positions;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() && !EditorPreviewEnabled)
			return;

		if (!_resolved || !IsInstanceValid(_skeleton) || !IsInstanceValid(_target))
		{
			_resolved = false;
			TryResolve();
			if (!_resolved)
				return;
		}

		// Same reasoning as in TryResolve: valid references can still be
		// momentarily out of the tree (editor paste/undo/duplicate), and
		// querying global transforms in that state spams is_inside_tree
		// warnings instead of throwing, so skip the frame instead.
		if (!IsInsideTree() || !_skeleton.IsInsideTree() || !_target.IsInsideTree())
			return;

		Vector3[] currentPositions = GetCurrentJointPositions();

		// Re-initializes the solver each frame
		_solver.Initialize(currentPositions);

		//Converts globaltransform of target position into local transform relative to the skeleton
		Vector3 targetInSkeletonSpace = _skeleton.GlobalTransform.AffineInverse() * _target.GlobalPosition;
		_solver.Solve(targetInSkeletonSpace);
		
		if(pole != null && IsInstanceValid(pole) && pole.IsInsideTree())
		{
			Vector3 poleInSkeletonSpace = _skeleton.GlobalTransform.AffineInverse() * pole.GlobalPosition;
			PullCurveToMagnet(_solver.Joints, poleInSkeletonSpace);
		}


		ApplyToSkeleton(currentPositions, _solver.Joints);
	}

	// Converts solved joint positions into bone global pose overrides.
	private void ApplyToSkeleton(Vector3[] oldPositions, Vector3[] solvedPositions)
	{
		if (!IsInstanceValid(_skeleton))
			return;

		for (int i = 0; i < _boneIndices.Length - 1; i++)
		{	

			//gets the direction from this bone to the next bone pre and post IK
			Vector3 oldDirection = (oldPositions[i + 1] - oldPositions[i]).Normalized();
			Vector3 newDirection = (solvedPositions[i + 1] - solvedPositions[i]).Normalized();

			// A zero-length direction means that bones have the same location, this means
			// they are skipped to avoid errors
			if (oldDirection.IsZeroApprox() || newDirection.IsZeroApprox())
				continue;

			//change in rot
			Quaternion deltaRotation = new Quaternion(oldDirection, newDirection);

			//Fun little math fact, the reason we dont set the rotation of the bone just directly
			//to the new direction is because both the old direction, and new direction dont care/know
			//about the twist currently applied to the skeleton, if we just set the direction
			//we risk weirdly twisting the bone. This next section pretty much adds on the change in rotation
			//without possibly twisting the bone

			//yay quaternions
			Quaternion currentRotation = _skeleton.GetBoneGlobalPoseNoOverride(_boneIndices[i]).Basis.GetRotationQuaternion();
			Quaternion newRotation = deltaRotation * currentRotation;

			Transform3D newPose = new Transform3D(new Basis(newRotation), solvedPositions[i]);
			_skeleton.SetBoneGlobalPoseOverride(_boneIndices[i], newPose, 1.0f, true);
		}

		// Tip bone should just point at target
		int tipIndex = _boneIndices.Length - 1;
		Basis tipRotation = _skeleton.GetBoneGlobalPoseNoOverride(_boneIndices[tipIndex]).Basis;
		Transform3D tipPose = new Transform3D(tipRotation, solvedPositions[tipIndex]);
		_skeleton.SetBoneGlobalPoseOverride(_boneIndices[tipIndex], tipPose, 1.0f, true);
	}

	

	public static void PullCurveToMagnet(Vector3[] curvePoints, Vector3 posC)
	{	
		if(curvePoints.Length < 3) return;
			
		Vector3 posA = curvePoints[0];
		Vector3 posB = curvePoints[^1];


		Vector3 axis = (posB - posA).Normalized();
		if (axis.IsZeroApprox()) return;

		// Vector from axis to magnet point C
		Vector3 rc = (posC - posA) - (posC - posA).Project(axis);
		if (rc.IsZeroApprox()) return; // C lies directly on the AB axis

		float sumX = 0f;
		float sumY = 0f;

		// 1. Accumulate components to find the optimal rotation angle
		for (int i = 1; i < curvePoints.Length - 1; i++) // Skip A (0) and B (last)
		{
			Vector3 ri = (curvePoints[i] - posA) - (curvePoints[i] - posA).Project(axis);
			if (ri.IsZeroApprox()) continue;

			sumX += ri.Dot(rc);
			sumY += (axis.Cross(ri)).Dot(rc);
		}

		// Optimal angle theta
		float theta = Mathf.Atan2(sumY, sumX);
		if (Mathf.IsZeroApprox(theta)) return;

		// 2. Construct the rotation quaternion around axis AB
		Quaternion rotation = new Quaternion(axis, theta);

		// 3. Rotate intermediate points around point A
		for (int i = 1; i < curvePoints.Length - 1; i++)
		{
			Vector3 localPoint = curvePoints[i] - posA;
			curvePoints[i] = posA + rotation * localPoint;
		}
	}
}
