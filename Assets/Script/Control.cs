using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Control : MonoBehaviour
{
    #region Ball 에 대한 정보
        /*
        Ball 하나를 나타내는 구조체
        객체 하나 = 3 + 3 + 1 + 3 + 3 + 3 = 16 * float
        */
        public struct Ball
        {
            //position, velocity = 월드 공간 기준
            public Vector3 position;
            public Vector3 velocity;
            public float speed;
            
            // 회전 후 모델의 기본 좌표계
            public Vector3 xBasis;
            public Vector3 yBasis;
            public Vector3 zBasis;
        }

        //ball 의 mesh 정보, 모델
        public Mesh ballMesh;

        //ball 을 그리는 데 쓰이는 material
        public Material ballMaterial;

        // 그리고자 하는 ball 의 최대개수    
        public int ballsCountMax;

        //Ball 이 움직일 수 있는 범위 => 이거는 실행도중 바꾸지 말자
        //inspector 창에서 수정은 할 수 있지만, 실행도중에는 바뀌지 않는다
        public Vector3 limitSize = new Vector3(10,10,10);

        //Ball 의 속도
        public float initSpeed = 2.0f;
        public float maxSpeed = 5.0f;

        //공의 반지름
        [Range(0.1f, 3.0f)]
        public float halfSize = 1.0f;

    #endregion

    #region Ball 의 꼬리, Plane 관련 = 결국 Vertex 들로 이루어져 있다

        // Plane 의 한 정점 구조체
        struct Vertex
        {
            public Vector3 position;
            public Vector3 normal;
        }

        // Plane 을 그리는 데 쓰이는 material
        public Material planeMaterial;

        [Range(0.01f, 0.1f)] //Plane 의 길이
        public float planeLength = 0.02f;

    #endregion

    #region Compute Shader Information
        
        //할당받는 Compute Shader
        public ComputeShader computeShader;

        //Dispatch 에 필요한 그룹의 수
        //Compute Shader 에서 사용하고자 하는 그룹 수
        private int groupSizeX;

        // delta time 을 쪼갤 반복 수 => 역시 실행 도중에는 바꾸지 않는다
        public int iteration = 5;

    #endregion

    #region ball - compute shader

        //Kernel Id 저장
        int ballKernelHandleMove;   //ball 에 대한 연산을 하는 커널 id
        int ballKernelHandleAssign;

        //Compute Shader 에서 사용하고자 하는 커널 함수 이름
        string ballKernelMove = "moveBalls";     // 공을 이동시키는 커널 함수
        string ballKernelAssign = "assignBalls"; // 공에 정보를 할당하는 커널 함수

        // ball buffer 에서 공을 늘리고자 하는 범위를 가리키는 인덱스
        public int startIndex = 0;
        public int endIndex = 1;

    #endregion

    #region plane - compute shader

        //사용하는 kerenl Id
        int planeKernelHandleAttach;    // plane 에 대한 연산을 하는 커널 id

        //사용하는 커널 함수 이름
        string planeKernelNameAttach = "attachPlane"; //커널 함수 하나만 사용
        
        //하나의 plane 을 이루는 quad 개수 => 처음에만 설정, 나중에는 바뀌지 않는다
        public int quadNum = 10;

    #endregion

    #region ball - array, compute buffer

        //ball 들의 정보를 모아둔 배열, 버퍼
        Ball[] ballArray;

        //ballArray 를 GPU 에 넘기는 버퍼
        ComputeBuffer ballBuffer;

        // ball buffer 에 할당되는 실제 길이
        int ballBufferSize;

    #endregion

    #region plane - array, compute buffer
        
        //plane 을 이루는 정점들을 모아둔 배열
        Vertex[] vertexArray;

        //vertexArray 를 GPU에 넘기는 버퍼
        ComputeBuffer vertexBuffer;

        // vertex buffer 실제 길이
        int vertexBufferSize;

    #endregion

    #region Draw Mesh Instance Indirect 관련
        /*  
            Draws the same mesh multiple times using GPU instancing.
            같은 도형을 여러 개, 빠르게 그려내는 GPU Instancing 을 사용하기 위해 필요한 arguments

            draw indirect 에 필요한 argument 배열
            1. index count per instance
            2. instance count
            3. start index location
            4. base vertex location
            5. start instance location.
        */
        uint[] args = new uint[5] { 0,0,0,0,0 };
        
        //args 배열을 GPU에 넘겨주는 버퍼
        ComputeBuffer argsBuffer;
        
        //필요하다고 해서 넣었는데... 자세히는 모르겠다
        Bounds bounds;
        MaterialPropertyBlock props;

    #endregion

    #region 경계를 그리는 선
        
        public Material lineMaterial;

        //경계는 직육면체 모양
        Vector3[] indexArray = new Vector3[8];  //직육면체의 각 점 위치를 저장

        //직육면체의 각 선을 저장
        //직육면체는 총 12 개의 선으로 구성되어 있으며
        //각 선은 2개의 정점으로 구성된다
        //따라서 12 * 2 = 24 개의 정점 배열로 구성
        Vector3[] lineVertex = new Vector3[24];

        ComputeBuffer lineBuffer;

    #endregion



    //Start is called before the first frame update
    void Start()
    {
        /*
        Compute Shader 에서 필요한 커널 함수를 할당하며
        GPU 에 정보를 넘길 때 필요한 버퍼의 최대 크기를 계산한다
        */
        InitComputeShader();

        //ball 정보가 담긴 array 를 만들고, ball data 를 초기화 한다
        InitBall();

        //Plane 의 vertex 정보가 담긴 array 를 만들고, vertex data 를 초기화 한다
        InitVertex();

        //ball 정보를 넘기는 compute buffer 를 만들고 GPU 에 연결한다
        InitBallComputeBuffer();

        //Plane 의 vertex 정보를 넘기는 compute buffer 를 만들고 GPU 에 연결한다
        InitVertexComputeBuffer();


        //마지막으로 GPU Instancing 에 필요한 argument buffer 를 만들고
        InitArgBuffer();

        //Draw Indirect 에 필요한 나머지 매개변수 설정
        //자세하게는 모르겠다...
        bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
        props = new MaterialPropertyBlock();
        props.SetFloat("_UniqueID", Random.value);

        //연산에 필요한 속성 값들을 GPU 로 넘긴다
        setPropertiesOnce();    //한번만 설정, 이후 변경 없음
        setProperties();        //변경가능

        //공이 움직일 수 있는 범위를 line 으로 그려서 표현
        Drawlines();
    }


    // 1
    void InitComputeShader()
    {
        //Compute Shader에서 ball 계산에 필요한 커널 함수 id 를 가져온다
        ballKernelHandleMove = computeShader.FindKernel(ballKernelMove);
        ballKernelHandleAssign = computeShader.FindKernel(ballKernelAssign);

        //plane 계산에 필요한 커널 함수 id
        planeKernelHandleAttach = computeShader.FindKernel(planeKernelNameAttach);

        /*
        Compute Shader 에서 하나의 커널 함수가 사용하는 스레드 개수를 가져온다
        나는 모든 커널 함수가 같은 스레드 개수를 사용하도록 설정했으며, 모두 x축 방향 스레드만 사용한다
        
        따라서, 임의의 하나의 커널 함수에 대해서 스레드 개수를 가져오며,
        x 축 방향 개수만 가져온다
         */
        uint x;
        computeShader.GetKernelThreadGroupSizes(ballKernelHandleMove, out x, out _, out _);
        
        //이번 실행에서 최대로 가질 수 있는 공의 개수를 이용해서, 최대로 필요한 그룹의 개수를 구한다
        //최대 개수의 Ball 을 표현하기 위해서, 필요한 최대 그룹의 수는 무엇인가?
        groupSizeX = Mathf.CeilToInt((float)ballsCountMax / (float)x);

        //Ball 의 정보를 GPU로 넘길 버퍼 크기를 구한다. 표현하려는 ball 최대 개수를 모두 포함할 수 있어야 한다
        ballBufferSize = groupSizeX * (int) x;

        /*
        하나의 plane 을 이루는 vertex 의 정보를 GPU 에 넘길 버퍼 크기를 구한다.
        quad 하나 당 vertex 가 6개가 필요하며, 하나의 plane 에는 quadNum 개의 quad 가 존재한다
        Ball 하나 당 하나의 Plane 이 할당되므로, 하나의 Ball 당 6 * quadNum 개의 Vertex 가 할당된다
        최대 ballBufferSize 개 의 Ball 정보를 저장하므로,
        ballBufferSize * 6 * quadNum 만큼의 버퍼 크기가 있어야, 최대 Plane Vertex 정보를 저장할 수 있다
         */
        vertexBufferSize = ballBufferSize * 6 * quadNum;
    }

    // 2
    void InitBall()
    {
        //먼저 공들에 대한 정보를 담는 배열을 만든다.
        //배열의 크기는 앞서 구한, 최대 개수의 공을 받는 경우를 고려해 최대 개수 크기로 할당한다
        ballArray = new Ball[ballBufferSize];

        /* 
            배열 각각에 Ball 객체 정보를 일단 할당해 놓는다.
            사용하지 않는 Ball 들까지 미리 객체를 만드는 것이라, 데이터 용량 낭비인가? 싶긴한데 ...
            어쨌든 최대개수를 지원해야 하니까, 미리 할당한다고 생각하자.
        */
        for(int i = 0; i < ballBufferSize; i++)
            ballArray[i] = new Ball();

        //첫 번째 공에 대해서만 초기화를 해준다, 처음에는 무조건 공 하나만 나타나기 때문에
        //속도 초기화, 랜덤한 값을 가진다
        //월드 공간 좌표계 기준으로 속도를 표현한다
        ballArray[0].velocity = new Vector3(Random.value, Random.value, Random.value).normalized;
        ballArray[0].speed = (Random.value + 1) * initSpeed;

        //공의 속도에 맞춰서 공의 모델 좌표계 초기화
        //공의 모델 좌표계는 월드 공간 좌표계 중심으로 표현한다
        //카메라 좌표계를 구하는 방식을 따라했다
        Vector3 UP = new Vector3(0,1,0); //월드 공간 y축        
        ballArray[0].zBasis = ballArray[0].velocity.normalized;                         //z축 => 공이 나아가는 방향
        ballArray[0].xBasis = Vector3.Cross(UP, ballArray[0].zBasis);                   //x = UP cross z
        ballArray[0].yBasis = Vector3.Cross(ballArray[0].zBasis, ballArray[0].xBasis);  //y = z cross x
    }

    // 3
    void InitVertex()
    {
        //사용할 vertex data 를 최대 개수로 생성한다
        //Ball 과 달리 초기화 할 데이터가 따로 없다
        vertexArray = new Vertex[vertexBufferSize];
    }

    // 4
    void InitBallComputeBuffer()
    {
        /* 
            CPU 의 balls array 를 GPU 로 넘길 수 있는 Compute Buffer 를 생성한다
            Compute Buffer 에 balls array 값을 저장한다
        */                                           //Ball Struct 하나의 사이즈 = float * 16
        ballBuffer = new ComputeBuffer(ballBufferSize, 16 * sizeof(float));
        ballBuffer.SetData(ballArray); //Ball Array 의 정보를 버퍼로 전달한다

        //ball buffer 를 Compute Shader 에서 Ball 을 계산하는 커널 함수에 연결
        //해당 커널 함수에서 버퍼에 접근할 수 있도록 한다
        computeShader.SetBuffer(ballKernelHandleMove, "ballBuffer", ballBuffer);
        computeShader.SetBuffer(ballKernelHandleAssign, "ballBuffer", ballBuffer);

        //ball 의 정보는 plane 계산 커널 함수에도 연결되어야 한다 => Plane 이 Ball 을 따라다녀야 하기 때문에
        computeShader.SetBuffer(planeKernelHandleAttach, "ballBuffer", ballBuffer);

        //Ball Compute Buffer 를 ball material 에도 연결 => Ball 렌더링에도 사용한다
        ballMaterial.SetBuffer("ballBuffer", ballBuffer);
    }

    // 5
    void InitVertexComputeBuffer()
    {
                                                        //Vertex 1 개 = float * 6
        vertexBuffer = new ComputeBuffer(vertexBufferSize, 6 * sizeof(float));
        vertexBuffer.SetData(vertexArray);

        //vertex buffer 를 공 정보 업데이트 할 때 사용
        computeShader.SetBuffer(ballKernelHandleAssign, "vertexBuffer", vertexBuffer);

        // vertex buffer 는 plane 커널 함수에서 사용
        computeShader.SetBuffer(planeKernelHandleAttach, "vertexBuffer", vertexBuffer);

        // plane 을 그리는 material 에 연결
        planeMaterial.SetBuffer("vertexBuffer", vertexBuffer);
    }

    // 6
    void InitArgBuffer()
    {
        //GPU Instancing, Draw Indirect 명령어를 사용하는 데 쓰이는
        //argument 를 넘기는 compute buffer 를 만들고
        //Draw Inderect 에 쓰일 것 이라는 걸 알린다.
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        //argument array 에서 값을 먼저 채우고
        if (ballMesh != null) //Ball 을 그릴 것이기 때문에, Ball Mesh 가 있을 때만 유효하다
        {
            args[0] = (uint)ballMesh.GetIndexCount(0); //Instance 당 필요한 index count
            args[1] = (uint)endIndex;   //그리고자 하는 Instance 개수, 처음에는 1개
            //모르겠으..
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
        computeShader.SetVector("limitSize", limitSize);   //공의 제한 범위
        computeShader.SetFloat("quadNum", quadNum);        //하나의 plane 을 구성하는 quad 개수

        //처음, 공을 늘리려는 범위를 지정해준다
        computeShader.SetInt("startIndex", startIndex);
        computeShader.SetInt("endIndex", endIndex);
               
        // In Ball Material
        ballMaterial.SetVector("limitSize", limitSize); // 공의 제한 범위

        // In Plane Material
        planeMaterial.SetFloat("quadNum", quadNum); //한 plane 에 들어가는 quad 개수 넘김
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


    
    //Update is called once per frame
    void Update()
    {
        /*
            매 프레임마다 compute buffer 에서 계산을 하게 하기 위해서, 프레임 사이 시간, deltaTime 을 넘겨주어야 한다.
            하지만 delta time 을 그냥 바로 넘겨버리면, 시간이 너무 짧아서 충돌 처리를 무시할 수 있다 => 벽 뚫고 나가버리는 버그

            그러니 delta time 을 5 개로 쪼개서, 각 5번에 대해 compute shader 에서 계산을 진행하게 하고
            이후에 렌더링 하도록 명령한다.
        */
        //먼저 iteration 으로 쪼갠 시간을 compute shader 에 넘겨준다
        computeShader.SetFloat("deltaTime", Time.deltaTime/iteration);

        // itertaion 만큼 반복해서 compute shader 내에서 iteration 번 계산하도록 한다
        for(int i = 0; i < iteration; i++)
        {
            //Compute Shader -> ball 커널 함수를 실행시켜, 그 결과를 버퍼에 저장
            //ball 들의 위치, 속도 등의 정보를 계산, 저장한다.
            computeShader.Dispatch(ballKernelHandleMove, groupSizeX, 1, 1);

            //움직인 공에 대한 plane 을 계산한다
            computeShader.Dispatch(planeKernelHandleAttach, groupSizeX, 1, 1);
        }    

        //Shader 를 이용하여, GPU Instancing 을 통해 바로 렌더링을 진행한다.
        // ball material 사용 => ball 을 그린다
        Graphics.DrawMeshInstancedIndirect(ballMesh, 0, ballMaterial, bounds, argsBuffer, 0, props);
        
        /*
        Plane 은 Procedual Mesh
        예제를 봤었을 때, 따로 그렸었다
        그걸 이용
        */
    }



    /*
    카메라가 씬을 렌더링 한 후에 자동으로 호출되는 함수
    사용자가 자신의 오브젝트를 렌더링 하는 경우에 사용, 나의 경우 Procedural Mesh 를 그리기 위해 사용
    ball 이 그려진 후에, plane 을 그리도록 한다
    */
    void OnRenderObject()
    {
        //plane 을 그리는 material 을 이용, shader 에서 pass 0 을 사용한다?
        //어차피 renderPlane shader 내에서 pass 는 하나만 존재한다
        planeMaterial.SetPass(0);
        
        //이번에는 primitive 가 삼각형이라고 넘겨주네 => 버퍼에서 알아서 3개씩 끊어서 읽고
        //1개의 mesh instance = quad 를 그리니까 총 6개의 정점마다 끊어주고
        //하나의 Plane 에는 quadNum 개의 quad 가 존재하므로, 6 * quadNum 개의 정점마다 끊어주어야 한다
        //endIndex = 총 그리는 procedural geometry 의 개수
        Graphics.DrawProceduralNow(MeshTopology.Triangles, 6 * quadNum, endIndex);
                
        
        //경계선인 line 을 그린다
        lineMaterial.SetPass(0);
        
        //하나의 line 은 2 개의 vertex 로 구성되어 있으며
        //경계선은 직육면체로 구성됨 => 12 개의 선으로 구성되어 있다
        Graphics.DrawProceduralNow(MeshTopology.Lines, 2, 12);
    }
 


    // 스크립트나 인스펙터를 통해, 매개변수 값이 바뀌는 경우
    void OnValidate()
    {
        setProperties();
    }



    void OnDestroy()    //프로그램 종료 시, Compute Buffer 제거
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
        /*
        한번 클릭할 때마다, 1 개의 공마다 2개의 공이 새로 생성된다
        Binary Tree 구조하고 비슷, 부모 공 하나마다 2개의 자식 공이 생성

        [startIndex, endIndex) = 새로 생성되는 공의 인덱스
        */
        //새로 생성되는 공의 인덱스 계산
        startIndex = endIndex;
        endIndex *= 3;

        //공의 개수가 버퍼 사이즈를 넘어가는 경우, 2 가지 다 고려를 해야 한다
        //먼저 endIndex 가 버퍼 사이즈 자체를 넘어가는 경우
        //endIndex = (endIndex >= ballBufferSize) ? ballBufferSize : endIndex;

        
        //버퍼 사이즈 고려할 필요 없이, 공의 개수가 최대 개수를 넘으면 그냥 무조건 최대 개수만 가지도록 한다
        endIndex = (endIndex >= ballsCountMax) ? ballsCountMax : endIndex;


        //새로운 정보를 구하고자 하는 공의 범위를 구했으니, compute shader 에 값을 갱신한다
        computeShader.SetInt("startIndex", startIndex);
        computeShader.SetInt("endIndex", endIndex);

        //세로 추가된 Ball 들의 정보를 구하는 커널 함수 실행
        computeShader.Dispatch(ballKernelHandleAssign, groupSizeX, 1, 1);

        // 새로 구한 draw number 를 argument 배열에 넣어주고, arg 도 갱신해준다
        args[1] = (uint)endIndex;   // GPU Instancing 을 통해 그릴 Instance 개수 갱신
        argsBuffer.SetData(args);
    }

    void decreaseDrawNumber() // click number 가 감소했을 때 처리
    {
        //이번에는 반대로 줄이고자 하는 범위를 구한다
        //최소 0~1 범위까지만 내려가야 한다
        startIndex /= 3;
        endIndex = (endIndex / 3 == 0) ? 1 : endIndex / 3;

        // 줄어든 범위를 compute shader 에 갱신한다
        computeShader.SetInt("startIndex", startIndex);
        computeShader.SetInt("endIndex", endIndex);

        //공의 개수가 줄어드는 경우에는, 호출할 커널 함수가 없다.      
        //그냥 그리는 Instance 개수를 줄이면 된다
        //새로 구한 draw number 를 argument 배열에 넣어주고, arg 도 갱신해준다
        args[1] = (uint)endIndex;
        argsBuffer.SetData(args);
    }



    // 9.
    void Drawlines() //공이 움직일 수 있는 경계를 그리는 데 필요한 데이터를 설정하는 함수
    {
        //경계 직육면체의 선분 데이터를 GPU 에 넘겨줄 컴퓨트 버퍼
        //한 점은 3*float 로 구성
        //12개의 선분 표현을 위해 총 24개의 점 데이터를 저장해야 한다
        lineBuffer = new ComputeBuffer(24, 3 * sizeof(float));

        //먼저 주어진 limitSize 정보를 바탕으로, 경계의 각 점 데이터를 초기화
        //직육면체의 8 개 점의 위치를 직접 할당
        //위
        indexArray[0] = new Vector3( -limitSize.x, +limitSize.y, -limitSize.z );
        indexArray[1] = new Vector3( -limitSize.x, +limitSize.y, +limitSize.z );
        indexArray[2] = new Vector3( +limitSize.x, +limitSize.y, +limitSize.z );
        indexArray[3] = new Vector3( +limitSize.x, +limitSize.y, -limitSize.z );
        //아래
        indexArray[4] = new Vector3( -limitSize.x, -limitSize.y, -limitSize.z );
        indexArray[5] = new Vector3( -limitSize.x, -limitSize.y, +limitSize.z );
        indexArray[6] = new Vector3( +limitSize.x, -limitSize.y, +limitSize.z );
        indexArray[7] = new Vector3( +limitSize.x, -limitSize.y, -limitSize.z );

        //할당한 점의 위치를 바탕으로 직육면체의 각 선분을 지정한다
        //선분은 시작점, 도착점 으로 구성된다
        //위
        lineVertex[0] = indexArray[0];
        lineVertex[1] = indexArray[1];
        lineVertex[2] = indexArray[1];
        lineVertex[3] = indexArray[2];
        lineVertex[4] = indexArray[2];
        lineVertex[5] = indexArray[3];
        lineVertex[6] = indexArray[3];
        lineVertex[7] = indexArray[0];
        //중간
        lineVertex[8] = indexArray[0];
        lineVertex[9] = indexArray[4];
        lineVertex[10] = indexArray[1];
        lineVertex[11] = indexArray[5];
        lineVertex[12] = indexArray[2];
        lineVertex[13] = indexArray[6];
        lineVertex[14] = indexArray[3];
        lineVertex[15] = indexArray[7];
        //아래
        lineVertex[16] = indexArray[4];
        lineVertex[17] = indexArray[5];
        lineVertex[18] = indexArray[5];
        lineVertex[19] = indexArray[6];
        lineVertex[20] = indexArray[6];
        lineVertex[21] = indexArray[7];
        lineVertex[22] = indexArray[7];
        lineVertex[23] = indexArray[4];

        //직접 초기화 한 선분 데이터를 컴퓨트 버퍼에 복사
        lineBuffer.SetData(lineVertex);

        //선을 그릴 수 있게 Material 에 버퍼를 연결한다
        lineMaterial.SetBuffer("lineBuffer", lineBuffer);
    }
}
