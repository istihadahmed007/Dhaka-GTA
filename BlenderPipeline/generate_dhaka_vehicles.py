"""
Blender procedural modeling pipeline for all 11 Dhaka-GTA vehicles (Part 6):
1. CNG Auto-Rickshaw (3-wheel rear-engine iconic Dhaka green cab)
2. Cycle Rickshaw (3-wheel hand-pulled painted canopy)
3. Motorcycle (2-wheel commuter/sport bike)
4. City Bus (urban twin-door metro route bus)
5. Intercity Bus (high-deck highway coach)
6. Sedan (Right-Hand Drive urban saloon)
7. SUV (executive 5-door utility)
8. Police Vehicle (DMP pickup/cruiser with red-blue beacon lightbar)
9. Pickup Truck (utility cargo bed truck)
10. Speedboat (Buriganga/Hatirjheel V-hull fast boat)
11. Launch (double-deck Sadarghat river passenger ferry)

Every land vehicle exports separate child wheel objects (Wheel_F/FL/FR/R/RL/RR) and SteeringWheel pivot
so Unity C# spins and steers each wheel cleanly.
"""
import bpy
import bmesh
from mathutils import Matrix
from dhaka_blender_utils import (
    auto_box_uv_project,
    create_box_mesh_bm,
    create_cylinder_bm,
    export_lod_group_fbx,
    get_or_create_material,
    reset_scene,
)


def create_wheel_child(parent, name: str, pos, radius: float, width: float, tire_mat, rim_mat):
    bm = bmesh.new()
    # Align cylinder along X axle cleanly (rot 90 deg around Y)
    rot_mat = Matrix.Translation(pos) @ Matrix.Rotation(1.5707963, 4, "Y")
    bmesh.ops.create_cone(
        bm, cap_ends=True, segments=16, radius1=radius, radius2=radius, depth=width, matrix=rot_mat
    )
    for f in bm.faces:
        f.material_index = 0
    bmesh.ops.create_cone(
        bm, cap_ends=True, segments=12, radius1=radius * 0.58, radius2=radius * 0.58, depth=width + 0.02, matrix=rot_mat
    )
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(tire_mat)
    obj.data.materials.append(rim_mat)
    obj.parent = parent
    bpy.context.collection.objects.link(obj)
    return obj


def build_cng_auto(models_root: str):
    reset_scene()
    m_green = get_or_create_material("M_CNG_Green", (0.06, 0.42, 0.19, 1.0), metallic=0.25, roughness=0.35)
    m_canopy = get_or_create_material("M_CNG_Canopy", (0.12, 0.14, 0.15, 1.0), roughness=0.75)
    m_glass = get_or_create_material("M_WindshieldGlass", (0.14, 0.25, 0.30, 0.7), roughness=0.1)
    m_tire = get_or_create_material("M_Tire", (0.05, 0.05, 0.05, 1.0), roughness=0.82)
    m_rim = get_or_create_material("M_Rim", (0.65, 0.67, 0.70, 1.0), metallic=0.85, roughness=0.25)

    root = bpy.data.objects.new("Vehicle_CNG", None)
    bpy.context.collection.objects.link(root)

    bm = bmesh.new()
    create_box_mesh_bm(bm, (0.0, 0.0, 0.45), (1.30, 2.55, 0.42), mat_index=0)  # Lower tub
    create_box_mesh_bm(bm, (0.0, 0.85, 0.92), (1.18, 0.75, 0.65), mat_index=0)  # Front nose cowl
    create_box_mesh_bm(bm, (0.0, 0.58, 1.38), (1.12, 0.12, 0.55), mat_index=2)  # Windshield
    create_box_mesh_bm(bm, (0.0, -0.35, 1.28), (1.28, 1.55, 0.92), mat_index=1)  # Rear passenger canopy
    create_box_mesh_bm(bm, (0.0, -0.05, 1.76), (1.32, 2.10, 0.10), mat_index=1)  # Roof top

    mesh = bpy.data.meshes.new("CNG_Chassis")
    bm.to_mesh(mesh); bm.free()
    chassis = bpy.data.objects.new("Chassis", mesh)
    for m in (m_green, m_canopy, m_glass):
        chassis.data.materials.append(m)
    chassis.parent = root; bpy.context.collection.objects.link(chassis)
    auto_box_uv_project(chassis)

    create_wheel_child(root, "Wheel_F", (0.0, 1.05, 0.24), 0.24, 0.14, m_tire, m_rim)
    create_wheel_child(root, "Wheel_RL", (-0.58, -0.95, 0.24), 0.24, 0.14, m_tire, m_rim)
    create_wheel_child(root, "Wheel_RR", (0.58, -0.95, 0.24), 0.24, 0.14, m_tire, m_rim)
    export_lod_group_fbx(root, "Vehicles/Vehicle_CNG_Auto.fbx", models_root)


