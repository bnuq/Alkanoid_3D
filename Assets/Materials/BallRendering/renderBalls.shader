Shader "myProject/renderBalls" {

    Properties
    {
        //ball 자체적으로 가지는 색깔
        _Color ( "Color", Color) = (1,0,0,1)

        //간접광, ambient 지수
        _Ambient ("Ambient", Range(0, 1)) = 0.25

        //반사광 계산에 필요
        _SpecColor ( "Specular Material Color", Color) = (1, 1, 1, 1) // 반사 광 색깔만 나타낼 것이기에, 물체는 무채색만 가진다
        _Shininess ( "Shininess", Float) = 10 // 스펙큘러 강도

        //가장자리를 판단하는 기준
        _Rim ( "Rim amount", Float) = 5
    }


    SubShader
    {
        Pass {
            //CG 작성
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"
                #include "UnityLightingCommon.cginc" // 라이팅 계산에 필요

                //Properties 에서 넘어오는 값 정의
                fixed4 _Color;
                float _Ambient;
                //fixed4 _SpecColor; => 셰이더에서 자동으로 정의 된다고 그랬든가..?
                float _Shininess;
                float _Rim;

                                
                //공을 그릴 때는 ball buffer 만 필요하다
                //Compute Buffer 를 통해서 데이터를 받기 위해, Ball 구조체를 정의한다.
                struct Ball
                {
                    float3 position;
                    float3 velocity;
                    float speed;

                    float3 xBasis;
                    float3 yBasis;
                    float3 zBasis;
                };
                //Compute Buffer 를 통해 받는 버퍼 정의
                //버퍼의 데이터를 읽기만 한다
                StructuredBuffer<Ball> ballBuffer;
                

                //그외 스크립트를 통해 넘겨받는 변수 값
                float3 limitSize;
                float radius;


                //모델 데이터
                struct appdata
                {
                    float4 vertex : POSITION; //정점의 위치
                    //float2 uv : TEXCOORD0;
                    float3 normal : NORMAL;   //정점의 노멀
                };

                //클립공간 -> Fragment 로 넘어가는 구조체
                struct v2f
                {
                    //float2 uv : TEXCOORD0;

                    //프레그먼트의 클립공간에서 좌표
                    float4 vertexClip : SV_POSITION;

                    //프레그먼트의 월드공간에서 좌표
                    float4 vertexWorld : TEXCOORD1;

                    //프레그먼트의 월드공간에서 노멀벡터
                    float3 normalWorld : TEXCOORD2;
                };

                
                //Vertex Shader
                                    //모델 내에서 Vertex 의 id         그려지는 여러 모델 중, 현재 모델의 id
                v2f vert (appdata v, uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
                {
                    v2f o;

                    /*
                    주어진 Ball 의 크기를 바꿀 수 있다
                    입력받은 radius 값을 이용
                    정점을 normal vector 방향으로 이동시키면
                    중심으로 부터 멀어져 => 공이 커지는 효과를 낼 수 있다

                    모델 공간 내에서, radius 만큼 늘어난 정점의 위치를 구함
                    */
                    //Vertex Extrude
                    v.vertex = float4(v.vertex.xyz + v.normal * radius, 1);
                    

                    /*
                    SV_InstanceID 값을 이용
                    현재 모델에 해당하는 Ball 객체를 찾을 수 있다

                    해당 Ball 의 모델 좌표계를 가져온다
                    모델 좌표계 => 공이 움직이는 방향에 맞춰져서 설정
                    월드 공간 좌표계를 기준으로 값을 가지고 있다
                    */
                    float3 xaxis = ballBuffer[instanceID].xBasis;
                    float3 yaxis = ballBuffer[instanceID].yBasis;
                    float3 zaxis = ballBuffer[instanceID].zBasis;
                    float3 pos = ballBuffer[instanceID].position; //현재 공의 위치
                    
                    
                    /*
                    변환 후 좌표계의 벡터를 알고 있으니, 변환행렬을 구할 수 있다
                    이 행렬 자체가 월드 변환 행렬
                    현재 모델의 기본 좌표계 => 받은 좌표계 값이 됐다고 생각할 수 있다
                    회전 + 이동 변환
                    기본 모델 공간 좌표계와 월드 공간 좌표계가 일치하기 때문에, 바로 적용할 수 있었다고 생각한다
                    */
                    float4x4 motionMat = float4x4 (
                                                    xaxis.x, yaxis.x, zaxis.x, pos.x,
                                                    xaxis.y, yaxis.y, zaxis.y, pos.y,
                                                    xaxis.z, yaxis.z, zaxis.z, pos.z,
                                                    0, 0, 0, 1
                                                  );
                    //회전 변환 부분만 따로 구함
                    float3x3 L = float3x3 (
                                            xaxis.x, yaxis.x, zaxis.x,
                                            xaxis.y, yaxis.y, zaxis.y,
                                            xaxis.z, yaxis.z, zaxis.z
                                          );

                    //정점의 위치를, 모델 공간에서 월드 공간으로 변환
                    v.vertex = mul(motionMat, v.vertex);


                    /*
                    정점의 위치가 변하면, normal vector 도 변환해주어야 한다

                    노멀벡터는 월드 변환의 [L | t] 에서 이동변환을 제외한 L 에 대해서만 생각
                    
                    => normal 의 경우엔 inverse trasnpose 를 이용하여 변환한다. 즉 (L^-1)^T
                    그런데, 이 변환의 경우 비균등 확대축소 없이, 그냥 이동,회전 만 있는 강체변환 이니까, 변환을 노멀에 그대로 적용해도 된다
                    그럼 노멀 벡터도 월드 공간으로 변환이 이루어 졌다
                    */
                    v.normal = mul(L, v.normal);

               
                    // 월드 공간에서 좌표를 구한다
                    //o.vertexWorld = mul(unity_ObjectToWorld, v.vertex);
                    o.vertexWorld = v.vertex;

                    // 클립 공간에서 좌표를 구한다
                    //o.vertexClip = UnityObjectToClipPos(v.vertex);
                    o.vertexClip = UnityWorldToClipPos(v.vertex);

                    // 월드 공간에서 노멀벡터를 구한다
                    //o.normalWorld = v.normal;
                    o.normalWorld = v.normal;

                    return o;
                }




                /* 
                    Fragment Shader

                    앞서 클립 공간에서 정의한 v2f => 레스터라이저를 통과
                    => 보간을 통해서 fragment 가 됐다

                    Texturing => 일단 하지 않는다
                    + 
                    Lighting ==> 보간된 월드 공간에서의 좌표 값을 이용해서 계산한다
                */
                fixed4 frag (v2f i) : SV_Target
                {
                    //해당 fragment 의 월드공간에서 노멀 벡터를 구한다
                    float3 norVec = normalize(i.normalWorld); //normalize 필수, 보간 과정에서 정규화를 기대할 수 없다

                    //뷰 벡터 = 해당 프레그먼트에서 카메라를 바라보는 벡터
                    //뷰 벡터를 월드 공간 좌표계 기준 값으로 구한다 (프레그먼트의 월드 공간 좌표를 넣어서 구할 수 있다)
                    float3 veiwVec = normalize(UnityWorldSpaceViewDir(i.vertexWorld));

                    //빛 벡터 = 해당 프레그먼트에서 광원을 바라보는 벡터
                    //월드 공간에서 빛 벡터를 구한다
                    float3 lightVec = normalize(UnityWorldSpaceLightDir(i.vertexWorld));


                    //해당 Fragment 의 월드 공간에서의 벡터 값을 이용해서 Lighting 을 계산한다
                    // 1. 난반사, 디퓨즈
                    // 노멀 벡터와 빛 벡터를 이용하면, 프레그먼트에 들어오는 빛의 양을 구할 수 있다
                    float lightAmount = max(_Ambient, dot(norVec, lightVec));
                    float4 diffuseTerm = lightAmount * _Color * _LightColor0; //물체의 색과 빛의 색 고려


                    // 2. 스펙큘러, 정반사 + 간접광
                    //빛 벡터를 반사시켜서, 프레그먼트에서 반사되어져서 나오는 빛 반사 벡터를 구한다
                    float3 reflectVec = reflect(lightVec, norVec);

                    //빛 반사 벡터와, 뷰 벡터 사이 내적을 구해 눈으로 들어오는 빛의 양을 구한다
                    //최소 일정 간접광이 들어온다
                    float3 lightAmountOnEye = max(_Ambient, dot(veiwVec, reflectVec));

                    //눈으로 들어오는 빛의 양이 줄어들면, 반사광에 의한 색은 기하급수적으로 줄어든다
                    //매끄러움 을 통해 이를 조절할 수 있다
                    float3 specular = pow(lightAmountOnEye, _Shininess);
                    float4 specularTerm = float4(specular, 1) * _SpecColor * _LightColor0;


                    // 3. 최종 색깔
                    float4 finalColor = diffuseTerm + specularTerm;



                    /*
                    뒤로 갈수록 어둡게 만들고 싶다

                    프레그먼트의 z 위치에 따라서 0~1 값을 구한다
                    그 값을 이용해서, z 위치가 앞에 올수록 더 밝게 보이게 한다
                    */
                    //smoothsteop => 부드러운 보간
                    float fragZ = smoothstep(-limitSize.z, limitSize.z, i.vertexWorld.z) * 0.5;
                    // 가까울 수록 밝게 보이고 싶다
                    finalColor *= (1 - fragZ);



                    /*
                    Rim Lighting

                    프레그먼트의 월드 공간에서 노멀벡터와 뷰 벡터 내적을 구한다
                    가장자리, Rim 에 해당할수록 그 값이 낮게 나올 것이다
                        잘 안보이는 fragment 이니까

                    그걸 역으로 이용하면, 가장자리만 다른 색을 칠할 수 있다
                    */                    
                    float rimAmount = max(0.0, dot(norVec, veiwVec));
                    //가장자리로 판단되면, 그냥 검정색만 리턴시킨다                
                    if(rimAmount < 0.4f) finalColor = float4(0,0,0,1);



                    return finalColor;
                }
            ENDCG
        }
    }
}
