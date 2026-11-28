"""
Blender generator for Dhaka environmental props, infrastructure & foliage (Categories 7-12):
7. Tea stalls ('Tong') and local shops
8. Rooftop water tanks & HVAC assemblies
9. Roadside electric poles with insulators & cross-arms
10. Traffic lights, billboards, benches
11. Tropical trees (Krishnachura, Coconut Palm, Banyan/RainTree)
12. MRT-6 Metro station canopies & flyover T-pier decks
"""
import math
import bpy
import bmesh
from dhaka_blender_utils import (
    auto_box_uv_project,
    create_box_mesh_bm,
    create_cylinder_bm,
    export_lod_group_fbx,
    get_or_create_material,
    reset_scene,
)


def build_tea_stall(models_root: str):
    reset_scene()
    m_tin = get_or_create_material("M_CorrugatedTin", (0.28, 0.42, 0.48, 1.0), metallic=0.65, roughness=0.45)
    m_wood = get_or_create_material("M_TeaWood", (0.36, 0.22, 0.12, 1.0), roughness=0.78)
    m_counter = get_or_create_material("M_ShopCounter", (0.72, 0.48, 0.22, 1.0), roughness=0.62)
    m_kettle = get_or_create_material("M_AluminiumKettle", (0.78, 0.80, 0.82, 1.0), metallic=0.92, roughness=0.22)

    root = bpy.data.objects.new("DhakaTongTeaStall", None)
    bpy.context.collection.objects.link(root)

    bm = bmesh.new()
    # Timber corner posts
    for sx in (-1.2, 1.2):
        for sy in (-0.95, 0.95):
            create_box_mesh_bm(bm, (sx, sy, 1.15), (0.10, 0.10, 2.3), mat_index=1)
    # Rear & side walls + front serving counter
    create_box_mesh_bm(bm, (0.0, 0.92, 1.05), (2.5, 0.08, 2.1), mat_index=1)
    create_box_mesh_bm(bm, (-1.22, 0.0, 1.05), (0.08, 1.9, 2.1), mat_index=1)
    create_box_mesh_bm(bm, (1.22, 0.0, 1.05), (0.08, 1.9, 2.1), mat_index=1)
    create_box_mesh_bm(bm, (0.0, -0.88, 0.52), (2.5, 0.52, 1.04), mat_index=2)
    # Sloped corrugated tin roof awning
    create_box_mesh_bm(bm, (0.0, -0.15, 2.38), (2.9, 2.55, 0.08), mat_index=0)
    # Customer wooden bench in front
    create_box_mesh_bm(bm, (0.0, -1.65, 0.45), (2.1, 0.42, 0.08), mat_index=1)
    # Tea kettle & biscuit jar on counter
    create_cylinder_bm(bm, (-0.5, -0.85, 1.20), 0.14, 0.28, segs=12, mat_index=3)

    mesh = bpy.data.meshes.new("TongTeaStall_LOD0")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    for m in (m_tin, m_wood, m_counter, m_kettle):
        obj.data.materials.append(m)
    obj.parent = root
    bpy.context.collection.objects.link(obj)
    auto_box_uv_project(obj)
    export_lod_group_fbx(root, "Props/DhakaTongTeaStall.fbx", models_root)


def build_street_furniture_and_poles(models_root: str):
    # Electric Utility Pole + Transformer + Crossarms
    reset_scene()
    m_conc = get_or_create_material("M_PoleConcrete", (0.58, 0.57, 0.55, 1.0), roughness=0.72)
    m_steel = get_or_create_material("M_GalvanizedSteel", (0.45, 0.48, 0.52, 1.0), metallic=0.85, roughness=0.35)
    root = bpy.data.objects.new("DhakaElectricPole", None)
    bpy.context.collection.objects.link(root)
    bm = bmesh.new()
    create_cylinder_bm(bm, (0.0, 0.0, 4.5), 0.16, 9.0, segs=10, mat_index=0)
    create_box_mesh_bm(bm, (0.0, 0.0, 8.3), (2.2, 0.12, 0.14), mat_index=1)
    create_box_mesh_bm(bm, (0.0, 0.0, 7.6), (1.8, 0.12, 0.14), mat_index=1)
    create_cylinder_bm(bm, (0.35, 0.0, 6.4), 0.34, 0.95, segs=12, mat_index=1)
    mesh = bpy.data.meshes.new("ElectricPole_LOD0")
    bm.to_mesh(mesh); bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    obj.data.materials.append(m_conc); obj.data.materials.append(m_steel)
    obj.parent = root; bpy.context.collection.objects.link(obj)
    auto_box_uv_project(obj)
    export_lod_group_fbx(root, "Props/DhakaElectricPole.fbx", models_root)

    # Rooftop Gazi Water Tank Stand + Tank
    reset_scene()
    m_tank = get_or_create_material("M_GaziBlueTank", (0.05, 0.24, 0.56, 1.0), roughness=0.38)
    m_frame = get_or_create_material("M_AngleIron", (0.20, 0.22, 0.25, 1.0), metallic=0.8, roughness=0.45)
    root = bpy.data.objects.new("RooftopWaterTank", None)
    bpy.context.collection.objects.link(root)
    bm = bmesh.new()
    create_box_mesh_bm(bm, (0.0, 0.0, 0.35), (1.6, 1.6, 0.7), mat_index=1)
    create_cylinder_bm(bm, (0.0, 0.0, 1.65), 0.72, 1.9, segs=16, mat_index=0)
    mesh = bpy.data.meshes.new("RooftopWaterTank_LOD0")
    bm.to_mesh(mesh); bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    obj.data.materials.append(m_tank); obj.data.materials.append(m_frame)
    obj.parent = root; bpy.context.collection.objects.link(obj)
    auto_box_uv_project(obj)
    export_lod_group_fbx(root, "Props/RooftopWaterTank.fbx", models_root)


