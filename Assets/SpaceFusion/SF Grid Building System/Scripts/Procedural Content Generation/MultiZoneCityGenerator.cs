using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using System.IO;

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

        public bool isOccupied = false;

        // --- 视觉引用 ---
        [HideInInspector] public LineRenderer groundOutline; // 地面线框
        [HideInInspector] public TextMesh statusText;        // 头顶文字
        [HideInInspector] public Transform arrowObj;         // 跳动箭头
        [HideInInspector] public Material instanceLineMat;   // 线条材质实例

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

    [Header("Visual Feedback (Holographic UI)")]
    [Tooltip("空闲区域颜色（建议亮绿色）")]
    public Color validColor = Color.green;
    [Tooltip("占用区域颜色（建议亮橙色）")]
    public Color occupiedColor = new Color(1f, 0.6f, 0f); // Orange

    [Tooltip("线框宽度")]
    public float lineWidth = 0.5f;
    [Tooltip("文字和箭头的高度")]
    public float uiHeight = 12.0f;
    [Tooltip("动画速度")]
    public float animSpeed = 2.0f;

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
        // 1. 初始化区域视觉效果
        InitZoneVisuals();

        yield return null;

        if (PlacementSystem.Instance == null)
        {
            Debug.LogError("PlacementSystem 尚未初始化！");
            yield break;
        }

        // 初始化 CSV
        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        InitCSV();

        // 开始默认的生成流程
        StartCoroutine(GenerateZonesSequence());
    }

    private void Update()
    {
        AnimateVisuals();
    }

    // --- 核心视觉逻辑：动画与渲染 ---

    private void AnimateVisuals()
    {
        if (zones == null) return;

        // 动画参数
        float bounceY = Mathf.Sin(Time.time * animSpeed) * 1.5f; // 上下跳动幅度
        float rotateAngle = Time.time * 90f; // 旋转速度

        // 颜色呼吸 (在 0.5 到 1.0 之间波动，保持高亮)
        float emissionMult = 0.8f + Mathf.PingPong(Time.time, 0.4f);

        foreach (var zone in zones)
        {
            // 确定当前目标颜色
            Color targetColor = zone.isOccupied ? occupiedColor : validColor;

            // 1. 箭头动画：旋转 + 跳动
            if (zone.arrowObj != null)
            {
                // 箭头始终保持在中心点上方一定高度 + 跳动偏移
                Vector3 center = GetZoneCenter(zone);
                zone.arrowObj.position = center + Vector3.up * (uiHeight + bounceY);
                zone.arrowObj.rotation = Quaternion.Euler(0, rotateAngle, 180); // 180度翻转让圆锥尖端朝下

                // 更新箭头颜色
                var renderers = zone.arrowObj.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.material.color = targetColor;
            }

            // 2. 文字朝向摄像机
            if (zone.statusText != null)
            {
                if (Camera.main != null)
                {
                    // 让文字始终正对摄像机
                    zone.statusText.transform.rotation = Quaternion.LookRotation(zone.statusText.transform.position - Camera.main.transform.position);
                }
                zone.statusText.color = targetColor;
                zone.statusText.text = zone.isOccupied ? "EDITABLE\nBUILDING" : "OPEN\nSLOT";
            }

            // 3. 线框颜色更新
            if (zone.instanceLineMat != null)
            {
                // 使用 SetColor 确保自发光
                zone.instanceLineMat.color = targetColor;
                zone.instanceLineMat.SetColor("_EmissionColor", targetColor * emissionMult);
            }
        }
    }

    private void InitZoneVisuals()
    {
        foreach (var zone in zones)
        {
            if (zone.originPoint == null) continue;

            // 清理旧物体
            if (zone.groundOutline != null) Destroy(zone.groundOutline.gameObject);
            if (zone.statusText != null) Destroy(zone.statusText.gameObject);
            if (zone.arrowObj != null) Destroy(zone.arrowObj.gameObject);

            // 计算区域中心和尺寸
            float realWidth = zone.width * cellSize;
            float realHeight = zone.height * cellSize;
            Vector3 centerPos = GetZoneCenter(zone);

            // 创建容器
            GameObject container = new GameObject($"{zone.zoneName}_Visuals");
            container.transform.SetParent(this.transform);

            // --- A. 创建地面线框 (LineRenderer) ---
            GameObject lineObj = new GameObject("Outline");
            lineObj.transform.SetParent(container.transform);
            lineObj.transform.position = zone.originPoint.position; // 局部坐标系原点

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; // 跟随物体移动
            lr.loop = true; // 闭环
            lr.positionCount = 4;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default")); // 使用Sprite Shader，无视光照，永远高亮
            zone.instanceLineMat = lr.material;
            zone.groundOutline = lr;

            // 设置四个角的位置 (稍微抬高 y=0.5 防止穿模)
            float yOffset = 0.5f;
            lr.SetPosition(0, new Vector3(0, yOffset, 0));
            lr.SetPosition(1, new Vector3(realWidth, yOffset, 0));
            lr.SetPosition(2, new Vector3(realWidth, yOffset, realHeight));
            lr.SetPosition(3, new Vector3(0, yOffset, realHeight));

            // --- B. 创建浮动文字 (TextMesh) ---
            GameObject textObj = new GameObject("StatusLabel");
            textObj.transform.SetParent(container.transform);
            textObj.transform.position = centerPos + Vector3.up * (uiHeight - 2.0f); // 比箭头低一点

            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = "Initializing...";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.5f;
            tm.fontSize = 20;
            tm.fontStyle = FontStyle.Bold;
            zone.statusText = tm;

            // --- C. 创建跳动箭头 (Cone Primitive) ---
            // 既然没有美术资源，我们用 Unity 的 Cylinder 捏一个简单的形状
            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrow.name = "ArrowIndicator";
            arrow.transform.SetParent(container.transform);
            Destroy(arrow.GetComponent<Collider>()); // 移除碰撞

            // 把它捏成尖的 (圆锥体效果不好模拟，直接用细长的圆柱或者倒金字塔)
            // 这里我们用一个简单的方块旋转 45 度，看起来像菱形水晶，这很常见
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(container.transform);
            marker.transform.localScale = new Vector3(2f, 2f, 2f);
            zone.arrowObj = marker.transform;

            // 销毁临时的 arrow，改用 marker
            Destroy(arrow);

            // 给 marker 设置无光照材质
            Renderer markerRend = marker.GetComponent<Renderer>();
            markerRend.material = new Material(Shader.Find("Sprites/Default")); // 同样使用高亮材质
        }
    }

    private Vector3 GetZoneCenter(GenerationZone zone)
    {
        float realWidth = zone.width * cellSize;
        float realHeight = zone.height * cellSize;
        return zone.originPoint.position + new Vector3(realWidth / 2f, 0, realHeight / 2f);
    }

    // ----------------------

    // --- 外部调用接口 ---
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

        // 重新初始化视觉效果
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

    // 兼容旧接口
    public void SetZoneOccupiedState(Vector3 worldPos, bool isOccupied)
    {
        SetZoneOccupiedStatus(worldPos, isOccupied);
    }

    public void SetZoneOccupiedStatus(Vector3 worldPos, bool occupied)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone != null)
        {
            zone.isOccupied = occupied;
            // Visual update happens automatically in Update loop
            Debug.Log($"[Generator] Zone '{zone.zoneName}' status updated: Occupied = {occupied}");
        }
    }

    // --- 内部辅助 ---

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

    private void OnDrawGizmos()
    {
        if (zones == null) return;

        foreach (var zone in zones)
        {
            if (zone.originPoint == null) continue;
            // Gizmos Draw ...
            Gizmos.color = zone.isOccupied ? occupiedColor : validColor;
            float realWidth = zone.width * cellSize;
            float realHeight = zone.height * cellSize;
            Vector3 center = zone.originPoint.position + new Vector3(realWidth / 2, 0, realHeight / 2);
            Vector3 size = new Vector3(realWidth, 1f, realHeight);
            Gizmos.DrawWireCube(center, size);
        }
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