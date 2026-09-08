@tool
class_name FishGenerator
extends Node3D

@export var bone_lengths: Array[float] = [0.5, 0.4, 0.4, 0.3, 0.3, 0.2]
@export var ik_node: Node3D

@export_tool_button("Generate Fish") var generate_button: Callable = _generate_fish

func _generate_fish() -> void:
	if bone_lengths.is_empty():
		push_warning("FishGenerator: Bone lengths array is empty.")
		return

	# 1. Get or create Skeleton3D child
	var skeleton: Skeleton3D = get_node_or_null("FishSkeleton")
	if not skeleton:
		skeleton = Skeleton3D.new()
		skeleton.name = "FishSkeleton"
		add_child(skeleton)
		_set_owner_recursive(skeleton, get_tree().edited_scene_root)
	else:
		skeleton.clear_bones()

	# 2. Add bones in sequence (Extending along +Z towards the tail)
	var generated_names: PackedStringArray = []

	for i in range(bone_lengths.size()):
		var bone_name: String = "Spine_%02d" % i
		generated_names.append(bone_name)

		var bone_idx: int = skeleton.add_bone(bone_name)

		if i > 0:
			skeleton.set_bone_parent(bone_idx, i - 1)
			# Offset from parent by the previous bone's length
			var local_rest := Transform3D(Basis.IDENTITY, Vector3(0, 0, bone_lengths[i - 1]))
			skeleton.set_bone_rest(bone_idx, local_rest)
			skeleton.set_bone_pose_position(bone_idx, local_rest.origin)
		else:
			skeleton.set_bone_rest(bone_idx, Transform3D.IDENTITY)

	# 3. Automatically hook up to the FishSpineIK node
	if ik_node:
		ik_node.set("SkeletonPath", ik_node.get_path_to(skeleton))
		ik_node.set("BoneNames", generated_names)
		
		if ik_node.has_method("ResetIK"):
			ik_node.call("ResetIK")
		elif ik_node.has_method("ForceResolve"):
			ik_node.call("ForceResolve")

	print("FishGenerator: Successfully created %d bones." % bone_lengths.size())

func _set_owner_recursive(node: Node, scene_owner: Node) -> void:
	if scene_owner and node != scene_owner:
		node.owner = scene_owner
	for child in node.get_children():
		_set_owner_recursive(child, scene_owner)
