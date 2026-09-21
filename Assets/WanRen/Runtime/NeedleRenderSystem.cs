using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace WanRen
{
    public sealed class NeedleRenderSystem : IDisposable
    {
        struct GpuNeedle
        {
            public float3 Position;
            public float Padding;
            public float4 Rotation;
        }

        readonly Mesh _needleMesh;
        readonly Material _needleMaterial;
        readonly ComputeShader _cull;
        readonly int _resetKernel;
        readonly int _cullKernel;
        readonly int _finalizeKernel;
        readonly bool _useCompute;
        readonly int _count;
        readonly int _maxVisible;
        readonly Bounds _bounds;
        readonly Vector3 _needleScale;
        readonly float _cullRadius;
        readonly uint[] _drawArgs = { 0, 0, 0, 0, 0 };
        readonly Vector4[] _planeScratch = new Vector4[6];

        ComputeBuffer _needles;
        ComputeBuffer _alive;
        ComputeBuffer _visible;
        ComputeBuffer _args;
        ComputeBuffer _counter;
        ComputeBuffer _planes;

        public NeedleRenderSystem(
            NeedleStore store,
            Shader needleShader,
            ComputeShader cullShader,
            NeedlePileParams pile,
            int maxVisible,
            Vector3 needleScale)
        {
            _count = store.Count;
            // 可见列表由 compute 组内压缩后再写；MaxVisible 至少等于总数，避免截断闪烁。
            _maxVisible = math.max(1024, math.max(maxVisible, _count));
            _needleScale = needleScale;
            _cullRadius = 0.5f * math.length((float3)needleScale);
            _needleMesh = NeedleMeshFactory.CreateNeedle();
            float pad = _cullRadius * 2f;
            _bounds = new Bounds(
                pile.Center + new float3(0f, pile.Height * 0.5f, 0f),
                new Vector3(pile.Radius * 2.4f + pad, pile.Height + 2f + pad, pile.Radius * 2.4f + pad));
            _needleMaterial = new Material(needleShader) { enableInstancing = true };
            _needleMaterial.SetColor("_BaseColor", new Color(0.62f, 0.64f, 0.68f, 1f));
            _needleMaterial.SetColor("_HighlightColor", new Color(1f, 0.92f, 0.25f, 1f));
            _needleMaterial.SetVector("_NeedleScale", needleScale);

            _drawArgs[0] = _needleMesh.GetIndexCount(0);
            // 位置、旋转信息
            _needles = new ComputeBuffer(_count, 32, ComputeBufferType.Structured);
            // 是否存活
            _alive = new ComputeBuffer(_count, 4, ComputeBufferType.Structured);
            // 可见的针（视野中的针，用于裁切的）
            _visible = new ComputeBuffer(_maxVisible, 4, ComputeBufferType.Structured);
            _args = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            _counter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
            _planes = new ComputeBuffer(6, 16, ComputeBufferType.Structured);
            _args.SetData(_drawArgs);
            UploadNeedles(store);
            _alive.SetData(store.Alive);

            _cull = cullShader;
            _useCompute = _cull != null && SystemInfo.supportsComputeShaders;
            if (_useCompute)
            {
                _resetKernel = _cull.FindKernel("CSReset");
                _cullKernel = _cull.FindKernel("CSCull");
                _finalizeKernel = _cull.FindKernel("CSFinalize");
                _cull.SetBuffer(_resetKernel, "_Args", _args);
                _cull.SetBuffer(_resetKernel, "_Counter", _counter);
                _cull.SetBuffer(_cullKernel, "_Needles", _needles);
                _cull.SetBuffer(_cullKernel, "_Alive", _alive);
                _cull.SetBuffer(_cullKernel, "_VisibleIds", _visible);
                _cull.SetBuffer(_cullKernel, "_Args", _args);
                _cull.SetBuffer(_cullKernel, "_Counter", _counter);
                _cull.SetBuffer(_cullKernel, "_Planes", _planes);
                _cull.SetBuffer(_finalizeKernel, "_Args", _args);
                _cull.SetBuffer(_finalizeKernel, "_Counter", _counter);
            }

            _needleMaterial.SetBuffer("_Needles", _needles);
            _needleMaterial.SetBuffer("_VisibleIds", _visible);
        }

        public void SetAlive(NeedleStore store, int id)
        {
            _alive.SetData(store.Alive, id, id, 1);
        }

        public void Render(
            NeedleStore store,
            Camera camera,
            int highlightId,
            float maxDistance,
            float lodDistance,
            int lodStride)
        {
            if (camera == null)
                return;

            _needleMaterial.SetInt("_HighlightId", highlightId);
            _needleMaterial.SetVector("_NeedleScale", _needleScale);

            if (_useCompute)
                DispatchCull(camera, maxDistance, lodDistance, lodStride);
            else
                CullOnCpu(store, camera, maxDistance, lodDistance, lodStride);

            Graphics.DrawMeshInstancedIndirect(
                _needleMesh, 0, _needleMaterial, _bounds, _args, 0, null, ShadowCastingMode.Off, false, 0, camera);
        }

        void UploadNeedles(NeedleStore store)
        {
            var packed = new NativeArray<GpuNeedle>(store.Count, Allocator.TempJob);
            new PackJob
            {
                Positions = store.Positions,
                Rotations = store.Rotations,
                Dst = packed
            }.Schedule(store.Count, 2048).Complete();
            _needles.SetData(packed);
            packed.Dispose();
        }

        void DispatchCull(Camera camera, float maxDistance, float lodDistance, int lodStride)
        {
            FillPlanes(camera);
            _cull.SetInt("_NeedleCount", _count);
            _cull.SetInt("_MaxVisible", _maxVisible);
            _cull.SetInt("_LodStride", math.max(lodStride, 1));
            _cull.SetFloat("_MaxDistance", maxDistance);
            _cull.SetFloat("_LodDistance", lodDistance);
            _cull.SetFloat("_CullRadius", _cullRadius);
            _cull.SetVector("_CameraPos", camera.transform.position);
            _cull.Dispatch(_resetKernel, 1, 1, 1);
            _cull.Dispatch(_cullKernel, (_count + 255) / 256, 1, 1);
            _cull.Dispatch(_finalizeKernel, 1, 1, 1);
        }

        void CullOnCpu(NeedleStore store, Camera camera, float maxDistance, float lodDistance, int lodStride)
        {
            var planes = new NativeArray<float4>(6, Allocator.Temp);
            var visible = new NativeArray<int>(_maxVisible, Allocator.Temp);
            ExtractPlanes(math.mul(
                (float4x4)GL.GetGPUProjectionMatrix(camera.projectionMatrix, false),
                (float4x4)camera.worldToCameraMatrix), planes);

            float3 cameraPos = camera.transform.position;
            float maxSq = maxDistance * maxDistance;
            float lodSq = lodDistance * lodDistance;
            int stride = math.max(lodStride, 1);
            int written = 0;
            for (int i = 0; i < store.Count && written < _maxVisible; i++)
            {
                if (store.Alive[i] == 0)
                    continue;
                float3 pos = store.Positions[i];
                float distSq = math.lengthsq(pos - cameraPos);
                if (distSq > maxSq)
                    continue;
                if (distSq > lodSq && (i % stride) != 0)
                    continue;
                if (!InsideFrustum(pos, _cullRadius, planes))
                    continue;
                visible[written++] = i;
            }

            _visible.SetData(visible, 0, 0, written);
            _drawArgs[1] = (uint)written;
            _args.SetData(_drawArgs);
            _drawArgs[1] = 0;
            planes.Dispose();
            visible.Dispose();
        }

        void FillPlanes(Camera camera)
        {
            var planes = new NativeArray<float4>(6, Allocator.Temp);
            float4x4 proj = (float4x4)GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            ExtractPlanes(math.mul(proj, (float4x4)camera.worldToCameraMatrix), planes);
            for (int i = 0; i < 6; i++)
                _planeScratch[i] = new Vector4(planes[i].x, planes[i].y, planes[i].z, planes[i].w);
            _planes.SetData(_planeScratch);
            planes.Dispose();
        }

        static void ExtractPlanes(float4x4 viewProj, NativeArray<float4> planes)
        {
            float4 r0 = new float4(viewProj.c0.x, viewProj.c1.x, viewProj.c2.x, viewProj.c3.x);
            float4 r1 = new float4(viewProj.c0.y, viewProj.c1.y, viewProj.c2.y, viewProj.c3.y);
            float4 r2 = new float4(viewProj.c0.z, viewProj.c1.z, viewProj.c2.z, viewProj.c3.z);
            float4 r3 = new float4(viewProj.c0.w, viewProj.c1.w, viewProj.c2.w, viewProj.c3.w);
            planes[0] = NormalizePlane(r3 + r0);
            planes[1] = NormalizePlane(r3 - r0);
            planes[2] = NormalizePlane(r3 + r1);
            planes[3] = NormalizePlane(r3 - r1);
            planes[4] = NormalizePlane(r3 + r2);
            planes[5] = NormalizePlane(r3 - r2);
        }

        static bool InsideFrustum(float3 point, float radius, NativeArray<float4> planes)
        {
            for (int i = 0; i < 6; i++)
            {
                if (math.dot(planes[i].xyz, point) + planes[i].w < -radius)
                    return false;
            }

            return true;
        }

        static float4 NormalizePlane(float4 plane)
        {
            return plane / math.max(math.length(plane.xyz), 1e-8f);
        }

        public void Dispose()
        {
            _needles?.Release();
            _alive?.Release();
            _visible?.Release();
            _args?.Release();
            _counter?.Release();
            _planes?.Release();
            if (_needleMaterial != null) UnityEngine.Object.Destroy(_needleMaterial);
            if (_needleMesh != null) UnityEngine.Object.Destroy(_needleMesh);
        }

        [BurstCompile]
        struct PackJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float4> Rotations;
            public NativeArray<GpuNeedle> Dst;

            public void Execute(int index)
            {
                Dst[index] = new GpuNeedle
                {
                    Position = Positions[index],
                    Rotation = Rotations[index]
                };
            }
        }
    }
}
