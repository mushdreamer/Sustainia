using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class TutorialLevelPreparer : MonoBehaviour
{
    public static TutorialLevelPreparer Instance;
    private void Awake() { Instance = this; }

    /// <summary>
    /// 彻底清理场景中所有已存在的建筑，并重置所有 Zone 的占用状态
    /// </summary>
    public void ClearAllBuildings()
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        // 停止可能正在运行的 PCG 协程
        MultiZoneCityGenerator.Instance.StopAllCoroutines();

        // 1. 遍历并重置所有生成区域
        foreach (var zone in MultiZoneCityGenerator.Instance.zones)
        {
            zone.isOccupied = false;
            if (zone.originPoint == null) continue;

            // 删除该 Zone 节点下的所有建筑实例
            foreach (Transform child in zone.originPoint)
            {
                // 保留辅助组件，只删除生成的建筑
                if (child.name != "RingOutline" && child.name != "StatusLabel" && child.name != "ArrowIndicator")
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // 2. 物理扫描残留（防止有建筑未挂载在 Zone 下）
        BuildingEffect[] buildings = FindObjectsOfType<BuildingEffect>();
        foreach (var b in buildings) Destroy(b.gameObject);

        TutorialBuildingEffect[] tBuildings = FindObjectsOfType<TutorialBuildingEffect>();
        foreach (var tb in tBuildings) Destroy(tb.gameObject);

        Debug.Log("[Tutorial] 场景已完全清空，等待加载定制布局。");
    }

    public void PrepareLayoutForEvent(int eventIndex)
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        if (eventIndex == 1)
        {
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, "PowerPlant");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, "LocalGeneration");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(2, "House T1");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(3, "Battery");
        }
        else if (eventIndex == 2)
        {
            Debug.Log("[Tutorial] 加载 Event 2 布局...");
        }
    }
}