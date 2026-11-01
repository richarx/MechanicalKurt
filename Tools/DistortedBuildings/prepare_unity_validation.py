"""Prepare an isolated, ignored Unity project using already installed packages."""
import json
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[2]
target = root / 'Temp/DistortedBuildingValidation'
for folder in ('Assets', 'Packages', 'ProjectSettings', 'ArtSource/DistortedBuildings'):
    (target / folder).mkdir(parents=True, exist_ok=True)
dependencies = {}
for package in (root / 'Library/PackageCache').iterdir():
    info = package / 'package.json'
    if info.exists():
        name = json.loads(info.read_text(encoding='utf-8-sig'))['name']
        dependencies[name] = 'file:' + package.as_posix()
manifest = json.loads((root / 'Packages/manifest.json').read_text())
dependencies.update({name: version for name,version in manifest['dependencies'].items() if name.startswith('com.unity.modules.')})
(target / 'Packages/manifest.json').write_text(json.dumps({'dependencies': dependencies}, indent=2))
shutil.copy2(root / 'ProjectSettings/ProjectVersion.txt', target / 'ProjectSettings/ProjectVersion.txt')
files = [
    'Assets/Decor/URBAN/Buildings Prebuild/Houses 3.mat',
    'Assets/Decor/URBAN/Buildings Prebuild/Houses 3.mat.meta',
    'Assets/Decor/URBAN/textures/houses.png',
    'Assets/Decor/URBAN/textures/houses.png.meta',
    'Assets/Decor/DistortedBuildings/flat_Lshape_vertical_stripes_distorted.fbx',
    'Assets/Decor/DistortedBuildings/Editor/DistortedBuildingSetup.cs',
]
for relative in files:
    dst = target / relative
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / relative, dst)
print(target)
