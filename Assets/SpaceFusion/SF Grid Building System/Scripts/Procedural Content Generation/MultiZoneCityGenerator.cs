using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using System.IO;

public class MultiZoneCityGenerator : MonoBehaviour
{
    [System.Serializable]
    public class GenerationZone { public string zoneName = "Area 1"; public Transform originPoint; public int width = 20; public int height = 20; }

    [System.Serializable]
    public struct BuildingType { public string name; public GameObject prefab; public Placeable data; }

    [Header("Global Settings")]
    public float cellSize = 10.0f;
    public LayerMask roadLayer;
    public List<BuildingType> buildingOptions;
    public float buildingYOffset = 0.0f;

    // ==========================================
    // 核心修改：多目标优化参数 (Multi-Objective Targets)
    // ==========================================
    [Header("Optimization Constraints (The Constraints)")]
    [Tooltip("数量硬约束：最多允许多少个建筑")]
    public int maxBuildingLimit = 20;

    [Header("Optimization Objectives (The Goals)")]
    public float targetCo2 = 50.0f;       // 目标 CO2
    public float targetCost = 1000.0f;    // 目标 预算/成本 (对应 Quiz Q7: Minimize Cost) 
    public float targetEnergy = 100.0f;   // 目标 电力产出 (对应 Quiz Q4: Input - Demand) 

    [Header("Weights (Teaching Trade-offs)")]
    [Tooltip("CO2 目标的权重")]
    public float weightCo2 = 1.0f;
    [Tooltip("成本目标的权重")]
    public float weightCost = 1.0f; // 如果设高，AI会倾向于造便宜的建筑
    [Tooltip("电力目标的权重")]
    public float weightEnergy = 1.5f; // 通常电力是硬指标，权重建议高一点

    // 内部计数器 (Current State)
    private float _currentCo2 = 0f;
    private float _currentCost = 0f;
    private float _currentEnergy = 0f;
    private int _placedCount = 0;

    private string _csvFilePath;
    private int _stepCount = 0;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    void Start()
    {
        _currentCo2 = 0f;
        _currentCost = 0f;
        _currentEnergy = 0f;
        _placedCount = 0;
        _stepCount = 0;

        if (maxBuildingLimit > zones.Count) maxBuildingLimit = zones.Count;

        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        InitCSV();

        StartCoroutine(GenerateZonesSequence());
    }

    void InitCSV()
    {
        // 更新表头：记录所有维度的数值
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

        // 计算初始的加权误差 (Initial Weighted Error)
        float currentWeightedError = CalculateWeightedError(_currentCo2, _currentCost, _currentEnergy);

        while (availableIndices.Count > 0 && _placedCount < maxBuildingLimit && totalAttempts < maxRetries)
        {
            totalAttempts++;

            // 1. 随机选地
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedZoneIndex = availableIndices[randomIndex];
            GenerationZone zoneToGenerate = zones[selectedZoneIndex];

            // 2. 随机提议建筑
            int buildTypeIndex = Random.Range(0, buildingOptions.Count);
            BuildingType candidateType = buildingOptions[buildTypeIndex];

            // 获取该建筑的所有属性 (Co2, Cost, Energy)
            BuildingStats stats = GetBuildingStats(candidateType.prefab);

            // 3. 预测生成后的状态 (State t+1)
            float nextCo2 = _currentCo2 + stats.co2;
            float nextCost = _currentCost + stats.cost;
            float nextEnergy = _currentEnergy + stats.energy;

            // 4. 计算生成后的加权误差
            float nextWeightedError = CalculateWeightedError(nextCo2, nextCost, nextEnergy);

            // Debug 日志：让学生看到数值变化
            // Debug.Log($"尝试: {candidateType.name}. 误差变化: {currentWeightedError:F3} -> {nextWeightedError:F3}");

            // 5. 决策逻辑：多维度的贪婪算法
            // 只要总的加权误差变小，就接受！
            if (nextWeightedError < currentWeightedError)
            {
                Debug.Log($"<color=green>[Accepted]</color> {candidateType.name} 优化了整体目标！\n" +
                          $"CO2: {_currentCo2}->{nextCo2} | Cost: {_currentCost}->{nextCost} | Energy: {_currentEnergy}->{nextEnergy}");

                GenerateOneZone(zoneToGenerate, candidateType);

                // 更新状态
                _currentCo2 = nextCo2;
                _currentCost = nextCost;
                _currentEnergy = nextEnergy;
                _placedCount++;
                _stepCount++;

                // 更新误差基准
                currentWeightedError = nextWeightedError;

                WriteCSV(_stepCount, zoneToGenerate.zoneName, candidateType.name, _currentCo2, _currentCost, _currentEnergy, _placedCount, currentWeightedError);

                availableIndices.RemoveAt(randomIndex);
            }
            else
            {
                // 即使拒绝，也稍微打印一下原因，方便教学分析
                // 比如：虽然 CO2 降了，但是 Cost 超太多，导致总误差变大
                // Debug.Log($"<color=red>[Rejected]</color> {candidateType.name} 导致偏离目标 (可能是太贵或电力溢出)。");
            }

            yield return new WaitForSeconds(0.05f); // 稍微快一点
        }

        Debug.Log($"[Finish] 最终结果: Co2({_currentCo2}/{targetCo2}), Cost({_currentCost}/{targetCost}), Energy({_currentEnergy}/{targetEnergy})");
    }

