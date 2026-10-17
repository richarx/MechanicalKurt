PSX SKY SAMPLER
===============
by Munchkin Gaming - https://munchkin-jonesy.itch.io

Five free PS1-style skyboxes: a summer day, a sunset, a moonlit night, a blood moon and a Silent-Hill fog. Dithered 15-bit colour, crisp pixels, drop-in ready.

SKIES (5)
  01  Summer Cumulus
  02  Sunset Orange
  03  Full Moon Night
  04  Blood Moon
  05  Silent Fog


WHAT'S INSIDE
-------------
  Equirect_1024x512/    every sky as a 2:1 panorama (the native PS1-style size)
  Equirect_2048x1024/   the same pixels doubled, for engines that want 2K
  Cubemap_256/<Sky>/    six 256x256 faces: right, left, up, down, front, back
  Cubemap_6x1_Strip/    the six faces in one 1536x256 strip (+X -X +Y -Y +Z -Z)
  Godot_Demo/           a Godot 4 sky viewer (see below)

All images are PNG in dithered 15-bit colour, like the original hardware.
Every sky is seamless all the way round. The lower half is a plain ground/fog
tone, so it works under a floor or a far terrain.


HOW TO USE
----------
Use nearest / point filtering (no smoothing) to keep the crunchy pixels.

Godot 4:   WorldEnvironment > Environment > Background: Sky >
           Sky Material: PanoramaSkyMaterial, Panorama = an Equirect PNG,
           Filter OFF. Set Sky Rotation Y to 180 so the image centre faces -Z.
Unity:     import an Equirect PNG, Texture Shape: Cube, Mapping: Latitude-Longitude,
           Filter Mode: Point. Make a Skybox/Cubemap material with it and set it
           in Lighting > Environment. (Or use the 6x1 strip with Mapping: Auto.)
Unreal:    import an Equirect PNG as a Texture Cube (Filter: Nearest) and use it
           in a Sky Sphere / Sky Light, or on an inward-facing sphere.
Blender:   World > Environment Texture > Equirect PNG, interpolation: Closest.

Godot_Demo: open project.godot in Godot 4, press F5.
  Left/Right or Q/E  change sky      mouse  look around
  WASD               walk            H      hide the label
The demo lights each sky with a matching sun or moon so you can see how your
level will look under it.


LICENCE
-------
See LICENSE.txt. Use them in personal and commercial projects. Don't resell
or share the skies themselves.

Made with AI assistance (procedural generation scripts written with AI).
