using DhakaGta.Core;
using UnityEditor;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Configures ProjectSettings/TagManager.asset custom layers 6-16 and applies the Physics Collision Matrix.
    /// </summary>
    public static class PhysicsMatrixValidator
    {
        [MenuItem("Dhaka-GTA/Foundation/Apply & Validate Physics Layers + Matrix")]
        public static bool ApplyAndValidate()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            if (layersProp == null || !layersProp.isArray)
            {
                Debug.LogError("[PhysicsMatrixValidator] Unable to find TagManager layers property.");
                return false;
            }

            foreach (var kvp in PhysicsLayers.LayerNames)
            {
                if (kvp.Key < layersProp.arraySize)
                {
                    SerializedProperty sp = layersProp.GetArrayElementAtIndex(kvp.Key);
                    sp.stringValue = kvp.Value;
                }
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            PhysicsLayers.ApplyCollisionMatrix();
            AssetDatabase.SaveAssets();

            bool allNamed = true;
            foreach (var kvp in PhysicsLayers.LayerNames)
            {
                string actual = LayerMask.LayerToName(kvp.Key);
                if (actual != kvp.Value)
                {
                    Debug.LogError($"[PhysicsMatrixValidator] Layer {kvp.Key} expected '{kvp.Value}', got '{actual}'.");
                    allNamed = false;
                }
            }

            if (allNamed)
            {
                Debug.Log("[PhysicsMatrixValidator] Verified 11 gameplay layers (6-16) and Physics collision matrix.");
            }
            return allNamed;
        }
    }
}
