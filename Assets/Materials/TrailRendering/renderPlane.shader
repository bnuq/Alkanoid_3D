Shader "myProject/renderPlane"
{
    Properties
    {
        //Plane 의 기본 색깔
        _Color ( "Color", Color) = (1,0,0,1)
    }


    SubShader
    {

        Tags
        {
            //알파값을 가지고 사용하기 때문에
            //Transparent 큐 를 사용 => 가장 마지막에 그린다
            "Queue" = "Transparent"
        }

        
        //알파 블렌딩
        //가장 기본적인 알파 블렌딩을 진행한다
        //생성되는 색깔 * 생성되는 알파 + 기존 뒤에 있던 색깔 * (1-알파)
        Blend SrcAlpha OneMinusSrcAlpha
                

        //Quad 를 언제나 볼 수 있도록 Culling 을 끈다
        //뒤집혀도 렌더링을 진행한다
        Cull Off


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc" //라이팅 계산에 필요


            //Properties
            fixed4 _Color;

            //클립 공간 -> fragment
            struct v2f
            {
                float4 vertex : SV_POSITION;    //클립공간에서 위치
                
                float4 vertexWorld : TEXCOORD1; //월드공간에서 위치
                float3 normalWorld : TEXCOORD2; //월드공간에서 노멀

                float ratio : RATIO;            //ratio 값을 정점마다 할당 -> 보간
            };


            // 버퍼를 통해 받고자 하는 Vertex struct
            struct Vertex
            {
                float3 position;
                float3 normal;
            };
            //compute buffer 를 통해 데이터를 받는 버퍼
            StructuredBuffer<Vertex> vertexBuffer;

            //스크립트 -> 한 plane 을 이루는 quad 개수
            float quadNum;


            //Vertex Shader
            v2f vert (uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                v2f o;

                /*
                먼저 전체 버퍼에서 처리하려는 정점 인덱스를 찾는다

                각 Plane 은 SV_InstanceID 로 구별되고
                하나의 인스턴스는 6 * quadNum 개의 정점으로 이루어져 있다
                그리고 하나의 인스턴스 내에서 정점의 id 는 SV_VertexID 로 나타난다
                */
                int index = instance_id * 6 * quadNum + vertex_id;


                //전체 quad 에서 가장 top 에 있는 정점과 가장 bottom 에 있는 정점을 가져온다
                int topLeftIndex = instance_id * 6 * quadNum;   // most top left
                int topRightIndex = topLeftIndex + 1;           // most top right

                int bottomRightIndex = (instance_id + 1) * 6 * quadNum -1; // most bottom right
                int bottomLeftIndex = bottomRightIndex - 2;                // most bottom left

                                
                // 해당 plane 의 most top 의 중점 위치
                float3 topPos = ( vertexBuffer[topLeftIndex].position 
                                  + vertexBuffer[topRightIndex].position ) / 2;

                // plane 의 most bottom 의 중점 위치
                float3 bottomPos = ( vertexBuffer[bottomLeftIndex].position 
                                     + vertexBuffer[bottomRightIndex].position ) / 2;
                

                // 전체 plane 의 길이
                float planeLength = length(topPos - bottomPos);

                // topPos 에서 현재 정점 까지의 거리
                float topToVert = length(topPos - vertexBuffer[index].position);


                //전체 plane length 에 대해서, 현재 정점의 상대적인 위치를 구하고 싶다
                //smoothstep 을 이용해, 보간을 통해서 현재 정점의 상대적 위치를 구한다
                float ratio = smoothstep(0, planeLength, topToVert);


                //버퍼에서 가져온 현재 정점의 위치 = 월드 공간 기준 좌표계 값
                //바로 클립 공간 위치롤 변환시켜준다
                o.vertex = UnityWorldToClipPos(float4(vertexBuffer[index].position, 1.0f));

                //버퍼에서 값을 바로 가져와 => 월드 공간에서 정점의 값을 채운다
                o.vertexWorld = float4(vertexBuffer[index].position, 1.0f);
                o.normalWorld = vertexBuffer[index].normal; // 이미 월드 공간에서 노멀

                //앞서 구한 상대적인 위치 값을 넘긴다 => 레스터라이저에 의해 보간
                o.ratio = ratio;
                
                return o;
            }


            //Fragment Shader
            fixed4 frag (v2f i) : SV_Target
            {
                // 월드 공간에서 노멀 벡터
                float3 norVec = normalize(i.normalWorld);

                // 월드 공간에서 빛 벡터 => fragment 에서 광원을 바라보는 벡터를 구한다
                float3 lightVec = normalize(UnityWorldSpaceLightDir(i.vertexWorld));

                //해당 fragment 에 들어오는 빛의 양을 구한다, 최소 0.2 값은 들어 오게 한다
                float lightAmount = max(0.2, dot(lightVec, norVec));

                                
                //기존 Plane 의 색깔 + 앞 부분에서 상대적으로 멀어질수록 투명해지게 한다
                float4 modiColor = float4(_Color.x, _Color.y, _Color.z, 1 - i.ratio);

                //최종 색깔
                fixed4 finalColor = modiColor * lightAmount * _LightColor0;

                return finalColor;
            }
            ENDCG
        }
    }
}
