Shader "myProject/renderPlane"
{
    Properties
    {
        _Color ( "Color", Color) = (1,0,0,1)
    }
    SubShader
    {

        Tags
        {
            // Transparent 큐 를 사용 => 가장 마지막에 그린다
            "Queue" = "Transparent"
        }

        

        // 알파 블렌딩
        // 가장 기본적인 알파 블렌딩을 진행한다
        Blend SrcAlpha OneMinusSrcAlpha

        

        // Quad 를 언제나 볼 수 있도록 Culling 을 끈다
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc" // 라이팅 계산에 필요


            fixed4 _Color;


            struct v2f
            {
                float4 vertex : SV_POSITION;
                
                float4 vertexWorld : TEXCOORD1;
                float3 normalWorld : TEXCOORD2;

                float ratio : RATIO;
            };




            // 버퍼를 통해 받고자 하는 Vertex struct
            struct Vertex
            {
                float3 position;
                float3 normal;
            };
            // compute buffer 를 통해 데이터를 받는 버퍼
            StructuredBuffer<Vertex> vertexBuffer;





            // 한 plane 을 이루는 quad 개수
            float quadNum;




            v2f vert (uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                v2f o;

                // 하나의 인스턴스는 6 * quadNum 개의 정점으로 이루어진다
                // vertex_id = [0, 11] 범위의 값을 가지겠지?
                // index = plane 을 이루는 하나의 정점 -> 을 가리킨다
                int index = instance_id * 6 * quadNum + vertex_id;




                // 전체 quad 에서 가장 top 에 있는 정점과 가장 bottom 에 있는 정점을 가져온다
                int topLeftIndex = instance_id * 6 * quadNum; // most top left
                int topRightIndex = topLeftIndex + 1; // most top right

                int bottomRightIndex = (instance_id + 1) * 6 * quadNum -1; // most bottom right
                int bottomLeftIndex = bottomRightIndex - 2; // most bottom left



                
                // 해당 plane 의 most top 의 중점 위치
                float3 topPos = ( vertexBuffer[topLeftIndex].position + vertexBuffer[topRightIndex].position ) / 2;

                // plane 의 most bottom 의 중점 위치
                float3 bottomPos = ( vertexBuffer[bottomLeftIndex].position + vertexBuffer[bottomRightIndex].position ) / 2;

                


                // 전체 plane 의 길이
                float planeLength = length(topPos - bottomPos);

                // topPos 에서 현재 정점 까지의 거리
                float topToVert = length(topPos - vertexBuffer[index].position);



                // 전체 plane length 에 대해서, 현재 정점의 길이의 상대적 값을 구한다
                float ratio = smoothstep(0, planeLength, topToVert);


/* 색깔처리 */
                //o.color = float4(0, 1 - ratio, 0, 1 - ratio);

                
/* 정점처리 */
            /* 
                // 현재 위치 값에 normal 방향으로 정점을 이동 시킨다 ~ 그러면 평평한 표면을 벗어나겠지
                // 그런데 normal 방향으로 이동하는 값을 sin 삼각함수를 이용해서 준다
                // 그러면 공이 움직이는 것과는 별개로 plane 이 움직이게 된다
                
                // t*3 값을 이용하자
                float time = _Time.w * _speed;
                

                // 기존 정점의 위치에, normal 방향으로 sin(x) / x 그래프 값만큼 이동시킨다
                float3 objPos = vertexBuffer[index].position + vertexBuffer[index].normal * sin(time + vertPos * _freq * 2) * _amp * vertPos;

 */
                // plane 의 좌표를 월드공간에서 정의했으니까,
                // 월드 공간 -> 클립 공간 변환을 해준다
                //o.vertex = UnityWorldToClipPos(float4(objPos, 1.0f));vertexBuffer[index].position

/* 
                float time = _Time.w * _speed;
                float3 objPos = vertexBuffer[index].position + vertexBuffer[index].normal * sin(time + ratio * _freq ) * _amp * ratio;
                o.vertex = UnityWorldToClipPos(float4(objPos, 1.0f));vertexBuffer[index].position;

 */
                
                o.vertex = UnityWorldToClipPos(float4(vertexBuffer[index].position, 1.0f));

                o.vertexWorld = float4(vertexBuffer[index].position, 1.0f);
                o.normalWorld = vertexBuffer[index].normal; // 이미 월드 공간에서 노멀

                o.ratio = ratio;
                
                return o;
            }






            fixed4 frag (v2f i) : SV_Target
            {
                
                // 월드 공간에서 노멀 벡터
                float3 norVec = normalize(i.normalWorld);

                // 월드 공간에서 빛 벡터 => 광원을 바라보는 벡터를 구한다
                float3 lightVec = normalize(UnityWorldSpaceLightDir(i.vertexWorld));

                float lightAmount = normalize(max(0.2, dot(lightVec, norVec)));



                
                
                float4 modiColor = float4(_Color.x, _Color.y, _Color.z, 1 - i.ratio);

                fixed4 finalColor = modiColor * lightAmount * _LightColor0;

                return finalColor;
            }
            ENDCG
        }
    }
}
