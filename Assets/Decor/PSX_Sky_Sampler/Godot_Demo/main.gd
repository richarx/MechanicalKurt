extends Node3D
# PSX skybox viewer: Left/Right (or Q/E) change sky, WASD + mouse to look around, H hides the label, Esc frees the mouse.
# "-- --autotest" saves two screenshots per sky to shots/ and quits.

var skies: Array = []
var idx := 0
var env: Environment
var sun: DirectionalLight3D
var cam: Camera3D
var hud: Label
var yaw := 0.0
var pitch := 0.0

func _ready() -> void:
	skies = JSON.parse_string(FileAccess.get_file_as_string("res://skies.json"))
	var fg: Node3D = load("res://foreground.glb").instantiate()
	add_child(fg)
	_nearest(fg)
	env = Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = Sky.new()
	env.sky_rotation = Vector3(0, PI, 0)  # image centre = north (-Z)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.fog_enabled = true
	env.fog_sky_affect = 0.0
	env.fog_aerial_perspective = 1.0
	env.fog_density = 0.012
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	sun = DirectionalLight3D.new()
	sun.shadow_enabled = true
	add_child(sun)
	cam = Camera3D.new()
	cam.position = Vector3(0, 1.7, 0)
	cam.fov = 75
	cam.far = 800
	add_child(cam)
	hud = Label.new()
	hud.position = Vector2(4, 2)
	hud.add_theme_font_size_override("font_size", 10)
	hud.add_theme_color_override("font_outline_color", Color.BLACK)
	hud.add_theme_constant_override("outline_size", 3)
	var ui := CanvasLayer.new()
	ui.add_child(hud)
	add_child(ui)
	_show(0)
	if "--autotest" in OS.get_cmdline_user_args():
		_autotest()
	else:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _nearest(n: Node) -> void:
	if n is MeshInstance3D:
		var mi := n as MeshInstance3D
		for i in mi.mesh.get_surface_count():
			var m := mi.mesh.surface_get_material(i) as BaseMaterial3D
			if m:
				m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST_WITH_MIPMAPS
	for c in n.get_children():
		_nearest(c)

func _dir(az: float, el: float) -> Vector3:
	# sky space: az clockwise from north (-Z), el up from the horizon
	var a := deg_to_rad(az)
	var e := deg_to_rad(el)
	return Vector3(cos(e) * sin(a), sin(e), -cos(e) * cos(a))

func _show(i: int) -> void:
	idx = wrapi(i, 0, skies.size())
	var s: Dictionary = skies[idx]
	var mat := PanoramaSkyMaterial.new()
	mat.panorama = load("res://skies/" + s["file"])
	mat.filter = false
	env.sky.sky_material = mat
	var L: Dictionary = s["light"]
	var d := _dir(L["az"], max(float(L["el"]), 3.0))
	sun.look_at_from_position(Vector3.ZERO, -d, Vector3.UP if abs(d.y) < 0.99 else Vector3.FORWARD)
	sun.light_color = Color(L["col"])
	sun.light_energy = L["energy"]
	env.ambient_light_energy = L["ambient"]
	env.fog_light_color = Color(s["fog"])
	env.fog_density = s["fog_density"]
	hud.text = "%d/%d  %s   [</> change sky, H hide]" % [idx + 1, skies.size(), s["name"]]

func _look(y: float, p: float) -> void:
	yaw = y
	pitch = p
	cam.rotation = Vector3(deg_to_rad(p), deg_to_rad(-y), 0)

func _unhandled_input(e: InputEvent) -> void:
	if e is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		_look(yaw + e.relative.x * 0.15, clamp(pitch - e.relative.y * 0.15, -85, 85))
	elif e is InputEventKey and e.pressed and not e.echo:
		match e.keycode:
			KEY_RIGHT, KEY_E: _show(idx + 1)
			KEY_LEFT, KEY_Q: _show(idx - 1)
			KEY_H: hud.visible = not hud.visible
			KEY_ESCAPE: Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	elif e is InputEventMouseButton and e.pressed:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _process(dt: float) -> void:
	var v := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_key_pressed(KEY_W): v.y -= 1
	if Input.is_key_pressed(KEY_S): v.y += 1
	if Input.is_key_pressed(KEY_A): v.x -= 1
	if Input.is_key_pressed(KEY_D): v.x += 1
	var f := Vector3(sin(deg_to_rad(yaw)), 0, -cos(deg_to_rad(yaw)))
	var r := Vector3(cos(deg_to_rad(yaw)), 0, sin(deg_to_rad(yaw)))
	cam.position += (r * v.x - f * v.y) * dt * 6.0

func _autotest() -> void:
	DirAccess.make_dir_recursive_absolute("res://shots")
	hud.visible = false
	for i in skies.size():
		_show(i)
		var s: Dictionary = skies[i]
		for k in s["views"].size():
			var vw: Array = s["views"][k]
			_look(vw[0], vw[1])
			for f in 4:
				await RenderingServer.frame_post_draw
			get_viewport().get_texture().get_image().save_png("res://shots/%s_%d.png" % [s["id"], k])
	print("AUTOTEST DONE ", skies.size())
	get_tree().quit()
