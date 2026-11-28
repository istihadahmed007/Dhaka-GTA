"""
Blender generator for Dhaka architectural categories 1-6 & 8:
1. Gulshan glass skyscrapers (curtain mullions, setback podiums, helipad/rooftop plant)
2. Banani commercial buildings (cantilevered balconies & retail storefront plinths)
3. Old Dhaka historic heritage buildings (colonnade arches, cornices, weathered terracotta brick)
4. Motijheel office towers (brutalist precast ribs, recessed glazing strips)
5. Residential apartments (balcony stacks, veranda grilles, rooftop Gazi water tanks & stairhead)
6. Markets and shopping centers (broad atrium entrance canopies & signage fascia)
8. Standalone rooftop structures (Gazi cylindrical water tanks on angle-iron stands, HVAC chillers)
"""
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


def build_gulshan_skyscraper(variant_index: int, models_root: str):
    reset_scene()
    m_glass = get_or_create_material("M_Gulshan_CurtainGlass", (0.12, 0.28, 0.36, 1.0), metallic=0.25, roughness=0.12)
    m_mullion = get_or_create_material("M_Gulshan_Mullion", (0.18, 0.20, 0.24, 1.0), metallic=0.85, roughness=0.35)
    m_stone = get_or_create_material("M_GranitePodium", (0.45, 0.44, 0.42, 1.0), metallic=0.0, roughness=0.65)
    m_plant = get_or_create_material("M_RooftopPlant", (0.52, 0.55, 0.58, 1.0), metallic=0.4, roughness=0.55)

    floors = 16 if variant_index == 0 else 22
    tower_h = floors * 3.8
    w = 24.0 if variant_index == 0 else 20.0
    d = 20.0 if variant_index == 0 else 26.0

    root = bpy.data.objects.new(f"GulshanSkyscraper_Var{variant_index}", None)
    bpy.context.collection.objects.link(root)

    # LOD0: Full articulated podium + recessed mullions + rooftop louver crown + chiller units
    bm0 = bmesh.new()
    create_box_mesh_bm(bm0, (0.0, 0.0, 4.5), (w + 4.0, d + 4.0, 9.0), mat_index=2)
    create_box_mesh_bm(bm0, (0.0, -(d * 0.5 + 3.2), 4.8), (10.0, 3.2, 0.6), mat_index=1)  # entrance canopy
    create_box_mesh_bm(bm0, (0.0, 0.0, 9.0 + tower_h * 0.5), (w - 0.6, d - 0.6, tower_h), mat_index=0)

    # Exterior structural fins / mullions (never flat texture-only facade!)
    fin_cols = 5
    for i in range(fin_cols):
        x = -w * 0.45 + i * (w * 0.9 / (fin_cols - 1))
        create_box_mesh_bm(bm0, (x, -d * 0.5, 9.0 + tower_h * 0.5), (0.45, 0.65, tower_h), mat_index=1)
        create_box_mesh_bm(bm0, (x, d * 0.5, 9.0 + tower_h * 0.5), (0.45, 0.65, tower_h), mat_index=1)

    for fl in range(1, floors, 2):
        z = 9.0 + fl * 3.8
        create_box_mesh_bm(bm0, (0.0, 0.0, z), (w + 0.25, d + 0.25, 0.45), mat_index=1)

    # Crown & Rooftop HVAC + Tank
    create_box_mesh_bm(bm0, (0.0, 0.0, 9.0 + tower_h + 1.4), (w * 0.85, d * 0.85, 2.8), mat_index=3)
    create_cylinder_bm(bm0, (3.5, 2.0, 9.0 + tower_h + 3.8), 1.4, 2.6, mat_index=3)

    mesh0 = bpy.data.meshes.new(f"GulshanSkyscraper_Var{variant_index}_LOD0")
    bm0.to_mesh(mesh0)
    bm0.free()
    lod0 = bpy.data.objects.new(mesh0.name, mesh0)
    for m in (m_glass, m_mullion, m_stone, m_plant):
        lod0.data.materials.append(m)
    lod0.parent = root
    bpy.context.collection.objects.link(lod0)
    auto_box_uv_project(lod0)

    # LOD2: Simplified stepped massing
    bm2 = bmesh.new()
    create_box_mesh_bm(bm2, (0.0, 0.0, 4.5), (w + 4.0, d + 4.0, 9.0), mat_index=2)
    create_box_mesh_bm(bm2, (0.0, 0.0, 9.0 + tower_h * 0.5), (w, d, tower_h), mat_index=0)
    mesh2 = bpy.data.meshes.new(f"GulshanSkyscraper_Var{variant_index}_LOD2")
    bm2.to_mesh(mesh2)
    bm2.free()
    lod2 = bpy.data.objects.new(mesh2.name, mesh2)
    for m in (m_glass, m_mullion, m_stone, m_plant):
        lod2.data.materials.append(m)
    lod2.parent = root
    bpy.context.collection.objects.link(lod2)

    export_lod_group_fbx(root, f"Buildings/GulshanSkyscraper_Var{variant_index}.fbx", models_root)


