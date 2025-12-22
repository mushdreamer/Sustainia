using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables
{
    [CreateAssetMenu(fileName = "NewOptimizationLevel", menuName = "SpaceFusion/Optimization Level")]
    public class OptimizationLevelData : ScriptableObject
    {
        [Header("Level Description")]
        public string levelName;
        [TextArea]
        public string levelBrief;

        [Header("Generator Input (The 'Flawed' State)")]
        [Tooltip("这是喂给生成器的目标，用于生成初始的'不完美'城市")]
        public float genTargetCo2 = 50.0f;
        public float genTargetCost = 1000.0f;
        public float genTargetEnergy = 50.0f; // 比如故意设低，制造缺电局面

        [Header("Player Goal (The 'Perfect' State)")]
        [Tooltip("这是玩家需要达成的真实优化目标")]
        public float goalCo2 = 50.0f;
        public float goalCost = 1200.0f; // 允许玩家花更多的钱来修复问题
        public float goalEnergy = 100.0f; // 玩家需要把电力修到这里

        [Header("Weights (Shared)")]
        public float weightCo2 = 1.0f;
        public float weightCost = 1.0f;
        public float weightEnergy = 1.5f;

        [Header("Tolerance")]
        [Tooltip("允许的误差范围，比如达到目标值的 +/- 5% 算成功")]
        public float successTolerancePercent = 0.05f;
    }
}