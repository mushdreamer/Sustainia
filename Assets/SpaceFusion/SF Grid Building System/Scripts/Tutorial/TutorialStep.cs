using UnityEngine;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

[System.Serializable]
public class BuildingRequirement
{
    public bool isTutorialBuilding;
    public SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType normalType;
    public SpaceFusion.SF_Grid_Building_System.Scripts.Core.TutorialBuildingType tutorialType;
    public int requiredCount = 1;
}

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    [TextArea(3, 10)]
    public string instructionText;

    [Header("Clean Slate Logic")]
    [Tooltip("½øÈë´Ë²½ÖèÇ°ÊÇ·ñÇå¿Õ³¡¾°ËùÓÐ½¨Öþ")]
    public bool clearSceneBeforeStart = false;
    [Tooltip("Çå¿Õºó¼ÓÔØµÄ²¼¾ÖË÷Òý (¶ÔÓ¦ Preparer ÖÐµÄ eventIndex)")]
    public int layoutToLoad = -1;

    [Header("Camera Control")]
    [Tooltip("¾µÍ·¾Û½¹µÄÄ¿±ê½¨Öþ»òÎ»ÖÃ")]
    public GameObject focusTarget;
    [Tooltip("¾µÍ·¾àÀëÄ¿±êµÄÔ¶½ü")]
    public float cameraDistance = 15f;
    [Tooltip("¾µÍ·µÄ¸©ÊÓ½Ç¶È")]
    public float cameraAngle = 45f;

    [Header("Visual Hint")]
    [Tooltip("ÊÇ·ñÔÚ¸Ã²½ÖèÏÔÊ¾ÊÓ¾õÖ¸Òý±êÖ¾")]
    public bool showIndicator = true;
    [Tooltip("ÊÓ¾õÖ¸Òý±êÖ¾µÄÑÕÉ«")]
    public Color indicatorColor = Color.yellow;

    public enum StartCondition
    {
        Immediate,
        WaitForElectricityDeficit
    }

    [Header("Start Conditions")]
    public StartCondition startCondition = StartCondition.Immediate;

    [Header("Game State Control")]
    public bool shouldPauseGame = true;

    // TutorialStep.cs
    [Header("Completion - Building (Flexible)")]
    public bool requireBuilding = false;
    // 如果勾选此项，只要玩家造了列表里任何一种建筑，且总数达到要求即可
    public bool allowAnyCombination = false;
    // 核心配置列表
    public List<BuildingRequirement> targetBuildings;

    // [注意] 可以移除旧的 targetBuildingType 和 isTutorialBuilding 字段以保持 Inspector 整洁

    [Header("Completion - Removal")]
    public bool requireRemoval = false;

    [Header("Completion - Simulation")]
    public bool requirePositiveEnergyBalance = false;

    [Header("Completion - Optimization (New)")]
    [Tooltip("Èç¹û¹´Ñ¡£¬½«¼ì²é LevelScenarioLoader ÖÐµÄÕæÊµÄ¿±êÊÇ·ñ´ï³É")]
    public bool requireOptimizationGoal = false;

    [Header("Completion - Interaction")]
    public bool requireInput = false;
}