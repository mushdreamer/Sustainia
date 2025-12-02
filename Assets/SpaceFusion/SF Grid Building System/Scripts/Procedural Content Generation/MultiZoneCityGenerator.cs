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

    [Header("Optimization Goals")]
    public float co2Target = 50.0f;
    public float targetTolerance = 1.0f;

    [Tooltip("最大建筑数量限制 (Constraint: Count <= Limit)")]
    public int maxBuildingLimit = 20;

    private float _currentTotalCo2 = 0f;
    private int _placedCount = 0;

    private string _csvFilePath;
    private int _stepCount = 0;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    void Start()
    {
        _currentTotalCo2 = 0f;
        _placedCount = 0;
        _stepCount = 0;

        // 安全检查：Limit 不能超过实际地块数
        if (maxBuildingLimit > zones.Count) maxBuildingLimit = zones.Count;

        Debug.Log($"[Init] 目标 Co2: {co2Target}, 数量限制: <={maxBuildingLimit}");

        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        InitCSV();

        StartCoroutine(GenerateZonesSequence());
    }

    void InitCSV()
    {
        // 表头去掉了 Avg_Needed，因为不再需要那个逻辑了
        string header = "Step,ZoneName,BuildingName,Added_Co2,Total_Co2,Count_Ratio,Error_Distance";
        File.WriteAllText(_csvFilePath, header + "\n");
    }

    void WriteCSV(int step, string zone, string building, float added, float total, float ratio, float error)
    {
        string line = $"{step},{zone},{building},{added},{total},{ratio:F2},{error}";
        File.AppendAllText(_csvFilePath, line + "\n");
    }

    IEnumerator GenerateZonesSequence()
    {
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < zones.Count; i++) availableIndices.Add(i);

        // 重试机制：防止随机不到合适的建筑导致死循环
        int maxRetries = 200;
        int totalAttempts = 0;

        // 记录初始误差
        float currentError = Mathf.Abs(_currentTotalCo2 - co2Target);

        // 循环条件：
        // 1. 还有空地
        // 2. 没超过数量限制
        // 3. 没超过最大尝试次数
        // 4. 【重要】误差还没达标 (如果达标了就直接停，省地皮！)
        while (availableIndices.Count > 0 &&
               _placedCount < maxBuildingLimit &&
               totalAttempts < maxRetries &&
               currentError > targetTolerance)
        {
            totalAttempts++;

            // 1. 随机选地
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedZoneIndex = availableIndices[randomIndex];
            GenerationZone zoneToGenerate = zones[selectedZoneIndex];

            // 2. 随机提议
            int buildTypeIndex = Random.Range(0, buildingOptions.Count);
            BuildingType candidateType = buildingOptions[buildTypeIndex];
            float proposedEmission = GetBuildingCo2FromPrefab(candidateType.prefab);

            // 3. 计算新误差
            // 逻辑：加上这个建筑后，是不是离目标更近了？
            float newError = Mathf.Abs((_currentTotalCo2 + proposedEmission) - co2Target);

            Debug.Log($"[Attempt {totalAttempts}] 当前误差: {currentError:F2}. 提议: {candidateType.name}({proposedEmission}). 预期误差: {newError:F2}");

            // 4. 决策逻辑：只有能减小误差（或保持不变），就接受！
            // 这就是“梯度下降”：只要往坑底走，不管步子跨多大，都走。
            if (newError < currentError)
            {
                Debug.Log($"<color=green>[Accepted]</color> 误差减小 ({currentError:F2} -> {newError:F2})，生成！");

                GenerateOneZone(zoneToGenerate, candidateType);

                _currentTotalCo2 += proposedEmission;
                _placedCount++;
                _stepCount++;

                // 更新当前误差
                currentError = newError;
                float ratio = (float)_placedCount / maxBuildingLimit;

                WriteCSV(_stepCount, zoneToGenerate.zoneName, candidateType.name, proposedEmission, _currentTotalCo2, ratio, currentError);

                // 成功生成后，移除该地块索引（这块地用掉了）
                availableIndices.RemoveAt(randomIndex);
            }
            else
            {
                Debug.Log($"<color=red>[Rejected]</color> 误差会变大 ({newError:F2})，不合适。换个建筑再试。");
                // 失败时不移除索引！保留这块地给下次机会。
            }

            yield return new WaitForSeconds(0.1f);
        }

        // --- 最终结算 ---
        if (currentError <= targetTolerance)
        {
            Debug.Log($"<color=green>[Success]</color> 任务完成！\n" +
                      $"最终误差: {currentError}\n" +
                      $"消耗地块: {_placedCount} (限制 {maxBuildingLimit})\n" +
                      $"优化评价: 极佳 (剩余名额 {maxBuildingLimit - _placedCount})");
        }
        else
        {
            Debug.Log($"<color=red>[Failed]</color> 未能达成目标。\n" +
                      $"原因可能是：尝试次数耗尽、地块用完、或者无法凑出精确数值。\n" +
                      $"最终误差: {currentError}, 已用数量: {_placedCount}");
        }
    }

    // --- 辅助方法保持不变 ---
    float GetBuildingCo2FromPrefab(GameObject prefab)
    {
        if (prefab == null) return 0f;
        BuildingEffect effect = prefab.GetComponent<BuildingEffect>();
        if (effect == null) return 0f;
        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House: return effect.houseCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm: return effect.farmCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute: return effect.instituteCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank: return effect.bankCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant: return effect.powerPlantCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage: return effect.storageCo2Change;
            default: return 0f;
        }
    }

    void GenerateOneZone(GenerationZone zone, BuildingType specificBuildingType)
    {
        // ... (保持原有的 GenerateOneZone 和 AttachAndInitialize 代码完全不变) ...
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