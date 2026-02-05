using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Managers
{
    public class LevelScenarioLoader : MonoBehaviour
    {
        public static LevelScenarioLoader Instance;

        [Header("Current Level Configuration")]
        public OptimizationLevelData currentLevel;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // 在生成器开始运行前，篡改它的目标参数
            ApplyScenarioToGenerator();
        }

        private void ApplyScenarioToGenerator()
        {
            if (currentLevel == null || MultiZoneCityGenerator.Instance == null) return;

            // 1. 修改生成器的目标，让它生成一个“有缺陷”的城市
            MultiZoneCityGenerator.Instance.targetCo2 = currentLevel.genTargetCo2;
            MultiZoneCityGenerator.Instance.targetCost = currentLevel.genTargetCost;
            MultiZoneCityGenerator.Instance.targetEnergy = currentLevel.genTargetEnergy;

            // 2. 同步权重
            MultiZoneCityGenerator.Instance.weightCo2 = currentLevel.weightCo2;
            MultiZoneCityGenerator.Instance.weightCost = currentLevel.weightCost;
            MultiZoneCityGenerator.Instance.weightEnergy = currentLevel.weightEnergy;
        }

        /// <summary>
        /// 检查当前游戏状态是否满足了 LevelData 中的“真实目标”
        /// </summary>
        public bool IsOptimizationGoalMet()
        {
            if (currentLevel == null || ResourceManager.Instance == null) return true;

            // 获取当前实际数值
            // 注意：ResourceManager中需要有方法获取当前的 Co2 和 TotalCost (如果没有需补充)
            // 这里假设 ResourceManager 已经维护了相关数据，或者我们需要简单计算一下

            // 由于 ResourceManager 的代码里没有直接的 Cost 统计（只有 Money），
            // 也没有直接的 Net Co2 统计（只有 UI 计算逻辑），我们需要在这里获取一下。

            // 为了不改动 ResourceManager太多，我们临时从 BuildingEffect 统计，或者假设 ResourceManager 有这些属性
            // 根据之前代码：ResourceManager 有 GetCurrentNetEmission 和 ElectricityBalance

            float currentCo2 = ResourceManager.Instance.GetCurrentNetEmission();
            float currentEnergy = ResourceManager.Instance.ElectricityBalance;

            // 关于 Cost：之前的 Generator 是算总造价，但 ResourceManager 是算剩余金钱。
            // 这里我们用一种灵活的方式：如果 Level 里 Cost 设为 -1 则不检查，否则检查剩余金钱是否在范围内，或者省略 Cost 检查
            // 为了简化教学，这里主要检查 Co2 和 Energy

            bool co2Ok = IsValueWithinTolerance(currentCo2, currentLevel.goalCo2, currentLevel.successTolerancePercent);

            // Energy 比较特殊，通常要求 >= 目标值，或者严格匹配
            // 这里假设是严格匹配优化目标
            bool energyOk = IsValueWithinTolerance(currentEnergy, currentLevel.goalEnergy, currentLevel.successTolerancePercent);

            // 如果你需要检查 Cost (比如剩余金钱)，可以在这里加

            return co2Ok && energyOk;
        }

        private bool IsValueWithinTolerance(float current, float target, float tolerancePercent)
        {
            float diff = Mathf.Abs(current - target);
            float allowedDiff = Mathf.Abs(target * tolerancePercent);
            // 如果目标是0，允许一个极小的绝对误差
            if (target == 0) allowedDiff = 2f;

            return diff <= allowedDiff;
        }
    }
}