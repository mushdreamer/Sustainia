using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MultiZoneCityGenerator : MonoBehaviour
{
    public static MultiZoneCityGenerator Instance;

    [System.Serializable]
    public class GenerationZone
    {
        public string zoneName = "Area 1";
        public Transform originPoint;
        public int width = 20;
        public int height = 20;

        // --- 新增：占用标记 ---
        [HideInInspector]
        public bool isOccupied = false;

        // 辅助方法：检查一个世界坐标点是否在这个区域内
        public bool Contains(Vector3 worldPos, float cellSize)
        {
            if (originPoint == null) return false;

            // 将世界坐标转换为相对于原点的局部坐标（以 Grid 数量为单位）
            // 假设 originPoint 在区域的左下角
            float relativeX = (worldPos.x - originPoint.position.x) / cellSize;
            float relativeZ = (worldPos.z - originPoint.position.z) / cellSize;

            // 稍微增加一点容差(epsilon)，防止边缘判定问题
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

    private float _currentCo2 = 0f;
    private float _currentCost = 0f;
    private float _currentEnergy = 0f;

    private string _csvFilePath;
    private int _stepCount = 0;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        Instance = this;
    }

    IEnumerator Start()
    {
        // 等待一帧，确保其他单例初始化
        yield return null;

        _currentCo2 = 0f;
        _currentCost = 0f;
        _currentEnergy = 0f;
        _stepCount = 0;

        // 初始化所有 Zone 为未占用
        foreach (var zone in zones) zone.isOccupied = false;

        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        InitCSV();

        StartCoroutine(GenerateZonesSequence());
    }

    // --- 新增 API：供 PlacementSystem 调用的核心逻辑 ---

    /// <summary>
    /// 检查指定位置所在的 Zone 是否允许建造（即是否存在 Zone 且未被占用）
    /// </summary>
    public bool IsZoneAvailableForBuilding(Vector3 worldPos)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);

        // 如果点不在任何 Zone 里，或者 Zone 已经被占用，则不可建造
        if (zone == null) return false;
        if (zone.isOccupied) return false;

        return true;
    }

    /// <summary>
    /// 更新指定位置所在 Zone 的占用状态
    /// </summary>
    public void SetZoneOccupiedState(Vector3 worldPos, bool isOccupied)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone != null)
        {
            zone.isOccupied = isOccupied;
            // Debug.Log($"Zone '{zone.zoneName}' status changed to: {(isOccupied ? "Occupied" : "Free")}");
        }
    }

    /// <summary>
    /// 根据位置查找对应的 Zone
    /// </summary>
    private GenerationZone GetZoneAtPosition(Vector3 worldPos)
    {
        foreach (var zone in zones)
        {
            if (zone.Contains(worldPos, cellSize))
            {
                return zone;
            }
        }
        return null;
    }
    // ----------------------------------------------------

    void InitCSV()
    {
        string header = "Step,Zone,Building,Total_Co2,Total_Cost,Total_Energy,Count,Weighted_Error";
        File.WriteAllText(_csvFilePath, header + "\n");
    }

    void WriteCSV(int step, string zone, string building, float totalCo2, float totalCost, float totalEnergy, int count, float wError)
    {
        string line = $"{step},{zone},{building},{totalCo2},{totalCost},{totalEnergy},{count},{wError:F4}";
        File.AppendAllText(_csvFilePath, line + "\n");
    }

    IEnumerator GenerateZonesSequence()
    {
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

            // 如果这个 Zone 已经被占用了（不管是之前的循环还是其他原因），跳过
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
                Debug.Log($"<color=green>[Accepted]</color> {candidateType.name} in {zoneToGenerate.zoneName}");

                // 生成建筑并标记占用
                GenerateOneZone(zoneToGenerate, candidateType);
                zoneToGenerate.isOccupied = true; // 标记占用！

                _currentCo2 = nextCo2;
                _currentCost = nextCost;
                _currentEnergy = nextEnergy;
                _stepCount++;

                currentWeightedError = nextWeightedError;
                WriteCSV(_stepCount, zoneToGenerate.zoneName, candidateType.name, _currentCo2, _currentCost, _currentEnergy, _stepCount, currentWeightedError);

                // 该 Zone 已完成，移出列表
                availableIndices.RemoveAt(randomIndex);
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    float CalculateWeightedError(float c, float m, float e)
    {
        float errCo2 = Mathf.Abs(c - targetCo2) / (targetCo2 == 0 ? 1 : Mathf.Abs(targetCo2));
        float errCost = Mathf.Abs(m - targetCost) / (targetCost == 0 ? 1 : Mathf.Abs(targetCost));
        float errEnergy = Mathf.Abs(e - targetEnergy) / (targetEnergy == 0 ? 1 : Mathf.Abs(targetEnergy));
        return (errCo2 * weightCo2) + (errCost * weightCost) + (errEnergy * weightEnergy);
    }

    struct BuildingStats
    {
        public float co2;
        public float cost;
        public float energy;
    }

    BuildingStats GetBuildingStats(GameObject prefab)
    {
        BuildingStats stats = new BuildingStats();
        if (prefab == null) return stats;

        BuildingEffect effect = prefab.GetComponent<BuildingEffect>();
        if (effect == null) return stats;

        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House: stats.co2 = effect.houseCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm: stats.co2 = effect.farmCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute: stats.co2 = effect.instituteCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank: stats.co2 = effect.bankCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant: stats.co2 = effect.powerPlantCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage: stats.co2 = effect.storageCo2Change; break;
        }

        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant: stats.cost = 200f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute: stats.cost = 150f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage: stats.cost = 100f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank: stats.cost = 80f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House: stats.cost = 50f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm: stats.cost = 30f; break;
        }

        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant: stats.energy = 80f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute: stats.energy = -20f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage: stats.energy = -10f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House: stats.energy = -5f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank: stats.energy = -5f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm: stats.energy = -2f; break;
        }

        return stats;
    }

    void GenerateOneZone(GenerationZone zone, BuildingType specificBuildingType)
    {
        // 简单化处理：既然一个 Zone 只有一个建筑，我们直接生成在区域中心附近，
        // 或者保留原本的找空位逻辑，但只要生成成功就结束。

        // 计算区域中心
        Vector3 centerPos = zone.originPoint.position + new Vector3(zone.width * cellSize / 2, 0, zone.height * cellSize / 2);
        // 稍微随机一点偏移，不要死板地在正中心
        float offsetX = Random.Range(-cellSize, cellSize);
        float offsetZ = Random.Range(-cellSize, cellSize);

        Vector3 spawnPos = new Vector3(centerPos.x + offsetX, zone.originPoint.position.y + buildingYOffset, centerPos.z + offsetZ);

        // 简单的对齐到网格
        spawnPos.x = Mathf.Round(spawnPos.x / cellSize) * cellSize;
        spawnPos.z = Mathf.Round(spawnPos.z / cellSize) * cellSize;

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

        Vector3Int gridPos = GameManager.Instance.PlacementGrid.WorldToCell(worldPos);
        placedObj.Initialize(placeableData, gridPos);

        building.SetActive(true);
        // 注意：这里不需要手动调用 SetZoneOccupiedState，因为我们在 GenerateZonesSequence 循环里已经设为 true 了。
        // 但为了保险（手动放置逻辑复用），保持一致性是好的。
    }
}