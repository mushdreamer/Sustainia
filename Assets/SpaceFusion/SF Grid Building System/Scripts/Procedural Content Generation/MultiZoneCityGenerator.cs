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

        [HideInInspector]
        public bool isOccupied = false;

        public bool Contains(Vector3 worldPos, float cellSize)
        {
            if (originPoint == null) return false;
            float relativeX = (worldPos.x - originPoint.position.x) / cellSize;
            float relativeZ = (worldPos.z - originPoint.position.z) / cellSize;
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
        if (Instance != null) { Destroy(gameObject); }
        Instance = this;
    }

    IEnumerator Start()
    {
        yield return null;

        if (PlacementSystem.Instance == null)
        {
            Debug.LogError("PlacementSystem 尚未初始化！");
            yield break;
        }

        // 初始化 CSV 也可以放在这里
        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        InitCSV();

        // 开始默认的生成流程
        StartCoroutine(GenerateZonesSequence());
    }

    // --- 新增：允许外部调用来重置并重新生成 ---
    public void ClearAndRestartGeneration()
    {
        // 1. 停止当前正在进行的任何生成协程
        StopAllCoroutines();

        // 2. 清理场景中已生成的建筑
        foreach (var zone in zones)
        {
            zone.isOccupied = false;
            // 销毁 Zone 原点下的所有子物体（即之前的建筑）
            if (zone.originPoint != null)
            {
                foreach (Transform child in zone.originPoint)
                {
                    // 注意：这里直接Destroy可能会有延迟，如果需要立即清理可能要用DestroyImmediate，
                    // 但运行时一般建议用Destroy。为了防止ResourceManager里的数据残留，
                    // 最好确保BuildingEffect的OnDestroy能正确处理反注册。
                    Destroy(child.gameObject);
                }
            }
        }

        // 3. 强制 ResourceManager 清理数据（可选，视你的架构而定）
        // 如果 BuildingEffect 的 OnDestroy 已经处理了 Unregister，这里可能不需要手动调用。
        // 但为了保险，可以重置 ResourceManager 的部分计数。

        // 4. 重置内部计数器
        _currentCo2 = 0f;
        _currentCost = 0f;
        _currentEnergy = 0f;
        _stepCount = 0;

        Debug.Log("[Generator] 场景已清理，准备重新生成...");

        // 5. 重新启动生成序列
        StartCoroutine(GenerateZonesSequence());
    }

    // --- API ---
    public bool IsZoneAvailableForBuilding(Vector3 worldPos)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone == null) return false;
        if (zone.isOccupied) return false;
        return true;
    }

    public void SetZoneOccupiedState(Vector3 worldPos, bool isOccupied)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone != null) zone.isOccupied = isOccupied;
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
            Gizmos.color = zone.isOccupied ? new Color(1, 0, 0, 0.5f) : new Color(0, 1, 0, 0.5f);
            float realWidth = zone.width * cellSize;
            float realHeight = zone.height * cellSize;
            Vector3 center = zone.originPoint.position + new Vector3(realWidth / 2, 0, realHeight / 2);
            Vector3 size = new Vector3(realWidth, 1f, realHeight);
            Gizmos.DrawCube(center, new Vector3(realWidth, 0.1f, realHeight));
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(center, 0.5f);
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
        // 稍微等待一帧，确保Destroy操作完成，避免位置冲突
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
                // Debug.Log($"<color=green>[Accepted]</color> {candidateType.name} in {zoneToGenerate.zoneName}");

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