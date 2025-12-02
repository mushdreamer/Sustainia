using UnityEngine;
using System.Collections; // --- 修改点1：必须引入这个命名空间以使用协程 (IEnumerator) ---
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;

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

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    // --- 修改点2：将 Start 中的直接循环替换为启动协程 ---
    // 我删除了原来 Start 方法中的 foreach 循环，因为那个循环会在游戏开始的一瞬间
    // 把所有区域都生成出来，无法实现你想要的“等待5秒”的效果。
    void Start()
    {
        // 启动我们自定义的序列生成协程
        StartCoroutine(GenerateZonesSequence());
    }

    // --- 修改点3：新增的协程方法，用于控制生成的时间和顺序 ---
    IEnumerator GenerateZonesSequence()
    {
        // 1. 创建一个包含所有索引的列表 (0 到 24)
        // 这样我们可以从中随机“抓取”一个索引，保证不重复
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < zones.Count; i++)
        {
            availableIndices.Add(i);
        }

        // 2. 只要列表里还有剩余的索引，就继续循环
        while (availableIndices.Count > 0)
        {
            // 随机挑选一个幸运儿索引
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedZoneIndex = availableIndices[randomIndex];

            // 获取对应的 Zone 配置
            GenerationZone zoneToGenerate = zones[selectedZoneIndex];

            // 3. 执行生成逻辑（只针对这一个 Zone）
            if (zoneToGenerate.originPoint != null)
            {
                // 这里我们稍微修改一下 Debug 信息，方便你看到进度
                Debug.Log($"[CityGen] 开始生成区域: {zoneToGenerate.zoneName} (索引: {selectedZoneIndex})");
                GenerateOneZone(zoneToGenerate);
            }

            // 4. 重要：从备选列表中移除这个索引，确保不会再次选中它
            availableIndices.RemoveAt(randomIndex);

            // 5. 等待 5 秒后再进行下一次循环
            // 如果你希望第一个建筑不需要等待直接生成，可以把这就话移到循环开头或加个判断
            yield return new WaitForSeconds(5.0f);
        }

        Debug.Log("[CityGen] 所有区域生成完毕！");
    }

    void GenerateOneZone(GenerationZone zone)
    {
        int[,] mapData = new int[zone.width, zone.height];
        Vector3 startPos = zone.originPoint.position;

        // --- 扫描阶段 ---
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

        // --- 生成阶段 ---
        bool[,] visited = new bool[zone.width, zone.height];

        if (buildingOptions == null || buildingOptions.Count == 0) return;

        for (int x = 0; x < zone.width; x++)
        {
            for (int z = 0; z < zone.height; z++)
            {
                if (mapData[x, z] == 1 || visited[x, z]) continue;

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

                int randomIndex = Random.Range(0, buildingOptions.Count);
                BuildingType selectedType = buildingOptions[randomIndex];

                if (selectedType.prefab != null && selectedType.data != null)
                {
                    GameObject newBuilding = Instantiate(selectedType.prefab, spawnPos, Quaternion.identity, zone.originPoint);
                    AttachAndInitialize(newBuilding, selectedType.data, spawnPos);
                }
            }
        }
    }

    void AttachAndInitialize(GameObject building, Placeable placeableData, Vector3 worldPos)
    {
        if (building.GetComponent<Collider>() == null)
        {
            building.AddComponent<BoxCollider>();
        }

        PlacedObject placedObj = building.GetComponent<PlacedObject>();
        if (placedObj == null)
        {
            placedObj = building.AddComponent<PlacedObject>();
        }

        BuildingEffect effect = building.GetComponent<BuildingEffect>();
        if (effect != null)
        {
            placedObj.buildingEffect = effect;
        }

        Vector3Int gridPos = new Vector3Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y), Mathf.RoundToInt(worldPos.z));
        placedObj.Initialize(placeableData, gridPos);

        building.SetActive(true);
    }

    void OnDrawGizmos()
    {
        if (zones == null) return;
        Gizmos.color = Color.yellow;
        foreach (var zone in zones)
        {
            if (zone.originPoint != null)
            {
                Vector3 center = zone.originPoint.position +
                                 new Vector3(zone.width * cellSize * 0.5f, 0, zone.height * cellSize * 0.5f);
                Vector3 size = new Vector3(zone.width * cellSize, 1f, zone.height * cellSize);
                Gizmos.DrawWireCube(center, size);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(zone.originPoint.position, 1f);
                Gizmos.color = Color.yellow;
            }
        }
    }
}