def build_tropical_trees(models_root: str):
    # Krishnachura Broad Scarlet Flowering Tree
    reset_scene()
    m_bark = get_or_create_material("M_TropicalBark", (0.30, 0.22, 0.16, 1.0), roughness=0.88)
    m_leaf = get_or_create_material("M_KrishnachuraFoliage", (0.14, 0.38, 0.14, 1.0), roughness=0.65)
    m_bloom = get_or_create_material("M_KrishnachuraBlossom", (0.86, 0.18, 0.08, 1.0), roughness=0.55)
    root = bpy.data.objects.new("Tree_Krishnachura", None)
    bpy.context.collection.objects.link(root)
    bm = bmesh.new()
    create_cylinder_bm(bm, (0.0, 0.0, 2.2), 0.28, 4.4, segs=8, mat_index=0)
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=3.4, matrix=bpy.data.objects.new("_t", None).matrix_world)
    from mathutils import Matrix
    for i, (cx, cy, cz, rad, mi) in enumerate([
        (0.0, 0.0, 5.8, 3.2, 1),
        (2.1, -1.2, 5.2, 2.2, 2),
        (-2.0, 1.4, 5.4, 2.4, 1),
        (0.4, 1.9, 6.2, 2.1, 2),
    ]):
        ret = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=rad, matrix=Matrix.Translation((cx, cy, cz)) @ Matrix.Diagonal((1.2, 1.2, 0.68, 1.0)))
        vset = set(ret["verts"])
        for f in bm.faces:
            if all(v in vset for v in f.verts):
                f.material_index = mi
    mesh = bpy.data.meshes.new("Krishnachura_LOD0")
    bm.to_mesh(mesh); bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    for m in (m_bark, m_leaf, m_bloom):
        obj.data.materials.append(m)
    obj.parent = root; bpy.context.collection.objects.link(obj)
    export_lod_group_fbx(root, "Foliage/Tree_Krishnachura.fbx", models_root)


def build_mrt6_and_flyover_modules(models_root: str):
    reset_scene()
    m_conc = get_or_create_material("M_MrtConcrete", (0.62, 0.61, 0.59, 1.0), roughness=0.60)
    m_glass = get_or_create_material("M_MrtStationGlass", (0.14, 0.42, 0.44, 1.0), metallic=0.2, roughness=0.14)
    m_brand = get_or_create_material("M_MrtGreenTrim", (0.06, 0.48, 0.22, 1.0), roughness=0.40)

    root = bpy.data.objects.new("MRT6_StationModule", None)
    bpy.context.collection.objects.link(root)
    bm = bmesh.new()
    # Twin central concourse piers + upper platform deck at 11.5m
    create_box_mesh_bm(bm, (-14.0, 0.0, 5.5), (2.4, 2.4, 11.0), mat_index=0)
    create_box_mesh_bm(bm, (14.0, 0.0, 5.5), (2.4, 2.4, 11.0), mat_index=0)
    create_box_mesh_bm(bm, (0.0, 0.0, 11.2), (46.0, 18.0, 1.6), mat_index=0)
    create_box_mesh_bm(bm, (0.0, 0.0, 14.5), (44.0, 16.5, 4.6), mat_index=1)
    create_box_mesh_bm(bm, (0.0, 0.0, 17.1), (48.0, 19.5, 0.7), mat_index=2)

    mesh = bpy.data.meshes.new("MRT6Station_LOD0")
    bm.to_mesh(mesh); bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    for m in (m_conc, m_glass, m_brand):
        obj.data.materials.append(m)
    obj.parent = root; bpy.context.collection.objects.link(obj)
    auto_box_uv_project(obj)
    export_lod_group_fbx(root, "Transit/MRT6_StationModule.fbx", models_root)


def run_all_props_and_foliage(models_root: str):
    build_tea_stall(models_root)
    build_street_furniture_and_poles(models_root)
    build_tropical_trees(models_root)
    build_mrt6_and_flyover_modules(models_root)
