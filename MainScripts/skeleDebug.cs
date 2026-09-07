using Godot;
using System.Collections.Generic;

[Tool]
public partial class skeleDebug : MeshInstance3D
{
	private Skeleton3D _targetSkeleton;

	[Export]
	public Skeleton3D TargetSkeleton
	{
		get => _targetSkeleton;
		set
		{
			if (_targetSkeleton != value)
			{
				UnsubscribeFromSkeleton();
				_targetSkeleton = value;
				SubscribeToSkeleton();
				RequestRedraw();
			}
		}
	}

	[Export] public bool EnableDebugVisualizer { get; set; } = true;
	[Export(PropertyHint.Range, "0.01, 0.3, 0.005")] public float BoneBaseWidth { get; set; } = 0.05f;

	[Export] public float MaxPositionDistance { get; set; } = 0.5f;
	[Export] public float MaxRotationAngle { get; set; } = 1.5708f;

	private ArrayMesh _arrayMesh;
	private OrmMaterial3D _debugMaterial;

	public override void _Ready()
	{
		InitializeResources();
		SetProcessInternal(true); // Enables NotificationInternalProcess for post-IK execution timing
	}

	public override void _EnterTree()
	{
		InitializeResources();

		if (TargetSkeleton == null && GetParent() is Skeleton3D parentSkel)
		{
			TargetSkeleton = parentSkel;
		}
		else
		{
			SubscribeToSkeleton();
		}
	}

