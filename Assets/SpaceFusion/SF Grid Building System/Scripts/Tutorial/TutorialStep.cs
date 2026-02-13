using UnityEngine;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

[System.Serializable]
public class BuildingCheck
{
    public bool checkThis = false;
    public int goalCount = 1;
}

[System.Serializable]
public class TutorialStep
{
    public string stepName;
    [TextArea(3, 10)]
    public string instructionText;

    // --- 新增：金钱控制 ---
    [Header("Economy Control")]
    [Tooltip("是否在该步骤开始时重置玩家的金钱")]
    public bool setSpecificMoney = false;
    [Tooltip("重置后的金钱数额")]
    public float moneyAmount = 1000f;

    // --- 独立公式面板配置 ---
    [Header("Independent Formula Display")]
    [Tooltip("是否为该步骤显示独立的公式面板")]
    public bool showFormulaPanel = false;
    [Tooltip("显示的公式模板，例如: {elec} + {food} = Result")]
    public string formulaContent;

    [Header("Clean Slate Logic")]
    public bool clearSceneBeforeStart = false;
    public int layoutToLoad = -1;

    [Header("Camera Control")]
    public GameObject focusTarget;
    public float cameraDistance = 15f;
    public float cameraAngle = 45f;

    [Header("Visual Hint")]
    public bool showIndicator = true;
    public Color indicatorColor = Color.yellow;

    public enum StartCondition
    {
        Immediate,
        WaitForElectricityDeficit,
        WaitForElectricityOverload
    }

    [Header("Start Conditions")]
    public StartCondition startCondition = StartCondition.Immediate;

    [Header("Game State Control")]
    public bool shouldPauseGame = true;

    [Header("Completion - Normal Buildings")]
    public bool requireBuilding = false;
    public BuildingCheck houseReq;
    public BuildingCheck farmReq;
    public BuildingCheck instituteReq;
    public BuildingCheck powerPlantReq;
    public BuildingCheck co2StorageReq;
    public BuildingCheck bankReq;

    [Header("Completion - Tutorial Specific Buildings")]
    public bool requireTutorialBuilding = false;
    public BuildingCheck localGenReq;
    public BuildingCheck batteryReq;
    public BuildingCheck negativeHouseReq;
    public BuildingCheck ccHouseReq;

    [Header("Completion - Other Conditions")]
    public bool requireRemoval = false;
    public bool requirePositiveEnergyBalance = false;
    public bool requireOptimizationGoal = false;
    public bool requireInput = false;

    [Header("P Value Condition")]
    public bool requirePValueGoal = false;
    public float targetPValue = 0f;

    [Header("New Status Conditions")]
    public bool requireFoodSatisfied = false;
    public bool requireFoodShortage = false;
    public bool requireElecStable = false;
    public bool requireElecDeficit = false;
    public bool requireElecOverload = false;
    public bool requireElecNormal = false;
    public bool requireCo2WithinLimit = false;
    public bool requireCo2OverLimit = false;
}