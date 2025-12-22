using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    [TextArea(3, 10)]
    public string instructionText; // 教程文字

    [Header("Building Requirements")]
    public bool requireBuilding = false; // 是否需要建造
    public bool allowAnyBuilding = false; // (新增) 是否允许建造任意建筑 (用于Step 2)
    public BuildingType targetBuildingType; // 如果不允许任意，则检查特定类型

    [Header("Removal Requirements")]
    public bool requireRemoval = false; // (新增) 是否需要执行删除操作 (用于Step 3)

    [Header("Interaction")]
    public bool requireInput = false; // 是否只需要点击“Next”按钮
}