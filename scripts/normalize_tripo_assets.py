#!/usr/bin/env python3
"""
Step 4 of @Mnilax pipeline: Clean and normalize all generated Tripo 3D models in Blender 5.2.
- Centers X/Y bounds and places the lowest vertex at Z = 0.0 (ground pivot).
- Scales each asset to its exact real-world meter height from GameDesignDoc.md.
- Exports both Unity-ready FBX (with embedded PBR textures) into Assets/Resources/TripoModels/
  and normalized GLB into public/assets/models/ for the Three.js web mirror.
"""
import bpy
import os
import json
import math
from mathutils import Vector

ROOT_DIR = "/Users/derya/Gemini-4/cozy-island-farm"
TRIPO_DIR = os.path.join(ROOT_DIR, "TripoSource")
UNITY_MODELS_DIR = os.path.join(ROOT_DIR, "Assets", "Resources", "TripoModels")
WEB_MODELS_DIR = os.path.join(ROOT_DIR, "public", "assets", "models")

os.makedirs(UNITY_MODELS_DIR, exist_ok=True)
os.makedirs(WEB_MODELS_DIR, exist_ok=True)

TARGET_HEIGHTS = {
    "farmer": 1.65,
    "barn": 5.60,
    "windmill": 6.80,
    "market_stall": 3.20,
    "wooden_bridge": 2.20,  # arch height (~4.5m span)
    "crop_pumpkin": 0.58,
    "crop_tomato": 0.85,
    "apple_tree": 4.20,
    "street_lantern": 2.40,
    "watering_can": 0.45,
}

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def get_mesh_objects():
    return [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']

def compute_world_bounds(mesh_objs):
    min_v = Vector((1e9, 1e9, 1e9))
    max_v = Vector((-1e9, -1e9, -1e9))
    for obj in mesh_objs:
        for corner in obj.bound_box:
            wc = obj.matrix_world @ Vector(corner)
            min_v.x = min(min_v.x, wc.x)
            min_v.y = min(min_v.y, wc.y)
            min_v.z = min(min_v.z, wc.z)
            max_v.x = max(max_v.x, wc.x)
            max_v.y = max(max_v.y, wc.y)
            max_v.z = max(max_v.z, wc.z)
    return min_v, max_v

def normalize_asset(name, target_height):
    asset_dir = os.path.join(TRIPO_DIR, name)
    glb_in = os.path.join(asset_dir, f"{name}.glb")
    fbx_in = os.path.join(asset_dir, f"{name}.fbx")

    # Prefer importing GLB into Blender because GLB preserves embedded PBR textures cleanly
    in_path = glb_in if os.path.exists(glb_in) else fbx_in
    if not os.path.exists(in_path):
        # Search recursively inside asset_dir
        for root, _, files in os.walk(asset_dir):
            for f in files:
                if f.endswith(".glb"):
                    in_path = os.path.join(root, f)
                    break
    if not os.path.exists(in_path):
        print(f"[WARN] Missing source model for {name}")
        return None

    clear_scene()
    if in_path.endswith(".glb") or in_path.endswith(".gltf"):
        bpy.ops.import_scene.gltf(filepath=in_path)
    else:
        bpy.ops.import_scene.fbx(filepath=in_path)

    bpy.context.view_layer.update()
    meshes = get_mesh_objects()
    if not meshes:
        print(f"[WARN] No mesh objects found in {in_path}")
        return None

    min_v, max_v = compute_world_bounds(meshes)
    size = max_v - min_v
    cur_height = max(size.z, 1e-4)
    scale_factor = target_height / cur_height

    center_x = (min_v.x + max_v.x) * 0.5
    center_y = (min_v.y + max_v.y) * 0.5
    bottom_z = min_v.z

    # Create or adjust root objects so the entire hierarchy scales and sits at (0,0,0)
    root_objs = [obj for obj in bpy.context.scene.objects if obj.parent is None]
    for root in root_objs:
        root.scale = (
            root.scale.x * scale_factor,
            root.scale.y * scale_factor,
            root.scale.z * scale_factor,
        )
    bpy.context.view_layer.update()

    min_v2, max_v2 = compute_world_bounds(meshes)
    offset = Vector((
        -(min_v2.x + max_v2.x) * 0.5,
        -(min_v2.y + max_v2.y) * 0.5,
        -min_v2.z,
    ))
    for root in root_objs:
        root.location += offset
    bpy.context.view_layer.update()

    # Apply transforms for static props (keep armature intact for farmer)
    has_armature = any(obj.type == 'ARMATURE' for obj in bpy.context.scene.objects)
    if not has_armature:
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.context.view_layer.update()

    final_min, final_max = compute_world_bounds(meshes)
    poly_count = sum(len(m.data.polygons) for m in meshes)
    if poly_count > 25000:
        target_faces = 12000
        ratio = max(0.005, min(1.0, target_faces / float(poly_count)))
        for m in meshes:
            if len(m.data.polygons) > 5000:
                bpy.context.view_layer.objects.active = m
                mod = m.modifiers.new(name="CozyLowPolyDecimate", type="DECIMATE")
                mod.decimate_type = "COLLAPSE"
                mod.ratio = ratio
                bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.context.view_layer.update()
        poly_count = sum(len(m.data.polygons) for m in meshes)

    out_fbx = os.path.join(UNITY_MODELS_DIR, f"{name}.fbx")
    out_glb = os.path.join(WEB_MODELS_DIR, f"{name}.glb")

    bpy.ops.export_scene.fbx(
        filepath=out_fbx,
        path_mode='COPY',
        embed_textures=True,
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=has_armature,
    )
    bpy.ops.export_scene.gltf(
        filepath=out_glb,
        export_format='GLB',
        export_apply=not has_armature,
    )

    # Also copy preview image to Unity and Web folders
    preview_src = os.path.join(asset_dir, f"{name}_preview.png")
    if os.path.exists(preview_src):
        import shutil
        shutil.copy2(preview_src, os.path.join(UNITY_MODELS_DIR, f"{name}_preview.png"))
        shutil.copy2(preview_src, os.path.join(WEB_MODELS_DIR, f"{name}_preview.png"))

    report = {
        "name": name,
        "source": in_path,
        "has_armature": has_armature,
        "polygons": poly_count,
        "bounds_min": [round(final_min.x, 4), round(final_min.y, 4), round(final_min.z, 4)],
        "bounds_max": [round(final_max.x, 4), round(final_max.y, 4), round(final_max.z, 4)],
        "height_m": round(final_max.z - final_min.z, 4),
        "unity_fbx": out_fbx,
        "web_glb": out_glb,
    }
    print(f"[BLENDER NORMALIZED] {name}: height={report['height_m']}m, polys={poly_count}, min_z={report['bounds_min'][2]}")
    return report

def main():
    reports = []
    for name, height in TARGET_HEIGHTS.items():
        r = normalize_asset(name, height)
        if r:
            reports.append(r)
    summary_path = os.path.join(TRIPO_DIR, "blender_normalization_report.json")
    with open(summary_path, "w") as f:
        json.dump({"normalized_assets": reports}, f, indent=2)
    print(f"[BLENDER COMPLETE] Normalized {len(reports)} assets -> {summary_path}")

if __name__ == "__main__":
    main()
