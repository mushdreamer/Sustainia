using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 定义别名避免冲突
using CoreBuildingType = SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType;

public class MultiZoneCityGenerator : MonoBehaviour
{
    public static MultiZoneCityGenerator Instance;

    [System.Serializable]
    public class GenerationZone
    {
        public string zoneName = "Area 1";
        public Transform originPoint;
        [Tooltip("区域宽度（以格子数为单位）")]
        public int width = 5;
        [Tooltip("区域高度（以格子数为单位）")]
        public int height = 5;

        [Header("Visual Adjustment")]
        [Tooltip("【编辑器实时调节】针对该区域的高度微调。拖动此值，Scene窗口中的圆环会实时变化。")]
        public float visualHeightOffset = 0.0f;

        public bool isOccupied = false;

        // --- 视觉引用 (Runtime Only) ---
        [HideInInspector] public LineRenderer groundOutline;
        [HideInInspector] public TextMesh statusText;
        [HideInInspector] public Transform arrowObj;
        [HideInInspector] public Material instanceLineMat;

        public bool Contains(Vector3 worldPos, float cellSize)
        {
            if (originPoint == null) return false;

            float diffX = worldPos.x - originPoint.position.x;
            float diffZ = worldPos.z - originPoint.position.z;
            float relativeX = diffX / cellSize;
            float relativeZ = diffZ / cellSize;

            return relativeX >= 0 && relativeX < width && relativeZ >= 0 && relativeZ < height;
        }
    }

    [System.Serializable]
    public struct BuildingType { public string name; public GameObject prefab; public Placeable data; }

    [Header("Global Settings")]
    public float cellSize = 10.0f;
    public LayerMask roadLayer;
    public List<BuildingType> buildingOptions;
    public float buildingYOffset = 0.0f;

    [Header("Optimization Objectives")]
    public float targetCo2 = 50.0f;
    public float targetCost = 1000.0f;
    public float targetEnergy = 100.0f;
    public float weightCo2 = 1.0f;
    public float weightCost = 1.0f;
    public float weightEnergy = 1.5f;

    [Header("Visual Feedback (Holographic Ring)")]
    [Tooltip("空闲区域颜色")]
    public Color validColor = Color.green;
    [Tooltip("占用区域颜色")]
    public Color occupiedColor = new Color(1f, 0.6f, 0f); // Orange

    [Tooltip("圆环线条宽度 (0.05 非常细，像线框)")]
    public float lineWidth = 0.05f; // 【修改】默认变得非常细

    [Tooltip("圆环距离地面的高度偏移")]
    public float lineYOffset = 0.1f;

    [Tooltip("UI的基础高度")]
    public float baseUiHeight = 12.0f;

    [Tooltip("动画速度")]
    public float animSpeed = 2.0f;

    [Tooltip("圆环的圆滑程度 (点数越多越圆)")]
    public int circleSegments = 128; // 【修改】增加段数，让圆更圆

    // <<< --- 新增：控制开关 ---
    [Header("Control")]
    [Tooltip("是否在Start时自动生成？如果使用IntroManager，请取消勾选")]
    public bool autoStartGeneration = false;
    // <<< ---------------------

    private float _currentCo2 = 0f;
    private float _currentCost = 0f;
    private float _currentEnergy = 0f;

