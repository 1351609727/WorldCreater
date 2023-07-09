using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewportHelper
{
    private static ViewportHelper _instance;
    public static ViewportHelper Instance
    {
        get
        {
            if (_instance == null)
                _instance = new ViewportHelper();
            return _instance;
        }
    }

    //视锥与地平线相交的计算相关属性
    private Ray[] rays = new Ray[4];
    private Vector3[] viewPoints = new Vector3[4];
    private Plane raycastPlane = new Plane(Vector3.up, 0);

    public bool ComputeViewportRange(Camera camera, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
    {
        minX = float.MaxValue;
        minZ = float.MaxValue;
        maxX = float.MinValue;
        maxZ = float.MinValue;

        //更新视野与地平线相交的四边形
        rays[0] = camera.ViewportPointToRay(new Vector3(0, 0, 1));
        rays[1] = camera.ViewportPointToRay(new Vector3(1, 0, 1));
        rays[2] = camera.ViewportPointToRay(new Vector3(1, 1, 1));
        rays[3] = camera.ViewportPointToRay(new Vector3(0, 1, 1));

        for (int i = 0; i < 4; i++)
        {
            float enter;
            raycastPlane.Raycast(rays[i], out enter);
            Vector3 point = rays[i].GetPoint(enter);
            viewPoints[i] = point;

            minX = point.x < minX ? point.x : minX;
            minZ = point.z < minZ ? point.z : minZ;
            maxX = point.x > maxX ? point.x : maxX;
            maxZ = point.z > maxZ ? point.z : maxZ;
        }
        return true;
    }
}
