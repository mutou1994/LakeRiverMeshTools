using System.Collections.Generic;
using UnityEngine;

public static class TriangulationTool
{
    const double epsilon = 1e-7;

    static bool floatLess(float value, float other)
    {
        return (other - value) > epsilon;
    }

    static bool floatGreat(float value, float other)
    {
        return (value - other) > epsilon;
    }

    static bool floatEqual(float value, float other)
    {
        return Mathf.Abs(value - other) < epsilon;
    }

    static bool Vector3Equal(Vector3 a, Vector3 b)
    {
        return floatEqual(a.x, b.x) && floatEqual(a.y, b.y) && floatEqual(a.z, b.z);
    }

    public static List<int> ConvexTriangleIndex(List<Vector3> verts, List<int> indexs)
    {
        int len = verts.Count;
        //若是闭环去除最后一点
        if(len > 1 && Vector3Equal(verts[0], verts[len-1]))
        {
            len--;
        }
        int triangleNum = len - 2;
        List<int> triangles = new List<int>(triangleNum * 3);
        for(int i=0; i < len - 1; i++)
        {
            if(i != len - i - 1 && i+1 != len - i - 1)
            {
                triangles.Add(indexs[i]);
                triangles.Add(indexs[i + 1]);
                triangles.Add(indexs[len - i - 1]);
            }
        }
        return triangles;
    }

    /// <summary>
    /// 三角剖分
    /// 1.寻找一个可划分顶点
    /// 2.分割出新的多边形和三角形
    /// 3.新多边形若为凸多边形，结束；否则继续剖分
    /// 
    /// 寻找可划分点
    /// 1.顶点是否为凸顶点：顶点在剩余顶点组成的图形外
    /// 2.新的多边形没有顶点在分割的三角形内
    /// </summary>
    /// <param name="verts">顺时针排列的顶点列表</param>
    /// <param name="indexs">顶点索引列表</param>
    /// <returns>三角形列表</returns>
    public static List<int> WidelyTriangleIndex(List<Vector3> verts, List<int> indexs)
    {
        int len = verts.Count;
        if (len <= 3) return ConvexTriangleIndex(verts, indexs);

        int searchIndex = 0;
        List<int> convexIndex = new List<int>();
        bool isConvexPolygon = true; //判断多边形是否凸多边形

        for (searchIndex = 0; searchIndex < len; searchIndex++)
        {
            List<Vector3> polygon = new List<Vector3>(verts.ToArray());
            polygon.RemoveAt(searchIndex);
            if(IsPointInsidePolygon(verts[searchIndex], polygon))
            {
                isConvexPolygon = false;
                break;
            }
            else
            {
                convexIndex.Add(searchIndex);
            }
        }

        if (isConvexPolygon) return ConvexTriangleIndex(verts, indexs);

        //查找可划分顶点
        int canFragementIndex = -1;//可划分顶点索引
        for(int i = 0; i < len; i++)
        {
            if(i > searchIndex)
            {
                List<Vector3> polygon = new List<Vector3>(verts.ToArray());
                polygon.RemoveAt(i);
                if(!IsPointInsidePolygon(verts[i], polygon) && IsFragementIndex(i, verts))
                {
                    canFragementIndex = i;
                    break;
                }
            }
            else
            {
                if(convexIndex.IndexOf(i) != -1 && IsFragementIndex(i, verts))
                {
                    canFragementIndex = i;
                    break;
                }
            }
        }

        if(canFragementIndex < 0)
        {
            Debug.LogError("数据有误找不到可划分顶点");
            return new List<int>();
        }

        //用可划分顶点将凹多边形划分为一个三角形和一个多边形
        List<int> tTriangles = new List<int>();
        int next = (canFragementIndex == len - 1) ? 0 : canFragementIndex + 1;
        int prev = (canFragementIndex == 0) ? len - 1 : canFragementIndex - 1;
        tTriangles.Add(indexs[prev]);
        tTriangles.Add(indexs[canFragementIndex]);
        tTriangles.Add(indexs[next]);
        //剔除可划分顶点及索引
        verts.RemoveAt(canFragementIndex);
        indexs.RemoveAt(canFragementIndex);

        //递归划分
        List<int> leaveTriangles = WidelyTriangleIndex(verts, indexs);
        tTriangles.AddRange(leaveTriangles);

        return tTriangles;
    }

    /// <summary>
    /// 是否是可划分顶点：新的多边形没有顶点在分割的三角形内
    /// </summary>
    /// <param name="index"></param>
    /// <param name="verts"></param>
    /// <returns></returns>
    private static bool IsFragementIndex(int index, List<Vector3> verts)
    {
        int len = verts.Count;
        List<Vector3> triangleVert = new List<Vector3>();
        int next = (index == len - 1) ? 0 : index + 1;
        int prev = (index == 0) ? len - 1 : index - 1;
        triangleVert.Add(verts[prev]);
        triangleVert.Add(verts[index]);
        triangleVert.Add(verts[next]);
        for(int i=0; i < len; i++)
        {
            if(i != index && i != prev && i != next)
            {
                if (IsPointInsidePolygon(verts[i], triangleVert))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// 射线与线段相交性判断
    /// </summary>
    /// <param name="ray"></param>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <returns></returns>
    private static bool IsDetectIntersect(Ray ray, Vector3 p1, Vector3 p2)
    {
        float pointZ;//交点Z坐标，x固定值
        if(floatEqual(p1.x, p2.x))
        {
            return false;
        }
        else if(floatEqual(p1.z, p2.z))
        {
            pointZ = p1.z;
        }
        else
        {
            //直线两点方程式：(y-y2)/(y1-y2) = (x-x2)/(x1-x2)
            float a = p1.x - p2.x;
            float b = p1.x - p2.z;
            float c = p2.z / b - p2.x / a;

            pointZ = b / a * ray.origin.x + b * c;
        }

        if (floatLess(pointZ, ray.origin.z))
        {
            //交点z小于射线起点z
            return false;
        }
        else
        {
            Vector3 leftP = floatLess(p1.x, p2.x) ? p1 : p2;//左端点
            Vector3 rightP = floatLess(p1.x, p2.x) ? p2 : p1;//右端点
            //交点x位于线段两个端点x之外，相较与线段某个端点时，仅将射线L与左侧多边形一边的端点记为焦点(即就是：只将右端点记为交点)
            if (!floatGreat(ray.origin.x, leftP.x) || floatGreat(ray.origin.x, rightP.x))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPointInsidePolygon(Vector3 point, List<Vector3> polygonVerts)
    {
        int len = polygonVerts.Count;
        Ray ray = new Ray(point, new Vector3(0, 0, 1));//Z方向射线
        int interNum = 0;

        for (int i = 1; i < len; i++)
        {
            if(IsDetectIntersect(ray, polygonVerts[i - 1], polygonVerts[i]))
            {
                interNum++;
            }
        }

        //不是闭环
        if(!Vector3Equal(polygonVerts[0], polygonVerts[len - 1]))
        {
            if(IsDetectIntersect(ray, polygonVerts[len - 1], polygonVerts[0]))
            {
                interNum++;
            }
        }
        int remainder = interNum % 2;
        return remainder == 1;
    }
}
