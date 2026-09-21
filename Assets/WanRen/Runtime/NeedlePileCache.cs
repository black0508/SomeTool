using System.IO;
using Unity.Mathematics;
using UnityEngine;

namespace WanRen
{
    // 缓存到本地，根据名字配置缓存C:\Users\CAT\AppData\LocalLow\DefaultCompany\SomeTool
    public static class NeedlePileCache
    {
        public static bool ShouldCache(int count) => count >= 1000000;

        public static bool TryLoad(NeedleStore store, NeedlePileParams pile)
        {
            string path = PathFor(pile, store.Count);
            if (!File.Exists(path))
                return false;

            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.ReadInt32() != store.Count)
                    return false;

                for (int i = 0; i < store.Count; i++)
                {
                    store.Positions[i] = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    store.Rotations[i] = new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
            }

            return true;
        }

        public static void Save(NeedleStore store, NeedlePileParams pile)
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            using (var writer = new BinaryWriter(File.Create(PathFor(pile, store.Count))))
            {
                writer.Write(store.Count);
                for (int i = 0; i < store.Count; i++)
                {
                    float3 p = store.Positions[i];
                    float4 r = store.Rotations[i];
                    writer.Write(p.x);
                    writer.Write(p.y);
                    writer.Write(p.z);
                    writer.Write(r.x);
                    writer.Write(r.y);
                    writer.Write(r.z);
                    writer.Write(r.w);
                }
            }
        }

        static string PathFor(NeedlePileParams pile, int count)
        {
            return Path.Combine(
                Application.persistentDataPath,
                $"needle_pile_{pile.Seed}_{count}_{pile.AngularCells}x{pile.RingCells}_{pile.Radius:0.##}_{pile.Height:0.##}.bin");
        }
    }
}
