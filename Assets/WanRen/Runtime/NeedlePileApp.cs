using UnityEngine;
using UnityEngine.InputSystem;

namespace WanRen
{
    public sealed class NeedlePileApp : MonoBehaviour
    {
        public NeedleCountPreset Preset = NeedleCountPreset.TenThousand;
        public uint Seed = 1;
        public float Reach = 2.8f;
        public float PickRadius = 0.055f;
        public float InstanceMaxDistance = 22f;
        public float LodDistance = 10f;
        public int LodStride = 12;
        public int MaxVisible = 1000000;
        public Vector3 NeedleScale = new Vector3(0.007f, 0.13f, 0.007f);
        [Range(0.2f, 1.5f)]
        public float PileScale = 0.45f;
        public Shader NeedleShader;
        public ComputeShader CullShader;
        public FirstPersonController Player;
        public GameObject HeldNeedle;

        NeedleStore _store;
        NeedleSpatialHash _hash;
        NeedleRenderSystem _render;
        Camera _camera;
        float _heldUntil;

        void Start()
        {
            if (NeedleShader == null)
                NeedleShader = Shader.Find("WanRen/NeedleInstanced");
            if (CullShader == null)
                CullShader = Resources.Load<ComputeShader>("NeedleCulling");
            if (NeedleShader == null || Player == null || Player.Look == null || HeldNeedle == null)
            {
                enabled = false;
                return;
            }

            int count = NeedleStore.CountOf(Preset);
            _store = new NeedleStore(count);
            var pile = NeedlePileParams.ForCount(count, Seed, PileScale);
            if (!(NeedlePileCache.ShouldCache(count) && NeedlePileCache.TryLoad(_store, pile)))
            {
                NeedlePileGenerator.Generate(_store, pile);
                if (NeedlePileCache.ShouldCache(count))
                    NeedlePileCache.Save(_store, pile);
            }

            _hash = new NeedleSpatialHash();
            _hash.Build(_store, pile);
            _render = new NeedleRenderSystem(_store, NeedleShader, CullShader, pile, MaxVisible, NeedleScale);
            _camera = Player.Look.GetComponent<Camera>();

            var filter = HeldNeedle.GetComponent<MeshFilter>();
            if (filter != null)
                filter.sharedMesh = NeedleMeshFactory.CreateNeedle();
            HeldNeedle.SetActive(false);
        }

        void Update()
        {
            if (_store == null || _camera == null || _render == null)
                return;

            var cam = _camera.transform;
            int id = _hash.QueryNearestOnRay(cam.position, cam.forward, Reach, PickRadius);
            _store.SetHighlight(id);

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && id >= 0 && _store.Pick(id))
            {
                _render.SetAlive(_store, id);
                HeldNeedle.transform.position = _store.Positions[id];
                HeldNeedle.SetActive(true);
                _heldUntil = Time.time + 0.35f;
            }

            if (HeldNeedle.activeSelf)
            {
                // 插值移动，从视角位置 -> 相机相对位置(模拟在手边了)
                if (Time.time > _heldUntil)
                    HeldNeedle.SetActive(false);
                else
                    HeldNeedle.transform.position = Vector3.Lerp(
                        HeldNeedle.transform.position,
                        cam.TransformPoint(0.22f, -0.12f, 0.38f),
                        Time.deltaTime * 14f);
            }

            _render.Render(_store, _camera, _store.HighlightedId, InstanceMaxDistance, LodDistance, LodStride);
        }

        void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 420, 72), "");
            GUI.Label(new Rect(24, 20, 396, 56),
                $"针：{(_store != null ? _store.AliveCount : 0)} / {(_store != null ? _store.Count : 0)}  已捡 {(_store != null ? _store.PickedCount : 0)}\n" +
                "WASD 移动，点击锁定鼠标，Esc 解锁，左键捡起。");
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.DrawTexture(new Rect(cx - 8, cy - 1, 16, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 8, 2, 16), Texture2D.whiteTexture);
        }

        void OnDestroy()
        {
            _render?.Dispose();
            _hash?.Dispose();
            _store?.Dispose();
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
