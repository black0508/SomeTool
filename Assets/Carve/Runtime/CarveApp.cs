using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Carve
{
    public sealed class CarveApp : MonoBehaviour
    {
        public CarvePlayer Player;
        public float Reach = 5f;
        public float CarveRadius = 0.55f;

        VoxelStore _store;
        Mesh _mesh;
        MeshCollider _collider;
        Material _material;
        Camera _camera;
        float _nextCarve;

        void Start()
        {
            if (Player == null || Player.Look == null)
            {
                enabled = false;
                return;
            }

            _camera = Player.Look.GetComponent<Camera>();
            if (_camera == null)
            {
                enabled = false;
                return;
            }

            _store = new VoxelStore(float3.zero);
            _store.FillSolid();

            _mesh = new Mesh { name = "VoxelMesh" };
            _mesh.MarkDynamic();

            var shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Diffuse");
            if (shader == null)
            {
                _store.Dispose();
                _store = null;
                enabled = false;
                return;
            }

            _material = new Material(shader) { color = new Color(0.55f, 0.38f, 0.22f) };

            var terrain = new GameObject("VoxelTerrain");
            var filter = terrain.AddComponent<MeshFilter>();
            var meshRenderer = terrain.AddComponent<MeshRenderer>();
            _collider = terrain.AddComponent<MeshCollider>();
            filter.sharedMesh = _mesh;
            meshRenderer.sharedMaterial = _material;

            Rebuild();
        }

        void Update()
        {
            if (_store == null || _camera == null)
                return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed || Time.time < _nextCarve)
                return;

            Transform cam = _camera.transform;
            if (!_store.TryHit(cam.position, cam.forward, Reach, out float3 hit))
                return;
            if (!_store.CarveSphere(hit, CarveRadius))
                return;

            Rebuild();
            _nextCarve = Time.time + 0.12f;
        }

        void Rebuild()
        {
            VoxelMesher.Build(_store, _mesh);
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;
        }

        void OnDestroy()
        {
            _store?.Dispose();
            if (_mesh != null)
                Destroy(_mesh);
            if (_material != null)
                Destroy(_material);
        }
    }
}
