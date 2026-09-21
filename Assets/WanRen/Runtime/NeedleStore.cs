using System;
using Unity.Collections;
using Unity.Mathematics;

namespace WanRen
{
    public enum NeedleCountPreset
    {
        TenThousand,
        HundredThousand,
        OneMillion,
        TenMillion
    }

    public sealed class NeedleStore : IDisposable
    {
        public NativeArray<float3> Positions;
        public NativeArray<float4> Rotations;
        public NativeArray<uint> Alive;

        public int Count { get; }
        public int AliveCount { get; private set; }
        public int PickedCount => Count - AliveCount;
        public int HighlightedId { get; private set; } = -1;

        public NeedleStore(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Count = count;
            AliveCount = count;
            Positions = new NativeArray<float3>(count, Allocator.Persistent);
            Rotations = new NativeArray<float4>(count, Allocator.Persistent);
            Alive = new NativeArray<uint>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++)
                Alive[i] = 1;
        }

        public static int CountOf(NeedleCountPreset preset)
        {
            switch (preset)
            {
                case NeedleCountPreset.HundredThousand: return 100000;
                case NeedleCountPreset.OneMillion: return 1000000;
                case NeedleCountPreset.TenMillion: return 10000000;
                default: return 10000;
            }
        }

        public bool Pick(int id)
        {
            if ((uint)id >= (uint)Count || Alive[id] == 0)
                return false;

            Alive[id] = 0;
            AliveCount--;
            if (HighlightedId == id)
                HighlightedId = -1;
            return true;
        }

        public void SetHighlight(int id)
        {
            if (id < 0 || (uint)id >= (uint)Count || Alive[id] == 0)
            {
                HighlightedId = -1;
                return;
            }

            HighlightedId = id;
        }

        public void Dispose()
        {
            if (Positions.IsCreated) Positions.Dispose();
            if (Rotations.IsCreated) Rotations.Dispose();
            if (Alive.IsCreated) Alive.Dispose();
        }
    }
}
