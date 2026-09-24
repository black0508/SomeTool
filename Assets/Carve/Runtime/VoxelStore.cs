using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Carve
{
    public sealed class VoxelStore : IDisposable
    {
        public const int Size = 64;
        public const float VoxelSize = 0.2f;

        public NativeArray<byte> Cells;
        public float3 Origin { get; }

        public VoxelStore(float3 origin)
        {
            Origin = origin;
            Cells = new NativeArray<byte>(Size * Size * Size, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        public static int IndexOf(int x, int y, int z) => x + Size * (y + Size * z);

        public void FillSolid()
        {
            for (int i = 0; i < Cells.Length; i++)
                Cells[i] = 1;
        }

        public bool CarveSphere(float3 center, float radius)
        {
            float r = math.max(radius, VoxelSize);
            float rSq = r * r;
            int3 minC = math.clamp((int3)math.floor((center - r - Origin) / VoxelSize), 0, Size - 1);
            int3 maxC = math.clamp((int3)math.floor((center + r - Origin) / VoxelSize), 0, Size - 1);
            bool changed = false;
            for (int z = minC.z; z <= maxC.z; z++)
            {
                for (int y = minC.y; y <= maxC.y; y++)
                {
                    if (y == 0)
                        continue;

                    for (int x = minC.x; x <= maxC.x; x++)
                    {
                        float3 voxelCenter = Origin + new float3(x + 0.5f, y + 0.5f, z + 0.5f) * VoxelSize;
                        if (math.lengthsq(voxelCenter - center) > rSq)
                            continue;

                        int index = IndexOf(x, y, z);
                        if (Cells[index] == 0)
                            continue;

                        Cells[index] = 0;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        public bool TryHit(float3 origin, float3 direction, float maxDistance, out float3 hitPoint)
        {
            hitPoint = origin;
            float3 dir = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            float3 bMin = Origin;
            float3 bMax = Origin + VoxelSize * Size;
            if (!RayAabb(origin, dir, maxDistance, bMin, bMax, out float tEnter, out float tExit))
                return false;

            tEnter = math.max(tEnter, 0f);
            tExit = math.min(tExit, maxDistance);
            if (tEnter > tExit)
                return false;

            float3 p = origin + dir * (tEnter + VoxelSize * 1e-4f);
            int3 v = (int3)math.floor((p - Origin) / VoxelSize);
            v = math.clamp(v, 0, Size - 1);

            int3 step = new int3(dir.x >= 0f ? 1 : -1, dir.y >= 0f ? 1 : -1, dir.z >= 0f ? 1 : -1);
            float3 nextBoundary = Origin + new float3(
                (dir.x >= 0f ? v.x + 1 : v.x) * VoxelSize,
                (dir.y >= 0f ? v.y + 1 : v.y) * VoxelSize,
                (dir.z >= 0f ? v.z + 1 : v.z) * VoxelSize);
            float3 inv = new float3(
                math.abs(dir.x) < 1e-8f ? 1e20f : 1f / dir.x,
                math.abs(dir.y) < 1e-8f ? 1e20f : 1f / dir.y,
                math.abs(dir.z) < 1e-8f ? 1e20f : 1f / dir.z);
            float3 tMax = (nextBoundary - origin) * inv;
            float3 tDelta = VoxelSize * math.abs(inv);

            for (int i = 0; i < Size * 3; i++)
            {
                if ((uint)v.x >= (uint)Size || (uint)v.y >= (uint)Size || (uint)v.z >= (uint)Size)
                    return false;

                if (Cells[IndexOf(v.x, v.y, v.z)] != 0)
                {
                    hitPoint = Origin + ((float3)v + 0.5f) * VoxelSize;
                    return true;
                }

                if (tMax.x < tMax.y)
                {
                    if (tMax.x < tMax.z)
                    {
                        if (tMax.x > tExit)
                            return false;
                        v.x += step.x;
                        tMax.x += tDelta.x;
                    }
                    else
                    {
                        if (tMax.z > tExit)
                            return false;
                        v.z += step.z;
                        tMax.z += tDelta.z;
                    }
                }
                else if (tMax.y < tMax.z)
                {
                    if (tMax.y > tExit)
                        return false;
                    v.y += step.y;
                    tMax.y += tDelta.y;
                }
                else
                {
                    if (tMax.z > tExit)
                        return false;
                    v.z += step.z;
                    tMax.z += tDelta.z;
                }
            }

            return false;
        }

        static bool RayAabb(
            float3 origin,
            float3 dir,
            float maxDistance,
            float3 min,
            float3 max,
            out float tEnter,
            out float tExit)
        {
            float3 inv = new float3(
                math.abs(dir.x) < 1e-8f ? 1e20f : 1f / dir.x,
                math.abs(dir.y) < 1e-8f ? 1e20f : 1f / dir.y,
                math.abs(dir.z) < 1e-8f ? 1e20f : 1f / dir.z);
            float3 t0 = (min - origin) * inv;
            float3 t1 = (max - origin) * inv;
            float3 tmin = math.min(t0, t1);
            float3 tmax = math.max(t0, t1);
            tEnter = math.cmax(tmin);
            tExit = math.cmin(tmax);
            return tExit >= 0f && tEnter <= tExit && tEnter <= maxDistance;
        }

        public void Dispose()
        {
            if (Cells.IsCreated)
                Cells.Dispose();
        }
    }
}
