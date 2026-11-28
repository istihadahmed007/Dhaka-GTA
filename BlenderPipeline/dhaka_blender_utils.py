"""
Shared Blender 4.x / 3.6+ modeling utilities for Dhaka-GTA Unity 6 HDRP environment art.
Exports metric-scaled (1 unit = 1m) FBX hierarchies with LOD0/LOD1/LOD2, UVs, and multi-material slots.
"""
import os
import bpy
import bmesh


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def get_or_create_material(name: str, base_color=(0.6, 0.6, 0.6, 1.0), metallic=0.0, roughness=0.5, emission=None):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = base_color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = emission
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 2.0
    return mat


def create_box_mesh_bm(bm, center, size, mat_index=0):
    mat = bpy.ops.object.mode_set if False else None
    _ = mat
    from mathutils import Matrix
    scale_mat = Matrix.Translation(center) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
    ret = bmesh.ops.create_cube(bm, size=1.0, matrix=scale_mat)
    verts = ret["verts"]
    for f in bm.faces:
        if all(v in verts for v in f.verts):
            f.material_index = mat_index


def create_cylinder_bm(bm, center, radius, height, segs=16, mat_index=0):
    from mathutils import Matrix
    xform = Matrix.Translation(center)
    ret = bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        cap_tris=False,
        segments=segs,
        radius1=radius,
        radius2=radius,
        depth=height,
        matrix=xform,
    )
    verts = set(ret["verts"])
    for f in bm.faces:
        if all(v in verts for v in f.verts):
            f.material_index = mat_index


def auto_box_uv_project(obj, scale=0.2):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")
    if obj.data.uv_layers:
        for loop in obj.data.uv_layers.active.data:
            loop.uv.x *= scale * 4.0
            loop.uv.y *= scale * 4.0
    obj.select_set(False)


def export_lod_group_fbx(root_obj, rel_subpath: str, unity_models_root: str):
    out_path = os.path.join(unity_models_root, rel_subpath)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root_obj.select_set(True)
    for child in root_obj.children_recursive:
        child.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    print("Exported Blender FBX:", out_path)
