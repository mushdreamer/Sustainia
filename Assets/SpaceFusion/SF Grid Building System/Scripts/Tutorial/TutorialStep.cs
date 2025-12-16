using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core; // 引用 BuildingType

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    [TextArea(3, 10)]
    public string instructionText; // 教程文字

    public bool requireBuilding = false; // 是否需要建造特定建筑来完成
    public BuildingType targetBuildingType; // 需要建造的类型

    public bool requireInput = false; // 是否只需要点击“下一步”
}