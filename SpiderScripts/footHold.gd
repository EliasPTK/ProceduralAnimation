extends RayCast3D

@export var target: Node3D
@export var step_height: float = 0.3
@export var step_speed: float = 8.0

var stepTime = true
var footMoving = false
var amAllowed = false

var startPos = Vector3.ZERO
var targetPos = Vector3.ZERO
var stepProgress: float = 0.0
var totalStepDistance: float = 1.0

func _ready() -> void:
	if is_colliding():
		target.global_position = get_collision_point()

func _process(delta: float) -> void:
	if not stepTime:
		return

	# Trigger a step if the foot is too far from the raycast collision point
	if is_colliding() and not footMoving:
		var hit_pos = get_collision_point()
		if target.global_position.distance_to(hit_pos) > 0.8:
			startPos = target.global_position
			targetPos = hit_pos
			totalStepDistance = max(startPos.distance_to(targetPos), 0.001)
			stepProgress = 0.0
			footMoving = true

	# Perform step movement
	if footMoving and amAllowed:
		# Update target destination in real time if raycast is still hitting ground
		if is_colliding():
			targetPos = get_collision_point()

		stepProgress += (step_speed * delta) / totalStepDistance
		stepProgress = clamp(stepProgress, 0.0, 1.0)

		# 1. Interpolate toward the real-time target position
		var current_pos = startPos.lerp(targetPos, stepProgress)

		# 2. Apply step arc height
		var vertical_offset = sin(pow(stepProgress, 0.7) * PI) * step_height
		current_pos.y += vertical_offset

		target.global_position = current_pos

		# Finish step at the updated target position
		if stepProgress >= 1.0:
			target.global_position = targetPos
			footMoving = false
			amAllowed = false
