using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Linq;

public class RiverCfgJson
{
    public bool horizontal = true;
    public bool useUVRepeat = false;
    public float uvRepeat = 10;
    public int perGridNum = 1;
    public float smoothNess = 0.01f;
    public int smoothAmount = 10;
    public Vector3[] wayPoints;
    public float[] wayPointsWidth;
}

public class RiverMeshGenTool : EditorWindow
{
    string m_TempCfgPath = "Assets/Editor/Config/tempRiver.json";
    string m_CfgPath = "Assets/Editor/Config/River/";
    string m_UVMat = "Assets/Editor/UVMat.mat";
    string m_DefaultMat = "Assets/Editor/Water.mat";
    Material m_CurMat;

    static RiverMeshGenTool wnd;
    private SerializedObject _serializedObjct;

    private GizmosHelper m_GizmosHelper;
    private GameObject m_RiverObj;
    private Mesh m_Mesh;
    private string m_SavePath;

    private List<GameObject> m_WayPoints;
    private Dictionary<GameObject, bool> m_WayPointsMap;
    private List<float> m_WayPointsWidth;
    private List<float> m_SmoothWayWidth;
    private List<Vector3> m_SmoothWayPoints;
    private List<Vector3> m_SmoothWayDir;
    private List<Vector3> m_Vertices;
    private List<int> m_Triangles;
    private List<Vector2> m_UVs;
    private List<Vector2> m_UV2s;

    private bool m_LockYaxis = true;
    private bool m_Choosed = false;
    private List<float> m_ChoosedYVals;

    private Color m_MeshGridColor = Color.black;
    private bool m_ShowGizmos = true;
    private bool m_CheckUV = false;
    private float m_WayPointScale = 1;
    private int m_PerGridNum = 4;
    private float m_SmoothNess = 0.01f;
    private int m_SmoothAmount = 5;
    private float m_DefaultWidth = 10;

    private bool m_UseRepeatUV = true;
    private float m_UVRepeat = 10;
    private bool m_Horizontal = true;
    private bool m_Vertical = false;

    private bool m_PointDraged = false;
    private bool m_TempDirty = false;

    private float m_RiverLength = 0;
    private float m_RiverMaxWidth = 0;

    private GameObject m_CenterObj;

    [MenuItem("WaterMesh/CreateRiverMeshGenWnd", priority = -110)]
    [MenuItem("GameObject/WaterMesh/EditorRiverMesh", priority = -110)]
    static void CreateRiverMeshGenWnd()
    {
        if(wnd)
        {
            wnd.Close();
            wnd = null;
        }
        wnd = GetWindow<RiverMeshGenTool>("RiverMeshGenWnd");
        wnd.minSize = new Vector2(250, 100);
        wnd.Show();
    }

    private void OnEnable()
    {
        m_SavePath = Application.dataPath + "/Water/Mesh/River";
        SceneView.duringSceneGui += OnSceneGUI;

        _serializedObjct = new SerializedObject(this);
        m_WayPoints = new List<GameObject>();
        m_WayPointsMap = new Dictionary<GameObject, bool>();
        m_WayPointsWidth = new List<float>();
        m_SmoothWayWidth = new List<float>();
        m_SmoothWayPoints = new List<Vector3>();
        m_SmoothWayDir = new List<Vector3>();
        m_Vertices = new List<Vector3>();
        m_Triangles = new List<int>();
        m_UVs = new List<Vector2>();
        m_UV2s = new List<Vector2>();
        m_ChoosedYVals = new List<float>();

        m_LockYaxis = true;
        m_ShowGizmos = true;
        m_MeshGridColor = Color.black;
        m_CheckUV = false;
        m_WayPointScale = 1;
        m_SmoothNess = 0.01f;
        m_SmoothAmount = 5;
        m_DefaultWidth = 10;
        m_UseRepeatUV = true;
        m_UVRepeat = 10;
        m_PerGridNum = 1;

        if(Selection.activeGameObject)
        {
            m_RiverObj = Selection.activeGameObject;
            InitMesh();
        }
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Clear();
    }

    void Clear()
    {
        SaveTempJson();
        m_SmoothWayPoints.Clear();
        m_SmoothWayDir.Clear();
        for(int i=0; i < m_WayPoints.Count; i++)
        {
            DestroyImmediate(m_WayPoints[i]);
        }
        m_WayPoints.Clear();
        m_WayPointsWidth.Clear();
        m_ChoosedYVals.Clear();
        if (m_CenterObj)
        {
            if(m_GizmosHelper)
            {
                m_GizmosHelper.OnGizmos -= OnDrawGizmos;
            }
            DestroyImmediate(m_CenterObj);
        }
        m_CurMat = null;
    }

