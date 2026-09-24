using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Carve
{
    public static class VoxelMesher
    {
        const int MaxVertices = 262144;

        public static void Build(VoxelStore store, Mesh mesh)
        {
            var vertices = new NativeArray<Vector3>(MaxVertices, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var normals = new NativeArray<Vector3>(MaxVertices, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var triangles = new NativeArray<int>(MaxVertices / 2 * 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var vertexCount = new NativeArray<int>(1, Allocator.TempJob);
            try
            {
                new GreedyMeshJob
                {
                    Cells = store.Cells,
                    Size = VoxelStore.Size,
                    VoxelSize = VoxelStore.VoxelSize,
                    Origin = store.Origin,
                    Vertices = vertices,
                    Normals = normals,
                    Triangles = triangles,
                    VertexCount = vertexCount
                }.Schedule().Complete();

                int vc = math.min(vertexCount[0], MaxVertices);
                int tc = (vc / 4) * 6;
                mesh.Clear();
                mesh.indexFormat = vc > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                if (vc > 0)
                {
                    mesh.SetVertices(vertices.GetSubArray(0, vc));
                    mesh.SetNormals(normals.GetSubArray(0, vc));
                    mesh.SetIndices(triangles.GetSubArray(0, tc), MeshTopology.Triangles, 0, true);
                }

                mesh.RecalculateBounds();
            }
            finally
            {
                vertices.Dispose();
                normals.Dispose();
                triangles.Dispose();
                vertexCount.Dispose();
            }
        }

        [BurstCompile]
        struct GreedyMeshJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Cells;
            public int Size;
            public float VoxelSize;
            public float3 Origin;
            public NativeArray<Vector3> Vertices;
            public NativeArray<Vector3> Normals;
            public NativeArray<int> Triangles;
            public NativeArray<int> VertexCount;

            byte At(int x, int y, int z)
            {
                if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || (uint)z >= (uint)Size)
                    return 0;
                return Cells[x + Size * (y + Size * z)];
            }

            public void Execute()
            {
                int n = Size;
                var mask = new NativeArray<int>(n * n, Allocator.Temp);
                int vertCount = 0;

                for (int d = 0; d < 3; d++)
                {
                    int u = (d + 1) % 3;
                    int v = (d + 2) % 3;
                    int3 x = 0;
                    int3 q = 0;
                    q[d] = 1;

                    for (x[d] = -1; x[d] < n;)
                    {
                        int nMask = 0;
                        for (x[v] = 0; x[v] < n; x[v]++)
                        {
                            for (x[u] = 0; x[u] < n; x[u]++)
                            {
                                int a = x[d] >= 0 ? At(x.x, x.y, x.z) : 0;
                                int3 xp = x + q;
                                int b = x[d] < n - 1 ? At(xp.x, xp.y, xp.z) : 0;
                                if (a != 0 && b == 0)
                                    mask[nMask] = 1;
                                else if (a == 0 && b != 0)
                                    mask[nMask] = -1;
                                else
                                    mask[nMask] = 0;
                                nMask++;
                            }
                        }

                        x[d]++;

                        nMask = 0;
                        for (int j = 0; j < n; j++)
                        {
                            for (int i = 0; i < n;)
                            {
                                int c = mask[nMask];
                                if (c != 0)
                                {
                                    int w = 1;
                                    while (i + w < n && mask[nMask + w] == c)
                                        w++;

                                    int h = 1;
                                    bool done = false;
                                    while (j + h < n && !done)
                                    {
                                        for (int k = 0; k < w; k++)
                                        {
                                            if (mask[nMask + k + h * n] != c)
                                            {
                                                done = true;
                                                break;
                                            }
                                        }

                                        if (!done)
                                            h++;
                                    }

                                    if (vertCount + 4 <= Vertices.Length)
                                    {
                                        int3 originVoxel = 0;
                                        originVoxel[d] = x[d];
                                        originVoxel[u] = i;
                                        originVoxel[v] = j;
                                        int3 du = 0;
                                        du[u] = w;
                                        int3 dv = 0;
                                        dv[v] = h;
                                        EmitQuad(ref vertCount, originVoxel, du, dv, d, c);
                                    }

                                    for (int l = 0; l < h; l++)
                                    {
                                        for (int k = 0; k < w; k++)
                                            mask[nMask + k + l * n] = 0;
                                    }

                                    i += w;
                                    nMask += w;
                                }
                                else
                                {
                                    i++;
                                    nMask++;
                                }
                            }
                        }
                    }
                }

                VertexCount[0] = vertCount;
                mask.Dispose();
            }

            void EmitQuad(ref int vertCount, int3 originVoxel, int3 du, int3 dv, int axis, int sign)
            {
                float3 p0 = Origin + (float3)originVoxel * VoxelSize;
                float3 p1 = Origin + (float3)(originVoxel + du) * VoxelSize;
                float3 p2 = Origin + (float3)(originVoxel + du + dv) * VoxelSize;
                float3 p3 = Origin + (float3)(originVoxel + dv) * VoxelSize;
                float3 nrm = new float3(0f, 0f, 0f);
                nrm[axis] = sign;

                if (sign > 0)
                {
                    WriteVert(ref vertCount, p0, nrm);
                    WriteVert(ref vertCount, p1, nrm);
                    WriteVert(ref vertCount, p2, nrm);
                    WriteVert(ref vertCount, p3, nrm);
                }
                else
                {
                    WriteVert(ref vertCount, p0, nrm);
                    WriteVert(ref vertCount, p3, nrm);
                    WriteVert(ref vertCount, p2, nrm);
                    WriteVert(ref vertCount, p1, nrm);
                }

                int start = vertCount - 4;
                int tri = (start / 4) * 6;
                Triangles[tri] = start;
                Triangles[tri + 1] = start + 1;
                Triangles[tri + 2] = start + 2;
                Triangles[tri + 3] = start;
                Triangles[tri + 4] = start + 2;
                Triangles[tri + 5] = start + 3;
            }

            void WriteVert(ref int vertCount, float3 position, float3 normal)
            {
                Vertices[vertCount] = new Vector3(position.x, position.y, position.z);
                Normals[vertCount] = new Vector3(normal.x, normal.y, normal.z);
                vertCount++;
            }
        }
    }
}
