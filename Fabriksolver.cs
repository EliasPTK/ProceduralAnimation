using System;
using System.Collections.Generic;
using Godot;


/*
Minimal FABRIK (Forward And Backward Reaching Inverse Kinematics) solver.

Raw Vector3 Fabrik solver

Usage:
var solver = new Fabriksolver();
solver.Initialize(initialJointPositions);
solver.Solve(target);
Vector3[] result = solver.Joints;
*/

[GlobalClass]
public partial class Fabriksolver : RefCounted
{
	// Joints[0] is the root (fixed to the body), Joints[^1] is the tip (foot).
	public Vector3[] Joints { get; private set; }

	// Distance between each consecutive pair of joints. BoneLengths[i] is the
	// length of the bone between Joints[i] and Joints[i + 1].
	private float[] _boneLengths;
	private float _totalLength;

	// Stop iterating once the tip is within this distance of the target.
	private const float ToleranceMeters = 0.01f;
	private const int MaxIterations = 10;

	/*
	Sets up the chain from a starting pose. Bone lengths are captured here
	and treated as fixed for the lifetime of the solver.
	*/
	public void Initialize(Vector3[] initialJoints)
	{
		if (initialJoints == null || initialJoints.Length < 2)
			throw new ArgumentException("FABRIK needs at least 2 joints (root + tip).");

		Joints = new Vector3[initialJoints.Length];
		for (int i = 0; i < initialJoints.Length; i++)
			Joints[i] = initialJoints[i];

		_boneLengths = new float[Joints.Length - 1];
		_totalLength = 0f;
		for (int i = 0; i < _boneLengths.Length; i++)
		{
			_boneLengths[i] = Joints[i].DistanceTo(Joints[i + 1]);
			_totalLength += _boneLengths[i];
		}
	}

	/*
	Solves the chain towards the given target position
	Does not return the new position but changes the joints in place
	Returns the number of iterations needed
	*/
	public int Solve(Vector3 target)
	{
		Vector3 root = Joints[0];
		int tipIndex = Joints.Length - 1;

		// Step 0: reachability check
		float rootToTarget = root.DistanceTo(target);
		if (rootToTarget > _totalLength)
		{
			StretchTowardTarget(root, target);
			return 0;
		}

		int iterations = 0;
		while (Joints[tipIndex].DistanceTo(target) > ToleranceMeters && iterations < MaxIterations)
		{
			BackwardPass(target);
			ForwardPass(root);
			iterations++;
		}

		return iterations;
	}

	// Tip snaps onto the target, then walk backward toward the root,
	// preserving each bone length but keeping the direction from the
	// previous joint.
	private void BackwardPass(Vector3 target)
	{
		int tipIndex = Joints.Length - 1;
		Joints[tipIndex] = target;

		for (int i = tipIndex - 1; i >= 0; i--)
		{
			Vector3 direction = (Joints[i] - Joints[i + 1]).Normalized();
			Joints[i] = Joints[i + 1] + direction * _boneLengths[i];
		}
	}

	// Root snaps back to its true fixed position, then walk forward
	// toward the tip, re-enforcing bone lengths again.
	private void ForwardPass(Vector3 root)
	{
		Joints[0] = root;

		for (int i = 1; i < Joints.Length; i++)
		{
			Vector3 direction = (Joints[i] - Joints[i - 1]).Normalized();
			Joints[i] = Joints[i - 1] + direction * _boneLengths[i - 1];
		}
	}

	// If the target is too far then the joints should point in a straight line
	// to target
	private void StretchTowardTarget(Vector3 root, Vector3 target)
	{
		Vector3 direction = (target - root).Normalized();
		Joints[0] = root;
		for (int i = 1; i < Joints.Length; i++)
			Joints[i] = Joints[i - 1] + direction * _boneLengths[i - 1];
	}
}
