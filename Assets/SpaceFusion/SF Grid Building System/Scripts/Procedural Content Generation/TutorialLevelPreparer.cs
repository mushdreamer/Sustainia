using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class TutorialLevelPreparer : MonoBehaviour
{
    public static TutorialLevelPreparer Instance;
    private void Awake() { Instance = this; }

    public void ClearAllBuildings()
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        // 停止协程防止清理时产生竞态条件
        MultiZoneCityGenerator.Instance.StopAllCoroutines();

        // --- 核心修复：清理系统逻辑层数据 ---
        // 1. 清理网格占用数据
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.ResetAllGridData();
        }

        // 2. 清理 ResourceManager 里的实例引用
        if (ResourceManager.Instance != null)
        {
            // 获取所有建筑并强制执行 RemoveEffect，清理 UI 和数值
            var allBuildings = ResourceManager.Instance.GetAllPlacedBuildings();
            foreach (var b in allBuildings)
            {
                if (b != null) b.RemoveEffect();
            }
        }

        // 3. 遍历并重置所有 Zone 的状态和物理物体
        foreach (var zone in MultiZoneCityGenerator.Instance.zones)
        {
            zone.isOccupied = false;
            if (zone.originPoint == null) continue;

            foreach (Transform child in zone.originPoint)
            {
                if (child.name != "RingOutline" && child.name != "StatusLabel" && child.name != "ArrowIndicator")
                {
                    // 物理销毁
                    Destroy(child.gameObject);
                }
            }
        }

        // 4. 清除可能存在的残留
        BuildingEffect[] leftovers = FindObjectsByType<BuildingEffect>(FindObjectsSortMode.None);
        foreach (var b in leftovers) Destroy(b.gameObject);
    }

    public void PrepareLayoutForEvent(int eventIndex)
    {
        if (MultiZoneCityGenerator.Instance == null) return;

        // 在生成前确保上一个状态被完全清理
        ClearAllBuildings();

        if (eventIndex == 1)
        {
            // 现在调用这个方法，内部的 RegisterExternalObject 就能成功执行了
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, "PowerPlant");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, "LocalGeneration");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(2, "House T1");
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(3, "Battery");
        }
        // ... 其他 event ...
    }
}