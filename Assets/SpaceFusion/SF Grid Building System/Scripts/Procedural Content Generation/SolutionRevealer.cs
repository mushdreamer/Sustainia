using UnityEngine;
using UnityEngine.UI;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    public class SolutionRevealer : MonoBehaviour
    {
        [Header("UI Reference")]
        public Button showSolutionButton;

        [Header("Settings")]
        [Tooltip("点击按钮后是否禁用它？")]
        public bool disableAfterClick = true;

        private void Start()
        {
            if (showSolutionButton != null)
            {
                showSolutionButton.onClick.AddListener(OnShowSolutionClicked);
            }
        }

        private void OnDestroy()
        {
            if (showSolutionButton != null)
            {
                showSolutionButton.onClick.RemoveListener(OnShowSolutionClicked);
            }
        }

        private void OnShowSolutionClicked()
        {
            if (LevelScenarioLoader.Instance == null || LevelScenarioLoader.Instance.currentLevel == null)
            {
                Debug.LogWarning("[SolutionRevealer] 缺少关卡配置，无法获取完美答案配置。");
                return;
            }

            if (MultiZoneCityGenerator.Instance == null)
            {
                Debug.LogError("[SolutionRevealer] 找不到生成器实例。");
                return;
            }

            // 1. 获取"完美目标"数据
            var levelData = LevelScenarioLoader.Instance.currentLevel;
            float perfectCo2 = levelData.goalCo2;
            float perfectCost = levelData.goalCost; // 或者设为一个很高的值，如果完美答案不计成本
            float perfectEnergy = levelData.goalEnergy;

            Debug.Log($"[SolutionRevealer] 正在展示完美答案... 目标: Co2={perfectCo2}, Cost={perfectCost}, Energy={perfectEnergy}");

            // 2. 将生成器的目标修改为完美目标
            MultiZoneCityGenerator.Instance.targetCo2 = perfectCo2;
            MultiZoneCityGenerator.Instance.targetCost = perfectCost;
            MultiZoneCityGenerator.Instance.targetEnergy = perfectEnergy;

            // 3. 调用生成器的重置方法
            MultiZoneCityGenerator.Instance.ClearAndRestartGeneration();

            // 4. (可选) 禁用按钮防止重复点击
            if (disableAfterClick)
            {
                showSolutionButton.interactable = false;
            }

            // 5. (可选) 如果有 TutorialManager，可能需要通知它停止当前的检测或者直接跳过步骤
            // 视你的设计而定，如果展示答案只是为了让玩家看，那可以不处理；
            // 如果展示答案就算玩家放弃挑战，可以调用 TutorialManager.Instance.NextStep();
        }
    }
}