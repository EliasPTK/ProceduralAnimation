@tool
class_name RunnerTargetDriver
extends Node3D

@export var move_targets: bool = true

@export var target_left: Node3D
@export var target_right: Node3D

@export_group("Gait Settings")
@export var speed: float = 6.0
@export var radius_z: float = 2.375  ## Radius of stride length
@export var radius_y: float = 0.5    ## Step height lift
@export var center_z: float = -0.125 ## Midpoint offset between 2.25 and -2.5

@export_tool_button("Re-initialize Base Angles/X") var reinit_button: Callable = func(): setup_base_positions()
@export_tool_button("Reset Targets to Default") var reset_button: Callable = func(): reset_targets_to_default()

var _time: float = 0.0
var _left_x: float = 0.0
var _right_x: float = 0.0
var _base_y: float = 0.0


@export var _default_left_pos: Vector3
@export var _default_right_pos: Vector3
var _defaults_captured: bool = false

func _ready() -> void:
	capture_defaults()
	setup_base_positions()
	$Skeleton3D2.reset_bone_poses()

func capture_defaults() -> void:
	if target_left and target_right:
		_default_left_pos = target_left.position
		_default_right_pos = target_right.position
		_defaults_captured = true

func reset_targets_to_default() -> void:
	if not _defaults_captured:
		capture_defaults()
	
	if target_left:
		target_left.position = _default_left_pos
	if target_right:
		target_right.position = _default_right_pos
	
	_time = 0.0

func setup_base_positions() -> void:
	if not _defaults_captured:
		capture_defaults()
		
	if target_left:
		_left_x = target_left.position.x
		_base_y = target_left.position.y
	if target_right:
		_right_x = target_right.position.x
	_time = 0.0
	
	

func _process(delta: float) -> void:
	if not target_left or not target_right or not move_targets:
		return

	_time += delta * speed

	# Phase angles (Left foot leads, Right foot is 180 degrees out of phase)
	var angle_left: float = _time
	var angle_right: float = _time + PI

	# Calculate 2D ellipse positions in the Y-Z plane
	var left_z: float = center_z + (cos(angle_left) * radius_z)
	var left_y: float = _base_y + (sin(angle_left) * radius_y)

	var right_z: float = center_z + (cos(angle_right) * radius_z)
	var right_y: float = _base_y + (sin(angle_right) * radius_y)

	# Apply updated positions while preserving lateral X spacing
	target_left.position = Vector3(_left_x, left_y, left_z)
	target_right.position = Vector3(_right_x, right_y, right_z)

	if not Engine.is_editor_hint():
		print("hey hey")
		
		# Ground Raycast Snapping
		var ray_left = target_left.get_node_or_null("ray") as RayCast3D
		if ray_left and ray_left.is_colliding():
			var col_local = to_local(ray_left.get_collision_point())
			target_left.position.y = max(target_left.position.y, col_local.y +0.2)

		var ray_right = target_right.get_node_or_null("ray") as RayCast3D
		if ray_right and ray_right.is_colliding():
			var col_local = to_local(ray_right.get_collision_point())
			target_right.position.y = max(target_right.position.y, col_local.y +0.2)

		var forward_velocity: float = (-2.0 * radius_z * speed) / PI

		
		global_position.z += forward_velocity * delta
