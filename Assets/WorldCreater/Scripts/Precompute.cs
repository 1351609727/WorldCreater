using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//定义一下顶点结构
public struct QuadVertex
{
    public Vector3 position;
    public Vector3 normal;
    public Vector2 uv;
    public Vector4 color;

    public QuadVertex(Vector3 position)
    {
        this.position = position;
        this.normal = Vector3.up;
        this.uv = Vector2.zero;
        this.color = Vector4.one;
    }

    public QuadVertex(QuadVertex quadVertex)
    {
        this.position = quadVertex.position;
        this.normal = quadVertex.normal;
        this.uv = quadVertex.uv;
        this.color = quadVertex.color;
    }
}

//该类执行所有与预计算
public class PreCompute
{
    private static PreCompute _instance;
    public static PreCompute Instance
    {
        get
        {
            if (_instance == null)
                _instance = new PreCompute();
            return _instance;
        }
    }

    public QuadVertex[] allTypeQuads;
    QuadVertex[] vertexTemp = new QuadVertex[6];
    QuadVertex[] vertexBase= new QuadVertex[4];
    int[] vertexStateTemp = new int[4];

    PreCompute()
    {
        //面片四个顶点分别有5种状态 下降2 下降1 不变 上升1 上升2 ,但是基准顶点固定是不变, 所以有 5^3种状态
        //这里将所有状态对应的顶点构建方式预先构造出来，是ComputeShader可以快速索引到相应网格
        //这里要构建的网格包括两个三角形的6个顶点的位置法线以及UV，致使可以使用DrawProcedural绘制地表
        allTypeQuads = new QuadVertex[5 * 5 * 5 * 6];
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                for (int k = 0; k < 5; k++)
                {
                    BuildQuad(i, j, k);
                }
            }
        }
    }

    void BuildQuad(int i, int j, int k)
    {
        //四个顶点基础位置
        // 3  2
        // 0  1
        vertexBase[0] = new QuadVertex(Vector3.zero);
        vertexBase[0].uv = new Vector2(0, 0);
        vertexBase[1] = new QuadVertex(new Vector3(1, 0, 0));
        vertexBase[1].uv = new Vector2(1, 0);
        vertexBase[2] = new QuadVertex(new Vector3(1, 0, 1));
        vertexBase[2].uv = new Vector2(1, 1);
        vertexBase[3] = new QuadVertex(new Vector3(0, 0, 1));
        vertexBase[3].uv = new Vector2(0, 1);

        //拿到四个顶点状态
        // 3  2
        // 0  1
        vertexStateTemp[0] = 0;
        vertexStateTemp[1] = k - 2;
        vertexStateTemp[2] = j - 2;
        vertexStateTemp[3] = i - 2;

        for (int n = 0; n < 4; n++)
        {
            vertexBase[n].position.Set(vertexBase[n].position.x,
                vertexBase[n].position.y + vertexStateTemp[n],
                vertexBase[n].position.z);
        }

        //判断对角线的方向默认0 2为对角线,但是1 3顶点高度都大于或者小于0 2顶点时则使用1 3对角线
        if (Mathf.Min(vertexBase[1].position.y, vertexBase[3].position.y) >= Mathf.Max(vertexBase[0].position.y, vertexBase[2].position.y)
            || Mathf.Max(vertexBase[1].position.y, vertexBase[3].position.y) < Mathf.Min(vertexBase[0].position.y, vertexBase[2].position.y))
        {
            vertexTemp[0] = new QuadVertex(vertexBase[0]);
            vertexTemp[1] = new QuadVertex(vertexBase[3]);
            vertexTemp[2] = new QuadVertex(vertexBase[1]);

            vertexTemp[0].normal = vertexTemp[1].normal = vertexTemp[2].normal =
                Vector3.Cross(vertexBase[3].position - vertexBase[0].position,
                vertexBase[1].position - vertexBase[0].position).normalized;

            vertexTemp[3] = new QuadVertex(vertexBase[1]);
            vertexTemp[4] = new QuadVertex(vertexBase[3]);
            vertexTemp[5] = new QuadVertex(vertexBase[2]);

            vertexTemp[3].normal = vertexTemp[4].normal = vertexTemp[5].normal =
                Vector3.Cross(vertexBase[1].position - vertexBase[2].position,
                vertexBase[3].position - vertexBase[2].position
                ).normalized;
        }
        else
        {
            vertexTemp[0] = new QuadVertex(vertexBase[0]);
            vertexTemp[1] = new QuadVertex(vertexBase[3]);
            vertexTemp[2] = new QuadVertex(vertexBase[2]);

            vertexTemp[0].normal = vertexTemp[1].normal = vertexTemp[2].normal =
                Vector3.Cross(vertexBase[2].position - vertexBase[3].position,
                vertexBase[0].position - vertexBase[3].position
                ).normalized;

            vertexTemp[3] = new QuadVertex(vertexBase[0]);
            vertexTemp[4] = new QuadVertex(vertexBase[2]);
            vertexTemp[5] = new QuadVertex(vertexBase[1]);

            vertexTemp[3].normal = vertexTemp[4].normal = vertexTemp[5].normal =
                Vector3.Cross(vertexBase[0].position - vertexBase[1].position,
                vertexBase[2].position - vertexBase[1].position
                ).normalized;
        }

        //将顶点加入Buffer
        int startIndex = (k + j * 5 + i * 25) * 6;
        for (int n = 0; n < 6; n++)
        {
            allTypeQuads[startIndex + n] = vertexTemp[n];
        }
    }
}
