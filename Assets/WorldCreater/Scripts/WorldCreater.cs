using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldCreater : MonoBehaviour
{
    [Header("随机生成种子")]
    public int seed = 1;
    [Header("Perlin噪声 x: scale y:strength z:pow")]
    public Vector4 perlinNoiseArgs = Vector4.one;
    [Header("Simplex噪声 x: scale y:strength z:pow")]
    public Vector4 simplexNoiseArgs = Vector4.one;
    [Header("单位大小")]
    public float unitSize = 1;
    [Header("最大层数")]
    public int maxLevel = 10;
    [Header("层高")]
    public float levelHeight = 1;

    [Header("噪声重置ComputeShader")]
    public ComputeShader noiseClearShader;
    [Header("噪声生成ComputeShader")]
    public ComputeShader noiseGenerateComputeShader;
    [Header("地形生成ComputeShader")]
    public ComputeShader terrainGenerateShader;
    [Header("地表修复ComputeShader")]
    public ComputeShader terrainRepairShader;
    [Header("物体摆放ComputeShader")]
    public ComputeShader objPlaceShader;
    [Header("地表渲染材质")]
    public Material terrainMaterial;

    [Header("1种摆放物预制体(多种容易个别电脑容易闪退)")]
    public List<GameObject> sceneObjects;
    [Header("开启植物绘制（进入游戏容易闪退）")]
    public bool enableObjsDraw = true;

    //[Header("3种摆放物出现频率")]
    //public Vector3 sceneObjectAppearRate;
    //[Header("3种摆放物基础大小")]
    //public Vector3 sceneObjectScale;
    //[Header("3种摆放物大小随机值(0.0 - 1.0)")]
    //public Vector3 sceneObjectScaleRandom;

    //噪声贴图金字塔
    public enum MaxDraw
    {
        _256X256 = 256,
        _512X512 = 512,
        _1024X1024 = 1024
    }
    private Dictionary<int, RenderTexture> noiseRTDic = new Dictionary<int, RenderTexture>();
    private RenderTexture activeNoiseRT;
    private int maxRTSize = (int)MaxDraw._1024X1024;
    private Vector2Int activeRTSize;

    private bool vaild;
    private Mesh terrainUnitMesh;
    private Bounds drawBounds;

    private Vector2Int drawSize;
    private Vector2Int startUnitID; //视野矩形范围左下角的ID
    Vector4[] cameraFrustumPlanes_Vec4 = new Vector4[6];
    Plane[] cameraFrustumPlanes = new Plane[6];


    //记录视野的四个坐标极值
    private float minX;
    private float maxX;
    private float minZ;
    private float maxZ;

    private int xMinID;
    private int zMinID;
    private int xMaxID;
    private int zMaxID;

    private Camera activeCamera
    {
        get
        {
            return Camera.main;
        }
    }

    //GPU相关Buffer
    ComputeBuffer terrainDrawBuffer;
    int[] terrainDrawArgsTemp = new int[5];
    ComputeBuffer terrainDrawArgs;
    int[] terrainRepairArgsTemp = new int[3];
    ComputeBuffer terrainRepairArgs;
    ComputeBuffer allQuadTypeVertexBuffer;
    ComputeBuffer flatTerrainQuadBuffer;
    ComputeBuffer flatTerrainQuadArgs;
    List<ObjDrawData> objsDrawDatas;
    int objsMaxDrawPerKind = 8192;
    bool flatQuadRequestFlag = false;

    class ObjDrawData
    {
        public Material mat;
        public Mesh mesh;
        public ComputeBuffer computeBuffer;
        public ComputeBuffer indirectArgs;
    }


    private void Awake()
    {
        if (!IsVaild())
        {
            Debug.LogError("WorldCreateError: Check properties and camera");
        }
        GenerateTerrainUnit();
        InitGPUBuffer();

        drawBounds = new Bounds(Vector3.zero, new Vector3(100, 100, 100));
    }

    //在异步回读协程之后执行
    void LateUpdate()
    {
        vaild = IsVaild();

        if (!vaild)
        {
            return;
        }

        bool viewportVaild = ViewportHelper.Instance.ComputeViewportRange(activeCamera, ref minX, ref maxX, ref minZ, ref maxZ);

        if (!viewportVaild)
        {
            Debug.LogError("Camera Viewport Error");
            return;
        }

        //整体时2X2为最小单位处理的，所以需要变成偶数
        xMinID = Mathf.FloorToInt(minX / unitSize);
        zMinID = Mathf.FloorToInt(minZ / unitSize);
        xMaxID = Mathf.CeilToInt(maxX / unitSize);
        zMaxID = Mathf.CeilToInt(maxZ / unitSize);

        if (xMinID % 2 == 1)
        {
            xMinID--;
        }

        if (zMinID % 2 == 1)
        {
            zMinID--;
        }

        if (xMaxID % 2 == 1)
        {
            xMaxID++;
        }

        if (zMaxID % 2 == 1)
        {
            zMaxID++;
        }

        drawSize = new Vector2Int(
            (xMaxID - xMinID) / 2,
            (zMaxID - zMinID) / 2
        );

        TerrainNoiseGeneterDispatch();
        TerrainDrawGeneDispatch();
        TerrainRepairDispatch();

        drawBounds.center = activeCamera.transform.position;
        DrawTerrain();
        DrawObjs();

        DebugHelper.Instance.PrintDebug();
    }

    void TerrainRepairDispatch()
    {
        flatTerrainQuadBuffer.SetCounterValue(0);
        terrainRepairShader.SetTexture(0, "renderTexture", activeNoiseRT);
        terrainRepairShader.SetFloat("unitSize", unitSize);
        ComputeBuffer.CopyCount(terrainDrawBuffer, terrainRepairArgs, 0);

        terrainRepairShader.Dispatch(0, Mathf.CeilToInt(activeRTSize.x * activeRTSize.y / 64f), 1, 1);

        //将平地的数量放到Args中
        ComputeBuffer.CopyCount(flatTerrainQuadBuffer, flatTerrainQuadArgs, 0);
        if (flatQuadRequestFlag == false)
        {
            flatQuadRequestFlag = true;
            StartCoroutine("RequestFlatQuadCount");
        }
    }

    bool TerrainNoiseGeneterDispatch()
    {
        activeRTSize = (drawSize + Vector2Int.one) * 2;
        int maxSize = Mathf.Max(activeRTSize.x, activeRTSize.y);
        int textureSize = (int)Mathf.Pow(2f, Mathf.Ceil(Mathf.Log(maxSize, 2f)));

        if (DebugHelper.Instance.debugEnable)
        {
            DebugHelper.Instance.AppendDebug("贴图噪声大小:" + textureSize);
        }

        if (textureSize > maxRTSize)
        {
            Debug.Log("视野范围过大，请重新调整视野:" + textureSize);
            return false;
        }

        if (!noiseRTDic.TryGetValue(textureSize, out activeNoiseRT))
        {
            activeNoiseRT = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RFloat);
            activeNoiseRT.enableRandomWrite = true;
            activeNoiseRT.useMipMap = false;
            activeNoiseRT.Create();
            activeNoiseRT.filterMode = FilterMode.Point;
            noiseRTDic.Add(textureSize, activeNoiseRT);
        }

        //先清除RenderTexture的内容
        noiseClearShader.SetTexture(0, "renderTexture", activeNoiseRT);
        noiseClearShader.SetFloat("targetColor", 0);
        noiseClearShader.Dispatch(0, Mathf.CeilToInt(activeRTSize.x / 8f), Mathf.CeilToInt(activeRTSize.y / 8f), 1);

        //生成噪声纹理
        noiseGenerateComputeShader.SetTexture(0, "renderTexture", activeNoiseRT);
        noiseGenerateComputeShader.SetVector("minUnitID", new Vector4(xMinID, zMinID, 0, 0));
        noiseGenerateComputeShader.SetVector("perlinNoiseArgs", perlinNoiseArgs);
        noiseGenerateComputeShader.SetVector("simplexNoiseArgs", simplexNoiseArgs);
        noiseGenerateComputeShader.SetFloat("maxLevel", maxLevel);
        noiseGenerateComputeShader.SetFloat("seed", seed);
        noiseGenerateComputeShader.Dispatch(0, Mathf.CeilToInt(activeRTSize.x / 8f), Mathf.CeilToInt(activeRTSize.y / 8f), 1);
        return true;
    }

    //只保留可见Quad
    void TerrainDrawGeneDispatch()
    {
        terrainGenerateShader.SetTexture(0, "renderTexture", activeNoiseRT);
        terrainGenerateShader.SetFloat("maxLevel", maxLevel);
        terrainGenerateShader.SetFloat("levelHeight", levelHeight);
        terrainGenerateShader.SetFloat("unitSize", unitSize);

        GeometryUtility.CalculateFrustumPlanes(Camera.main, cameraFrustumPlanes);
        Vector3 normal;
        for (int i = 0; i < 6; i++)
        {
            normal = cameraFrustumPlanes[i].normal;
            cameraFrustumPlanes_Vec4[i] = new Vector4(-normal.x, -normal.y, -normal.z, -cameraFrustumPlanes[i].distance);
        }

        Matrix4x4 v = activeCamera.worldToCameraMatrix;
        Matrix4x4 p = activeCamera.projectionMatrix;
        Matrix4x4 vp = p * v;
        Vector3 srcPosition = new Vector3(xMinID * unitSize, 0, zMinID * unitSize);
        terrainDrawBuffer.SetCounterValue(0);
        terrainGenerateShader.SetMatrix("_VPMatrix", vp);
        terrainGenerateShader.SetVectorArray("planes", cameraFrustumPlanes_Vec4);
        terrainGenerateShader.SetVector("cameraPosition", activeCamera.transform.position);
        terrainGenerateShader.SetVector("renderTextureActiveSize", new Vector4(activeRTSize.x, activeRTSize.y, 0, 0));
        terrainGenerateShader.SetVector("srcPosition", srcPosition);
        terrainGenerateShader.Dispatch(0, Mathf.CeilToInt(activeRTSize.x / 16f), Mathf.CeilToInt(activeRTSize.y / 16f), 1);
    }


    void DrawTerrain()
    {
        terrainMaterial.SetFloat("_UnitSize", unitSize);
        terrainMaterial.SetFloat("_LevelHeight", levelHeight);
        terrainMaterial.SetTexture("_DebugTexture", activeNoiseRT);
        terrainMaterial.SetFloat("_DebugTextureSize", activeNoiseRT.width);
        ComputeBuffer.CopyCount(terrainDrawBuffer, terrainDrawArgs, 4);
        Graphics.DrawMeshInstancedIndirect(terrainUnitMesh, 0, terrainMaterial, drawBounds, terrainDrawArgs, 0, null, UnityEngine.Rendering.ShadowCastingMode.On, false, 0);
    }

    void InitGPUBuffer()
    {
        int maxDraw = maxRTSize * maxRTSize;
        terrainDrawBuffer = new ComputeBuffer(maxDraw, 4 * sizeof(float) + 3 * sizeof(int), ComputeBufferType.Append);

        terrainDrawArgs = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        terrainDrawArgsTemp[0] = 6;
        terrainDrawArgsTemp[1] = 0;
        terrainDrawArgsTemp[2] = 0;
        terrainDrawArgsTemp[3] = 0;
        terrainDrawArgsTemp[4] = 0;
        terrainDrawArgs.SetData(terrainDrawArgsTemp);

        terrainRepairArgs = new ComputeBuffer(1, 3 * sizeof(uint), ComputeBufferType.IndirectArguments);
        terrainRepairArgsTemp[0] = 1;
        terrainRepairArgsTemp[1] = 1;
        terrainRepairArgsTemp[2] = 1;
        terrainRepairArgs.SetData(terrainRepairArgsTemp);

        flatTerrainQuadBuffer = new ComputeBuffer(maxDraw, 3 * sizeof(float), ComputeBufferType.Append);
        flatTerrainQuadArgs = new ComputeBuffer(1, 1 * sizeof(uint), ComputeBufferType.IndirectArguments);
        terrainRepairShader.SetBuffer(0, "drawArgs", terrainRepairArgs);
        terrainRepairShader.SetBuffer(0, "terrainDrawBuffer", terrainDrawBuffer);
        terrainRepairShader.SetBuffer(0, "flatTerrainQuadBuffer", flatTerrainQuadBuffer);

        allQuadTypeVertexBuffer = new ComputeBuffer(5 * 5 * 5 * 6, 12 * sizeof(float));
        allQuadTypeVertexBuffer.SetData(PreCompute.Instance.allTypeQuads);
        terrainGenerateShader.SetBuffer(0, "terrainDrawBuffer", terrainDrawBuffer);

        terrainMaterial.SetBuffer("terrainDrawBuffer", terrainDrawBuffer);
        terrainMaterial.SetBuffer("allQuadTypeVertexBuffer", allQuadTypeVertexBuffer);

        InitSceneObjsBuffer();
        objPlaceShader.SetBuffer(0, "Obj0Buffer", objsDrawDatas[0].computeBuffer);
        objPlaceShader.SetBuffer(0, "Obj1Buffer", objsDrawDatas[1].computeBuffer);
        objPlaceShader.SetBuffer(0, "Obj2Buffer", objsDrawDatas[2].computeBuffer);
        objPlaceShader.SetBuffer(0, "flatTerrainQuadBuffer", flatTerrainQuadBuffer);
    }

    void InitSceneObjsBuffer()
    {
        objsDrawDatas = new List<ObjDrawData>();
        if (sceneObjects == null || sceneObjects.Count < 3)
        {
            Debug.LogError("摆放物体必须为三种");
        }
        int[] argsTemp = new int[5];
        for (int i = 0; i < 3; i++)
        {
            Mesh mesh;
            Material material;
            if (sceneObjects[i] != null && CheckObjectVaild(sceneObjects[i], out mesh, out material))
            {
                ObjDrawData objDrawData = new ObjDrawData();
                objDrawData.mesh = mesh;
                objDrawData.mat = material;
                objDrawData.computeBuffer = new ComputeBuffer(objsMaxDrawPerKind, 5 * sizeof(float), ComputeBufferType.Append);
                objDrawData.indirectArgs = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
                argsTemp[0] = mesh.triangles.Length;
                objDrawData.indirectArgs.SetData(argsTemp);
                objDrawData.mat.SetBuffer("ObjDrawDataBuffer", objDrawData.computeBuffer);
                objsDrawDatas.Add(objDrawData);
            }
        }
    }

    bool CheckObjectVaild(GameObject gameObject, out Mesh mesh, out Material mat)
    {
        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        if (!gameObject.TryGetComponent<MeshRenderer>(out meshRenderer))
        {
            Debug.LogError("摆放物" + gameObject.name + ":没有meshRender");
            mesh = null;
            mat = null;
            return false;
        }

        if (meshRenderer.sharedMaterial == null || meshRenderer.sharedMaterial.shader.name != "GPUTerrain/SceneObjLit")
        {
            Debug.LogError("摆放物" + gameObject.name + ":Material不合法，请检查Material是否为使用名为SceneObjLit的shader");
            mesh = null;
            mat = null;
            return false;
        }

        meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter.sharedMesh == null)
        {
            Debug.LogError("摆放物" + gameObject.name + ":mesh为空");
            mesh = null;
            mat = null;
            return false;
        }

        mesh = meshFilter.sharedMesh;
        mat = meshRenderer.sharedMaterial;
        return true;
    }

    //异步回读平地数据，进行物体摆放
    IEnumerator RequestFlatQuadCount()
    {
        var request = AsyncGPUReadback.Request(flatTerrainQuadArgs);
        yield return new WaitUntil(() => request.done);
        int flatQuadCount = request.GetData<int>().ToArray()[0];
        if (flatQuadCount > 0)
        {
            ObjsPlace(flatQuadCount);
        }
        flatQuadRequestFlag = false;
    }

    void ObjsPlace(int flatQuadCount)
    {
        for (int i = 0; i < 3; i++)
        {
            objsDrawDatas[i].computeBuffer.SetCounterValue(0);
        }
        objPlaceShader.SetFloat("seed", seed);
        objPlaceShader.SetFloat("scale", 1);
        objPlaceShader.Dispatch(0, Mathf.CeilToInt(flatQuadCount / 64f), 1, 1);
    }

    void DrawObjs()
    {
        if (enableObjsDraw)
        {
            for (int i = 0; i < 1; i++)
            {
                ComputeBuffer.CopyCount(objsDrawDatas[i].computeBuffer, objsDrawDatas[i].indirectArgs, 4);
                Graphics.DrawMeshInstancedIndirect(objsDrawDatas[i].mesh, 0, objsDrawDatas[i].mat,
                    drawBounds, objsDrawDatas[i].indirectArgs, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false, 0);
            }
        }
    }

    /// <summary>
    /// 生成地形单元的基础格子
    /// </summary>
    void GenerateTerrainUnit()
    {
        terrainUnitMesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>(6);

        // 1 - 2   3 
        // | /   / |
        // 0   5 - 4
        //添加两个三角形顶点
        vertices.Add(new Vector3(-0.5f, 0, -0.5f));
        vertices.Add(new Vector3(-0.5f, 0, 0.5f));
        vertices.Add(new Vector3(0.5f, 0, 0.5f));

        vertices.Add(new Vector3(0.5f, 0, 0.5f));
        vertices.Add(new Vector3(0.5f, 0, -0.5f));
        vertices.Add(new Vector3(-0.5f, 0, -0.5f));

        //标记一下6个顶点的位置
        List<Vector2> uvs = new List<Vector2>(6);
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(2, 0));

        uvs.Add(new Vector2(3, 0));
        uvs.Add(new Vector2(4, 0));
        uvs.Add(new Vector2(5, 0));

        List<int> triangles = new List<int>(6) { 0, 1, 2, 3, 4, 5 };

        terrainUnitMesh.SetVertices(vertices);
        terrainUnitMesh.SetTriangles(triangles, 0);
        terrainUnitMesh.SetUVs(0, uvs);
    }

    private bool IsVaild()
    {
        return terrainGenerateShader != null
            && activeCamera != null
            && noiseGenerateComputeShader != null
            && terrainMaterial != null
            && terrainRepairShader != null;
    }

    private void OnDestroy()
    {
        Destroy(terrainUnitMesh);

        terrainDrawArgs.Dispose();
        terrainDrawBuffer.Dispose();
        terrainRepairArgs.Dispose();
        allQuadTypeVertexBuffer.Dispose();
        flatTerrainQuadArgs.Dispose();
        flatTerrainQuadBuffer.Dispose();

        foreach (KeyValuePair<int, RenderTexture> pair in noiseRTDic)
        {
            pair.Value.Release();
        }

        for (int i = 0; i < 3; i++)
        {
            objsDrawDatas[i].computeBuffer.Dispose();
            objsDrawDatas[i].indirectArgs.Dispose();
        }
    }
}
