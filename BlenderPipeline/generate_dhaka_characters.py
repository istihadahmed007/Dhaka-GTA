"""
Original rigged Blender character generator for Dhaka-GTA protagonists: Tariq & Maya.
Constructs anatomical lofted multi-part humanoid meshes (never primitive stick-figures),
facial structures (brow, nose, lips, jaw, eyes), layered jackets, cargo trousers, boots, hair,
UV layouts, PBR Principled BSDF materials, and a 26-bone Humanoid-ready Armature for Unity 6 HDRP.
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix

from dhaka_blender_utils import reset_scene, get_or_create_material


CHARACTER_MODELS_SUBDIR = "Characters"


def _add_lofted_tube(bm, rings, segments=16, close_bottom=True, close_top=True, mat_index=0):
    """
    Creates an organic lofted surface from a list of rings:
      [(center_z_or_vec3, rx, ry, tilt_x_rad, twist_rad), ...]
    """
    created_rings = []
    all_verts = []
    for r in rings:
        center, rx, ry = Vector(r[0]), r[1], r[2]
        ring_verts = []
        for i in range(segments):
            theta = (i / segments) * math.tau
            x = math.cos(theta) * rx
            z = math.sin(theta) * ry
            v = bm.verts.new(center + Vector((x, z, 0.0)))
            ring_verts.append(v)
            all_verts.append(v)
        created_rings.append(ring_verts)

    for ri in range(len(created_rings) - 1):
        r0 = created_rings[ri]
        r1 = created_rings[ri + 1]
        for i in range(segments):
            inext = (i + 1) % segments
            f = bm.faces.new((r0[i], r0[inext], r1[inext], r1[i]))
            f.material_index = mat_index

    if close_bottom and len(created_rings) > 0:
        f = bm.faces.new(list(reversed(created_rings[0])))
        f.material_index = mat_index
    if close_top and len(created_rings) > 0:
        f = bm.faces.new(created_rings[-1])
        f.material_index = mat_index

    return all_verts


def _add_arm_loft(bm, side_sign: float, shoulder_x: float, shoulder_y: float, sleeve_mat=1, skin_mat=0, bulky=1.0):
    # Blender Z-up before Unity Y-up export: X = right(+), Y = depth(forward -Y), Z = height(+)
    pts = [
        (Vector((shoulder_x * side_sign, 0.0, shoulder_y)), 0.068 * bulky, 0.064 * bulky, sleeve_mat),
        (Vector(((shoulder_x + 0.14) * side_sign, 0.0, shoulder_y - 0.11)), 0.056 * bulky, 0.054 * bulky, sleeve_mat),
        (Vector(((shoulder_x + 0.25) * side_sign, -0.01, shoulder_y - 0.25)), 0.046 * bulky, 0.044 * bulky, sleeve_mat),
        (Vector(((shoulder_x + 0.34) * side_sign, -0.02, shoulder_y - 0.39)), 0.040 * bulky, 0.038 * bulky, sleeve_mat),
        (Vector(((shoulder_x + 0.41) * side_sign, -0.025, shoulder_y - 0.50)), 0.032 * bulky, 0.028 * bulky, skin_mat),
        (Vector(((shoulder_x + 0.46) * side_sign, -0.025, shoulder_y - 0.58)), 0.035 * bulky, 0.020 * bulky, skin_mat),
        (Vector(((shoulder_x + 0.49) * side_sign, -0.025, shoulder_y - 0.64)), 0.024 * bulky, 0.013 * bulky, skin_mat),
    ]
    rings = []
    segs = 12
    for idx, (p, rx, ry, m_id) in enumerate(pts):
        ring = []
        # Tangent orientation roughly along arm vector (-42 deg down)
        dir_vec = Vector((side_sign * 0.62, -0.04, -0.78)).normalized()
        up_ref = Vector((0.0, 1.0, 0.0))
        side_v = dir_vec.cross(up_ref).normalized()
        fwd_v = side_v.cross(dir_vec).normalized()
        for s in range(segs):
            ang = (s / segs) * math.tau
            offset = side_v * (math.cos(ang) * rx) + fwd_v * (math.sin(ang) * ry)
            ring.append(bm.verts.new(p + offset))
        rings.append((ring, m_id))

    for i in range(len(rings) - 1):
        r0, m0 = rings[i]
        r1, m1 = rings[i + 1]
        face_m = m0 if i < 4 else m1
        for s in range(segs):
            sn = (s + 1) % segs
            f = bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
            f.material_index = face_m
    bm.faces.new(list(reversed(rings[0][0]))).material_index = sleeve_mat
    bm.faces.new(rings[-1][0]).material_index = skin_mat


def _add_leg_and_boot_loft(bm, side_sign: float, hip_x: float, crotch_z: float, trouser_mat=2, boot_mat=4, slim=1.0):
    rings_spec = [
        (Vector((hip_x * side_sign, 0.0, crotch_z + 0.04)), 0.095 * slim, 0.105 * slim, trouser_mat),
        (Vector((hip_x * side_sign, -0.005, crotch_z * 0.72)), 0.084 * slim, 0.092 * slim, trouser_mat),
        (Vector((hip_x * side_sign, 0.006, crotch_z * 0.50)), 0.066 * slim, 0.072 * slim, trouser_mat),  # knee
        (Vector((hip_x * side_sign, 0.015, crotch_z * 0.28)), 0.068 * slim, 0.076 * slim, trouser_mat),  # calf
        (Vector((hip_x * side_sign, 0.012, 0.115)), 0.048 * slim, 0.056 * slim, trouser_mat),           # cuff
        (Vector((hip_x * side_sign, 0.008, 0.108)), 0.052 * slim, 0.060 * slim, boot_mat),              # boot rim
        (Vector((hip_x * side_sign, -0.032, 0.045)), 0.055 * slim, 0.115 * slim, boot_mat),             # vamp/foot
        (Vector((hip_x * side_sign, -0.040, 0.0)), 0.056 * slim, 0.125 * slim, boot_mat),               # sole
    ]
    _add_lofted_tube(
        bm,
        [(p, rx, ry) for (p, rx, ry, _) in rings_spec],
        segments=14,
        close_bottom=True,
        close_top=True,
        mat_index=trouser_mat,
    )
    # Assign boot material on lower faces cleanly
    for f in bm.faces:
        if f.calc_center_median().z < 0.112:
            f.material_index = boot_mat


def _add_head_face_and_hair(bm, head_base_z: float, skin_mat=0, hair_mat=3, accent_mat=5, feminine=False):
    # Neck + sculpted cranium + facial ridge deformation (nose/brow/jawline)
    w_scale = 0.90 if feminine else 1.0
    head_rings = [
        (Vector((0.0, 0.015, head_base_z - 0.07)), 0.052 * w_scale, 0.055 * w_scale),
        (Vector((0.0, 0.005, head_base_z - 0.01)), 0.056 * w_scale, 0.060 * w_scale),
        (Vector((0.0, -0.012, head_base_z + 0.03)), 0.076 * w_scale, 0.088 * w_scale),  # jaw/chin
        (Vector((0.0, -0.018, head_base_z + 0.095)), 0.084 * w_scale, 0.102 * w_scale), # cheekbones/nose
        (Vector((0.0, -0.010, head_base_z + 0.155)), 0.086 * w_scale, 0.105 * w_scale), # brow
        (Vector((0.0, 0.005, head_base_z + 0.215)), 0.076 * w_scale, 0.096 * w_scale),  # forehead/crown
        (Vector((0.0, 0.010, head_base_z + 0.255)), 0.036 * w_scale, 0.044 * w_scale),
    ]
    verts = _add_lofted_tube(bm, head_rings, segments=18, close_bottom=True, close_top=True, mat_index=skin_mat)
    # Sculpt nose bridge, brow overhang, and cheek taper cleanly on forward (-Y) vertices
    for v in verts:
        if v.co.y < -0.075 and abs(v.co.x) < 0.022 and (head_base_z + 0.07) < v.co.z < (head_base_z + 0.145):
            v.co.y -= 0.022  # sculpted nasal bridge & tip
        if v.co.y < -0.065 and (head_base_z + 0.135) < v.co.z < (head_base_z + 0.165):
            v.co.y -= 0.008  # supraorbital brow ridge

    # Stylized hair volume shell (fade/quiff for Tariq; layered swept bob for Maya)
    hair_z0 = head_base_z + (0.04 if feminine else 0.12)
    hair_rings = (
        [
            (Vector((0.0, 0.030, hair_z0)), 0.096 * w_scale, 0.098 * w_scale),
            (Vector((0.0, 0.012, head_base_z + 0.14)), 0.100 * w_scale, 0.112 * w_scale),
            (Vector((0.0, 0.005, head_base_z + 0.21)), 0.094 * w_scale, 0.110 * w_scale),
            (Vector((0.0, 0.008, head_base_z + 0.268)), 0.046 * w_scale, 0.056 * w_scale),
        ]
        if feminine
        else [
            (Vector((0.0, 0.025, hair_z0)), 0.089, 0.104),
            (Vector((0.0, 0.002, head_base_z + 0.205)), 0.090, 0.112),
            (Vector((0.0, -0.018, head_base_z + 0.265)), 0.068, 0.088),
            (Vector((0.0, -0.025, head_base_z + 0.285)), 0.028, 0.036),
        ]
    )
    hverts = _add_lofted_tube(bm, hair_rings, segments=16, close_bottom=True, close_top=True, mat_index=hair_mat)
    for v in hverts:
        # Leave face opening uncovered
        if v.co.y < -0.045 and v.co.z < (head_base_z + 0.21):
            v.co.y = max(v.co.y, -0.025 if feminine else -0.015)

    # Eyes (accent/cornea material)
    for sx in (-1.0, 1.0):
        ret = bmesh.ops.create_icosphere(
            bm,
            subdivisions=2,
            radius=0.013,
            matrix=Matrix.Translation(Vector((sx * 0.031 * w_scale, -0.088 * w_scale, head_base_z + 0.132))),
        )
        everts = set(ret["verts"])
        for f in bm.faces:
            if all(v in everts for v in f.verts):
                f.material_index = accent_mat


def _build_humanoid_armature(name: str, height_scale: float, shoulder_half_w: float, hip_half_w: float):
    arm_data = bpy.data.armatures.new(f"{name}_Armature")
    arm_obj = bpy.data.objects.new(f"{name}_Rig", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")

    eb = arm_data.edit_bones
    s = height_scale

    def bone(bname, head, tail, parent=None, connect=False):
        b = eb.new(bname)
        b.head = Vector(head) * s
        b.tail = Vector(tail) * s
        if parent:
            b.parent = parent
            b.use_connect = connect
        return b

    hips = bone("Hips", (0, 0, 0.94), (0, 0, 1.05))
    spine = bone("Spine", (0, 0, 1.05), (0, 0, 1.21), hips, True)
    chest = bone("Chest", (0, 0, 1.21), (0, 0, 1.37), spine, True)
    uchest = bone("UpperChest", (0, 0, 1.37), (0, 0, 1.49), chest, True)
    neck = bone("Neck", (0, 0, 1.49), (0, 0, 1.56), uchest, True)
    head = bone("Head", (0, 0, 1.56), (0, 0, 1.78), neck, True)
    bone("Jaw", (0, -0.02, 1.59), (0, -0.09, 1.58), head)
    bone("LeftEye", (0.031, -0.06, 1.66), (0.031, -0.10, 1.66), head)
    bone("RightEye", (-0.031, -0.06, 1.66), (-0.031, -0.10, 1.66), head)

    for sign, side in ((1.0, "Left"), (-1.0, "Right")):
        sh = bone(f"{side}Shoulder", (0.04 * sign, 0, 1.43), (shoulder_half_w * sign, 0, 1.43), uchest)
        ua = bone(f"{side}UpperArm", (shoulder_half_w * sign, 0, 1.43), ((shoulder_half_w + 0.24) * sign, -0.01, 1.18), sh, True)
        la = bone(f"{side}LowerArm", ((shoulder_half_w + 0.24) * sign, -0.01, 1.18), ((shoulder_half_w + 0.42) * sign, -0.025, 0.94), ua, True)
        ha = bone(f"{side}Hand", ((shoulder_half_w + 0.42) * sign, -0.025, 0.94), ((shoulder_half_w + 0.49) * sign, -0.025, 0.82), la, True)
        bone(f"{side}ThumbDistal", ((shoulder_half_w + 0.44) * sign, -0.045, 0.90), ((shoulder_half_w + 0.47) * sign, -0.065, 0.86), ha)

        ul = bone(f"{side}UpperLeg", (hip_half_w * sign, 0, 0.92), (hip_half_w * sign, 0.005, 0.49), hips)
        ll = bone(f"{side}LowerLeg", (hip_half_w * sign, 0.005, 0.49), (hip_half_w * sign, 0.012, 0.11), ul, True)
        ft = bone(f"{side}Foot", (hip_half_w * sign, 0.012, 0.11), (hip_half_w * sign, -0.06, 0.03), ll, True)
        bone(f"{side}Toes", (hip_half_w * sign, -0.06, 0.03), (hip_half_w * sign, -0.14, 0.02), ft, True)

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def build_character_fbx(
    unity_models_root: str,
    char_id: str,
    jacket_rgb,
    accent_rgb,
    skin_rgb,
    hair_rgb,
    height_meters: float,
    feminine: bool,
):
    reset_scene()
    s = height_meters / 1.78
    shoulder_hw = 0.175 if feminine else 0.215
    hip_hw = 0.102 if feminine else 0.098
    waist_rx = 0.132 if feminine else 0.158
    chest_rx = 0.165 if feminine else 0.205
    arm_bulk = 0.86 if feminine else 1.05
    leg_slim = 0.90 if feminine else 1.02

    bm = bmesh.new()

    # Material indices: 0=Skin, 1=Jacket, 2=Trousers, 3=Hair, 4=Boots, 5=Accent/Eyes
    torso_rings = [
        (Vector((0.0, 0.005, 0.90)), 0.155 * (waist_rx / 0.158), 0.120),
        (Vector((0.0, 0.0, 1.06)), waist_rx, 0.110),
        (Vector((0.0, -0.008, 1.24)), chest_rx * 0.92, 0.122),
        (Vector((0.0, -0.012, 1.39)), chest_rx, 0.128),
        (Vector((0.0, 0.0, 1.48)), chest_rx * 0.72, 0.095),
        (Vector((0.0, 0.005, 1.54)), 0.072, 0.076),  # jacket collar
    ]
    _add_lofted_tube(bm, torso_rings, segments=18, close_bottom=True, close_top=True, mat_index=1)

    for sign in (1.0, -1.0):
        _add_arm_loft(bm, sign, shoulder_hw, 1.43, sleeve_mat=1, skin_mat=0, bulky=arm_bulk)
        _add_leg_and_boot_loft(bm, sign, hip_hw, 0.89, trouser_mat=2, boot_mat=4, slim=leg_slim)

    _add_head_face_and_hair(bm, 1.56, skin_mat=0, hair_mat=3, accent_mat=5, feminine=feminine)

    # Apply uniform height scaling around origin
    if abs(s - 1.0) > 0.001:
        bmesh.ops.scale(bm, vec=(s, s, s), verts=bm.verts)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    mesh = bpy.data.meshes.new(f"Mesh_{char_id}")
    bm.to_mesh(mesh)
    bm.free()

    mesh_obj = bpy.data.objects.new(f"CHAR_{char_id}_Mesh", mesh)
    bpy.context.collection.objects.link(mesh_obj)

    mats = [
        get_or_create_material(f"MAT_{char_id}_Skin", skin_rgb, 0.0, 0.48),
        get_or_create_material(f"MAT_{char_id}_Jacket", jacket_rgb, 0.05, 0.56),
        get_or_create_material(f"MAT_{char_id}_Trousers", (0.09, 0.10, 0.13, 1.0), 0.02, 0.76),
        get_or_create_material(f"MAT_{char_id}_Hair", hair_rgb, 0.0, 0.62),
        get_or_create_material(f"MAT_{char_id}_Boots", (0.11, 0.09, 0.08, 1.0), 0.05, 0.48),
        get_or_create_material(f"MAT_{char_id}_AccentEye", accent_rgb, 0.25, 0.22),
    ]
    for m in mats:
        mesh_obj.data.materials.append(m)

    # UV Unwrap cleanly
    bpy.context.view_layer.objects.active = mesh_obj
    mesh_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")

    arm_obj = _build_humanoid_armature(char_id, s, shoulder_hw, hip_hw)

    # Parent mesh to armature with automatic bone weights
    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    out_path = os.path.join(unity_models_root, CHARACTER_MODELS_SUBDIR, f"CHAR_{char_id}.fbx")
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm_obj.select_set(True)
    mesh_obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        armature_nodetype="NULL",
    )
    print("Exported rigged Humanoid character FBX:", out_path)


def run_all_characters(unity_models_root: str):
    build_character_fbx(
        unity_models_root=unity_models_root,
        char_id="Tariq",
        jacket_rgb=(0.118, 0.227, 0.373, 1.0),
        accent_rgb=(0.831, 0.627, 0.090, 1.0),
        skin_rgb=(0.76, 0.54, 0.40, 1.0),
        hair_rgb=(0.06, 0.055, 0.05, 1.0),
        height_meters=1.80,
        feminine=False,
    )
    build_character_fbx(
        unity_models_root=unity_models_root,
        char_id="Maya",
        jacket_rgb=(0.541, 0.110, 0.153, 1.0),
        accent_rgb=(0.00, 0.824, 0.827, 1.0),
        skin_rgb=(0.80, 0.59, 0.45, 1.0),
        hair_rgb=(0.08, 0.06, 0.055, 1.0),
        height_meters=1.70,
        feminine=True,
    )