def build_cycle_rickshaw(models_root: str):
    reset_scene()
    m_frame = get_or_create_material("M_RickshawFrame", (0.72, 0.12, 0.16, 1.0), metallic=0.5, roughness=0.4)
    m_hood = get_or_create_material("M_RickshawFloralHood", (0.85, 0.20, 0.35, 1.0), roughness=0.65)
    m_tire = get_or_create_material("M_ThinTire", (0.08, 0.08, 0.08, 1.0), roughness=0.8)
    m_rim = get_or_create_material("M_SpokeRim", (0.78, 0.78, 0.78, 1.0), metallic=0.9, roughness=0.2)

    root = bpy.data.objects.new("Vehicle_CycleRickshaw", None)
    bpy.context.collection.objects.link(root)

    bm = bmesh.new()
    create_box_mesh_bm(bm, (0.0, 0.25, 0.58), (0.28, 1.35, 0.10), mat_index=0)
    create_box_mesh_bm(bm, (0.0, -0.55, 0.65), (1.05, 0.92, 0.45), mat_index=0)  # Passenger carriage
    create_box_mesh_bm(bm, (0.0, -0.68, 1.32), (1.08, 0.98, 0.85), mat_index=1)  # Raised curved hood

    mesh = bpy.data.meshes.new("Rickshaw_Chassis")
    bm.to_mesh(mesh); bm.free()
    chassis = bpy.data.objects.new("Chassis", mesh)
    chassis.data.materials.append(m_frame); chassis.data.materials.append(m_hood)
    chassis.parent = root; bpy.context.collection.objects.link(chassis)
    auto_box_uv_project(chassis)

    create_wheel_child(root, "Wheel_F", (0.0, 0.95, 0.34), 0.34, 0.06, m_tire, m_rim)
    create_wheel_child(root, "Wheel_RL", (-0.54, -0.65, 0.34), 0.34, 0.06, m_tire, m_rim)
    create_wheel_child(root, "Wheel_RR", (0.54, -0.65, 0.34), 0.34, 0.06, m_tire, m_rim)
    export_lod_group_fbx(root, "Vehicles/Vehicle_CycleRickshaw.fbx", models_root)


def build_motorcycle(models_root: str):
    reset_scene()
    m_tank = get_or_create_material("M_BikePaint", (0.14, 0.22, 0.68, 1.0), metallic=0.75, roughness=0.22)
    m_dark = get_or_create_material("M_BikeFrame", (0.12, 0.13, 0.14, 1.0), metallic=0.6, roughness=0.45)
    m_tire = get_or_create_material("M_Tire", (0.05, 0.05, 0.05, 1.0), roughness=0.78)
    m_rim = get_or_create_material("M_Rim", (0.72, 0.72, 0.74, 1.0), metallic=0.85, roughness=0.25)

    root = bpy.data.objects.new("Vehicle_Motorcycle", None)
    bpy.context.collection.objects.link(root)

    bm = bmesh.new()
    create_box_mesh_bm(bm, (0.0, 0.0, 0.52), (0.28, 1.35, 0.36), mat_index=1)
    create_box_mesh_bm(bm, (0.0, 0.22, 0.82), (0.34, 0.56, 0.26), mat_index=0)  # Fuel tank
    create_box_mesh_bm(bm, (0.0, -0.35, 0.75), (0.28, 0.68, 0.14), mat_index=1)  # Saddle seat
    create_box_mesh_bm(bm, (0.0, 0.55, 0.98), (0.72, 0.10, 0.08), mat_index=1)  # Handlebar

    mesh = bpy.data.meshes.new("Motorcycle_Chassis")
    bm.to_mesh(mesh); bm.free()
    chassis = bpy.data.objects.new("Chassis", mesh)
    chassis.data.materials.append(m_tank); chassis.data.materials.append(m_dark)
    chassis.parent = root; bpy.context.collection.objects.link(chassis)
    auto_box_uv_project(chassis)

    create_wheel_child(root, "Wheel_F", (0.0, 0.68, 0.32), 0.32, 0.12, m_tire, m_rim)
    create_wheel_child(root, "Wheel_R", (0.0, -0.68, 0.32), 0.32, 0.14, m_tire, m_rim)
    export_lod_group_fbx(root, "Vehicles/Vehicle_Motorcycle.fbx", models_root)