	public override void _ExitTree()
	{
		UnsubscribeFromSkeleton();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			UnsubscribeFromSkeleton();
		}
		base.Dispose(disposing);
	}

	private void SubscribeToSkeleton()
	{
		if (!IsInstanceValid(_targetSkeleton)) return;

		StringName signalName = Skeleton3D.SignalName.SkeletonUpdated;
		Callable callable = new Callable(this, MethodName.OnSkeletonPoseUpdated);

		if (!_targetSkeleton.IsConnected(signalName, callable))
		{
			_targetSkeleton.Connect(signalName, callable);
		}
	}

	private void UnsubscribeFromSkeleton()
	{
		if (!IsInstanceValid(_targetSkeleton)) return;

		StringName signalName = Skeleton3D.SignalName.SkeletonUpdated;
		Callable callable = new Callable(this, MethodName.OnSkeletonPoseUpdated);

		if (_targetSkeleton.IsConnected(signalName, callable))
		{
			_targetSkeleton.Disconnect(signalName, callable);
		}
	}

	private void InitializeResources()
	{
		if (!IsInstanceValid(this)) return;

		_arrayMesh ??= new ArrayMesh();
		Mesh = _arrayMesh;

		_debugMaterial ??= new OrmMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			//NoDepthTest = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		MaterialOverride = _debugMaterial;
	}

	private void OnSkeletonPoseUpdated()
	{
		RequestRedraw();
	}

	public override void _Notification(int what)
	{
		// Internal Process runs late in frame execution, capturing post-SkeletonModifier3D transforms
		if (what == NotificationInternalProcess)
		{
			if (!IsInstanceValid(this)) return;

			if (TargetSkeleton == null && GetParent() is Skeleton3D parentSkel)
			{
				TargetSkeleton = parentSkel;
				SubscribeToSkeleton();
			}

			RequestRedraw();
		}
	}

	public override void _Process(double delta)
	{
		if (!IsInstanceValid(this)) return;

		if (Engine.IsEditorHint())
		{
			RequestRedraw();
		}
	}

	private void RequestRedraw()
	{
		if (!IsInstanceValid(_targetSkeleton) || !EnableDebugVisualizer)
		{
			if (_arrayMesh != null && _arrayMesh.GetSurfaceCount() > 0)
			{
				_arrayMesh.ClearSurfaces();
			}
			return;
		}

		RedrawSkeletonBones();
	}

	private void RedrawSkeletonBones()
	{
		InitializeResources();

		int boneCount = _targetSkeleton.GetBoneCount();
		if (boneCount == 0) return;

		SurfaceTool st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		Aabb customBounds = new Aabb();

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
			// Derive actual post-IK local pose from global poses
			Transform3D globalPose = _targetSkeleton.GetBoneGlobalPose(i);
			int parentIndex = _targetSkeleton.GetBoneParent(i);
			
			Transform3D currentPose;
			if (parentIndex != -1)
			{
				Transform3D parentGlobalPose = _targetSkeleton.GetBoneGlobalPose(parentIndex);
				currentPose = parentGlobalPose.AffineInverse() * globalPose;
			}
			else
			{
				currentPose = globalPose;
			}

			Transform3D restPose = _targetSkeleton.GetBoneRest(i);

			// Calculate deviation from rest pose
			float posOffset = currentPose.Origin.DistanceTo(restPose.Origin);
			float rotOffset = currentPose.Basis.GetRotationQuaternion().AngleTo(restPose.Basis.GetRotationQuaternion());

			float posFactor = Mathf.Clamp(posOffset / Mathf.Max(MaxPositionDistance, 0.001f), 0.0f, 1.0f);
			float rotFactor = Mathf.Clamp(rotOffset / Mathf.Max(MaxRotationAngle, 0.001f), 0.0f, 1.0f);

			float displacementFactor = Mathf.Max(posFactor, rotFactor);

			Color greyColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
			Color redColor = new Color(1.0f, 0.1f, 0.1f, 1.0f);
			Color boneColor = greyColor.Lerp(redColor, displacementFactor);

			if (boneChildrenMap.TryGetValue(i, out List<int> childIndices))
			{
				Vector3 startPos = ToLocal(_targetSkeleton.ToGlobal(_targetSkeleton.GetBoneGlobalPose(i).Origin));

				foreach (int childIdx in childIndices)
				{
					Vector3 endPos = ToLocal(_targetSkeleton.ToGlobal(_targetSkeleton.GetBoneGlobalPose(childIdx).Origin));

					if (startPos.DistanceTo(endPos) < 0.001f) continue;

					AddConeBone(st, startPos, endPos, BoneBaseWidth, boneColor);

					customBounds = customBounds.Expand(startPos);
					customBounds = customBounds.Expand(endPos);
				}
			}
		}

		_arrayMesh.ClearSurfaces();
		st.Commit(_arrayMesh);

		CustomAabb = customBounds.Grow(2.0f);
	}

	private static void AddConeBone(SurfaceTool st, Vector3 start, Vector3 end, float baseWidth, Color color)
	{
		Vector3 dir = end - start;
		float boneLength = dir.Length();
		if (boneLength < 0.001f) return;

		Vector3 normDir = dir / boneLength;
		Vector3 up = Mathf.Abs(normDir.Y) > 0.99f ? Vector3.Right : Vector3.Up;
		Vector3 side = normDir.Cross(up).Normalized() * baseWidth;
		up = side.Cross(normDir).Normalized() * baseWidth;

		Vector3 basePoint = start + (normDir * (boneLength * 0.2f));

		Vector3 p0 = start;
		Vector3 p1 = end;

		Vector3 b1 = basePoint + side;
		Vector3 b2 = basePoint + up;
		Vector3 b3 = basePoint - side;
		Vector3 b4 = basePoint - up;

		st.SetColor(color);

		st.AddVertex(p0); st.AddVertex(b2); st.AddVertex(b1);
		st.AddVertex(p0); st.AddVertex(b3); st.AddVertex(b2);
		st.AddVertex(p0); st.AddVertex(b4); st.AddVertex(b3);
		st.AddVertex(p0); st.AddVertex(b1); st.AddVertex(b4);

		st.AddVertex(p1); st.AddVertex(b1); st.AddVertex(b2);
		st.AddVertex(p1); st.AddVertex(b2); st.AddVertex(b3);
		st.AddVertex(p1); st.AddVertex(b3); st.AddVertex(b4);
		st.AddVertex(p1); st.AddVertex(b4); st.AddVertex(b1);
	}
}
