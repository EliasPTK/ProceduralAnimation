@tool
extends Node3D
@export var targetPos: Node3D
@export var rootPos: Node3D
@export var enabled: bool = false
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	if(enabled):
		if(targetPos != null and is_instance_valid(targetPos)):
			if(rootPos != null and is_instance_valid(rootPos)):
				#var midXPos = (rootPos.global_position.x + targetPos.global_position.x)/2
				#var midYPos = (rootPos.global_position.y + targetPos.global_position.y)/2
				#var midZPos = (rootPos.global_position.z + targetPos.global_position.z)/2
				
				
				var CornerPos = Vector3(0,0,0)
				if(targetPos.global_position.y > rootPos.global_position.y):
					CornerPos.x = rootPos.global_position.x
					CornerPos.z = rootPos.global_position.z
					CornerPos.y = targetPos.global_position.y + 5
				else:
					CornerPos.x = targetPos.global_position.x
					CornerPos.z = targetPos.global_position.z
					CornerPos.y = rootPos.global_position.y + 5
				global_position = CornerPos
