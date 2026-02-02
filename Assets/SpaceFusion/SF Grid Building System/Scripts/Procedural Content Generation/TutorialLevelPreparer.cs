using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class TutorialLevelPreparer : MonoBehaviour
{
    public static TutorialLevelPreparer Instance;
    private void Awake() { Instance = this; }

    public void PrepareLayoutForEvent(int eventIndex)
    {
        if (MultiZoneCityGenerator.Instance == null) return;
        foreach (var zone in MultiZoneCityGenerator.Instance.zones)
        {
            zone.isOccupied = false;
            foreach (Transform child in zone.originPoint)
                if (child.name != "RingOutline" && child.name != "StatusLabel" && child.name != "ArrowIndicator")
                    Destroy(child.gameObject);
        }

        if (eventIndex == 1)
        {
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, BuildingType.PowerPlant);
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, BuildingType.Co2Storage);
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(2, BuildingType.House);
        }
        else if (eventIndex == 2)
        {
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(0, BuildingType.PowerPlant);
            MultiZoneCityGenerator.Instance.ForceSpawnBuildingInZone(1, BuildingType.Institute);
        }
    }
}