Shader "WanRen/NeedleInstanced"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.62, 0.64, 0.68, 1)
        _HighlightColor ("Highlight", Color) = (1, 0.92, 0.25, 1)
        _NeedleScale ("Scale", Vector) = (0.007, 0.13, 0.007, 0)
        _HighlightId ("Highlight", Int) = -1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct Needle
            {
                float3 position;
                float pad;
                float4 rotation;
            };

            StructuredBuffer<Needle> _Needles;
            StructuredBuffer<uint> _VisibleIds;
            int _HighlightId;
            float4 _BaseColor;
            float4 _HighlightColor;
            float4 _NeedleScale;

            struct appdata
            {
                float3 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float3 QuatRotate(float4 q, float3 v)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                uint nid = _VisibleIds[instanceID];
                Needle n = _Needles[nid];
                float3 scaled = v.vertex * _NeedleScale.xyz;
                float3 world = QuatRotate(n.rotation, scaled) + n.position;
                float3 worldN = QuatRotate(n.rotation, v.normal);

                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.normal = worldN;
                o.color = (_HighlightId >= 0 && nid == (uint)_HighlightId) ? _HighlightColor : _BaseColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float wrap = saturate(dot(n, normalize(float3(0.28, 0.86, 0.42))));
                return i.color * (0.32 + 0.68 * wrap);
            }
            ENDCG
        }
    }
    FallBack Off
}
