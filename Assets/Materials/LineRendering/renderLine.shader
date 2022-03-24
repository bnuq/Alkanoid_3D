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
                float4 vertex : SV_POSITION; //Ŭ�� ���������� ��ġ
            };
            
            //��ũ��Ʈ�� ���� ���� ���� �����ϴ� ���� ����
            //�� 2���� �ϳ��� ���� �����Ѵ�
            StructuredBuffer<float3> lineBuffer;

            //Vertex Shader
            v2f vert (uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                v2f o;

                //�ϳ��� ���� 2���� ������ ���� + �ش� ���� id
                //��ü ���ۿ��� ���� ������ �ε����� ã�´�
                int index = instance_id * 2 + vertex_id;

                //���ۿ� ����� ���� ��ġ = ���� ���� ��ǥ�� ���� ��ǥ ��
                //�ٷ� Ŭ�� �������� ��ȯ�Ѵ�
                o.vertex = UnityWorldToClipPos(float4(lineBuffer[index], 1));

                return o;
            }

            //Fragment Shader => �� ������ �׳� ���������� ������Ų��
            fixed4 frag (v2f i) : SV_Target
            {
                return float4(1,0,0,1);
        
            }
            ENDCG
        }
    }
}
