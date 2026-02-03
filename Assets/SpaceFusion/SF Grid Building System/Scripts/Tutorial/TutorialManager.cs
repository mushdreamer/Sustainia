using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Configuration")]
    public bool enableTutorial = true;
    public List<TutorialStep> steps;

    [Header("References")]
    public TutorialUI tutorialUI;
    public MonoBehaviour externalCameraController;

    private int _currentStepIndex = 0;
    private bool _isTutorialActive = false;
    private bool _isWaitingForStartCondition = false;
    private Coroutine _cameraCoroutine;
    private float _stepStartTime;

    // 记录当前步骤内“新增”建筑数量的计数器
    private Dictionary<string, int> _currentStepProgress = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (!enableTutorial) { if (tutorialUI) tutorialUI.Hide(); return; }
        if (tutorialUI) tutorialUI.Initialize(this);

        if (PlacementSystem.Instance != null)
        {
            // 绑定放置事件
            PlacementSystem.Instance.OnBuildingPlaced += OnBuildingPlacedTrigger;
            // 绑定移除事件
            PlacementSystem.Instance.OnBuildingRemoved += OnBuildingRemovedTrigger;
        }

        StartTutorial();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N)) SkipCurrentStep();
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        if (_isWaitingForStartCondition)
        {
            bool met = false;
            if (currentStep.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
                met = ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < -0.1f;
            else met = true;

            if (met) ActivateStepUI(currentStep);
            return;
        }

        bool statusMet = true;

        if (currentStep.requirePositiveEnergyBalance)
        {
            if (ResourceManager.Instance == null || ResourceManager.Instance.ElectricityBalance < 0f) statusMet = false;
        }

        if (currentStep.requireOptimizationGoal)
        {
            if (LevelScenarioLoader.Instance == null || !LevelScenarioLoader.Instance.IsOptimizationGoalMet()) statusMet = false;
        }

        if (statusMet && !currentStep.requireBuilding && !currentStep.requireTutorialBuilding &&
            !currentStep.requireInput && !currentStep.requireRemoval)
        {
            if (Time.time - _stepStartTime > 0.5f) NextStep();
        }
    }

    private void OnBuildingPlacedTrigger(Placeable data)
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;

        // 因为 Placeable 只是配置文件，我们要等场景里的物体生成并注册后再检查
        StartCoroutine(HandleNewPlacement());
    }

    private IEnumerator HandleNewPlacement()
    {
        // 等待两帧，确保新建筑的 Start() 运行并将自己注册到了 ResourceManager
        yield return null;
        yield return null;

        if (ResourceManager.Instance == null) yield break;

        TutorialStep current = steps[_currentStepIndex];

        // 获取最新注册到管理器的建筑实例
        var normalList = ResourceManager.Instance.GetAllPlacedBuildings();
        var tutorialList = ResourceManager.Instance.GetAllTutorialBuildings();

        string key = "";

        // 尝试从最新加入的普通建筑里判断类型
        if (normalList.Count > 0)
        {
            var lastNormal = normalList[normalList.Count - 1];
            // 检查这个最新建筑是不是刚刚“刚出生”的（这里通过时间或简单逻辑判断，或者直接查类型）
            key = "N_" + lastNormal.type.ToString();
        }

        // 如果不是普通建筑，看看是不是教学建筑
        if (tutorialList.Count > 0 && (string.IsNullOrEmpty(key) || !IsTypeRequiredInCurrentStep(current, key)))
        {
            var lastTutorial = tutorialList[tutorialList.Count - 1];
            key = "T_" + lastTutorial.tutorialType.ToString();
        }

        if (string.IsNullOrEmpty(key)) yield break;

        // 判定该类型是否为当前步骤所需
        if (!IsTypeRequiredInCurrentStep(current, key)) yield break;

        // 增加本步计数
        if (!_currentStepProgress.ContainsKey(key)) _currentStepProgress[key] = 0;
        _currentStepProgress[key]++;

        // 检查是否达标
        if (IsStepRequirementFulfilled(current))
        {
            NextStep();
        }
    }

    private bool IsTypeRequiredInCurrentStep(TutorialStep step, string key)
    {
        if (key.StartsWith("N_"))
        {
            string typeStr = key.Substring(2);
            if (typeStr == "House") return step.houseReq.checkThis;
            if (typeStr == "Farm") return step.farmReq.checkThis;
            if (typeStr == "Institute") return step.instituteReq.checkThis;
            if (typeStr == "PowerPlant") return step.powerPlantReq.checkThis;
            if (typeStr == "Co2Storage") return step.co2StorageReq.checkThis;
            if (typeStr == "Bank") return step.bankReq.checkThis;
        }
        else if (key.StartsWith("T_"))
        {
            string typeStr = key.Substring(2);
            if (typeStr == "LocalGen") return step.localGenReq.checkThis;
            if (typeStr == "Battery") return step.batteryReq.checkThis;
            if (typeStr == "NegativeHouse") return step.negativeHouseReq.checkThis;
            if (typeStr == "CCHouse") return step.ccHouseReq.checkThis;
        }
        return false;
    }

    private bool IsStepRequirementFulfilled(TutorialStep step)
    {
        if (step.houseReq.checkThis && GetCurrentStepCount("N_House") < step.houseReq.goalCount) return false;
        if (step.farmReq.checkThis && GetCurrentStepCount("N_Farm") < step.farmReq.goalCount) return false;
        if (step.instituteReq.checkThis && GetCurrentStepCount("N_Institute") < step.instituteReq.goalCount) return false;
        if (step.powerPlantReq.checkThis && GetCurrentStepCount("N_PowerPlant") < step.powerPlantReq.goalCount) return false;
        if (step.co2StorageReq.checkThis && GetCurrentStepCount("N_Co2Storage") < step.co2StorageReq.goalCount) return false;
        if (step.bankReq.checkThis && GetCurrentStepCount("N_Bank") < step.bankReq.goalCount) return false;

        if (step.localGenReq.checkThis && GetCurrentStepCount("T_LocalGen") < step.localGenReq.goalCount) return false;
        if (step.batteryReq.checkThis && GetCurrentStepCount("T_Battery") < step.batteryReq.goalCount) return false;
        if (step.negativeHouseReq.checkThis && GetCurrentStepCount("T_NegativeHouse") < step.negativeHouseReq.goalCount) return false;
        if (step.ccHouseReq.checkThis && GetCurrentStepCount("T_CCHouse") < step.ccHouseReq.goalCount) return false;

        return true;
    }

    private int GetCurrentStepCount(string key)
    {
        return _currentStepProgress.ContainsKey(key) ? _currentStepProgress[key] : 0;
    }

    public void StartTutorial() { _isTutorialActive = true; _currentStepIndex = 0; PrepareStep(0); }

    private void PrepareStep(int index)
    {
        if (index >= steps.Count) { CompleteTutorial(); return; }

        _stepStartTime = Time.time;
        _currentStepProgress.Clear();

        TutorialStep step = steps[index];

        if (step.clearSceneBeforeStart && TutorialLevelPreparer.Instance != null)
        {
            TutorialLevelPreparer.Instance.ClearAllBuildings();
            if (step.layoutToLoad >= 0)
            {
                TutorialLevelPreparer.Instance.PrepareLayoutForEvent(step.layoutToLoad);
            }
        }

        if (step.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
        {
            _isWaitingForStartCondition = true;
            tutorialUI.Hide();
            if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        }
        else { ActivateStepUI(step); }
    }

    private void ActivateStepUI(TutorialStep step)
    {
        _isWaitingForStartCondition = false;
        tutorialUI.ShowStep(step);

        if (step.focusTarget != null)
            UpdateZoneHighlights(step.focusTarget, step.indicatorColor, step.showIndicator);

        if (step.focusTarget != null)
        {
            if (_cameraCoroutine != null) StopCoroutine(_cameraCoroutine);
            _cameraCoroutine = StartCoroutine(MoveCameraSmoothly(step));
        }

        if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = step.shouldPauseGame;
    }

    private void UpdateZoneHighlights(GameObject target, Color color, bool show)
    {
        if (MultiZoneCityGenerator.Instance == null) return;
        foreach (var zone in MultiZoneCityGenerator.Instance.zones)
        {
            bool isTarget = zone.originPoint.gameObject == target;
            zone.isTutorialHighlight = show && isTarget;
            zone.customHighlightColor = color;
        }
    }

    private IEnumerator MoveCameraSmoothly(TutorialStep step)
    {
        Camera mainCam = Camera.main;
        var controller = externalCameraController as CameraController;
        if (controller) controller.enabled = false;

        Vector3 targetPos = step.focusTarget.transform.position;
        float pitchRad = step.cameraAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(0, Mathf.Sin(pitchRad), -Mathf.Cos(pitchRad)) * step.cameraDistance;
        Vector3 finalPos = targetPos + offset;

        float elapsed = 0;
        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;
        Quaternion finalRot = Quaternion.LookRotation(targetPos - finalPos);

        while (elapsed < 1.0f)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed);
            mainCam.transform.position = Vector3.Lerp(startPos, finalPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, finalRot, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (controller)
        {
            controller.SyncTutorialFocus(targetPos, step.cameraDistance, step.cameraAngle, mainCam.transform.eulerAngles.y);
            controller.enabled = true;
        }
    }

    public void NextStep() { _currentStepIndex++; PrepareStep(_currentStepIndex); }

    public void SkipCurrentStep() { if (_isTutorialActive) NextStep(); }

    private void OnBuildingRemovedTrigger()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;

        if (steps[_currentStepIndex].requireRemoval && Time.time - _stepStartTime > 0.5f)
        {
            NextStep();
        }
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false; tutorialUI.Hide();
        if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        if (externalCameraController != null) externalCameraController.enabled = true;
    }
}