    // ==========================================
    // 核心数学方法：计算加权误差 (The Objective Function)
    // ==========================================
    float CalculateWeightedError(float c, float m, float e)
    {
        // 归一化误差 (Normalized Error): |Current - Target| / Target
        // 这样可以让 Cost(1000) 和 Co2(50) 在同一个量级上比较

        float errCo2 = Mathf.Abs(c - targetCo2) / (targetCo2 == 0 ? 1 : Mathf.Abs(targetCo2));
        float errCost = Mathf.Abs(m - targetCost) / (targetCost == 0 ? 1 : Mathf.Abs(targetCost));
        float errEnergy = Mathf.Abs(e - targetEnergy) / (targetEnergy == 0 ? 1 : Mathf.Abs(targetEnergy));

        // 加权求和
        return (errCo2 * weightCo2) + (errCost * weightCost) + (errEnergy * weightEnergy);
    }

    // ==========================================
    // 数据结构与辅助方法
    // ==========================================

    // 一个临时的结构体，用来一次性返回建筑的三个属性
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

        // 1. 获取 CO2 (保持之前的逻辑)
        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House: stats.co2 = effect.houseCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm: stats.co2 = effect.farmCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute: stats.co2 = effect.instituteCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank: stats.co2 = effect.bankCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant: stats.co2 = effect.powerPlantCo2Change; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage: stats.co2 = effect.storageCo2Change; break;
        }

        // 2. 获取 Cost (你需要确保 BuildingEffect 里有 cost 变量，这里我先模拟一下)
        // 假设: 发电厂很贵，房子便宜，Co2Storage 中等
        // 如果你的脚本里有 public float buildingCost，请直接替换下面的模拟值
        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant: stats.cost = 200f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute: stats.cost = 150f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage: stats.cost = 100f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank: stats.cost = 80f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House: stats.cost = 50f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm: stats.cost = 30f; break;
        }

        // 3. 获取 Energy (正数产电，负数耗电)
        // 对应 Quiz 中的 Supply - Demand 
        // 如果你的脚本里有 electricityGeneration/Consumption，请替换
        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant: stats.energy = 80f; break; // 产电大户
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute: stats.energy = -20f; break; // 耗电大户
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage: stats.energy = -10f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House: stats.energy = -5f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank: stats.energy = -5f; break;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm: stats.energy = -2f; break;
        }

        return stats;
    }

    void GenerateOneZone(GenerationZone zone, BuildingType specificBuildingType)
    {
        // ... (这部分代码保持你原本的 Raycast 和 Instantiate 逻辑不变) ...
        // ... 请直接复制之前版本中的 GenerateOneZone 和 AttachAndInitialize ...
        int[,] mapData = new int[zone.width, zone.height];
        Vector3 startPos = zone.originPoint.position;
        for (int x = 0; x < zone.width; x++)
        {
            for (int z = 0; z < zone.height; z++)
            {
                float worldX = startPos.x + x * cellSize + cellSize * 0.5f;
                float worldZ = startPos.z + z * cellSize + cellSize * 0.5f;
                Vector3 rayOrigin = new Vector3(worldX, 100f, worldZ);
                if (Physics.Raycast(rayOrigin, Vector3.down, 200f, roadLayer)) mapData[x, z] = 1;
                else mapData[x, z] = 0;
            }
        }
        bool[,] visited = new bool[zone.width, zone.height];
        for (int x = 0; x < zone.width; x++)
        {
            for (int z = 0; z < zone.height; z++)
            {
                if (mapData[x, z] == 1 || visited[x, z]) continue;
                int blockW = 0;
                while ((x + blockW) < zone.width && mapData[x + blockW, z] == 0) blockW++;
                int blockH = 0;
                while ((z + blockH) < zone.height && mapData[x, z + blockH] == 0) blockH++;
                for (int i = 0; i < blockW; i++) for (int j = 0; j < blockH; j++) visited[x + i, z + j] = true;
                float centerXIndex = x + blockW / 2.0f;
                float centerZIndex = z + blockH / 2.0f;
                float worldX = startPos.x + centerXIndex * cellSize;
                float worldZ = startPos.z + centerZIndex * cellSize;
                Vector3 spawnPos = new Vector3(worldX, startPos.y + buildingYOffset, worldZ);
                if (specificBuildingType.prefab != null && specificBuildingType.data != null)
                {
                    GameObject newBuilding = Instantiate(specificBuildingType.prefab, spawnPos, Quaternion.identity, zone.originPoint);
                    AttachAndInitialize(newBuilding, specificBuildingType.data, spawnPos);
                }
            }
        }
    }

    void AttachAndInitialize(GameObject building, Placeable placeableData, Vector3 worldPos)
    {
        if (building.GetComponent<Collider>() == null) building.AddComponent<BoxCollider>();
        PlacedObject placedObj = building.GetComponent<PlacedObject>();
        if (placedObj == null) placedObj = building.AddComponent<PlacedObject>();
        BuildingEffect effect = building.GetComponent<BuildingEffect>();
        if (effect != null) placedObj.buildingEffect = effect;
        Vector3Int gridPos = new Vector3Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y), Mathf.RoundToInt(worldPos.z));
        placedObj.Initialize(placeableData, gridPos);
        building.SetActive(true);
    }
}