    private string _csvFilePath;
    private int _stepCount = 0;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); }
        Instance = this;
    }

    IEnumerator Start()
    {
        InitZoneVisuals();
        yield return null;

        if (PlacementSystem.Instance == null)
        {
            Debug.LogError("PlacementSystem 尚未初始化！");
            yield break;
        }

        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        InitCSV();

        // <<< --- 修改：只有勾选了自动开始才执行 ---
        if (autoStartGeneration)
        {
            StartCoroutine(GenerateZonesSequence());
        }
        // <<< ------------------------------------
    }

    // <<< --- 新增：供外部调用的开始方法 ---
    public void BeginGeneration()
    {
        Debug.Log("[Generator] 收到开始指令，开始生成城市...");
        StopAllCoroutines(); //以此确保不会重复运行
        StartCoroutine(GenerateZonesSequence());
    }
    // <<< --------------------------------

    private void Update()
    {
        AnimateVisuals();
    }

    // --- 核心视觉逻辑：动画与渲染 ---

    private void AnimateVisuals()
    {
        if (zones == null) return;

        // 如果Time.timeScale是0（暂停中），Time.time不会增加，动画会静止
        // 如果你希望在暂停时动画依然播放，可以使用 Time.unscaledTime
        float timeVar = Time.time;

        float bounceY = Mathf.Sin(timeVar * animSpeed) * 1.5f;
        float rotateAngle = timeVar * 90f;
        float emissionMult = 0.8f + Mathf.PingPong(timeVar, 0.4f);

        foreach (var zone in zones)
        {
            Color targetColor = zone.isOccupied ? occupiedColor : validColor;
            float finalHeight = baseUiHeight + zone.visualHeightOffset;

            // 1. 箭头动画
            if (zone.arrowObj != null)
            {
                Vector3 center = GetZoneCenter(zone);
                zone.arrowObj.position = center + Vector3.up * (finalHeight + bounceY);
                zone.arrowObj.rotation = Quaternion.Euler(0, rotateAngle, 180);

                var renderers = zone.arrowObj.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.material.color = targetColor;
            }

            // 2. 文字位置
            if (zone.statusText != null)
            {
                if (Camera.main != null)
                {
                    zone.statusText.transform.rotation = Quaternion.LookRotation(zone.statusText.transform.position - Camera.main.transform.position);
                }

                Vector3 center = GetZoneCenter(zone);
                zone.statusText.transform.position = center + Vector3.up * (finalHeight - 2.5f);

                zone.statusText.color = targetColor;
                zone.statusText.text = zone.isOccupied ? "EDITABLE\nBUILDING" : "OPEN\nSLOT";
            }

            // 3. 线框颜色
            if (zone.instanceLineMat != null)
            {
                zone.instanceLineMat.color = targetColor;
                zone.instanceLineMat.SetColor("_EmissionColor", targetColor * emissionMult);

                // 实时更新圆环
                UpdateCirclePositions(zone);
            }
        }
    }

    // --- 画细线圈 ---
    private void UpdateCirclePositions(GenerationZone zone)
    {
        if (zone.groundOutline == null) return;

        // 实时更新宽度（如果你在Inspector调了）
        zone.groundOutline.startWidth = lineWidth;
        zone.groundOutline.endWidth = lineWidth;

        float realWidth = zone.width * cellSize;
        float realHeight = zone.height * cellSize;

        // 半径取宽高的各一半
        float radiusX = realWidth * 0.5f;
        float radiusZ = realHeight * 0.5f;

        // 中心点（相对于Zone Origin的局部坐标）
        float centerX = realWidth * 0.5f;
        float centerZ = realHeight * 0.5f;

        float y = lineYOffset;
        int steps = circleSegments;

        zone.groundOutline.positionCount = steps + 1;

        for (int i = 0; i <= steps; i++)
        {
            // 计算角度 (0 到 2PI)
            float angle = (float)i / steps * Mathf.PI * 2f;

            // 椭圆公式
            float x = centerX + Mathf.Cos(angle) * radiusX;
            float z = centerZ + Mathf.Sin(angle) * radiusZ;

            zone.groundOutline.SetPosition(i, new Vector3(x, y, z));
        }
    }

    private void InitZoneVisuals()
    {
        foreach (var zone in zones)
        {
            if (zone.originPoint == null) continue;

            if (zone.groundOutline != null) Destroy(zone.groundOutline.gameObject);
            if (zone.statusText != null) Destroy(zone.statusText.gameObject);
            if (zone.arrowObj != null) Destroy(zone.arrowObj.gameObject);

            Vector3 centerPos = GetZoneCenter(zone);

            GameObject container = new GameObject($"{zone.zoneName}_Visuals");
            container.transform.SetParent(this.transform);

            // A. 圆环 (LineRenderer)
            GameObject lineObj = new GameObject("RingOutline");
            lineObj.transform.SetParent(container.transform);
            lineObj.transform.position = zone.originPoint.position;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;

            // 使用 Sprite/Default 材质，这对于画细线是最干净的
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            // 【关键修改】关闭阴影，让它看起来像纯粹的UI
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            zone.instanceLineMat = lr.material;
            zone.groundOutline = lr;

            // 初始化圆环形状
            UpdateCirclePositions(zone);

            // B. 文字
            GameObject textObj = new GameObject("StatusLabel");
            textObj.transform.SetParent(container.transform);
            textObj.transform.position = centerPos;

            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = "Init...";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.5f;
            tm.fontSize = 20;
            tm.fontStyle = FontStyle.Bold;
            zone.statusText = tm;

            // C. 箭头
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ArrowIndicator";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(container.transform);
            marker.transform.localScale = new Vector3(2f, 2f, 2f);
            zone.arrowObj = marker.transform;
            marker.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"));
            marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private Vector3 GetZoneCenter(GenerationZone zone)
    {
        float realWidth = zone.width * cellSize;
        float realHeight = zone.height * cellSize;
        return zone.originPoint.position + new Vector3(realWidth / 2f, 0, realHeight / 2f);
    }

    // -------------------------------------------------------------------
    // EDITOR VISUALIZATION (Scene窗口可视化)
    // -------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        if (zones == null) return;

        foreach (var zone in zones)
        {
            if (zone.originPoint == null) continue;

            float realWidth = zone.width * cellSize;
            float realHeight = zone.height * cellSize;
            Vector3 center = zone.originPoint.position + new Vector3(realWidth / 2, 0, realHeight / 2);

            // 半径
            float radiusX = realWidth * 0.5f;
            float radiusZ = realHeight * 0.5f;

            // 1. 画地面圆环 (在编辑器里看个大概)
#if UNITY_EDITOR
            UnityEditor.Handles.color = zone.isOccupied ? occupiedColor : validColor;
            Vector3 discCenter = center;
            discCenter.y = zone.originPoint.position.y + lineYOffset;

            // 画圆 (如果长宽不同，Unity Handles只支持圆，这里取平均值示意)
            float avgRadius = (radiusX + radiusZ) * 0.5f;
            UnityEditor.Handles.DrawWireDisc(discCenter, Vector3.up, avgRadius);
#endif

            // 2. 画 UI 高度指示器 (黄色)
            float finalUiHeight = baseUiHeight + zone.visualHeightOffset;
            Vector3 uiPos = center + Vector3.up * finalUiHeight;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(center, uiPos);
            Gizmos.DrawWireSphere(uiPos, 0.5f);

#if UNITY_EDITOR
            string labelInfo = $"{zone.zoneName}\nUI Height: {finalUiHeight:F1}";
            UnityEditor.Handles.Label(uiPos + Vector3.up * 1.5f, labelInfo);
#endif
        }
    }

    // ----------------------
    // 外部接口 & 逻辑
    // ----------------------

    public void ClearAndRestartGeneration()
    {
        StopAllCoroutines();

        foreach (var zone in zones)
        {
            zone.isOccupied = false;
            if (zone.originPoint != null)
            {
                foreach (Transform child in zone.originPoint) Destroy(child.gameObject);
            }
        }
        InitZoneVisuals();

        _currentCo2 = 0f;
        _currentCost = 0f;
        _currentEnergy = 0f;
        _stepCount = 0;

        Debug.Log("[Generator] 场景已清理，准备重新生成...");
        StartCoroutine(GenerateZonesSequence());
    }

    public bool IsZoneValidAndEmpty(Vector3 worldPos)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone == null) return false;
        if (zone.isOccupied) return false;
        return true;
    }

    public void SetZoneOccupiedState(Vector3 worldPos, bool isOccupied) => SetZoneOccupiedStatus(worldPos, isOccupied);

    public void SetZoneOccupiedStatus(Vector3 worldPos, bool occupied)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone != null)
        {
            zone.isOccupied = occupied;
            Debug.Log($"[Generator] Zone '{zone.zoneName}' status updated: Occupied = {occupied}");
        }
    }

    public bool IsZoneAvailableForBuilding(Vector3 worldPos)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone == null) return false;
        if (zone.isOccupied) return false;
        return true;
    }

    private GenerationZone GetZoneAtPosition(Vector3 worldPos)
    {
        foreach (var zone in zones)
        {
            if (zone.Contains(worldPos, cellSize)) return zone;
        }
        return null;
    }

    void InitCSV()
    {
        if (string.IsNullOrEmpty(_csvFilePath)) return;
        string header = "Step,Zone,Building,Total_Co2,Total_Cost,Total_Energy,Count,Weighted_Error";
        try { File.WriteAllText(_csvFilePath, header + "\n"); } catch { }
    }

    void WriteCSV(int step, string zone, string building, float totalCo2, float totalCost, float totalEnergy, int count, float wError)
    {
        if (string.IsNullOrEmpty(_csvFilePath)) return;
        string line = $"{step},{zone},{building},{totalCo2},{totalCost},{totalEnergy},{count},{wError:F4}";
        try { File.AppendAllText(_csvFilePath, line + "\n"); } catch { }
    }

    IEnumerator GenerateZonesSequence()
    {
        yield return new WaitForEndOfFrame();

        List<int> availableIndices = new List<int>();
        for (int i = 0; i < zones.Count; i++) availableIndices.Add(i);

        int maxRetries = 300;
        int totalAttempts = 0;

        float currentWeightedError = CalculateWeightedError(_currentCo2, _currentCost, _currentEnergy);

        while (availableIndices.Count > 0 && totalAttempts < maxRetries)
        {
            totalAttempts++;
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedZoneIndex = availableIndices[randomIndex];
            GenerationZone zoneToGenerate = zones[selectedZoneIndex];

            if (zoneToGenerate.isOccupied)
            {
                availableIndices.RemoveAt(randomIndex);
                continue;
            }

            int buildTypeIndex = Random.Range(0, buildingOptions.Count);
            BuildingType candidateType = buildingOptions[buildTypeIndex];
            BuildingStats stats = GetBuildingStats(candidateType.prefab);

            float nextCo2 = _currentCo2 + stats.co2;
            float nextCost = _currentCost + stats.cost;
            float nextEnergy = _currentEnergy + stats.energy;
            float nextWeightedError = CalculateWeightedError(nextCo2, nextCost, nextEnergy);

            if (nextWeightedError < currentWeightedError)
            {
                GenerateOneZone(zoneToGenerate, candidateType);
                zoneToGenerate.isOccupied = true;
                _currentCo2 = nextCo2;
                _currentCost = nextCost;
                _currentEnergy = nextEnergy;
                _stepCount++;
                currentWeightedError = nextWeightedError;
                WriteCSV(_stepCount, zoneToGenerate.zoneName, candidateType.name, _currentCo2, _currentCost, _currentEnergy, _stepCount, currentWeightedError);
                availableIndices.RemoveAt(randomIndex);
            }
            yield return new WaitForSeconds(0.05f);
        }
        Debug.Log("生成序列完成。");
    }

    float CalculateWeightedError(float c, float m, float e)
    {
        float errCo2 = Mathf.Abs(c - targetCo2) / (targetCo2 == 0 ? 1 : Mathf.Abs(targetCo2));
        float errCost = Mathf.Abs(m - targetCost) / (targetCost == 0 ? 1 : Mathf.Abs(targetCost));
        float errEnergy = Mathf.Abs(e - targetEnergy) / (targetEnergy == 0 ? 1 : Mathf.Abs(targetEnergy));
        return (errCo2 * weightCo2) + (errCost * weightCost) + (errEnergy * weightEnergy);
    }

    struct BuildingStats { public float co2; public float cost; public float energy; }

    BuildingStats GetBuildingStats(GameObject prefab)
    {
        BuildingStats stats = new BuildingStats();
        if (prefab == null) return stats;

        BuildingEffect effect = prefab.GetComponent<BuildingEffect>();
        if (effect == null) return stats;

        switch (effect.type)
        {
            case CoreBuildingType.House: stats.co2 = effect.houseCo2Change; break;
            case CoreBuildingType.Farm: stats.co2 = effect.farmCo2Change; break;
            case CoreBuildingType.Institute: stats.co2 = effect.instituteCo2Change; break;
            case CoreBuildingType.Bank: stats.co2 = effect.bankCo2Change; break;
            case CoreBuildingType.PowerPlant: stats.co2 = effect.powerPlantCo2Change; break;
            case CoreBuildingType.Co2Storage: stats.co2 = effect.storageCo2Change; break;
        }
        switch (effect.type)
        {
            case CoreBuildingType.PowerPlant: stats.cost = 200f; break;
            case CoreBuildingType.Institute: stats.cost = 150f; break;
            case CoreBuildingType.Co2Storage: stats.cost = 100f; break;
            case CoreBuildingType.Bank: stats.cost = 80f; break;
            case CoreBuildingType.House: stats.cost = 50f; break;
            case CoreBuildingType.Farm: stats.cost = 30f; break;
        }
        switch (effect.type)
        {
            case CoreBuildingType.PowerPlant: stats.energy = 80f; break;
            case CoreBuildingType.Institute: stats.energy = -20f; break;
            case CoreBuildingType.Co2Storage: stats.energy = -10f; break;
            case CoreBuildingType.House: stats.energy = -5f; break;
            case CoreBuildingType.Bank: stats.energy = -5f; break;
            case CoreBuildingType.Farm: stats.energy = -2f; break;
        }
        return stats;
    }

    void GenerateOneZone(GenerationZone zone, BuildingType specificBuildingType)
    {
        if (zone.originPoint == null) return;
        float halfWidth = zone.width * cellSize * 0.5f;
        float halfHeight = zone.height * cellSize * 0.5f;
        Vector3 centerWorldPos = zone.originPoint.position + new Vector3(halfWidth, 0, halfHeight);
        float snappedX = Mathf.Floor(centerWorldPos.x / cellSize) * cellSize + cellSize * 0.5f;
        float snappedZ = Mathf.Floor(centerWorldPos.z / cellSize) * cellSize + cellSize * 0.5f;
        Vector3 spawnPos = new Vector3(snappedX, zone.originPoint.position.y + buildingYOffset, snappedZ);

        if (specificBuildingType.prefab != null && specificBuildingType.data != null)
        {
            GameObject newBuilding = Instantiate(specificBuildingType.prefab, spawnPos, Quaternion.identity, zone.originPoint);
            AttachAndInitialize(newBuilding, specificBuildingType.data, spawnPos);
        }
    }

    void AttachAndInitialize(GameObject building, Placeable placeableData, Vector3 worldPos)
    {
        if (building.GetComponent<Collider>() == null) building.AddComponent<BoxCollider>();
        PlacedObject placedObj = building.GetComponent<PlacedObject>();
        if (placedObj == null) placedObj = building.AddComponent<PlacedObject>();
        BuildingEffect effect = building.GetComponent<BuildingEffect>();
        if (effect != null) placedObj.buildingEffect = effect;

        if (GameManager.Instance != null && GameManager.Instance.PlacementGrid != null)
        {
            Vector3Int gridPos = GameManager.Instance.PlacementGrid.WorldToCell(worldPos);
            placedObj.Initialize(placeableData, gridPos);
            if (PlacementSystem.Instance != null)
                PlacementSystem.Instance.RegisterExternalObject(building, placeableData, gridPos);
        }
        building.SetActive(true);
    }
}