using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WanRen
{
    public struct NeedlePileParams
    {
        public float3 Center;
        public float Radius;
        public float Height;
        public uint Seed;
        public int AngularCells;
        public int RingCells;

        public int CellCount => math.max(1, AngularCells * RingCells);

        public static NeedlePileParams ForCount(int count, uint seed, float pileScale)
        {
            float t = math.pow(count / 10000f, 1f / 3f);
            t = math.max(t, 0.55f) * math.max(pileScale, 0.15f);
            int cellsTarget = math.max(64, count / 96);
            int rings = math.clamp((int)math.round(math.sqrt(cellsTarget / 4f)), 8, 48);
            int angles = math.clamp(rings * 4, 32, 192);
            return new NeedlePileParams
            {
                Center = float3.zero,
                Radius = 3.1f * t,
                Height = 2.5f * t,
                Seed = seed == 0 ? 1u : seed,
                AngularCells = angles,
                RingCells = rings
            };
        }

        public int CellOf(int id) => (int)((uint)id % (uint)CellCount);

        public int CapacityOf(int cell, int total)
        {
            int cells = CellCount;
            if (cells <= 0 || total <= 0 || (uint)cell >= (uint)cells)
                return 0;
            return total / cells + (cell < total % cells ? 1 : 0);
        }

        public static int IdOf(int cell, int ordinal, int cellCount) => ordinal * cellCount + cell;

        public int CellAt(float3 world)
        {
            int angles = math.max(AngularCells, 1);
            int rings = math.max(RingCells, 1);
            float3 d = world - Center;
            float ang01 = math.frac(math.atan2(d.z, d.x) / (2f * math.PI) + 1f);
            float r01 = math.saturate(math.length(d.xz) / math.max(Radius, 1e-4f));
            int angular = math.min(angles - 1, (int)math.floor(ang01 * angles));
            int ring = math.min(rings - 1, (int)math.floor((1f - r01) * rings));
            return ring * angles + angular;
        }
    }

    public static class NeedlePileGenerator
    {
        public static void Generate(NeedleStore store, NeedlePileParams pile)
        {
            new GenerateJob
            {
                Center = pile.Center,
                Radius = pile.Radius,
                Height = pile.Height,
                Seed = pile.Seed,
                AngularCells = math.max(pile.AngularCells, 1),
                RingCells = math.max(pile.RingCells, 1),
                Count = store.Count,
                Positions = store.Positions,
                Rotations = store.Rotations
            }.Schedule(store.Count, 2048).Complete();
        }

        [BurstCompile]
        struct GenerateJob : IJobParallelFor
        {
            public float3 Center;
            public float Radius;
            public float Height;
            public uint Seed;
            public int AngularCells;
            public int RingCells;
            public int Count;
            public NativeArray<float3> Positions;
            public NativeArray<float4> Rotations;

            public void Execute(int index)
            {
                int cellCount = AngularCells * RingCells;
                int cell = index % cellCount;
                int ordinal = index / cellCount;
                int capacity = Count / cellCount + (cell < Count % cellCount ? 1 : 0);
                int angular = cell % AngularCells;
                int ring = cell / AngularCells;

                uint h = Hash((uint)index ^ Seed ^ 0x9E3779B9u);
                float u0 = Hash01(h);
                float u1 = Hash01(h + 1);
                float u2 = Hash01(h + 3);
                float u3 = Hash01(h + 4);
                float u4 = Hash01(h + 5);

                float ring0 = (float)ring / RingCells;
                float ring1 = (float)(ring + 1) / RingCells;
                float outer = 1f - ring0;
                float inner = 1f - ring1;
                float r = math.sqrt(math.lerp(inner * inner, outer * outer, u0));
                float ang = (angular + u1) / AngularCells * (2f * math.PI);
                float3 xz = new float3(math.cos(ang), 0f, math.sin(ang)) * (r * Radius);
                float surface = Height * math.sqrt(math.saturate(1f - r * r));
                float depth = math.saturate(((float)ordinal + u2) / math.max(capacity, 1));
                float3 pos = Center + xz;
                pos.y = Center.y + surface * depth;

                float3 dir = math.normalize(new float3(
                    math.cos(u3 * 2f * math.PI),
                    math.lerp(-0.42f, 0.76f, u4),
                    math.sin(u3 * 2f * math.PI)));
                float3 tangent = math.abs(dir.y) < 0.92f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                float3 z = math.normalizesafe(math.cross(dir, tangent), new float3(0f, 0f, 1f));

                Positions[index] = pos;
                Rotations[index] = quaternion.LookRotationSafe(z, dir).value;
            }

            static uint Hash(uint value)
            {
                value ^= value >> 16;
                value *= 2146121005u;
                value ^= value >> 15;
                value *= 2221713035u;
                value ^= value >> 16;
                return value;
            }

            static float Hash01(uint value)
            {
                return (Hash(value) & 0xFFFFFFu) * (1f / 16777216f);
            }
        }
    }
}
