using System;
using System.Collections.Generic;
using UnityEngine;

namespace DhakaGta.Environment
{
    /// <summary>
    /// High-throughput GPU instancing batcher for repeated Dhaka trees, streetlamps, utility poles, and bollards
    /// using Unity 6 RenderMeshInstanced API (RenderParams + slices of <=1023 transforms per batch).
    /// </summary>
    public sealed class GpuInstancedPropBatcher : MonoBehaviour
    {
        [Serializable]
        public sealed class InstancedLayer
        {
            public string label;
            public Mesh mesh;
            public Material material;
            public List<Matrix4x4> matrices = new List<Matrix4x4>();
            public bool castShadows = true;
            public int unityLayer;
        }

        private const int MaxInstancesPerBatch = 1023;
        [SerializeField] private List<InstancedLayer> layers = new List<InstancedLayer>();
        private readonly Matrix4x4[] _scratchBatch = new Matrix4x4[MaxInstancesPerBatch];

        public IReadOnlyList<InstancedLayer> Layers => layers;

        public void ClearAll()
        {
            layers.Clear();
        }

        public void RegisterInstance(string label, Mesh mesh, Material material, Matrix4x4 matrix, int layer = 0, bool castShadows = true)
        {
            if (mesh == null || material == null) return;
            if (!material.enableInstancing)
            {
                material.enableInstancing = true;
            }

            InstancedLayer target = null;
            foreach (InstancedLayer entry in layers)
            {
                if (entry.label == label && entry.mesh == mesh && entry.material == material)
                {
                    target = entry;
                    break;
                }
            }

            if (target == null)
            {
                target = new InstancedLayer
                {
                    label = label,
                    mesh = mesh,
                    material = material,
                    castShadows = castShadows,
                    unityLayer = layer,
                };
                layers.Add(target);
            }

            target.matrices.Add(matrix);
        }

        private void Update()
        {
            RenderAllLayers();
        }

        public void RenderAllLayers()
        {
            foreach (InstancedLayer layer in layers)
            {
                if (layer?.mesh == null || layer.material == null || layer.matrices == null || layer.matrices.Count == 0)
                {
                    continue;
                }

                if (!layer.material.enableInstancing)
                {
                    layer.material.enableInstancing = true;
                }

                var rp = new RenderParams(layer.material)
                {
                    layer = layer.unityLayer,
                    shadowCastingMode = layer.castShadows
                        ? UnityEngine.Rendering.ShadowCastingMode.On
                        : UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = true,
                };

                int total = layer.matrices.Count;
                for (int offset = 0; offset < total; offset += MaxInstancesPerBatch)
                {
                    int batchCount = Mathf.Min(MaxInstancesPerBatch, total - offset);
                    layer.matrices.CopyTo(offset, _scratchBatch, 0, batchCount);
                    Graphics.RenderMeshInstanced(rp, layer.mesh, 0, _scratchBatch, batchCount);
                }
            }
        }
    }
}
