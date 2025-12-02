using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using System.IO; // <<< 1. 引入 IO 用于写 CSV >>>

public class MultiZoneCityGenerator : MonoBehaviour
{
    [System.Serializable]
    public class GenerationZone
    {
        public string zoneName = "Area 1";
        public Transform originPoint;
        public int width = 20;
        public int height = 20;
    }

    [System.Serializable]
    public struct BuildingType
    {
        public string name;
        public GameObject prefab;
        public Placeable data;
    }

    [Header("Global Settings")]
    public float cellSize = 10.0f;
    public LayerMask roadLayer;
    public List<BuildingType> buildingOptions;
    public float buildingYOffset = 0.0f;

    [Header("Optimization Goals")]
    [Tooltip("我们希望城市达到的目标 CO2 排放总量")]
    public float co2Target = 50.0f;

    // <<< 2. 新增容忍度，只要误差小于这个值就视为达成目标 >>>
    [Tooltip("误差容忍度 (当 Error 小于此值时停止生成)")]
    public float targetTolerance = 1.0f;

    // 用于记录当前生成过程中累积的 CO2 总量 (用于比较)
    private float _currentTotalCo2 = 0f;

    // <<< 3. CSV 相关变量 >>>
    private string _csvFilePath;
    private int _stepCount = 0;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    void Start()
    {
        // 游戏开始前，重置累积计数
        _currentTotalCo2 = 0f;
        _stepCount = 0; // 重置步数

        // <<< 4. 初始化 CSV 文件路径 (放在 Assets 文件夹同级) >>>
        _csvFilePath = Path.Combine(Application.dataPath, $"TrainingData_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");

        StartCoroutine(GenerateZonesSequence());
    }

    // <<< 5. 辅助方法：写 CSV 表头 >>>
    void InitCSV()
    {
        // 写入表头：步数, 区域, 建筑, 当前总排放, 距离目标的误差(Error)
        string header = "Step,ZoneName,BuildingName,CurrentTotalCo2,ErrorDistance";
        File.WriteAllText(_csvFilePath, header + "\n");
        Debug.Log($"[CSV] 文件已创建: {_csvFilePath}");
    }

    // <<< 6. 辅助方法：追加 CSV 数据 >>>
    void WriteCSV(int step, string zone, string building, float total, float error)
    {
        string line = $"{step},{zone},{building},{total},{error}";
        File.AppendAllText(_csvFilePath, line + "\n");
    }

    IEnumerator GenerateZonesSequence()
    {
        // 0. 先写入表头
        InitCSV();

        // 1. 准备随机索引列表
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < zones.Count; i++)
        {
            availableIndices.Add(i);
        }

        Debug.Log($"[CityGen] 开始生成序列。目标 CO2: {co2Target}");

        // 记录初始误差
        float currentError = Mathf.Abs(_currentTotalCo2 - co2Target);

        // 2. 循环生成区域
        while (availableIndices.Count > 0)
        {
            // --- 步骤 A: 选择一个 Zone ---
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedZoneIndex = availableIndices[randomIndex];
            GenerationZone zoneToGenerate = zones[selectedZoneIndex];

            // --- 步骤 B: 提议一个建筑 (Proposed Building) ---
            // 随机选一个建筑类型作为“候选”
            int buildTypeIndex = Random.Range(0, buildingOptions.Count);
            BuildingType candidateType = buildingOptions[buildTypeIndex];

            // --- 步骤 C: 获取它的预测排放量 (Proposed Co2 Emission) ---
            float proposedEmission = GetBuildingCo2FromPrefab(candidateType.prefab);

            // --- 步骤 D: 计算比较 (The Calculation) ---
            // 现在的误差
            // float currentError = Mathf.Abs(_currentTotalCo2 - co2Target); // (移到了循环外更新，或者在下面更新)

            // 如果加上这个建筑后的误差
            float newError = Mathf.Abs((_currentTotalCo2 + proposedEmission) - co2Target);

            Debug.Log($"[CityGen] 尝试在 {zoneToGenerate.zoneName} 生成 {candidateType.name}...\n" +
                      $"当前总排放: {_currentTotalCo2}, 目标: {co2Target}\n" +
                      $"预测增量: {proposedEmission}\n" +
                      $"当前误差: {currentError:F2} vs 新误差: {newError:F2}");

            // --- 步骤 E: 决策 (Decision Rule) ---
            // 只有当新误差 < 当前误差 (或者这是第一个建筑，且误差肯定会变小) 时才生成
            if (newError < currentError || _currentTotalCo2 == 0)
            {
                Debug.Log($"<color=green>[Accepted]</color> 方案通过！生成建筑。");

                // 执行生成，并传入指定的建筑类型
                GenerateOneZone(zoneToGenerate, candidateType);

                // 更新累积值
                _currentTotalCo2 += proposedEmission;

                // 更新当前误差
                currentError = newError;

                // <<< 7. 记录数据到 CSV >>>
                _stepCount++;
                WriteCSV(_stepCount, zoneToGenerate.zoneName, candidateType.name, _currentTotalCo2, currentError);

                // <<< 8. 功能：如果达到目标，停止生成 >>>
                if (currentError <= targetTolerance)
                {
                    Debug.Log($"<color=green>[Success]</color> 已达到目标 CO2 (误差 {currentError} <= {targetTolerance})！停止生成。");
                    break; // 跳出循环
                }
            }
            else
            {
                Debug.Log($"<color=red>[Rejected]</color> 方案拒绝！这会让排放量偏离目标。该区域将保持空置 (或跳过)。");
                // 在这里我们选择“跳过”该区域的生成，即该区域为空。
            }

            // 移除已处理的区域索引
            availableIndices.RemoveAt(randomIndex);

            yield return new WaitForSeconds(2.0f); // 稍微加快一点演示速度
        }