def build_old_dhaka_heritage(variant_index: int, models_root: str):
    reset_scene()
    m_plaster = get_or_create_material("M_OldDhakaPlaster", (0.80, 0.72, 0.58, 1.0), roughness=0.78)
    m_brick = get_or_create_material("M_OldDhakaBrick", (0.58, 0.28, 0.21, 1.0), roughness=0.82)
    m_trim = get_or_create_material("M_ColonialTrim", (0.90, 0.88, 0.82, 1.0), roughness=0.68)
    m_wood = get_or_create_material("M_ShutterWood", (0.20, 0.34, 0.24, 1.0), roughness=0.72)

    w = 16.0 if variant_index == 0 else 20.0
    d = 12.0
    h = 10.8 if variant_index == 0 else 14.0

    root = bpy.data.objects.new(f"OldDhakaHeritage_Var{variant_index}", None)
    bpy.context.collection.objects.link(root)

    bm = bmesh.new()
    create_box_mesh_bm(bm, (0.0, 0.0, h * 0.5), (w, d, h), mat_index=0)
    # Colonnade verandah piers along street front
    num_piers = 5
    for i in range(num_piers):
        x = -w * 0.44 + i * (w * 0.88 / (num_piers - 1))
        create_box_mesh_bm(bm, (x, -d * 0.5 - 1.1, h * 0.5), (0.65, 0.65, h), mat_index=2)
        if i < num_piers - 1:
            mid_x = x + (w * 0.88 / (num_piers - 1)) * 0.5
            create_box_mesh_bm(bm, (mid_x, -d * 0.5 - 0.8, 3.6), (w * 0.22, 0.45, 0.75), mat_index=1)
            create_box_mesh_bm(bm, (mid_x, -d * 0.5 + 0.1, 6.2), (1.3, 0.25, 2.1), mat_index=3)  # louvered shutters

    # Rooftop cornice & parapet balustrade
    create_box_mesh_bm(bm, (0.0, 0.0, h + 0.3), (w + 0.8, d + 2.2, 0.6), mat_index=2)
    create_box_mesh_bm(bm, (0.0, 0.0, h + 1.0), (w, d, 0.85), mat_index=1)

    mesh0 = bpy.data.meshes.new(f"OldDhakaHeritage_Var{variant_index}_LOD0")
    bm.to_mesh(mesh0)
    bm.free()
    lod0 = bpy.data.objects.new(mesh0.name, mesh0)
    for m in (m_plaster, m_brick, m_trim, m_wood):
        lod0.data.materials.append(m)
    lod0.parent = root
    bpy.context.collection.objects.link(lod0)
    auto_box_uv_project(lod0)

    export_lod_group_fbx(root, f"Buildings/OldDhakaHeritage_Var{variant_index}.fbx", models_root)


def build_motijheel_banani_apartment_market(models_root: str):
    configs = [
        ("Buildings/BananiCommercial_A.fbx", (18.0, 14.0, 28.0), (0.22, 0.34, 0.44, 1.0), (0.72, 0.70, 0.68, 1.0), True),
        ("Buildings/MotijheelOffice_A.fbx", (22.0, 18.0, 48.0), (0.16, 0.22, 0.28, 1.0), (0.58, 0.56, 0.54, 1.0), False),
        ("Buildings/ResidentialApartment_A.fbx", (15.0, 13.0, 21.0), (0.15, 0.24, 0.30, 1.0), (0.82, 0.76, 0.68, 1.0), True),
        ("Buildings/MarketShoppingCenter_A.fbx", (32.0, 24.0, 14.5), (0.20, 0.34, 0.40, 1.0), (0.75, 0.68, 0.60, 1.0), False),
    ]
    for rel_path, (w, d, h), win_col, wall_col, add_balconies in configs:
        reset_scene()
        m_wall = get_or_create_material("M_Wall", wall_col, roughness=0.65)
        m_win = get_or_create_material("M_Win", win_col, metallic=0.2, roughness=0.15)
        m_tank = get_or_create_material("M_GaziTankBlue", (0.05, 0.22, 0.52, 1.0), roughness=0.40)

        root = bpy.data.objects.new("BuildingRoot", None)
        bpy.context.collection.objects.link(root)

        bm = bmesh.new()
        create_box_mesh_bm(bm, (0.0, 0.0, h * 0.5), (w, d, h), mat_index=0)
        floors = max(2, int(h // 3.5))
        for fl in range(1, floors):
            z = fl * 3.5
            create_box_mesh_bm(bm, (0.0, -d * 0.5 - 0.1, z), (w * 0.88, 0.25, 1.7), mat_index=1)
            if add_balconies:
                create_box_mesh_bm(bm, (-w * 0.28, -d * 0.5 - 0.75, z - 0.5), (4.2, 1.5, 0.95), mat_index=0)
                create_box_mesh_bm(bm, (w * 0.28, -d * 0.5 - 0.75, z - 0.5), (4.2, 1.5, 0.95), mat_index=0)

        # Rooftop stairhead & cylindrical Dhaka rooftop water tank
        create_box_mesh_bm(bm, (w * 0.2, 0.0, h + 1.4), (3.5, 3.5, 2.8), mat_index=0)
        create_cylinder_bm(bm, (-w * 0.2, 1.5, h + 1.4), 0.95, 2.1, mat_index=2)
        create_cylinder_bm(bm, (-w * 0.2 + 2.4, 1.5, h + 1.4), 0.95, 2.1, mat_index=2)

        mesh = bpy.data.meshes.new("LOD0")
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new("Mesh_LOD0", mesh)
        for m in (m_wall, m_win, m_tank):
            obj.data.materials.append(m)
        obj.parent = root
        bpy.context.collection.objects.link(obj)
        auto_box_uv_project(obj)
        export_lod_group_fbx(root, rel_path, models_root)


def run_all_buildings(models_root: str):
    build_gulshan_skyscraper(0, models_root)
    build_gulshan_skyscraper(1, models_root)
    build_old_dhaka_heritage(0, models_root)
    build_old_dhaka_heritage(1, models_root)
    build_motijheel_banani_apartment_market(models_root)
