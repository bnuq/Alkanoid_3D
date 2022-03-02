Shader "Test/renderLine"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                //float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                //float2 uv : TEXCOORD0;
        
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;



            StructuredBuffer<float3> lineBuffer;

            v2f vert (uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                v2f o;

                int index = instance_id * 2 + vertex_id;


                o.vertex = UnityWorldToClipPos(float4(lineBuffer[index], 1));

                //o.vertex = UnityObjectToClipPos(v.vertex);
//                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(1,0,0,1);
        
            }
            ENDCG
        }
    }
}