    void InitMesh()
    {
        if (!m_RiverObj) return;
        Clear();
                                                         
        MeshFilter meshFilter = m_RiverObj.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = m_RiverObj.AddComponent<MeshFilter>();
        }
        m_Mesh = meshFilter.sharedMesh;
        if(m_Mesh == null)
        {
            m_Mesh = new Mesh();
            m_Mesh.name = "newRiverMesh";
            meshFilter.sharedMesh = m_Mesh;
        }
        CreateCenterObject();
    }

    void CreateCenterObject()
    {
        m_CenterObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        m_CenterObj.transform.SetParent(m_RiverObj.transform);
        m_CenterObj.transform.localScale = Vector3.one;
        m_CenterObj.transform.localPosition = Vector3.zero;
        m_CenterObj.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        m_GizmosHelper = m_CenterObj.AddComponent<GizmosHelper>();
        m_GizmosHelper.OnGizmos += OnDrawGizmos;
    }

    void UpdateWayPointScale()
    {
        foreach(var go in m_WayPoints)
        {
            go.transform.localScale = Vector3.one * m_WayPointScale;
        }
    }

    GameObject NewWayPoint(Vector3 pos)
    {
        int index = m_WayPoints.Count;
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.name = "WayPoint";
        point.transform.localScale = Vector3.one * m_WayPointScale;
        point.transform.SetParent(m_RiverObj.transform);
        point.transform.localPosition = pos;
        point.hideFlags = HideFlags.DontSave;
        return point;
    }

    void DeleteWayPoint(GameObject[] points)
    {
        if (points == null || points.Length == 0) return;
        GameObject go = null;
        bool changed = false;
        for(int i=0; i < points.Length; i++)
        {
            go = points[i];
            if(m_WayPointsMap.ContainsKey(go))
            {
                changed = true;
                int index = m_WayPoints.FindIndex(c => c == go);
                if(index >= 0)
                {
                    m_WayPoints.RemoveAt(index);
                    m_WayPointsWidth.RemoveAt(index);
                    m_WayPointsMap.Remove(go);
                }
                DestroyImmediate(go);
            }
        }
        if(changed)
        {
            RebuildRiverMesh();
            SaveTempJson();
        }
    }

    void CreateNewRiver(Vector3 pos)
    {
        m_RiverObj = new GameObject();
        m_RiverObj.name = "NewRiver";
        m_RiverObj.transform.localScale = Vector3.one;
        m_RiverObj.transform.position = pos;

        UpdateMaterial();
        InitMesh();
    }

    void SaveMesh()
    {
        if (m_Mesh == null)
        {
            EditorUtility.DisplayDialog("RiverMeshTips", "请先编辑一个Mesh", "确定");
            return;
        }
        string path = EditorUtility.SaveFilePanelInProject("SaveMesh", m_Mesh.name, "mesh", "请选择一个存储目录", m_SavePath);
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(m_Mesh, path);
            AssetDatabase.Refresh();
        }
    }

    void UpdateMaterial()
    {
        if (m_RiverObj == null) return;
        var renderer = m_RiverObj.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = m_RiverObj.AddComponent<MeshRenderer>();
        if(m_CheckUV)
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
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(m_DefaultMat);
        }
    }

    void LoadFromJson()
    {
        /*var objs = Selection.objects;
        if(objs == null || objs.Length != 1 || !(objs[0] is TextAsset))
        {
            EditorUtility.DisplayDialog("RiverMeshTips", string.Format("请选择一个RiverJson配置文件(目录：【{0}】)", m_CfgPath), "确定");
            return;
        }*/
        string path = EditorUtility.OpenFilePanelWithFilters("SelectRiverConfig", m_CfgPath, new string[] { "Json", "json" });
        if (string.IsNullOrEmpty(path)) return;
        if(!path.EndsWith(".json"))
        {
            EditorUtility.DisplayDialog("SelectRiverConfig", string.Format("请选择有效的RiverJson配置文件（目录：【{0}】）", m_CfgPath), "确定");
            return;
        }
        int index = path.IndexOf(Application.dataPath);
        if(index < 0)
        {
            EditorUtility.DisplayDialog("SelectRiverConfig", string.Format("请选择有效的RiverJson配置文件（目录【{0}】）", m_CfgPath), "确定");
            return;
        }
        path = "Assets" + path.Substring(index + Application.dataPath.Length);
        var txt = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if(txt == null)
        {
            EditorUtility.DisplayDialog("SelectRiverConfig", string.Format("请选择有效的RiverJson配置文件（目录【{0}】）", m_CfgPath), "确定");
            return;
        }
        LoadFromJson(txt);
    }   

    void LoadFromJson(TextAsset txt)
    {
        var cfgJson = JsonUtility.FromJson<RiverCfgJson>(txt.text);
        if(cfgJson != null && cfgJson.wayPoints != null && cfgJson.wayPoints.Length > 0)
        {
            m_Horizontal = cfgJson.horizontal;
            m_Vertical = !m_Horizontal;
            m_UseRepeatUV = cfgJson.useUVRepeat;
            m_UVRepeat = cfgJson.uvRepeat;
            m_PerGridNum = cfgJson.perGridNum;
            m_SmoothNess = cfgJson.smoothNess;
            m_SmoothAmount = cfgJson.smoothAmount;
            var wayPoints = cfgJson.wayPoints;
            var wayPointsWidth = cfgJson.wayPointsWidth;
            for(int i=0; i<wayPoints.Length; i++)
            {
                if(i < m_WayPoints.Count)
                {
                    m_WayPoints[i].transform.localPosition = wayPoints[i];
                }
                else
                {
                    var point = NewWayPoint(wayPoints[i]);
                    m_WayPoints.Add(point);
                    m_WayPointsMap.Add(point, true);
                }

                if(i < m_WayPointsWidth.Count)
                {
                    m_WayPointsWidth[i] = wayPointsWidth[i];
                }
                else
                {
                    m_WayPointsWidth.Add(wayPointsWidth[i]);
                }
            }
            if(m_WayPoints.Count > wayPoints.Length)
            {
                for(int i=wayPoints.Length; i<m_WayPoints.Count; i++)
                {
                    DestroyImmediate(m_WayPoints[i]);
                }
                m_WayPoints.RemoveRange(wayPoints.Length, m_WayPoints.Count - wayPoints.Length);
            }
            if(m_WayPointsWidth.Count > m_WayPoints.Count)
            {
                m_WayPointsWidth.RemoveRange(m_WayPoints.Count, m_WayPointsWidth.Count - m_WayPoints.Count);
            }
            RebuildRiverMesh();
        }
        else
        {
            EditorUtility.DisplayDialog("RiverMesh", "配置文件无效，加载失败", "确定");
        }
    }

    void SaveTempJson(bool refreshEditor = false)
    {
        if (m_Mesh == null || m_RiverObj == null) return;
        m_TempDirty = !refreshEditor;
        SaveToJson(m_TempCfgPath, false, refreshEditor);
    }

    void SaveToJson(string path, bool overideTips = true, bool refreshEditor = true)
    {
        if (m_Mesh == null || m_RiverObj == null) return;
        if (string.IsNullOrEmpty(path)) return;
        if (m_WayPoints.Count == 0) return;
        if(overideTips && File.Exists(path))
        {
            if(!EditorUtility.DisplayDialog("存储RiverMesh配置文件", string.Format("【{0}】文件已存在，是否覆盖？", path), "是", "否"))
            {
                return;
            }
        }
        if(!Directory.Exists(m_CfgPath))
        {
            Directory.CreateDirectory(m_CfgPath);
        }
        RiverCfgJson cfgJson = new RiverCfgJson();
        cfgJson.horizontal = m_Horizontal;
        cfgJson.useUVRepeat = m_UseRepeatUV;
        cfgJson.uvRepeat = m_UVRepeat;
        cfgJson.perGridNum = m_PerGridNum;
        cfgJson.smoothNess = m_SmoothNess;
        cfgJson.smoothAmount = m_SmoothAmount;
        cfgJson.wayPoints = m_WayPoints.Select(point => point.transform.localPosition).ToArray();
        cfgJson.wayPointsWidth = m_WayPointsWidth.ToArray();
        string json = JsonUtility.ToJson(cfgJson);
        File.WriteAllText(path, json);
        if(refreshEditor)
        {
            m_TempDirty = false;
            AssetDatabase.Refresh();
        }
    }


    private void OnGUI()
    {
        _serializedObjct.Update();
        EditorGUILayout.LabelField("shift+右键插入节点到末尾，ctrl+右键向前插入，alt+右键向后插入，shift+D删除节点");
        EditorGUILayout.LabelField(string.Format("配置文件路径：{0}", m_CfgPath));
        EditorGUILayout.LabelField("tempRiver.json文件会在一些变更节点保存当前正在编辑的Mesh信息");
        EditorGUILayout.Space();

        EditorGUILayout.ObjectField("WaterObject", m_RiverObj, typeof(GameObject), true);
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
        if(m_CheckUV != flag)
        {
            m_CheckUV = flag;
            UpdateMaterial();
        }
        EditorGUILayout.EndHorizontal();
        m_MeshGridColor = EditorGUILayout.ColorField("MeshGridColor", m_MeshGridColor);
        

        EditorGUILayout.BeginHorizontal();
        flag = EditorGUILayout.Toggle("Horizontal", m_Horizontal);
        if(flag)
        {
            if (m_Horizontal != flag) 
            {
                m_Horizontal = flag;
                m_Vertical = false;
                RebuildRiverMesh();
                SaveTempJson();
            }
        }
        flag = EditorGUILayout.Toggle("Vertical", m_Vertical);
        if(flag)
        {
            if(m_Vertical != flag)
            {
                m_Vertical = flag;
                m_Horizontal = false;
                RebuildRiverMesh();
                SaveTempJson();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        float defaultWidth = EditorGUILayout.FloatField("DefaultWidth", m_DefaultWidth);
        if(!Mathf.Approximately(defaultWidth, m_DefaultWidth))
        {
            m_DefaultWidth = defaultWidth;
            RebuildRiverMesh();
        }
        
        var gos = Selection.gameObjects;
        if (gos != null && gos.Length == 1 && m_WayPointsMap.ContainsKey(gos[0]))
        {
            int index = m_WayPoints.FindIndex(c => c == gos[0]);
            if(index >= 0)
            {
                float width = EditorGUILayout.FloatField("PointWidth", m_WayPointsWidth[index]);
                if(!Mathf.Approximately(width, m_WayPointsWidth[index]))
                {
                    m_WayPointsWidth[index] = width;
                    RebuildRiverMesh();
                    SaveTempJson();
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        flag = EditorGUILayout.Toggle("UseRepeatUV", m_UseRepeatUV);
        if(m_UseRepeatUV != flag)
        {
            m_UseRepeatUV = flag;
            RebuildRiverMesh();
            SaveTempJson();
        }
        if(m_UseRepeatUV)
        {
            float repeatUV = EditorGUILayout.FloatField("UVRepeat", m_UVRepeat);
            if(!Mathf.Approximately(repeatUV, m_UVRepeat))
            {
                m_UVRepeat = repeatUV;
                RebuildRiverMesh();
                SaveTempJson();
            }
        }
        EditorGUILayout.EndHorizontal();

        int gridNum = EditorGUILayout.IntSlider("PerGridNum", m_PerGridNum, 1, 20);
        if(m_PerGridNum != gridNum)
        {
            m_PerGridNum = gridNum;
            RebuildRiverMesh();
            SaveTempJson();
        }

        float smoothNess = EditorGUILayout.Slider("SmoothNess", m_SmoothNess, 0, 0.1f);
        if(smoothNess != m_SmoothNess)
        {
            m_SmoothNess = smoothNess;
            RebuildRiverMesh();
            SaveTempJson();
        }
        int amount = EditorGUILayout.IntSlider("SmoothAmount", m_SmoothAmount, 1, 30);
        if(amount != m_SmoothAmount)
        {
            m_SmoothAmount = amount;
            RebuildRiverMesh();
            SaveTempJson();
        }
        float scale = EditorGUILayout.Slider("WayPointScale", m_WayPointScale, 0.1f, 10);
        if(!Mathf.Approximately(scale, m_WayPointScale))
        {
            m_WayPointScale = scale;
            UpdateWayPointScale();
        }

        if(GUILayout.Button("RebuildMesh"))
        {
            RebuildRiverMesh();
        }

        if(GUILayout.Button("NewRiver"))
        {
            Clear();
            m_RiverObj = null;
            m_Mesh = null;
        }

        if (GUILayout.Button("ChooseRiver"))
        {
            var go = Selection.activeGameObject;
            if(go && !m_WayPointsMap.ContainsKey(go))
            {
                m_RiverObj = go;
                m_Mesh = null;
                InitMesh();
            }
            if(m_RiverObj != null && m_CenterObj == null)
            {
                CreateCenterObject();
            }
        }
        if(GUILayout.Button("ChooseAllPoints"))
        {
            Selection.objects = m_WayPoints.ToArray();
        }
        if (GUILayout.Button("LoadTemp"))
        {
            if(!File.Exists(m_TempCfgPath))
            {
                EditorUtility.DisplayDialog("LoadTempCfg", string.Format("{0} 不存在", m_TempCfgPath), "确定");
            }
            else
            {
                var go = Selection.activeGameObject;
                if(go && go != m_RiverObj && !m_WayPointsMap.ContainsKey(go))
                {
                    m_RiverObj = go;
                    m_Mesh = null;
                    InitMesh();
                }
                if(m_RiverObj)
                {
                    if(m_CenterObj == null)
                    {
                        CreateCenterObject();
                    }
                    if(m_TempDirty)
                    {
                        m_TempDirty = false;
                        AssetDatabase.Refresh();
                    }
                    var txt = AssetDatabase.LoadAssetAtPath<TextAsset>(m_TempCfgPath);
                    LoadFromJson(txt);
                }
            }
        }

        if(GUILayout.Button("LoadConfig"))
        {
            if(m_CenterObj == null)
            {
                CreateCenterObject();
            }
            LoadFromJson();
        }

        if(GUILayout.Button("DeletePoints"))
        {
            DeleteWayPoint(Selection.gameObjects);
        }

        if(GUILayout.Button("SaveConfig"))
        {
            if(m_Mesh != null)
            {
                SaveToJson(string.Format("{0}{1}.json", m_CfgPath, m_Mesh.name));
            }
        }

        if(GUILayout.Button("SaveMesh"))
        {
            SaveMesh();
        }
        _serializedObjct.ApplyModifiedProperties();
    }


    void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        if(e.type == EventType.MouseUp && e.button == 1)
        {
            bool insertNew = e.shift;
            bool insertBefore = false;
            bool insertBack = false;
            GameObject _go = null;
            if(!insertNew)
            {
                _go = (Selection.gameObjects != null && Selection.gameObjects.Length == 1) ? Selection.gameObjects[0] : null;
                insertBefore = e.control && _go && m_WayPointsMap.ContainsKey(_go);
                insertBack = e.alt && _go && m_WayPointsMap.ContainsKey(_go); 
            }
            if(insertNew || insertBefore || insertBack)
            {
                Vector3 mousePos = e.mousePosition;
                mousePos.y = Screen.height - mousePos.y;
                if(m_RiverObj == null)
                {
                    mousePos.z = 10;
                    CreateNewRiver(sceneView.camera.ScreenToWorldPoint(mousePos));
                    return;
                }
                Vector3 pos = sceneView.camera.WorldToScreenPoint(m_RiverObj.transform.position);
                mousePos.z = pos.z;
                Vector3 worldMouse = sceneView.camera.ScreenToWorldPoint(mousePos);
                Vector3 localPos = m_RiverObj.transform.InverseTransformPoint(worldMouse);
                if(insertNew)
                {
                    if(m_WayPoints.Count > 0)
                    {
                        localPos.y = m_WayPoints[m_WayPoints.Count - 1].transform.localPosition.y;
                    }
                    else
                    {
                        localPos.y = 0;
                    }
                }
                else
                {
                    localPos.y = _go.transform.localPosition.y;
                }

                GameObject point = NewWayPoint(localPos);
                if(insertNew)
                {
                    m_WayPoints.Add(point);
                    m_WayPointsWidth.Add(m_DefaultWidth);
                }
                else
                {
                    int index = m_WayPoints.FindIndex(c => c == _go);
                    if(insertBefore)
                    {
                        m_WayPoints.Insert(index, point);
                        m_WayPointsWidth.Insert(index, m_DefaultWidth);
                        point.transform.SetSiblingIndex(_go.transform.GetSiblingIndex());
                    }
                    else
                    {
                        m_WayPoints.Insert(index+1, point);
                        m_WayPointsWidth.Insert(index + 1, m_DefaultWidth);

                        point.transform.SetSiblingIndex(_go.transform.GetSiblingIndex() + 1);
                    }
                }
                Selection.activeGameObject = point;
                m_WayPointsMap.Add(point, true);
                SaveTempJson();
                RebuildRiverMesh();
            }
        }
        else
        {
            var gos = Selection.gameObjects;
            if(e.type == EventType.MouseDown && e.button == 0)
            {
                m_Choosed = false;
                m_ChoosedYVals.Clear();
                for(int i=0; i < gos.Length; i++)
                {
                    if(m_WayPointsMap.ContainsKey(gos[i]))
                    {
                        m_Choosed = true;
                    }
                    m_ChoosedYVals.Add(gos[i].transform.localPosition.y);
                }
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                bool haveChanged = false;
                for(int i = 0; i < gos.Length; i++)
                {
                    if(m_WayPointsMap.ContainsKey(gos[i]))
                    {
                        haveChanged = true;
                        if(m_LockYaxis && m_Choosed)
                        {
                            Vector3 pos = gos[i].transform.localPosition;
                            pos.y = m_ChoosedYVals[i];
                            gos[i].transform.localPosition = pos;
                        }
                    }
                }
                if(haveChanged)
                {
                    RebuildRiverMesh();
                    m_PointDraged = true;
                    sceneView.Repaint();
                }
            }
            else if((e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow) && e.button == 0)
            {
                bool haveChanged = false;
                if(m_PointDraged)
                {
                    for(int i = 0; i < gos.Length; i++)
                    {
                        if(m_WayPointsMap.ContainsKey(gos[i]))
                        {
                            haveChanged = true;
                            if(m_LockYaxis && m_Choosed)
                            {
                                Vector3 pos = gos[i].transform.localPosition;
                                pos.y = m_ChoosedYVals[i];
                                gos[i].transform.localPosition = pos;
                            }
                        }
                    }
                    if(haveChanged)
                    {
                        SaveTempJson();
                        RebuildRiverMesh();
                        m_PointDraged = false;
                        sceneView.Repaint();
                    }
                    m_Choosed = false;
                }
            }
            else if(e.shift && e.keyCode == KeyCode.D)
            {
                DeleteWayPoint(Selection.gameObjects);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!m_ShowGizmos) return;
        if(m_SmoothWayPoints.Count > 1)
        {
            Gizmos.color = Color.green;
            Vector3 from, to;
            Transform trans = m_RiverObj.transform;
            Vector3 scale = Vector3.one * 0.5f;
            scale.x = 2;
            scale.z = 2;
            Gizmos.DrawCube(trans.TransformPoint(m_SmoothWayPoints[0]), scale);
            for(int i=0; i < m_SmoothWayPoints.Count-1; i++)
            {
                from = m_RiverObj.transform.TransformPoint(m_SmoothWayPoints[i]);
                to = trans.TransformPoint(m_SmoothWayPoints[i + 1]);
                Gizmos.DrawLine(from, to);
                Gizmos.DrawCube(to, scale);
            }

            Gizmos.color = m_MeshGridColor;
            Vector3[] vertices = m_Mesh.vertices;
            int[] triangles = m_Mesh.triangles;
            Vector3 a, b, c;
            for(int i=0; i< triangles.Length; i += 3)
            {
                if(vertices.Length > triangles[i] && vertices.Length > triangles[i+1] && vertices.Length > triangles[i+2])
                {
                    a = trans.TransformPoint(vertices[triangles[i]]);
                    b = trans.TransformPoint(vertices[triangles[i + 1]]);
                    c = trans.TransformPoint(vertices[triangles[i + 2]]);
                    Gizmos.DrawLine(a, b);
                    Gizmos.DrawLine(a, c);
                    Gizmos.DrawLine(b, c);
                }
            }
        }
    }

    public void RebuildRiverMesh()
    {
        if (m_RiverObj == null || m_Mesh == null) return;
        m_Mesh.triangles = null;
        m_Mesh.uv = null;
        m_Mesh.vertices = null;
        m_SmoothWayPoints.Clear();
        m_SmoothWayDir.Clear();
        if (m_WayPoints.Count < 2) return;
        Vector3[] _wayPoints = m_WayPoints.Select(point => point.transform.localPosition).ToArray();
        GetWayPoints(_wayPoints, m_WayPointsWidth, m_SmoothAmount, m_SmoothNess, ref m_SmoothWayPoints, ref m_SmoothWayWidth, ref m_SmoothWayDir, ref m_RiverLength, ref m_RiverMaxWidth);

        RiverVerticeCaculate(m_Mesh, m_SmoothWayPoints, m_SmoothWayDir);
        m_Mesh.RecalculateNormals();
        m_Mesh.RecalculateBounds();
        m_Mesh.RecalculateTangents();
    }

    public float GetRiverLength()
    {
        float length = 0;
        for(int i=0;i<m_SmoothWayPoints.Count-1;i++)
        {
            length += Vector3.Distance(m_SmoothWayPoints[i], m_SmoothWayPoints[i + 1]);
        }
        return length;
    }

    void RiverVerticeCaculate(Mesh _riverMesh, List<Vector3> _resultWayPoints, List<Vector3> _resultWayDir)
    {
        Vector3 widthDir = Vector3.zero;
        m_Vertices.Clear();
        m_Triangles.Clear();
        m_UVs.Clear();
        m_UV2s.Clear();

        float _curLength = 0;
        float _curUVRate = 0;
        float _curUV2Rate = 0;
        float _uvRela = m_RiverLength > m_RiverMaxWidth ? m_RiverLength : m_RiverMaxWidth;
        for(int i=0; i < _resultWayPoints.Count; i++)
        {
            Vector3 _wayPoint = _resultWayPoints[i];
            Vector3 _wayDir = _resultWayDir[i];
            //叉乘求出中心向量的垂线向量 即宽度方向
            widthDir = Vector3.Cross(_wayDir, Vector3.up).normalized;
            //河流宽度
            float _halfRiverWidth = m_DefaultWidth;
            if (i < m_SmoothWayWidth.Count)
            {
                _halfRiverWidth = m_SmoothWayWidth[i] * 0.5f;
            }
            else
            {
                m_SmoothWayWidth.Add(m_DefaultWidth);
            }
            //计算曲线两边的顶点位置
            Vector3 vecA = _wayPoint - widthDir * _halfRiverWidth;
            Vector3 vecB = _wayPoint + widthDir * _halfRiverWidth;
            if (i >= 1)
            {
                Vector3 preA = m_Vertices[m_Vertices.Count - 1 - m_PerGridNum];
                Vector3 preB = m_Vertices[m_Vertices.Count - 1];
                Vector3 lastWayDir = _resultWayDir[i - 1];
                //CheckForwardLineCross(ref vecA, ref vecB, preA, preB, lastWayDir, _wayDir, _wayPoint);
                CheckTopBottomLineCross(ref vecA, ref vecB, preA, preB, lastWayDir, _wayDir, _wayPoint);
                CheckTopHypotenuseLineCross(ref vecA, ref vecB, preA, preB, lastWayDir, _wayDir, _wayPoint);
                CheckBottomHypotenuseLineCross(ref vecA, ref vecB, preA, preB, lastWayDir, _wayDir, _wayPoint);
                CheckTopLineCross(ref vecA, ref vecB, m_Vertices, _wayDir, _wayPoint);
                CheckBottomLineCross(ref vecA, ref vecB, m_Vertices, _wayDir, _wayPoint);

                _curLength += Vector3.Distance(_resultWayPoints[i - 1], _resultWayPoints[i]); //Vector3.Distance(_vertices[2 * (i - 1)], _vertices[2 * i]);
                _curUVRate = _curLength / m_UVRepeat;
                _curUV2Rate = _curLength / _uvRela;
                Vector3 point;
                if (Mathf.Abs(vecA.y - preA.y) <= 0.01f && CheckLineCross(preA, preB, vecA, vecB, out point, true))
                {
                    continue;
                }

                if (Vector3.Distance(vecA, preA) <= 0.01f || Vector3.Distance(vecB, preB) <= 0.01f)
                {
                    continue;
                }
            }
            Vector2 uv, uv2;
            float _perAdditive = (2 * _halfRiverWidth / m_UVRepeat) / m_PerGridNum;
            float _perAdditive2 = (2 * _halfRiverWidth / _uvRela) / m_PerGridNum;
            if (m_Horizontal)
            {
                uv.x = _curUVRate;
                uv.y = 0.5f - _halfRiverWidth / m_UVRepeat;
                uv2.x = _curUV2Rate;
                uv2.y = 0.5f - _halfRiverWidth / _uvRela;
            }
            else
            {
                uv.x = _halfRiverWidth / m_UVRepeat + 0.5f;
                uv.y = _curUVRate;
                uv2.x = _halfRiverWidth / _uvRela + 0.5f;
                uv2.y = _curUV2Rate;
            }
            Vector3 vertex = vecA;
            m_Vertices.Add(vertex);
            m_UVs.Add(uv);
            m_UV2s.Add(uv2);
            for(int j=1; j <= m_PerGridNum; j++)
            {
                vertex += (vecB - vecA) / m_PerGridNum;
                if (m_Horizontal)
                {
                    uv.y += _perAdditive;
                    uv2.y += _perAdditive2;
                }
                else
                {
                    uv.x -= _perAdditive;
                    uv2.x -= _perAdditive2;
                }
                m_Vertices.Add(vertex);
                m_UVs.Add(uv);
                m_UV2s.Add(uv2);
            }
            if(i>=1)
            {
                int idx = m_Vertices.Count - 1 - m_PerGridNum;
                for(int j = 0;j < m_PerGridNum; j++)
                {
                    m_Triangles.Add(idx - 1 - m_PerGridNum);
                    m_Triangles.Add(idx - m_PerGridNum);
                    m_Triangles.Add(idx+1);

                    m_Triangles.Add(idx + 1);
                    m_Triangles.Add(idx);
                    m_Triangles.Add(idx - 1 - m_PerGridNum);
                    idx += 1;
                }
            }
        }
        _riverMesh.vertices = m_Vertices.ToArray();
        _riverMesh.triangles = m_Triangles.ToArray();
        if(m_UseRepeatUV)
        {
            _riverMesh.uv = m_UVs.ToArray();
            _riverMesh.uv2 = m_UV2s.ToArray();
        }
        else
        {
            _riverMesh.uv = m_UV2s.ToArray();
            _riverMesh.uv2 = m_UVs.ToArray();
        }
    }

    void CheckForwardLineCross(ref Vector3 a, ref Vector3 b, Vector3 preA, Vector3 preB, Vector3 lastWayDir, Vector3 wayDir, Vector3 wayPoint)
    {
        Vector3 point = Vector3.zero;
        var lastP = preA; 
        var lastP1 = preB;
        bool ret = CheckLineCross(a, b, lastP, lastP1, out point);
        if(ret)
        {
            lastWayDir.y = 0;
            wayDir.y = 0;
            var cross = Vector3.Cross(lastWayDir, wayDir);
            if(cross.y < 0)
            {
                b.x = preB.x;
                b.y = preB.y;
                b.z = preB.z;
            }
            else
            {
                a.x = preA.x;
                a.y = preA.y;
                a.z = preA.z;
            }
        }
    }

    void CheckTopLineCross(ref Vector3 a, ref Vector3 b, List<Vector3> vertices, Vector3 wayDir, Vector3 wayPoint)
    {
        Vector3 point = Vector3.zero;
        for(int i = vertices.Count - 1; i > m_PerGridNum; i -= (m_PerGridNum+1))
        {
            var lastP = vertices[i];
            var lastP1 = vertices[i-m_PerGridNum-1];
            bool ret = Mathf.Approximately(lastP.y, wayPoint.y)
                && CheckLineCross(a, b, lastP, lastP1, out point);
            if(ret)
            {
                b.x = point.x;
                b.z = point.z;
                break;
            }
        }
    }

    void CheckBottomLineCross(ref Vector3 a, ref Vector3 b, List<Vector3> vertices, Vector3 wayDir, Vector3 wayPoint)
    {
        Vector3 point = Vector3.zero;
        for(int i = vertices.Count - 1; i > m_PerGridNum; i -= (m_PerGridNum+1))
        {
            var lastP = vertices[i-m_PerGridNum];
            var lastP1 = vertices[i-m_PerGridNum-m_PerGridNum-1];
            bool ret = Mathf.Approximately(lastP.y, wayPoint.y)
                && CheckLineCross(a, b, lastP, lastP1, out point);
            if(ret)
            {
                a.x = point.x;
                a.z = point.z;
                break;
            }
        }
    }

    void CheckTopBottomLineCross(ref Vector3 a, ref Vector3 b, Vector3 preA, Vector3 preB, Vector3 lastWayDir, Vector3 wayDir, Vector3 wayPoint)
    {
        lastWayDir.y = 0;
        wayDir.y = 0;
        var cross = Vector3.Cross(lastWayDir, wayDir);
        if (cross.y > 0)
        {
            return;
        }
        var point = Vector3.zero;
        var lastP = preA;
        var lastP1 = preB;
        bool ret = Mathf.Approximately(lastP.y, wayPoint.y)
                && CheckLineCross(lastP, a, lastP1, b, out point);
        if (ret)
        { 
            b.x = point.x;
            b.z = point.z;
        }
    }

    void CheckTopHypotenuseLineCross(ref Vector3 _a, ref Vector3 _b, Vector3 preA, Vector3 preB, Vector3 lastWayDir, Vector3 wayDir, Vector3 wayPoint)
    {
        lastWayDir.y = 0;
        wayDir.y = 0;
        var _cross = Vector3.Cross(lastWayDir, wayDir);
        if (_cross.y < 0)
        {
            return;
        }
        Vector3 point = Vector3.zero;
        var o = _b;
        var a = preA;
        var b = preB;
        if (!Mathf.Approximately(b.y, o.y))
        {
            return;
        }
        var oa = o - a;
        var ob = o - b;
        Vector3 cross = Vector3.Cross(oa, ob);
        if (cross.y <= 0)
        {
            _b.x = b.x;
            _b.y = b.y;
            _b.z = b.z;
        }
    }

    void CheckBottomHypotenuseLineCross(ref Vector3 _a, ref Vector3 _b, Vector3 preA, Vector3 preB, Vector3 lastWayDir, Vector3 wayDir, Vector3 wayPoint)
    {
        lastWayDir.y = 0;
        wayDir.y = 0;
        var _cross = Vector3.Cross(lastWayDir, wayDir);
        if (_cross.y < 0)
        {
            return;
        } 
        Vector3 point = Vector3.zero;
        var o = _b;
        var a = _a;
        var b = preA;
        if (!Mathf.Approximately(b.y, o.y))
        {
            return;
        }
        var oa = o - a;
        var ob = o - b;
        Vector3 cross = Vector3.Cross(oa, ob);
        if (cross.y <= 0)
        {
            _a.x = b.x;
            _a.y = b.y;
            _a.z = b.z;
        }
    }


    bool CheckLineCross(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 point, bool includePoint = false)
    {
        point = Vector3.zero;
        /** 1解线性方程组， 求线段交点. **/
        // 如果坟墓为0 则平行或共线，不相交
        var denominator = (b.z - a.z) * (d.x - c.x) - (a.x - b.x) * (c.z - d.z);
        if(denominator == 0)
        {
            return false;
        }

        //线段所在直线的交点坐标 (x, y)
        var x = ((b.x - a.x) * (d.x - c.x) * (c.z - a.z)
            + (b.z - a.z) * (d.x - c.x) * a.x
            - (d.z - c.z) * (b.x - a.x) * c.x) / denominator;
        var z = -((b.z - a.z) * (d.z - c.z) * (c.x - a.x)
            + (b.x - a.x) * (d.z - c.z) * a.z
            - (d.x - c.x) * (b.z - a.z) * c.z) / denominator;
        /** 2 判断交点是否在两条线段上 **/
        if (
            // 交点在线段1上
            (x - a.x) * (x - b.x) <= 0 && (z - a.z) * (z - b.z) <= 0
            //且交点也在线段2上
            && (x - c.x) * (x - d.x) <= 0 && (z - c.z) * (z - d.z) <=0
            )
        {
            if(!includePoint)
            {
                if (
                    (Mathf.Approximately(x, a.x) && Mathf.Approximately(z, a.z))
                    || (Mathf.Approximately(x, b.x) && Mathf.Approximately(z, b.z))
                    || (Mathf.Approximately(x, c.x) && Mathf.Approximately(z, c.z))
                    || (Mathf.Approximately(x, d.x) && Mathf.Approximately(z, d.z))
                    )
                {
                    return false;
                }
            }
            
            //返回交点p
            point.x = x;
            point.z = z;
            return true;
        }
        return false;
    }

    #region Catmull-rom曲线
    //根据提供的关键点获取平滑曲线
    public void GetWayPoints(Vector3[] points, List<float> pointsWidth, int smoothAmount, float smoothNess, ref List<Vector3> wayPoints, ref List<float> wayPointsWidth, ref List<Vector3> wayPointsDir, ref float riverLength, ref float maxWidth)
    {
        if (points == null || points.Length <= 1) { Debug.LogError("Points Empty!!!"); return; }
        
        wayPointsWidth.Clear();
        wayPoints.Clear();
        wayPointsDir.Clear();
        riverLength = 0;
        maxWidth = 0;

        Vector3[] linePoints = PathControlPointGenerator(points);
        Vector3 bigDir;
        Vector3 curDir, nextDir;
        Vector3 dir, dir1, dir2;
        float fromWidth , tgtWidth, distance;
        for(int i=0; i < points.Length - 1; i++)
        {
            if(i==0)
            {
                curDir = (points[i + 1] - points[i]).normalized;
                if(i + 2 < points.Length)
                {
                    nextDir = (points[i + 2] - points[i + 1]).normalized;
                    nextDir = (curDir + nextDir) * 0.5f;
                }
                else
                {
                    nextDir = curDir;
                }
            }
            else
            {
                dir1 = (points[i] - points[i - 1]).normalized;
                dir2 = (points[i + 1] - points[i]).normalized;
                curDir = (dir1 + dir2) * 0.5f;
                if(i+2 < points.Length)
                {
                    dir1 = (points[i + 2] - points[i + 1]).normalized;
                    nextDir = (dir1 + dir2) * 0.5f;
                }
                else
                {
                    nextDir = dir2;
                }
            }

            wayPoints.Add(points[i]);
            wayPointsDir.Add(curDir);
            wayPointsWidth.Add(pointsWidth[i]);
            fromWidth = pointsWidth[i];
            tgtWidth = pointsWidth[i + 1];
            distance = Vector3.Distance(points[i], points[i + 1]);
            if(wayPoints.Count > 1)
            {
                riverLength += Vector3.Distance(wayPoints[wayPoints.Count - 1], wayPoints[wayPoints.Count - 2]);
            }
            maxWidth = maxWidth < wayPointsWidth[wayPointsWidth.Count - 1] ? wayPointsWidth[wayPointsWidth.Count - 1] : maxWidth;
            for (int j = 1; j < smoothAmount; j++)
            {
                Vector3 smoothPoint = Interp(linePoints, i, (float)j / smoothAmount);
                bigDir = (points[i + 1] - wayPoints[wayPoints.Count - 1]).normalized;
                dir = (smoothPoint - wayPoints[wayPoints.Count - 1]).normalized;
                float _offset = Mathf.Abs(1 - Vector3.Dot(bigDir, dir));
                if(_offset <= smoothNess)
                {
                    //剔除曲度变化不大的点
                    //Debug.LogError(i + " " + j + Vector3.Dot(bigDir, _dir));
                }
                else
                {
                    float rate = Vector3.Distance(smoothPoint, points[i]) / distance;
                    float width = Mathf.Lerp(fromWidth, tgtWidth, rate);
                    dir = Vector3.Lerp(curDir, nextDir, rate).normalized;
                    wayPointsWidth.Add(width);
                    wayPointsDir.Add(dir);
                    wayPoints.Add(smoothPoint);
                    if(wayPoints.Count > 1)
                    {
                        riverLength += Vector3.Distance(wayPoints[wayPoints.Count - 1], wayPoints[wayPoints.Count - 2]);
                    }
                    maxWidth = maxWidth < wayPointsWidth[wayPointsWidth.Count - 1] ? wayPointsWidth[wayPointsWidth.Count - 1] : maxWidth;
                }
            }
        }
        int len = points.Length;
        dir = (points[len - 1] - points[len - 2]).normalized;
        wayPointsDir.Add(dir);
        wayPointsWidth.Add(pointsWidth[pointsWidth.Count - 1]);
        wayPoints.Add(points[points.Length - 1]);
        if(wayPoints.Count > 1)
        {
            riverLength += Vector3.Distance(wayPoints[wayPoints.Count - 1], wayPoints[wayPoints.Count - 2]);
        }
        maxWidth = maxWidth < wayPointsWidth[wayPointsWidth.Count - 1] ? wayPointsWidth[wayPointsWidth.Count - 1] : maxWidth;
    }

    //因为4个才能点控制一条曲线， 添加首尾控制点
    private Vector3[] PathControlPointGenerator(Vector3[] path)
    {
        Vector3[] suppliedPath;
        Vector3[] linePoints;
        suppliedPath = path;
        int offset = 2;
        linePoints = new Vector3[suppliedPath.Length + offset];
        Array.Copy(suppliedPath, 0, linePoints, 1, suppliedPath.Length);

        //计算第一个控制点和最后一个控制点位置
        linePoints[0] = linePoints[1] + (linePoints[1] - linePoints[2]);
        linePoints[linePoints.Length - 1] = linePoints[linePoints.Length - 2] + (linePoints[linePoints.Length - 2] - linePoints[linePoints.Length - 3]);

        //首尾点重合时， 形成闭合的Catmull-Rom曲线
        if(linePoints[1] == linePoints[linePoints.Length-2])
        {
            Vector3[] tmpLoopSpline = new Vector3[linePoints.Length];
            Array.Copy(linePoints, tmpLoopSpline, linePoints.Length);
            tmpLoopSpline[0] = tmpLoopSpline[tmpLoopSpline.Length - 3];
            tmpLoopSpline[tmpLoopSpline.Length - 1] = tmpLoopSpline[2];
            linePoints = new Vector3[tmpLoopSpline.Length];
            Array.Copy(tmpLoopSpline, linePoints, tmpLoopSpline.Length);
        }
        return linePoints;
    }

    //每4个点构成一条曲线函数 前后两个为控制点 中间两个为曲线起始结束点
    private Vector3 Interp(Vector3[] pts, int startIndex, float t)
    {
        Vector3 p0 = pts[startIndex];
        Vector3 p1 = pts[startIndex + 1];
        Vector3 p2 = pts[startIndex + 2];
        Vector3 p3 = pts[startIndex + 3];

        Vector3 a = 2 * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2 * p0 - 5 * p1 + 4 * p2 - p3;
        Vector3 d = -p0 + 3 * p1 - 3 * p2 + p3;

        Vector3 pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

        return pos;
    }

    #endregion
}
