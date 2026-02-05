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
    private bool _isSystemClearing = false;
    private Coroutine _cameraCoroutine;
    private float _stepStartTime;

    private Dictionary<string, int> _initialCounts = new Dictionary<string, int>();
    private int _initialTotalCount = 0;

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
            // 修正后的事件绑定：OnBuildingPlaced 通常带参数，而 OnBuildingRemoved 在某些版本中不带参数
            // 这里使用匿名函数 () => 处理不带参数的情况
            PlacementSystem.Instance.OnBuildingPlaced += (data) => OnAnyBuildingAction();

            // 修复 CS1593 错误：如果 OnBuildingRemoved 不带参数，则去掉 (data)
            PlacementSystem.Instance.OnBuildingRemoved += () => {
                OnAnyBuildingAction();
                OnBuildingRemovedTrigger();
            };
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
            {
                met = ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < -0.1f;
            }
            else met = true;

            if (met) ActivateStepUI(currentStep);
            return;
        }

        CheckNonBuildingConditions(currentStep);
    }

    private void OnAnyBuildingAction()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _isSystemClearing || _currentStepIndex >= steps.Count) return;
        StartCoroutine(CheckRequirementsDelayed());
    }

    private IEnumerator CheckRequirementsDelayed()
    {
        yield return new WaitForEndOfFrame();
        if (ResourceManager.Instance == null) yield break;

        TutorialStep current = steps[_currentStepIndex];
        if (IsStepRequirementFulfilled(current))
        {
            Debug.Log($"<color=green>[Tutorial]</color> Step {_currentStepIndex} completed via Building Action.");
            NextStep();
        }
    }

    private bool IsStepRequirementFulfilled(TutorialStep step)
    {
        if (step.requireRemoval) return false;

        if (step.requireBuilding || step.requireTutorialBuilding)
        {
            bool hasSpecificRequirement = false;

            if (step.houseReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("House") < step.houseReq.goalCount) return false; }
            if (step.farmReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("Farm") < step.farmReq.goalCount) return false; }
            if (step.instituteReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("Institute") < step.instituteReq.goalCount) return false; }
            if (step.powerPlantReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("PowerPlant") < step.powerPlantReq.goalCount) return false; }
            if (step.co2StorageReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("Co2Storage") < step.co2StorageReq.goalCount) return false; }
            if (step.bankReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("Bank") < step.bankReq.goalCount) return false; }

            if (step.localGenReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("LocalGen") < step.localGenReq.goalCount) return false; }
            if (step.batteryReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("Battery") < step.batteryReq.goalCount) return false; }
            if (step.negativeHouseReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("NegativeHouse") < step.negativeHouseReq.goalCount) return false; }
            if (step.ccHouseReq.checkThis) { hasSpecificRequirement = true; if (GetCountDelta("CCHouse") < step.ccHouseReq.goalCount) return false; }

            if (!hasSpecificRequirement)
            {
                int currentTotal = 0;
                if (ResourceManager.Instance != null)
                {
                    string[] allKeys = { "House", "Farm", "Institute", "PowerPlant", "Co2Storage", "Bank", "LocalGen", "Battery", "NegativeHouse", "CCHouse" };
                    foreach (var key in allKeys) currentTotal += ResourceManager.Instance.GetTotalBuildingCount(key);
                }
                return currentTotal > _initialTotalCount;
            }

            return true;
        }

        return false;
    }

    private int GetCountDelta(string typeKey)
    {
        int currentCount = ResourceManager.Instance.GetTotalBuildingCount(typeKey);
        int initialCount = _initialCounts.ContainsKey(typeKey) ? _initialCounts[typeKey] : 0;
        return Mathf.Max(0, currentCount - initialCount);
    }

    private void CheckNonBuildingConditions(TutorialStep currentStep)
    {
        bool statusMet = true;
        bool hasAnyCondition = currentStep.requirePositiveEnergyBalance || currentStep.requireOptimizationGoal;

        if (currentStep.requirePositiveEnergyBalance)
        {
            if (ResourceManager.Instance == null || ResourceManager.Instance.ElectricityBalance < 0f) statusMet = false;
        }

        if (currentStep.requireOptimizationGoal)
        {
            if (LevelScenarioLoader.Instance == null || !LevelScenarioLoader.Instance.IsOptimizationGoalMet()) statusMet = false;
        }

        if (hasAnyCondition && statusMet && !currentStep.requireBuilding && !currentStep.requireTutorialBuilding &&
            !currentStep.requireInput && !currentStep.requireRemoval)
        {
            if (Time.time - _stepStartTime > 0.8f)
            {
                Debug.Log($"<color=green>[Tutorial]</color> Step {_currentStepIndex} completed via Conditions.");
                NextStep();
            }
        }
    }

    public void StartTutorial() { _isTutorialActive = true; _currentStepIndex = 0; PrepareStep(0); }

    private void PrepareStep(int index)
    {
        if (index >= steps.Count) { CompleteTutorial(); return; }
        StartCoroutine(PrepareStepRoutine(index));
    }

    private IEnumerator PrepareStepRoutine(int index)
    {
        _stepStartTime = Time.time;
        TutorialStep step = steps[index];
        Debug.Log($"<color=yellow>[Tutorial]</color> Preparing Step {index}: {step.stepName}");

        if (step.clearSceneBeforeStart && TutorialLevelPreparer.Instance != null)
        {
            _isSystemClearing = true;
            TutorialLevelPreparer.Instance.ClearAllBuildings();
            if (step.layoutToLoad >= 0) TutorialLevelPreparer.Instance.PrepareLayoutForEvent(step.layoutToLoad);

            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();

            RecordInitialCounts();
            _isSystemClearing = false;
        }
        else
        {
            RecordInitialCounts();
        }

        if (step.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
        {
            _isWaitingForStartCondition = true;
            tutorialUI.Hide();
            if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        }
        else { ActivateStepUI(step); }
    }

    private void RecordInitialCounts()
    {
        _initialCounts.Clear();
        _initialTotalCount = 0;
        if (ResourceManager.Instance == null) return;
        string[] allKeys = { "House", "Farm", "Institute", "PowerPlant", "Co2Storage", "Bank", "LocalGen", "Battery", "NegativeHouse", "CCHouse" };
        foreach (var key in allKeys)
        {
            int count = ResourceManager.Instance.GetTotalBuildingCount(key);
            _initialCounts[key] = count;
            _initialTotalCount += count;
        }
    }

    private void ActivateStepUI(TutorialStep step)
    {
        _isWaitingForStartCondition = false;
        tutorialUI.ShowStep(step);
        if (step.focusTarget != null) UpdateZoneHighlights(step.focusTarget, step.indicatorColor, step.showIndicator);
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

    public void NextStep()
    {
        _currentStepIndex++;
        PrepareStep(_currentStepIndex);
    }

    public void SkipCurrentStep() { if (_isTutorialActive) NextStep(); }

    private void OnBuildingRemovedTrigger()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _isSystemClearing || _currentStepIndex >= steps.Count) return;

        if (steps[_currentStepIndex].requireRemoval && Time.time - _stepStartTime > 0.5f)
        {
            Debug.Log($"<color=green>[Tutorial]</color> Step {_currentStepIndex} completed via Building Removal.");
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