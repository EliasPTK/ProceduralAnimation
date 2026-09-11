extends Node3D

@export var y_offset: float = 1.0

func _process(_delta: float) -> void:
	var viewport := get_viewport()
	var camera := viewport.get_camera_3d()
	if not camera:
		return

	var mouse_pos := viewport.get_mouse_position()
	var ray_origin := camera.project_ray_origin(mouse_pos)
	var ray_end := ray_origin + camera.project_ray_normal(mouse_pos) * 1000.0

	var space_state := get_world_3d().direct_space_state
	var query := PhysicsRayQueryParameters3D.create(ray_origin, ray_end)
	var result := space_state.intersect_ray(query)

	if result:
		global_position = result.position + Vector3(0, y_offset, 0)
