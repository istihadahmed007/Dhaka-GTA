"""
Master headless runner for Blender:
  blender --background --python BlenderPipeline/build_all_dhaka_fbx.py
Exports all 12 categories of original Dhaka architectural, prop, transit, and tropical foliage FBX assets
directly into UnityProject/Assets/Models/Environment/.
"""
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

from generate_dhaka_buildings import run_all_buildings
from generate_dhaka_props_and_foliage import run_all_props_and_foliage
from generate_dhaka_vehicles import run_all_vehicles
from generate_dhaka_characters import run_all_characters


def main():
    repo_root = os.path.dirname(SCRIPT_DIR)
    env_models_root = os.path.join(repo_root, "UnityProject", "Assets", "Models", "Environment")
    base_models_root = os.path.join(repo_root, "UnityProject", "Assets", "Models")
    os.makedirs(env_models_root, exist_ok=True)
    os.makedirs(os.path.join(base_models_root, "Characters"), exist_ok=True)
    run_all_buildings(env_models_root)
    run_all_props_and_foliage(env_models_root)
    run_all_vehicles(env_models_root)
    run_all_characters(base_models_root)
    print("Completed exporting all Dhaka Blender FBX assets (Environment + 11 Vehicles + Tariq/Maya Characters).")


if __name__ == "__main__":
    main()