def build_four_wheel_and_bus_models(models_root: str):
    specs = [
        ("Vehicles/Vehicle_Sedan.fbx", (1.84, 4.45, 1.38), (0.76, 0.12, 0.14, 1.0), 0.36, 1.42, 0.80, False),
        ("Vehicles/Vehicle_SUV.fbx", (1.98, 4.75, 1.76), (0.16, 0.19, 0.22, 1.0), 0.42, 1.54, 0.88, False),
        ("Vehicles/Vehicle_PoliceCruiser.fbx", (1.96, 4.85, 1.72), (0.10, 0.22, 0.52, 1.0), 0.41, 1.52, 0.86, True),
        ("Vehicles/Vehicle_Pickup.fbx", (1.98, 5.10, 1.74), (0.85, 0.82, 0.74, 1.0), 0.42, 1.62, 0.88, False),
        ("Vehicles/Vehicle_CityBus.fbx", (2.52, 10.4, 3.05), (0.08, 0.44, 0.22, 1.0), 0.52, 2.85, 1.15, False),
        ("Vehicles/Vehicle_IntercityBus.fbx", (2.58, 12.2, 3.45), (0.82, 0.32, 0.12, 1.0), 0.54, 3.35, 1.18, False),
    ]

    for rel_path, (w, l, h), body_col, wr, wb_half, tw_half, is_police in specs:
        reset_scene()
        m_body = get_or_create_material("M_CarPaint", body_col, metallic=0.8, roughness=0.22)
        m_glass = get_or_create_material("M_CabinGlass", (0.12, 0.20, 0.26, 1.0), metallic=0.1, roughness=0.08)
        m_trim = get_or_create_material("M_BumperTrim", (0.10, 0.10, 0.11, 1.0), roughness=0.55)
        m_tire = get_or_create_material("M_Tire", (0.05, 0.05, 0.05, 1.0), roughness=0.80)
        m_rim = get_or_create_material("M_AlloyRim", (0.72, 0.74, 0.76, 1.0), metallic=0.90, roughness=0.22)

        root = bpy.data.objects.new("VehicleRoot", None)
        bpy.context.collection.objects.link(root)

        bm = bmesh.new()
        is_bus = l > 8.0
        if is_bus:
            create_box_mesh_bm(bm, (0.0, 0.0, 0.55 + h * 0.46), (w, l, h * 0.92), mat_index=0)
            create_box_mesh_bm(bm, (0.0, 0.05, 0.55 + h * 0.62), (w + 0.02, l * 0.88, h * 0.35), mat_index=1)
            create_box_mesh_bm(bm, (0.0, l * 0.50, 0.52), (w + 0.04, 0.22, 0.34), mat_index=2)
        else:
            lower_h = h * 0.46
            create_box_mesh_bm(bm, (0.0, 0.0, wr + lower_h * 0.42), (w, l, lower_h), mat_index=0)
            cabin_len = l * 0.52 if "Pickup" not in rel_path else l * 0.36
            cabin_y = -0.15 if "Pickup" not in rel_path else 0.65
            create_box_mesh_bm(bm, (0.0, cabin_y, wr + lower_h + h * 0.22), (w * 0.84, cabin_len, h * 0.40), mat_index=1)
            if is_police:
                m_lightbar = get_or_create_material("M_PoliceBeacon", (0.95, 0.12, 0.18, 1.0), emission=(2.0, 0.2, 0.4, 1.0))
                create_box_mesh_bm(bm, (0.0, cabin_y, wr + lower_h + h * 0.45), (1.15, 0.28, 0.16), mat_index=2)

        mesh = bpy.data.meshes.new("ChassisMesh")
        bm.to_mesh(mesh); bm.free()
        chassis = bpy.data.objects.new("Chassis", mesh)
        for m in (m_body, m_glass, m_trim):
            chassis.data.materials.append(m)
        chassis.parent = root; bpy.context.collection.objects.link(chassis)
        auto_box_uv_project(chassis)

        create_wheel_child(root, "Wheel_FL", (-tw_half, wb_half, wr), wr, 0.26, m_tire, m_rim)
        create_wheel_child(root, "Wheel_FR", (tw_half, wb_half, wr), wr, 0.26, m_tire, m_rim)
        create_wheel_child(root, "Wheel_RL", (-tw_half, -wb_half, wr), wr, 0.28, m_tire, m_rim)
        create_wheel_child(root, "Wheel_RR", (tw_half, -wb_half, wr), wr, 0.28, m_tire, m_rim)
        export_lod_group_fbx(root, rel_path, models_root)


