extends Node3D
@export var feet: Array[Node3D]
var startDist = 0.5
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	#startDist = global_position.y - feet[0].global_position.y
	pass

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	position += Vector3(1,0,1) * delta
	#rotation_degrees.y += 10 * delta
	var avgY = 0
	for i in feet:
		avgY += i.global_position.y
	avgY = avgY/len(feet)
	global_position.y = startDist + avgY
	
