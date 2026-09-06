extends Node

const TOLERANCE := 0.01

var _passed := 0
var _failed := 0


func _ready() -> void:
	print("--- FabrikSolver tests ---")

	test_reachable_target_converges()
	test_bone_lengths_preserved()
	test_root_stays_fixed()
	test_unreachable_target_stretches_straight()
	test_target_at_root_does_not_crash()

	print("--- Results: %d passed, %d failed ---" % [_passed, _failed])


#assertion helpers

func assert_true(condition: bool, message: String) -> void:
	if condition:
		_passed += 1
		print("  PASS: %s" % message)
	else:
		_failed += 1
		print("  FAIL: %s" % message)


func assert_vec3_near(a: Vector3, b: Vector3, tolerance: float, message: String) -> void:
	assert_true(a.distance_to(b) <= tolerance, "%s (got %s, expected near %s)" % [message, a, b])


func assert_float_near(a: float, b: float, tolerance: float, message: String) -> void:
	assert_true(abs(a - b) <= tolerance, "%s (got %f, expected near %f)" % [message, a, b])


# unit tests

func test_reachable_target_converges() -> void:
	print("test_reachable_target_converges")

	# 3-bone chain, each bone length 1, resting bent along the way.
	var joints: Array[Vector3] = [
		Vector3(0, 0, 0),
		Vector3(1, 0, 0),
		Vector3(1, 1, 0),
		Vector3(1, 2, 0),
	]
	
	var solver := Fabriksolver.new()
	solver.Initialize(joints)

	# Well within the chain's total reach of 3.
	var target := Vector3(1.5, 0.5, 0)
	solver.Solve(target)

	var result: Array = solver.Joints
	var tip: Vector3 = result[result.size() - 1]
	assert_vec3_near(tip, target, TOLERANCE, "tip should land on a reachable target")


func test_bone_lengths_preserved() -> void:
	print("test_bone_lengths_preserved")

	var joints: Array[Vector3] = [
		Vector3(0, 0, 0),
		Vector3(1, 0, 0),
		Vector3(2, 0, 0),
		Vector3(3, 0, 0),
	]
	var solver := Fabriksolver.new()
	solver.Initialize(joints)
	solver.Solve(Vector3(1, 1.5, 0))

	var result: Array = solver.Joints
	for i in range(result.size() - 1):
		var bone_length: float = result[i].distance_to(result[i + 1])
		assert_float_near(bone_length, 1.0, TOLERANCE, "bone %d length should stay 1.0 after solving" % i)


func test_root_stays_fixed() -> void:
	print("test_root_stays_fixed")

	var root := Vector3(5, 0, 0)
	var joints: Array[Vector3] = [
		root,
		Vector3(6, 0, 0),
		Vector3(7, 0, 0),
	]
	var solver := Fabriksolver.new()
	solver.Initialize(joints)
	solver.Solve(Vector3(6, 1, 0))

	var result: Array = solver.Joints
	assert_vec3_near(result[0], root, TOLERANCE, "root joint should never move")


func test_unreachable_target_stretches_straight() -> void:
	print("test_unreachable_target_stretches_straight")

	# Total chain length is 2 (two bones of length 1 each).
	var joints: Array[Vector3] = [
		Vector3(0, 0, 0),
		Vector3(1, 0, 0),
		Vector3(1, 1, 0),
	]
	var solver := Fabriksolver.new()
	solver.Initialize(joints)

	# Target is far outside the chain's reach.
	var target := Vector3(100, 0, 0)
	solver.Solve(target)

	var result: Array = solver.Joints
	var tip: Vector3 = result[result.size() - 1]
	var expected_tip := Vector3(2, 0, 0) # fully extended toward target, length 2

	assert_vec3_near(tip, expected_tip, TOLERANCE, "tip should stop at full extension, not overshoot to target")
	assert_true(result[0].distance_to(Vector3(0, 0, 0)) <= TOLERANCE, "root should still be fixed in the stretch case")


func test_target_at_root_does_not_crash() -> void:
	print("test_target_at_root_does_not_crash")

	# Degenerate case: target sits exactly on the root. Mostly checking
	# this doesn't divide by zero / throw inside Normalized().
	var joints: Array[Vector3] = [
		Vector3(0, 0, 0),
		Vector3(1, 0, 0),
		Vector3(2, 0, 0),
	]
	var solver := Fabriksolver.new()
	solver.Initialize(joints)

	var did_crash := false
	solver.Solve(Vector3(0, 0, 0))
	# If we got this far without an exception propagating up, we're fine.
	assert_true(not did_crash, "solving with target at root should not throw")
