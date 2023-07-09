using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//����һ�¶���ṹ
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

//����ִ��������Ԥ����
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
        //��Ƭ�ĸ�����ֱ���5��״̬ �½�2 �½�1 ���� ����1 ����2 ,���ǻ�׼����̶��ǲ���, ������ 5^3��״̬
        //���ｫ����״̬��Ӧ�Ķ��㹹����ʽԤ�ȹ����������ComputeShader���Կ�����������Ӧ����
        //����Ҫ����������������������ε�6�������λ�÷����Լ�UV����ʹ����ʹ��DrawProcedural���Ƶر�
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
        //�ĸ��������λ��
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

        //�õ��ĸ�����״̬
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

        //�ж϶Խ��ߵķ���Ĭ��0 2Ϊ�Խ���,����1 3����߶ȶ����ڻ���С��0 2����ʱ��ʹ��1 3�Խ���
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

        //���������Buffer
        int startIndex = (k + j * 5 + i * 25) * 6;
        for (int n = 0; n < 6; n++)
        {
            allTypeQuads[startIndex + n] = vertexTemp[n];
        }
    }
}
