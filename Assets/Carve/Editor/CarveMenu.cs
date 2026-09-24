using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Carve.Editor
{
    public static class CarveMenu
    {
        const string ScenePath = "Assets/Carve/Scenes/CarveSandbox.unity";

        [MenuItem("Carve/打开挖土场景")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void VerifyMesher()
        {
            var store = new VoxelStore(float3.zero);
            var mesh = new Mesh();
            try
            {
                store.FillSolid();
                VoxelMesher.Build(store, mesh);
                if (mesh.vertexCount != 24)
                    throw new Exception("solid cube expected 24 verts, got " + mesh.vertexCount);

                var verts = mesh.vertices;
                var nrms = mesh.normals;
                float3 center = new float3(6.4f, 6.4f, 6.4f);
                for (int i = 0; i < verts.Length; i++)
                {
                    if (math.dot((float3)(Vector3)verts[i] - center, (float3)(Vector3)nrms[i]) <= 0f)
                        throw new Exception("solid cube normals must point outward");
                }

                store.CarveSphere(new float3(0.1f, 0.1f, 0.1f), 0.05f);
                if (store.Cells[VoxelStore.IndexOf(0, 0, 0)] == 0)
                    throw new Exception("bedrock layer should not carve");

                if (!store.CarveSphere(new float3(6.4f, 6.4f, 6.4f), 1.2f))
                    throw new Exception("sphere carve should remove voxels");

                VoxelMesher.Build(store, mesh);
                if (mesh.vertexCount <= 24)
                    throw new Exception("carved mesh should gain interior faces, verts=" + mesh.vertexCount);

                if (!store.TryHit(new float3(6.4f, 20f, 6.4f), new float3(0f, -1f, 0f), 30f, out _))
                    throw new Exception("ray from above should hit the cube");

                Debug.Log("Carve mesher verify ok verts=" + mesh.vertexCount);
            }
            finally
            {
                store.Dispose();
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
