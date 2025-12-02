using UnityEngine;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables; // 必须引入这个以访问 Placeable

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

    // --- 修改点1：定义一个结构体，把 Prefab 和它的数据文件绑定在一起 ---
    [System.Serializable]
    public struct BuildingType
    {
        public string name; // 方便你在Inspector里看
        public GameObject prefab; // 拖入建筑的Prefab
        public Placeable data;    // 【关键】拖入对应的 Placeable (.asset) 文件
    }

    [Header("Global Settings")]
    public float cellSize = 10.0f;
    public LayerMask roadLayer;

    // --- 修改点2：这里不再是 GameObject[]，而是我们定义的结构体数组 ---
    public List<BuildingType> buildingOptions;

    public float buildingYOffset = 0.0f;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    void Start()
    {
        foreach (var zone in zones)
        {
            if (zone.originPoint != null)
            {
                GenerateOneZone(zone);
            }
        }
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

                // --- 修改点3：随机选取一个 BuildingType ---
                int randomIndex = Random.Range(0, buildingOptions.Count);
                BuildingType selectedType = buildingOptions[randomIndex];

                if (selectedType.prefab != null && selectedType.data != null)
                {
                    GameObject newBuilding = Instantiate(selectedType.prefab, spawnPos, Quaternion.identity, zone.originPoint);

                    // 传入对应的 data 进行初始化
                    AttachAndInitialize(newBuilding, selectedType.data, spawnPos);
                }
            }
        }
    }

    // --- 修改点4：完善的挂载与初始化方法 ---
    void AttachAndInitialize(GameObject building, Placeable placeableData, Vector3 worldPos)
    {
        // 1. 确保 Collider 存在
        if (building.GetComponent<Collider>() == null)
        {
            building.AddComponent<BoxCollider>();
        }

        // 2. 获取或添加 PlacedObject 脚本
        PlacedObject placedObj = building.GetComponent<PlacedObject>();
        if (placedObj == null)
        {
            placedObj = building.AddComponent<PlacedObject>();
        }

        // 3. 【关键】自动链接 BuildingEffect
        // 很多建筑系统需要这个组件来实现选中高亮等效果
        BuildingEffect effect = building.GetComponent<BuildingEffect>();
        if (effect != null)
        {
            placedObj.buildingEffect = effect;
        }

        // 4. 【核心修复】使用完整数据进行初始化
        // 我们需要把世界坐标转换成大致的 Grid 坐标，或者直接存入数据
        // 这里的 Vector3Int 是为了填补 gridPosition 字段
        Vector3Int gridPos = new Vector3Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y), Mathf.RoundToInt(worldPos.z));

        // 调用这个版本的 Initialize 会自动设置 Data、Asset Identifier 和 GUID
        placedObj.Initialize(placeableData, gridPos);

        // 5. 激活物体
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