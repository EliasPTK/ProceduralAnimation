@tool
extends Node3D

## Places the pole where the elbow (or knee) "should" naturally sit

@export var targetPos: Node3D
@export var rootPos: Node3D
@export var enabled: bool = false

@export_group("IK Stability")
## Use the axis that NEVER flips during your animation. 
## If X (Left/Right) doesn't flip, keep this as (1, 0, 0) or (-1, 0, 0).
@export var stable_pole_axis: Vector3 = Vector3(1, 0, 0)

@export_group("Anatomy Tweaks")


@export_range(-180, 180) var elbow_twist_degrees: float = -90.0
@export var offset_distance: float = 5.0

func _process(_delta: float) -> void:
	if not enabled:
		return
	if targetPos == null or not is_instance_valid(targetPos):
		return
	if rootPos == null or not is_instance_valid(rootPos):
		return
	if not is_inside_tree() or not targetPos.is_inside_tree() or not rootPos.is_inside_tree():
		return

	var pos_A = targetPos.global_position
	var pos_B = rootPos.global_position
	
	var midpoint = (pos_A + pos_B) / 2.0
	var arm_vector = pos_A - pos_B
	
	if arm_vector.length_squared() < 0.0001:
		global_position = midpoint
		return
		
	# n is the axis the arm is currently pointing along
	var n = arm_vector.normalized()
	
	# 1. Get the stable reference vector
	var p = (rootPos.global_basis * stable_pole_axis).normalized()
	
	# 2. Project it to find the stable elbow direction
	var w = p - (p.dot(n) * n)
	
	if w.length_squared() < 0.0001:
		# Fallback just in case
		if abs(n.dot(Vector3.UP)) > 0.99:
			p = (rootPos.global_basis * Vector3.RIGHT).normalized()
		else:
			p = (rootPos.global_basis * Vector3.UP).normalized()
		w = p - (p.dot(n) * n)

	var e = w.normalized()
	
	# 3. We rotate the stable elbow direction smoothly around the arm vector.

	var twist_rads = deg_to_rad(elbow_twist_degrees)
	var final_elbow_dir = e.rotated(n, twist_rads)
	
	# 4. Final placement
	global_position = midpoint + (final_elbow_dir * offset_distance)
