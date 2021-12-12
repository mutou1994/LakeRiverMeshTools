using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class LakeMeshGenTool :EditorWindow
{
    string m_UVMat = "Assets/Editor/UVMat.mat";
    string m_DefaultMat = "Assets/Editor/Water.mat";
    

    static LakeMeshGenTool wnd;

    private SerializedObject _serializedObjec;
    private GizmosHelper m_GizmoHelper;

    private GameObject m_LakeObj;
    private Mesh m_Mesh;
    private string m_SavePath;

    private List<GameObject> m_VerticePoints;
    private Dictionary<GameObject, bool> m_VerticePointsMap;
    private List<float> m_ChoosedYvals;

    private GameObject m_CenterObj;
    private Vector3 m_BoundsCenter;
    private GameObject m_BoundsCenterObj;
    private Vector3 m_GravityCenter;
    private GameObject m_GravityCenterObj;

    private bool m_UseRepeatUV = true;
    private float m_UVRepeat = 10;

    private bool m_LockYaxis;
    private bool m_Choosed = false;
    private bool m_PointDraged = false;

    private float m_VerticeScale;
    private bool m_ShowGizmos = true;
    private bool m_CheckUV = false;
    private Color m_MeshGridColor = Color.black;
    private Material m_CurMat;


    [MenuItem("WaterMesh/CreateLakeMeshGenWnd", priority =-110)]
    [MenuItem("GameObject/WaterMesh/EditorLakeMesh", priority = -110)]
    static void CreateWaterMeshGenWnd()
    {
        if(wnd)
        {
            wnd.Close();
            wnd = null;
        }
        wnd = GetWindow<LakeMeshGenTool>("LakeMeshGenWnd");
        wnd.minSize = new Vector2(250, 100);
        wnd.Show();
    }

    private void OnEnable()
    {
        m_SavePath = Application.dataPath + "/Water/Mesh/Lake";
        SceneView.duringSceneGui += SceneGUI;
        _serializedObjec = new SerializedObject(this);
        m_VerticePoints = new List<GameObject>();
        m_VerticePointsMap = new Dictionary<GameObject, bool>();
        m_ChoosedYvals = new List<float>();

        m_PointDraged = false;
        m_LockYaxis = true;
        m_VerticeScale = 1f;
        m_ShowGizmos = true;
        m_CheckUV = false;

        m_UseRepeatUV = true;
        m_UVRepeat = 10;

        if (Selection.activeGameObject)
        {
            m_LakeObj = Selection.activeGameObject;
            InitMesh();
        }
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= SceneGUI;
        Clear();
    }

    void Clear()
    {
        for(int i=0; i < m_VerticePoints.Count; i++)
        {
            DestroyImmediate(m_VerticePoints[i]);
        }
        m_VerticePoints.Clear();
        m_VerticePointsMap.Clear();
        m_ChoosedYvals.Clear();

        if(m_CenterObj)
        {
            if(m_GizmoHelper)
            {
                m_GizmoHelper.OnGizmos -= OnDrawGizmos;
            }
            DestroyImmediate(m_CenterObj);
            m_CenterObj = null;
        }
        if(m_GravityCenterObj)
        {
            DestroyImmediate(m_GravityCenterObj);
            m_GravityCenterObj = null;
        }
        if(m_BoundsCenterObj)
        {
            DestroyImmediate(m_BoundsCenterObj);
        }
        m_CurMat = null;
    }

    void InitMesh()
    {
        if (!m_LakeObj) return;
        Clear();

        MeshFilter meshFilter = m_LakeObj.GetComponent<MeshFilter>();
        if(meshFilter == null)
        {
            meshFilter = m_LakeObj.AddComponent<MeshFilter>();
        }
        m_Mesh = meshFilter.sharedMesh;
        if(m_Mesh == null)
        {
            m_Mesh = new Mesh();
            m_Mesh.name = "newLakeMesh";
            meshFilter.sharedMesh = m_Mesh;
        }
        for(int i=0;i< m_Mesh.vertexCount;i++)
        {
            Vector3 pos = m_Mesh.vertices[i];
            if(i >= m_VerticePoints.Count)
            {
                AddVerticePoint(pos);
            }
            else
            {
                m_VerticePoints[i].transform.localPosition = pos;
            }
        }

        CreateCenterObject();
        DrawBoundsCenter();
        DrawGravityCenter();
    }

    void CreateCenterObject()
    {
        m_CenterObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        m_CenterObj.transform.SetParent(m_LakeObj.transform);
        m_CenterObj.transform.localScale = Vector3.one;
        m_CenterObj.transform.localPosition = Vector3.zero;
        m_CenterObj.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

        m_GizmoHelper = m_CenterObj.AddComponent<GizmosHelper>();
        m_GizmoHelper.OnGizmos += OnDrawGizmos;
    }

    void AddVerticePoint(Vector3 pos)
    {
        int index = m_VerticePoints.Count;
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.name = string.Format("Vertice{0}", index);
        point.transform.localScale = Vector3.one * m_VerticeScale;
        point.transform.SetParent(m_LakeObj.transform);

        point.transform.localPosition = pos;
        point.hideFlags = HideFlags.DontSave;
        m_VerticePoints.Add(point);
        m_VerticePointsMap.Add(point, true);
    }

    void DeleteVerticePoint(GameObject[] points)
    {
        if (points == null || points.Length == 0) return;
        GameObject go = null;
        bool changed = false;
        for(int i=0; i < points.Length; i++)
        {
            go = points[i];
            if(m_VerticePointsMap.ContainsKey(go))
            {
                changed = true;
                m_VerticePoints.Remove(go);
                m_VerticePointsMap.Remove(go);
                DestroyImmediate(go);
            }
        }
        if(changed)
        {
            RebuildMesh();
        }
    }

    void CreateNewLake(Vector3 pos)
    {
        m_LakeObj = new GameObject();
        m_LakeObj.name = "NewLake";
        m_LakeObj.transform.localScale = Vector3.one;
        m_LakeObj.transform.position = pos;

        UpdateMaterial();
        InitMesh();
    }

    void SaveMesh()
    {
        if(m_Mesh == null)
        {
            EditorUtility.DisplayDialog("LakeMesh", "请先编辑一个Mesh", "确定");
            return;
        }
        string path = EditorUtility.SaveFilePanelInProject("SaveMesh", m_Mesh.name, "mesh", "选择一个存储目录", m_SavePath);
        if(!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(m_Mesh, path);
            AssetDatabase.Refresh();
        }
    }

    void UpdateMaterial()
    {
        if (m_LakeObj == null) return;
        var renderer = m_LakeObj.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = m_LakeObj.AddComponent<MeshRenderer>();
        if (m_CheckUV)
        {
            m_CurMat = renderer.sharedMaterial;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(m_UVMat);
        }
        else
        {
            if(m_CurMat != null)
            {
                renderer.sharedMaterial = m_CurMat;
            }
            else
            {
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(m_DefaultMat);
            }
        }
    }

    void UpdateVerticeScale()
    {
        foreach(var go in m_VerticePoints)
        {
            go.transform.localScale = Vector3.one * m_VerticeScale;
        }
    }

    private void OnGUI()
    {
        _serializedObjec.Update();
        EditorGUILayout.LabelField("shift+右键 添加顶点");
        EditorGUILayout.LabelField("shift+D 删除顶点");
        EditorGUILayout.LabelField("绿色小球表示UV左下角，绿色Cube表示UV右上角");
        EditorGUILayout.ObjectField("LakeMesh", m_LakeObj, typeof(GameObject), true);
        EditorGUILayout.ObjectField("Mesh", m_Mesh, typeof(Mesh), true);
        if(m_Mesh != null)
        {
            string name = EditorGUILayout.TextField("MeshName", m_Mesh.name);
            if(!m_Mesh.name.Equals(name))
            {
                m_Mesh.name = name;
            }
        }

        m_LockYaxis = EditorGUILayout.Toggle("LockYaxis", m_LockYaxis);

        EditorGUILayout.BeginHorizontal();
        m_ShowGizmos = EditorGUILayout.Toggle("ShowGizmos", m_ShowGizmos);
        bool flag = EditorGUILayout.Toggle("CheckUV", m_CheckUV);
        if (m_CheckUV != flag)
        {
            m_CheckUV = flag;
            UpdateMaterial();
        }
        EditorGUILayout.EndHorizontal();
        m_MeshGridColor = EditorGUILayout.ColorField("MeshGridColor", m_MeshGridColor);

        EditorGUILayout.BeginHorizontal();
        flag = EditorGUILayout.Toggle("UseRepeatUV", m_UseRepeatUV);
        if(m_UseRepeatUV != flag)
        {
            m_UseRepeatUV = flag;
            RebuildMesh();
        }
        if(m_UseRepeatUV)
        {
            float repeatUV = EditorGUILayout.FloatField("UVRepeat", m_UVRepeat);
            if(!Mathf.Approximately(repeatUV, m_UVRepeat))
            {
                m_UVRepeat = repeatUV;
                RebuildMesh();
            }
        }
        EditorGUILayout.EndHorizontal();

        float scale = EditorGUILayout.Slider("VerticeScale", m_VerticeScale, 0.1f, 20f);
        if(!Mathf.Approximately(scale, m_VerticeScale))
        {
            m_VerticeScale = scale;
            UpdateVerticeScale();
        }

        if(GUILayout.Button("RebuildMesh"))
        {
            RebuildMesh();
        }
        if(GUILayout.Button("ChooseAndEditor"))
        {
            var go = Selection.activeGameObject;
            if(go && !m_VerticePointsMap.ContainsKey(go))
            {
                m_LakeObj = go;
                m_Mesh = null;
                InitMesh();
            }
        }
        if(GUILayout.Button("ChooseAllPoints"))
        {
            Selection.objects = m_VerticePoints.ToArray();
        }
        if(GUILayout.Button("NewLake"))
        {
            Clear();
            m_LakeObj = null;
            m_Mesh = null;
        }
        if (GUILayout.Button("DeleteVertice"))
        {
            DeleteVerticePoint(Selection.gameObjects);
        }
        if (GUILayout.Button("SetToBoundsCenter"))
        {
            if(m_Mesh != null)
            {
                SetMeshToCenter(m_Mesh.bounds.center);
            }
        }
        if(GUILayout.Button("SaveLake"))
        {
            SaveMesh();
        }
        _serializedObjec.ApplyModifiedProperties();
    }

    private void SceneGUI(SceneView sceneView)
    {
        var e = Event.current;
        if(e.type == EventType.MouseUp && e.button == 1)
        {
            if(e.shift)
            {
                Vector3 mousePos = Event.current.mousePosition;
                mousePos.y = Screen.height - mousePos.y;
                if(m_LakeObj == null)
                {
                    mousePos.z = 10;
                    CreateNewLake(sceneView.camera.ScreenToWorldPoint(mousePos));
                    return;
                }
                Vector3 pos = sceneView.camera.WorldToScreenPoint(m_LakeObj.transform.position);
                mousePos.z = pos.z;
                Vector3 worldmouse = sceneView.camera.ScreenToWorldPoint(mousePos);
                Vector3 localPos = m_LakeObj.transform.InverseTransformPoint(worldmouse);
                localPos.y = 0;
                AddVerticePoint(localPos);
                RebuildMesh();
            }
        }
        else
        {
            var gos = Selection.gameObjects;
            if(e.type == EventType.MouseDown && e.button == 0)
            {
                m_Choosed = false;
                m_ChoosedYvals.Clear();
                for(int i = 0; i < gos.Length; i++)
                {
                    if(m_VerticePointsMap.ContainsKey(gos[i]))
                    {
                        m_Choosed = true;
                    }
                    m_ChoosedYvals.Add(gos[i].transform.localPosition.y);
                }
            }
            else if(e.type == EventType.MouseDrag && e.button == 0)
            {
                bool haveChanged = false;
                for(int i = 0; i < gos.Length; i++)
                {
                    if(m_VerticePointsMap.ContainsKey(gos[i]))
                    {
                        haveChanged = true;
                        if(m_LockYaxis && m_Choosed)
                        {
                            Vector3 pos = gos[i].transform.localPosition;
                            pos.y = m_ChoosedYvals[i];
                            gos[i].transform.localPosition = pos;
                        }
                    }
                }
                if(haveChanged)
                {
                    RebuildMesh();
                    m_PointDraged = true;
                    sceneView.Repaint();
                }
            }
            else if((e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow) && e.button == 0)
            {
                bool haveChanged = false;
                if(m_PointDraged)
                {
                    for(int i=0; i < gos.Length; i++)
                    {
                        if(m_VerticePointsMap.ContainsKey(gos[i]))
                        {
                            haveChanged = true;
                            if(m_LockYaxis && m_Choosed)
                            {
                                Vector3 pos = gos[i].transform.localPosition;
                                pos.y = m_ChoosedYvals[i];
                                gos[i].transform.localPosition = pos;
                            }
                        }
                    }
                    if(haveChanged)
                    {
                        RebuildMesh();
                        m_PointDraged = false;
                        sceneView.Repaint();
                    }
                }
                m_Choosed = false;
            }
            else if(e.shift && e.keyCode == KeyCode.D)
            {
                DeleteVerticePoint(Selection.gameObjects);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (m_LakeObj == null || m_Mesh == null) return;
        if (!m_ShowGizmos) return;
        Transform trans = m_LakeObj.transform;
        Gizmos.color = m_MeshGridColor;
        Vector3[] vertices = m_Mesh.vertices;
        int[] triangles = m_Mesh.triangles;
        Vector3 a, b, c;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (vertices.Length > triangles[i] && vertices.Length > triangles[i + 1] && vertices.Length > triangles[i + 2])
            {
                a = trans.TransformPoint(vertices[triangles[i]]);
                b = trans.TransformPoint(vertices[triangles[i + 1]]);
                c = trans.TransformPoint(vertices[triangles[i + 2]]);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(a, c);
                Gizmos.DrawLine(b, c);
            }
        }
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(trans.TransformPoint(m_Mesh.bounds.min), 1);
        Gizmos.DrawCube(trans.TransformPoint(m_Mesh.bounds.max), Vector3.one);
    }

    void DrawBoundsCenter()
    {
        int count = m_VerticePoints.Count;
        //计算Bounds中心
        Vector3 min = count > 0 ? m_VerticePoints[0].transform.localPosition : Vector3.one;
        Vector3 max = min;
        Vector3 pos;
        for (int i = 0; i < count; i++)
        {
            pos = m_VerticePoints[i].transform.localPosition;
            if (min.x > pos.x) min.x = pos.x;
            if (min.y > pos.y) min.y = pos.y;
            if (min.z > pos.z) min.z = pos.z;
            if (max.x < pos.x) max.x = pos.x;
            if (max.y < pos.y) max.y = pos.y;
            if (max.z < pos.z) max.z = pos.z;
        }
        m_BoundsCenter = (min + max) / 2;
        if(m_BoundsCenterObj == null)
        {
            m_BoundsCenterObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_BoundsCenterObj.transform.SetParent(m_LakeObj.transform);
            m_BoundsCenterObj.transform.localScale = Vector3.one;
            m_BoundsCenterObj.hideFlags = HideFlags.HideAndDontSave;
        }
        m_BoundsCenterObj.transform.localPosition = m_BoundsCenter;
    }

    void DrawGravityCenter()
    {
        //计算重心
        int count = m_VerticePoints.Count;
        m_GravityCenter = Vector3.zero;
        for(int i = 0; i < count; i++)
        {
            m_GravityCenter += m_VerticePoints[i].transform.localPosition;
        }
        if(count > 0)
        {
            m_GravityCenter /= count;
        }
        if(m_GravityCenterObj == null)
        {
            m_GravityCenterObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            m_GravityCenterObj.transform.SetParent(m_LakeObj.transform);
            m_GravityCenterObj.transform.localScale = Vector3.one;
            m_GravityCenterObj.hideFlags = HideFlags.HideAndDontSave;
        }
        m_GravityCenterObj.transform.localPosition = m_GravityCenter;
    }

    void SetMeshToCenter(Vector3 center)
    {
        if (m_LakeObj == null || m_Mesh == null) return;
        Vector3 offset = -center;
        if(Vector3.zero != offset)
        {
            foreach(GameObject point in m_VerticePoints)
            {
                var pos = point.transform.localPosition;
                pos += offset;
                point.transform.localPosition = pos;
            }
            RebuildMesh();
        }
    }

    void RebuildMesh()
    {
        if (m_LakeObj == null || m_Mesh == null) return;
        m_Mesh.triangles = null;
        m_Mesh.uv = null;
        m_Mesh.vertices = null;

        DrawBoundsCenter();
        DrawGravityCenter();
        int count = m_VerticePoints.Count;
        if (count < 3) return;

        SortPolyPoints(m_VerticePoints, m_GravityCenter);
        var array = m_VerticePoints.Select(c => c.transform.localPosition);
        Vector3[] vertices = array.ToArray();
        List<int> indexs = new List<int>(count);
        for(int i=0; i < count; i++)
        {
            indexs.Add(i);
        }
        List<int> triangles = TriangulationTool.WidelyTriangleIndex(new List<Vector3>(vertices), indexs);
        m_Mesh.vertices = vertices;
        m_Mesh.triangles = triangles.ToArray();

        m_Mesh.RecalculateBounds();
        Vector2[] uvs = new Vector2[count];
        Vector2[] uv2s = new Vector2[count];
        //Vector3 min = m_Mesh.bounds.min;
        Vector3 size = m_Mesh.bounds.size;
        Vector3 center = m_Mesh.bounds.center;
        float _uvRela = size.x > size.z ? size.x : size.z;
        for(int i=0; i < count; i++)
        {
            //uvs[i] = new Vector2((vertices[i].x - min.x) / size.x, (vertices[i].z - min.z) / size.z);
            uvs[i] = new Vector2(0.5f + (vertices[i].x - center.x) / m_UVRepeat, 0.5f + (vertices[i].z - center.z) / m_UVRepeat);
            uv2s[i] = new Vector2(0.5f + (vertices[i].x - center.x) / _uvRela, 0.5f + (vertices[i].z - center.z) / _uvRela);
        }
        if(m_UseRepeatUV)
        {
            m_Mesh.uv = uvs;
            m_Mesh.uv2 = uv2s;
        }
        else
        {
            m_Mesh.uv = uv2s;
            m_Mesh.uv2 = uvs;
        }
        m_Mesh.RecalculateNormals();
        m_Mesh.RecalculateTangents();
    }

    /// <summary>
    /// 多边形点集排序
    /// </summary>
    /// <param name="vPoints"></param>
    /// <returns></returns>
    public List<GameObject> SortPolyPoints(List<GameObject> Points, Vector3 center)
    {
        Points.Sort((left, right) =>
        {
            if(PointCmp(left.transform.localPosition, right.transform.localPosition, center))
            {
                return -1;
            }
            else
            {
                return 1;
            }
        });
        for(int i = 0; i < Points.Count; i++)
        {
            Points[i].transform.SetAsLastSibling();
        }
        return Points;
    }

    /// <summary>
    /// 若点a大于点b,即点a在点b顺时针方向,返回true,否则返回false
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="center"></param>
    /// <returns></returns>
    private bool PointCmp(Vector3 a, Vector3 b, Vector3 o)
    {
        Vector3 oa = a - o;
        Vector3 ob = b - o;
        float angleA = Mathf.Atan2(oa.z, oa.x) * Mathf.Rad2Deg;
        float angleB = Mathf.Atan2(ob.z, ob.x) * Mathf.Rad2Deg;
        if (angleA < 0) angleA = angleA + 360;
        if (angleB < 0) angleB = angleB + 360;
        if(Mathf.Approximately(angleA, angleB))
        {
            if(Mathf.Approximately(oa.x, 0) && Mathf.Approximately(oa.z, 0))
            {
                return false;
            }
            if(Mathf.Approximately(ob.x, 0) && Mathf.Approximately(ob.z, 0))
            {
                return true;
            }
            if (oa.x > 0 && oa.z >= 0)
            {
                return oa.sqrMagnitude > ob.sqrMagnitude;
            }
            else if (oa.x <= 0 && oa.z > 0)
            {
                return oa.sqrMagnitude > ob.sqrMagnitude;
            }
            else if(oa.x < 0 && oa.z <= 0)
            {
                return oa.sqrMagnitude > ob.sqrMagnitude;
            }
            else //if(oa.x >= 0 && oa.z < 0)
            {
                return oa.sqrMagnitude < ob.sqrMagnitude;
            }
        }else
        {
            return angleA > angleB;
        }
    }

    //判断点P是否在三角新ABC内
    bool PointTriangle(Vector3 A, Vector3 B, Vector3 C, Vector3 P)
    {
        Vector3 v0 = C - A;
        Vector3 v1 = B - A;
        Vector3 v2 = P - A;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float inverDeno = 1 / (dot00 * dot11 - dot01 * dot01);

        float u = (dot11 * dot02 - dot01 * dot12) * inverDeno;
        if(u < 0 || u > 1) //if u out of range, return directly
        {
            return false;
        }

        float v = (dot00 * dot12 - dot01 * dot02) * inverDeno;
        if(v < 0 || v > 1) // if v out of range, return directly
        {
            return false;
        }

        return u + v <= 1;
    }
}