        // <<< 9. 功能：循环结束后的失败判定 >>>
        // 如果循环结束了（availableIndices 为空），但误差依然大于容忍度，说明没能达成目标
        if (currentError > targetTolerance)
        {
            Debug.Log($"<color=red>[Failed]</color> 任务失败！所有区域已处理完毕，但未能达到目标 CO2。\n" +
                      $"最终误差: {currentError}, 目标: {co2Target}");
        }
        else
        {
            Debug.Log($"[CityGen] 生成流程结束。最终模拟计算总 CO2: {_currentTotalCo2}");
        }
    }

    // --- 辅助方法：从 Prefab 读取数据而不实例化 --- (完全保持原样)
    float GetBuildingCo2FromPrefab(GameObject prefab)
    {
        if (prefab == null) return 0f;

        BuildingEffect effect = prefab.GetComponent<BuildingEffect>();
        if (effect == null) return 0f;

        // 根据类型读取你在 BuildingEffect 中定义的对应变量
        switch (effect.type)
        {
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.House:
                return effect.houseCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Farm:
                return effect.farmCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Institute:
                return effect.instituteCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Bank:
                return effect.bankCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.PowerPlant:
                return effect.powerPlantCo2Change;
            case SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType.Co2Storage:
                return effect.storageCo2Change; // 这是负数
            default:
                return 0f;
        }
    }

    // --- 修改后的生成方法，接收特定的 BuildingType --- (完全保持原样)
    void GenerateOneZone(GenerationZone zone, BuildingType specificBuildingType)
    {
        int[,] mapData = new int[zone.width, zone.height];
        Vector3 startPos = zone.originPoint.position;

        // 扫描地形 (保持不变)
        for (int x = 0; x < zone.width; x++)
        {
            for (int z = 0; z < zone.height; z++)
            {
                float worldX = startPos.x + x * cellSize + cellSize * 0.5f;
                float worldZ = startPos.z + z * cellSize + cellSize * 0.5f;
                Vector3 rayOrigin = new Vector3(worldX, 100f, worldZ);
                if (Physics.Raycast(rayOrigin, Vector3.down, 200f, roadLayer))
                    mapData[x, z] = 1;
                else
                    mapData[x, z] = 0;
            }
        }

        bool[,] visited = new bool[zone.width, zone.height];

        // 遍历格子生成建筑
        for (int x = 0; x < zone.width; x++)
        {
            for (int z = 0; z < zone.height; z++)
            {
                if (mapData[x, z] == 1 || visited[x, z]) continue;

                // 简单的分块算法 (保持不变)
                int blockW = 0;
                while ((x + blockW) < zone.width && mapData[x + blockW, z] == 0) blockW++;
                int blockH = 0;
                while ((z + blockH) < zone.height && mapData[x, z + blockH] == 0) blockH++;

                for (int i = 0; i < blockW; i++)
                    for (int j = 0; j < blockH; j++)
                        visited[x + i, z + j] = true;

                float centerXIndex = x + blockW / 2.0f;
                float centerZIndex = z + blockH / 2.0f;
                float worldX = startPos.x + centerXIndex * cellSize;
                float worldZ = startPos.z + centerZIndex * cellSize;

                Vector3 spawnPos = new Vector3(worldX, startPos.y + buildingYOffset, worldZ);

                // <<< --- 修改: 不再随机，而是使用传入的 specificBuildingType ---
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
        // 保持不变
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