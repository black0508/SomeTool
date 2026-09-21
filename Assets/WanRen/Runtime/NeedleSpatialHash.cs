using System;
using Unity.Collections;
using Unity.Mathematics;

namespace WanRen
{
    public sealed class NeedleSpatialHash : IDisposable
    {
        NeedlePileParams _pile;
        int _count;
        NativeArray<float3> _positions;
        NativeArray<uint> _alive;
        NativeArray<byte> _marked;
        NativeArray<int> _cells;
        bool _built;

        public void Build(NeedleStore store, NeedlePileParams pile)
        {
            DisposeArrays();
            _pile = pile;
            _count = store.Count;
            _positions = store.Positions;
            _alive = store.Alive;
            int cellCount = math.max(pile.CellCount, 1);
            _marked = new NativeArray<byte>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _cells = new NativeArray<int>(math.min(cellCount, 512), Allocator.Persistent);
            _built = _count > 0;
        }

        public int QueryNearestOnRay(float3 origin, float3 direction, float maxDistance, float pickRadius)
        {
            if (!_built || maxDistance <= 0f || pickRadius <= 0f)
                return -1;

            float3 dir = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            float3 bMin = _pile.Center - new float3(_pile.Radius, 0.1f, _pile.Radius);
            float3 bMax = _pile.Center + new float3(_pile.Radius, _pile.Height, _pile.Radius);
            if (!RayAabb(origin, dir, maxDistance, bMin, bMax, out float tEnter))
                return -1;

            int cellCount = _pile.CellCount;
            int angles = math.max(_pile.AngularCells, 1);
            int rings = math.max(_pile.RingCells, 1);
            int cellN = 0;
            float t0 = math.max(tEnter, 0f);
            float step = math.max(pickRadius * 0.75f, _pile.Radius / rings * 0.35f);
            for (float t = t0; t <= maxDistance; t += step)
                AddCellWithNeighbors(_pile.CellAt(origin + dir * t), angles, rings, ref cellN);
            AddCellWithNeighbors(_pile.CellAt(origin + dir * maxDistance), angles, rings, ref cellN);

            int best = -1;
            float bestT = maxDistance + 1f;
            float radiusSq = pickRadius * pickRadius;
            for (int c = 0; c < cellN; c++)
            {
                int cell = _cells[c];
                int cap = _pile.CapacityOf(cell, _count);
                for (int o = 0; o < cap; o++)
                {
                    int id = NeedlePileParams.IdOf(cell, o, cellCount);
                    if ((uint)id >= (uint)_count || _alive[id] == 0)
                        continue;

                    float3 rel = _positions[id] - origin;
                    float tt = math.dot(rel, dir);
                    if (tt < 0f || tt > maxDistance || tt >= bestT)
                        continue;

                    if (math.lengthsq(_positions[id] - (origin + dir * tt)) <= radiusSq)
                    {
                        best = id;
                        bestT = tt;
                    }
                }
            }

            for (int i = 0; i < cellN; i++)
                _marked[_cells[i]] = 0;
            return best;
        }

        void AddCellWithNeighbors(int cell, int angles, int rings, ref int cellN)
        {
            int angular = cell % angles;
            int ring = cell / angles;
            for (int dr = -1; dr <= 1; dr++)
            {
                int rr = ring + dr;
                if ((uint)rr >= (uint)rings)
                    continue;
                for (int da = -1; da <= 1; da++)
                    AddCell(rr * angles + (angular + da + angles) % angles, ref cellN);
            }
        }

        void AddCell(int cell, ref int cellN)
        {
            if ((uint)cell >= (uint)_marked.Length || _marked[cell] != 0 || cellN >= _cells.Length)
                return;
            _marked[cell] = 1;
            _cells[cellN++] = cell;
        }

        static bool RayAabb(float3 origin, float3 dir, float maxDistance, float3 min, float3 max, out float tEnter)
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
            float tExit = math.cmin(tmax);
            return tExit >= 0f && tEnter <= tExit && tEnter <= maxDistance;
        }

        void DisposeArrays()
        {
            if (_marked.IsCreated) _marked.Dispose();
            if (_cells.IsCreated) _cells.Dispose();
            _built = false;
        }

        public void Dispose()
        {
            DisposeArrays();
        }
    }
}