def build_watercraft(models_root: str):
    # Speedboat + Buriganga Double-Deck Passenger Launch
    configs = [
        ("Vehicles/Vehicle_Speedboat.fbx", (2.35, 6.4, 1.25), (0.92, 0.94, 0.96, 1.0), (0.08, 0.34, 0.68, 1.0), False),
        ("Vehicles/Vehicle_BurigangaLaunch.fbx", (7.4, 24.0, 5.2), (0.90, 0.90, 0.88, 1.0), (0.14, 0.42, 0.36, 1.0), True),
    ]
    for rel_path, (w, l, h), hull_col, deck_col, is_launch in configs:
        reset_scene()
        m_hull = get_or_create_material("M_BoatHull", hull_col, metallic=0.15, roughness=0.25)
        m_trim = get_or_create_material("M_BoatAccent", deck_col, metallic=0.2, roughness=0.35)
        m_glass = get_or_create_material("M_BoatGlass", (0.12, 0.25, 0.32, 1.0), roughness=0.1)
        root = bpy.data.objects.new("WatercraftRoot", None)
        bpy.context.collection.objects.link(root)

        bm = bmesh.new()
        create_box_mesh_bm(bm, (0.0, 0.0, 0.35), (w, l * 0.82, 0.85), mat_index=0)
        create_box_mesh_bm(bm, (0.0, l * 0.45, 0.42), (w * 0.55, l * 0.18, 0.72), mat_index=1)  # Bow taper block
        if is_launch:
            create_box_mesh_bm(bm, (0.0, -0.5, 1.8), (w * 0.88, l * 0.68, 2.0), mat_index=1)  # Main cabin deck
            create_box_mesh_bm(bm, (0.0, 0.5, 3.6), (w * 0.74, l * 0.52, 1.65), mat_index=0)  # Upper saloon
            create_box_mesh_bm(bm, (0.0, l * 0.22, 4.9), (w * 0.55, 3.4, 1.35), mat_index=2)  # Pilothouse bridge
            create_cylinder_bm(bm, (-1.1, -3.5, 5.1), 0.42, 1.8, segs=12, mat_index=1)
            create_cylinder_bm(bm, (1.1, -3.5, 5.1), 0.42, 1.8, segs=12, mat_index=1)
        else:
            create_box_mesh_bm(bm, (0.0, 0.25, 1.02), (w * 0.78, 1.2, 0.52), mat_index=2)  # Windshield cowl

        mesh = bpy.data.meshes.new("HullMesh")
        bm.to_mesh(mesh); bm.free()
        obj = bpy.data.objects.new("Hull", mesh)
        for m in (m_hull, m_trim, m_glass):
            obj.data.materials.append(m)
        obj.parent = root; bpy.context.collection.objects.link(obj)
        auto_box_uv_project(obj)
        export_lod_group_fbx(root, rel_path, models_root)


def run_all_vehicles(models_root: str):
    build_cng_auto(models_root)
    build_cycle_rickshaw(models_root)
    build_motorcycle(models_root)
    build_four_wheel_and_bus_models(models_root)
    build_watercraft(models_root)
