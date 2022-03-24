Shader "Test/renderLine"
{
    Properties
    {
    }


    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        
            #include "UnityCG.cginc"

            struct v2f
            {
                //float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION; //클립 공간에서의 위치
            };
            
            //스크립트를 통해 받은 선을 구성하는 정점 버퍼
            //점 2개가 하나의 선을 구성한다
            StructuredBuffer<float3> lineBuffer;

            //Vertex Shader
            v2f vert (uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                v2f o;

                //하나의 선당 2개의 정점을 차지 + 해당 점의 id
                //전체 버퍼에서 현재 정점의 인덱스를 찾는다
                int index = instance_id * 2 + vertex_id;

                //버퍼에 저장된 점의 위치 = 월드 공간 좌표계 기준 좌표 값
                //바로 클립 공간으로 변환한다
                o.vertex = UnityWorldToClipPos(float4(lineBuffer[index], 1));

                return o;
            }

            //Fragment Shader => 선 색깔은 그냥 빨간색으로 고정시킨다
            fixed4 frag (v2f i) : SV_Target
            {
                return float4(1,0,0,1);
        
            }
            ENDCG
        }
    }
}
