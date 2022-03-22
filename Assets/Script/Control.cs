using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Control : MonoBehaviour
{

    #region Ball 에 대한 정보
    
        public struct Ball // ball 하나의 구조체
        {

            // 여기서 position, velocity = 월드 공간 기준이잖아
            // 3 + 3 + 1 + 3 + 3 + 3 = 16 개
            public Vector3 position;
            public Vector3 velocity;
            public float speed;
            


            // 회전 후 모델 좌표계
            public Vector3 xBasis;
            public Vector3 yBasis;
            public Vector3 zBasis;

        }


        // ball 의 mesh 정보
        public Mesh ballMesh;


        // ball 을 그리는 데 쓰이는 material
        public Material ballMaterial;


        // 그리고자 하는 ball 의 최대개수    
        public int ballsCountMax;





        // Ball 이 움직일 수 있는 범위 => 이거는 실행도중 바꾸지 말자
        // inspector 창에서 수정은 할 수 있지만, 실행도중에는 바뀌지 않는다
        public Vector3 limitSize = new Vector3(10,10,10);


        public float initSpeed = 2.0f;
        public float maxSpeed = 5.0f;


/*         [Range(0.1f, 3.0f)]
        public float radius = 1.0f; */
        

    #endregion

    



    #region Plane 관련 = 결국 Vertex 들로 이루어져 있다

        struct Vertex // Plane 의 한 정점 구조체
        {
            public Vector3 position;
            public Vector3 normal;
        }

        

        // Plane 을 그리는 데 쓰이는 material
        public Material planeMaterial;



        [Range(0.01f, 0.1f)]
        public float planeLength = 0.02f;

    #endregion




    public ComputeShader computeShader;

    // Dispatch 에 필요한 그룹의 수
    private int groupSizeX;


    // delta time 을 쪼갤 반복 수 => 역시 실행 도중에는 바꾸지 않는다
    public int iteration = 5;




    #region ball - compute shader
        
        // ball 에 대한 연산을 하는 커널
        int ballKernelHandleMove;

        int ballKernelHandleAssign;



        // 사용하고자 하는 커널 함수 이름
        
        // 공을 이동시키는 커널 함수
        string ballKernelMove = "moveBalls";



        // 공에 정보를 할당하는 커널 함수
        string ballKernelAssign = "assignBalls";


        // ball buffer 에서 공을 늘리고자 하는 범위를 가리키는 인덱스
        public int startIndex = 0;

        public int endIndex = 1;

    #endregion



    #region plane - compute shader

        // plane 에 대한 연산을 하는 커널
        // 커널 하나만 사용한다
        int planeKernelHandleAttach;



        // 사용하는 커널 이름
        string planeKernelNameAttach = "attachPlane";


        
        // plane 을 이루는 quad 개수 => 처음에만 설정, 나중에는 바뀌지 않는다
        public int quadNum = 10;

    #endregion




    #region ball - array, compute buffer

        // ball 들의 정보를 모아둔 배열
        Ball[] ballArray;



        // ball array 를 GPU 에 넘기는 버퍼
        ComputeBuffer ballBuffer;



        // ball buffer 에 할당되는 실제 길이
        int ballBufferSize;

    #endregion




    #region plane - array, compute buffer
        
        // plane 을 이루는 정점들을 모아둔 배열
        Vertex[] vertexArray;



        // vertex array 를 넘기는 버퍼
        ComputeBuffer vertexBuffer;



        // vertex buffer 실제 길이
        int vertexBufferSize;
        
    #endregion





    #region Draw Mesh Instance Indirect 관련

        /*  
            draw indirect 에 필요한 argument 배열
            1. index count per instance
            2. instance count
            3. start index location
            4. base vertex location
            5. start instance location.
        */
        uint[] args = new uint[5] { 0,0,0,0,0 };
        // argument 배열을 넘겨주는 버퍼
        ComputeBuffer argsBuffer;


        Bounds bounds;


        MaterialPropertyBlock props;

    #endregion





    #region 실행 도중에 바꿀 수 있는 속성들
        
        // To compute shader

        /* [Range(0.1f, 5.0f)]
        public float threshold = 0.5f; */

        [Range(0.1f, 3.0f)]
        public float halfSize = 1.0f;


    #endregion






    #region 경계를 그리는 선
        public Material lineMaterial;

        ComputeBuffer lineBuffer;
        Vector3[] lineVertex = new Vector3[24];

        Vector3[] indexArray = new Vector3[8];

    #endregion






    

    // Start is called before the first frame update
    void Start()
    {
        // array 를 만들기 전에, 버퍼의 길이, 배열의 길이를 알아야 하므로
        // compute shader 관련 초기화를 먼저 진행해야 한다
        InitComputeShader();


        // ball array 를 만들고, ball data 를 초기화 한다
        InitBall();


        // vertex array 를 만들고, vertex data 를 초기화 한다
        InitVertex();


        // ball compute buffer 를 연결한다
        InitBallComputeBuffer();


        // vertex compute buffer 를 연결한다
        InitVertexComputeBuffer();


        // 마지막으로 argument buffer 를 만들고
        InitArgBuffer();

        // Draw Indirect 에 필요한 나머지 매개변수 설정
        bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
        props = new MaterialPropertyBlock();
        props.SetFloat("_UniqueID", Random.value);



        // 필요한 속성 값들을 GPU 로 넘긴다
        setPropertiesOnce();
        setProperties();



        Drawlines();
    }


    
    // 1
    void InitComputeShader()
    {

        // 셰이더에서 ball 계산을 담당하는 커널 함수를 가져온다
        ballKernelHandleMove = computeShader.FindKernel(ballKernelMove);

        ballKernelHandleAssign = computeShader.FindKernel(ballKernelAssign);



        // 셰이더에서 plane 계산을 담당하는 커널 함수를 가져온다
        planeKernelHandleAttach = computeShader.FindKernel(planeKernelNameAttach);




        // ball 을 계산하는 커널의, 한 스레드 그룹을 이루는 스레드 개수를 구한다.
        // 모든 커널 함수들은 한 스레드 그룹으 크기가 모두 동일하다
        uint x;
        computeShader.GetKernelThreadGroupSizes(ballKernelHandleMove, out x, out _, out _);
        



        // 한 그룹을 이루는 스레드의 개수를 이용해서, ball 최대 개수를 표현하려면 그룹이 최소 몇개가 필요한지 구한다.
        groupSizeX = Mathf.CeilToInt((float)ballsCountMax / (float)x);



        // ball 의 버퍼 크기를 구한다. 표현하려는 ball 최대 개수를 모두 포함할 수 있어야 한다
        ballBufferSize = groupSizeX * (int) x;



        // plane vertex 의 버퍼 크기를 구한다.
        // quad 하나에 vertex 가 6개가 필요하며, quadNum 개의 quad 가 존재한다
        // 따라서 ball 하나에, vertex 6 개 * 총 사용할 quad 갯수 만큼 공간을 확보한다
        vertexBufferSize = ballBufferSize * 6 * quadNum;
        
    }
    
    
    


    // 2
    void InitBall()
    {

        // 먼저 공들에 대한 정보를 담는 배열을 만든다.
        ballArray = new Ball[ballBufferSize];

        /* 
            배열 각각에 Ball 객체 정보를 일단 할당해 놓는다.
            데이터 용량 낭비인가? 싶긴한데 ...
            어쨌든 최대개수를 지원해야 하니까, 미리 할당한다고 생각하자.
        */
        for(int i = 0; i < ballBufferSize; i++)
            ballArray[i] = new Ball();



        // 첫 번째 공에 대해서만 초기화를 해준다
        // 속도 초기화
        ballArray[0].velocity = new Vector3(Random.value, Random.value, Random.value).normalized;
        ballArray[0].speed = (Random.value + 1) * initSpeed;


        
        Vector3 UP = new Vector3(0,1,0);
        // 속도에 맞춰서 좌표계 초기화
        ballArray[0].zBasis = ballArray[0].velocity.normalized;
        ballArray[0].xBasis = Vector3.Cross(UP, ballArray[0].zBasis);
        ballArray[0].yBasis = Vector3.Cross(ballArray[0].zBasis, ballArray[0].xBasis);
        
    }




    // 3
    void InitVertex()
    {

        // 사용할 vertex data 를 초기화 한다
        vertexArray = new Vertex[vertexBufferSize];

    }




    // 4
    void InitBallComputeBuffer()
    {

        /* 
            CPU 의 balls array 를 GPU 로 넘길 수 있는 Compute Buffer 를 생성한다.
            Compute Buffer 에 balls array 값을 저장한다.
        */
        ballBuffer = new ComputeBuffer(ballBufferSize, 16 * sizeof(float));
        ballBuffer.SetData(ballArray);



        // ball buffer 를 ball 계산 커널 함수에 연결
        computeShader.SetBuffer(ballKernelHandleMove, "ballBuffer", ballBuffer);
        computeShader.SetBuffer(ballKernelHandleAssign, "ballBuffer", ballBuffer);



        // ball 의 정보는 plane 계산 커널 함수에도 연결되어야 한다
        computeShader.SetBuffer(planeKernelHandleAttach, "ballBuffer", ballBuffer);
                


        // Compute Buffer 를 ball material 에 연결 => 렌더링에 사용한다
        ballMaterial.SetBuffer("ballBuffer", ballBuffer);

    }




    // 5
    void InitVertexComputeBuffer()
    {
        
        vertexBuffer = new ComputeBuffer(vertexBufferSize, 6 * sizeof(float));
        vertexBuffer.SetData(vertexArray);


        // vertex buffer 를 공 정보 업데이트 할 때 사용
        computeShader.SetBuffer(ballKernelHandleAssign, "vertexBuffer", vertexBuffer);
        
        
        // vertex buffer 는 plane 커널 함수에서 사용
        computeShader.SetBuffer(planeKernelHandleAttach, "vertexBuffer", vertexBuffer);



        // plane 을 그리는 material 에 연결
        planeMaterial.SetBuffer("vertexBuffer", vertexBuffer);

    }




    // 6
    void InitArgBuffer()
    {
        // Draw Indirect 명령어를 사용하는 데 쓰이는 argument 를 넘기는 compute buffer ~ 라는 것을 알린다.
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);


        if (ballMesh != null)
        {
            args[0] = (uint)ballMesh.GetIndexCount(0);
            args[1] = (uint)endIndex;

            //args[2] = (uint)ballMesh.GetIndexStart(0);
            //args[3] = (uint)ballMesh.GetBaseVertex(0);
        }


        // argument 버퍼에 값을 넣는다
        argsBuffer.SetData(args);

    }




    // 7
    void setPropertiesOnce() // 한 번만 들어가는 변수 값들 
    {
        
        // In Compute Shader
        computeShader.SetVector("limitSize", limitSize); // 공의 제한 범위
        computeShader.SetFloat("quadNum", quadNum); // plane 에서 사용할 quad 개수

        // 처음, 공을 늘리려는 범위를 지정해준다
        computeShader.SetInt("startIndex", startIndex);
        computeShader.SetInt("endIndex", endIndex);
               


        // In Ball Material
        ballMaterial.SetVector("limitSize", limitSize); // 공의 제한 범위



        // In Plane Material
        // material 에 한 plane 에 들어가는 quad 개수 넘김
        planeMaterial.SetFloat("quadNum", quadNum);
        
    }




    
    // 8
    void setProperties() // 값을 조절하고자 하는 변수 값들만 여기에 넣는다
    {
        
        // 먼저 compute shader 에 들어가는 변수 세팅
        computeShader.SetFloat("halfSize", halfSize);
        computeShader.SetFloat("maxSpeed", maxSpeed);
        computeShader.SetFloat("planeLength", planeLength);


        // To Ball Material
        ballMaterial.SetFloat("radius", halfSize);

    }


    









    // Update is called once per frame
    void Update()
    {
        /*
            매 프레임마다 compute buffer 에서 계산을 하게 하기 위해서, 프레임 사이 시간, deltaTime 을 넘겨주어야 한다.
            하지만 delta time 을 그냥 바로 넘겨버리면, 시간이 너무 짧아서 충돌 처리를 무시할 수 있다 => 벽 뚫고 나가버리는 버그

            그러니 delta time 을 5 개로 쪼개서, 각 5번에 대해 compute shader 에서 계산을 진행하게 하고
            이후에 렌더링 하도록 명령한다.
        */
        // 먼저 쪼갠 시간을 compute shader 에 넘겨준다.
        computeShader.SetFloat("deltaTime", Time.deltaTime/iteration);



        // itertaion 만큼 반복해서 compute shader 내에서 계산한다.
        for(int i = 0; i < iteration; i++)
        {
            // Compute Shader -> ball 커널 함수를 실행시켜, 그 결과를 버퍼에 저장
            // ball 들의 위치, 속도 등의 정보를 계산, 저장한다.
            computeShader.Dispatch(ballKernelHandleMove, groupSizeX, 1, 1);


            // 움직인 공에 대한 plane 을 계산한다
            computeShader.Dispatch(planeKernelHandleAttach, groupSizeX, 1, 1);
        }
      


        // Shader 를 이용하여, GPU Instancing 을 통해 바로 렌더링을 진행한다.
        // ball material 사용 => ball 을 그린다
        Graphics.DrawMeshInstancedIndirect(ballMesh, 0, ballMaterial, bounds, argsBuffer, 0, props);

    }





    // 카메라가 씬을 렌더링 한 후에 호출
    // 사용자가 자신의 오브젝트를 렌더링 하는 경우에 사용
    // ball 이 그려진 후에, plane 을 그리도록 한다
    void OnRenderObject()
    {
        
        // plane 을 그리는 material 을 이용 
        planeMaterial.SetPass(0);
        
        // 이번에는 primitive 가 삼각형이라고 넘겨주네 => 버퍼에서 알아서 3개씩 끊어서 읽고
        
        // 1개의 mesh instace = quad 를 그리니까 총 6개의 정점마다 끊어주고
        // 이제 여러개의 quad 를 그릴꺼니까, 6 * quadNum 개의 정점마다 끊어주어야 한다

        // 총 그리는 procedural geometry 의 개수
        Graphics.DrawProceduralNow(MeshTopology.Triangles, 6 * quadNum, endIndex);



        lineMaterial.SetPass(0);

        Graphics.DrawProceduralNow(MeshTopology.Lines, 2, 12);

    }
 



    // 스크립트나 인스펙터를 통해, 매개변수 값이 바뀌는 경우
    void OnValidate()
    {
        setProperties();
    }






    void OnDestroy()
    {
        if (ballBuffer != null)
        {
            ballBuffer.Dispose();
        }


        if (vertexBuffer != null)
        {
            vertexBuffer.Dispose();
        }


        if (argsBuffer != null)
        {
            argsBuffer.Dispose();
        }

        if (lineBuffer != null)
        {
            lineBuffer.Dispose();
        }
    }





    // 버튼은 다루는 메서드
    public void upClickNum()
    {
        if(endIndex == ballsCountMax) return;
        else increaseDrawNumber();

    }


    public void downClickNum()
    {
        decreaseDrawNumber();
    }





    void increaseDrawNumber() // click number 가 증가했을 때, 그릴 공의 정보를 늘리는 것
    {

        // 공의 개수를 늘리기 전에, 생각을 하는거지 => 공의 개수를 늘렸을 때, 범위를 미리 계산한다
        
        startIndex = endIndex;
        endIndex *= 3;



        // 2 가지 다 고려를 해야 한다
        // 먼저 endIndex 가 버퍼 사이즈 자체를 넘어가는 경우
        endIndex = (endIndex >= ballBufferSize) ? ballBufferSize : endIndex;


        // tmpEndIndex 가 주어진 최대 버퍼 크기를 넘어설 수 있다 => 이 경우, 최대 버퍼 크기 값을 가지도록 한다
        // ballBuffer 는 ballBufferSize - 1 까지만 인덱스를 가질 수 있겠지??
        endIndex = (endIndex >= ballsCountMax) ? ballsCountMax : endIndex;



        // 새로운 정보를 구하고자 하는 공의 범위를 구했으니, compute shader 에 값을 갱신한다
        computeShader.SetInt("startIndex", startIndex);
        computeShader.SetInt("endIndex", endIndex);

        

        // 현재 그려져 있던 공들에 대해서, 공의 갯수를 증가시키는 것
        // 지정된 범위 내에 있는 공들에 대해서, 정보를 앞에 있던 공에서 구한다
        computeShader.Dispatch(ballKernelHandleAssign, groupSizeX, 1, 1);




        // 새로 구한 draw number 를 argument 배열에 넣어주고, arg 도 갱신해준다
        args[1] = (uint)endIndex;
        argsBuffer.SetData(args);

    }







    void decreaseDrawNumber() // click number 가 감소했을 때 처리
    {

        // 이번에는 반대로 줄이고자 하는 범위를 구한다
        // 최소 0~1 범위까지만 내려가야 한다
        startIndex /= 3;
        endIndex = (endIndex / 3 == 0) ? 1 : endIndex / 3;



        // 줄어든 범위를 compute shader 에 갱신한다
        computeShader.SetInt("startIndex", startIndex);
        computeShader.SetInt("endIndex", endIndex);

        

        // 공의 개수가 줄어드는 경우에는, 호출할 커널 함수가 없다.                
        


        // 새로 구한 draw number 를 argument 배열에 넣어주고, arg 도 갱신해준다
        args[1] = (uint)endIndex;
        argsBuffer.SetData(args);


    }




    void Drawlines()
    {
        lineBuffer = new ComputeBuffer(24, 3 * sizeof(float));

        indexArray[0] = new Vector3( -limitSize.x, +limitSize.y, -limitSize.z );
        indexArray[1] = new Vector3( -limitSize.x, +limitSize.y, +limitSize.z );
        indexArray[2] = new Vector3( +limitSize.x, +limitSize.y, +limitSize.z );
        indexArray[3] = new Vector3( +limitSize.x, +limitSize.y, -limitSize.z );
        
        indexArray[4] = new Vector3( -limitSize.x, -limitSize.y, -limitSize.z );
        indexArray[5] = new Vector3( -limitSize.x, -limitSize.y, +limitSize.z );
        indexArray[6] = new Vector3( +limitSize.x, -limitSize.y, +limitSize.z );
        indexArray[7] = new Vector3( +limitSize.x, -limitSize.y, -limitSize.z );



        lineVertex[0] = indexArray[0];
        lineVertex[1] = indexArray[1];
        lineVertex[2] = indexArray[1];
        lineVertex[3] = indexArray[2];
        lineVertex[4] = indexArray[2];
        lineVertex[5] = indexArray[3];
        lineVertex[6] = indexArray[3];
        lineVertex[7] = indexArray[0];

        lineVertex[8] = indexArray[0];
        lineVertex[9] = indexArray[4];
        lineVertex[10] = indexArray[1];
        lineVertex[11] = indexArray[5];
        lineVertex[12] = indexArray[2];
        lineVertex[13] = indexArray[6];
        lineVertex[14] = indexArray[3];
        lineVertex[15] = indexArray[7];

        lineVertex[16] = indexArray[4];
        lineVertex[17] = indexArray[5];
        lineVertex[18] = indexArray[5];
        lineVertex[19] = indexArray[6];
        lineVertex[20] = indexArray[6];
        lineVertex[21] = indexArray[7];
        lineVertex[22] = indexArray[7];
        lineVertex[23] = indexArray[4];



        lineBuffer.SetData(lineVertex);

        lineMaterial.SetBuffer("lineBuffer", lineBuffer);


        

    }


}
