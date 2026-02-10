using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public enum TutorialBuildingType { LocalGen, Battery, NegativeHouse, CCHouse }

    public class TutorialBuildingEffect : MonoBehaviour
    {
        public TutorialBuildingType tutorialType;

        [Header("Tutorial Stats")]
        [Tooltip("正数增加耗电，负数增加发电")]
        public float electricityChange = 0f;

        [Tooltip("正数增加排放，负数增加吸收")]
        public float co2Change = 0f;

        [Header("Custom Overload Settings")]
        [Tooltip("如果该值不为0，此建筑会使用该自定义阈值判定电力平衡是否过载。")]
        public float customOverloadThreshold = 0f;

        // --- 兼容性属性 ---
        [HideInInspector] public float energyValue { get => electricityChange; set => electricityChange = value; }
        [HideInInspector] public float co2Effect { get => co2Change; set => co2Change = value; }

        public float storageCapacity = 100f;

        private bool _isActive = false;

        private void Start()
        {
            ApplyTutorialEffect();
        }

        public void ApplyTutorialEffect()
        {
            if (_isActive || ResourceManager.Instance == null) return;
            _isActive = true;

            ResourceManager.Instance.RegisterTutorialBuildingInstance(this);

            if (electricityChange > 0) ResourceManager.Instance.AddConsumption(electricityChange);
            else if (electricityChange < 0) ResourceManager.Instance.AddGeneration(Mathf.Abs(electricityChange));

            if (co2Change > 0) ResourceManager.Instance.AddPowerPlantEffect(co2Change);
            else if (co2Change < 0) ResourceManager.Instance.AddCo2Absorption(Mathf.Abs(co2Change));
        }

        private void OnDestroy()
        {
            if (!_isActive || ResourceManager.Instance == null) return;

            if (electricityChange > 0) ResourceManager.Instance.RemoveConsumption(electricityChange);
            else if (electricityChange < 0) ResourceManager.Instance.RemoveGeneration(Mathf.Abs(electricityChange));

            if (co2Change > 0) ResourceManager.Instance.RemovePowerPlantEffect(co2Change);
            else if (co2Change < 0) ResourceManager.Instance.RemoveCo2Absorption(Mathf.Abs(co2Change));

            ResourceManager.Instance.UnregisterTutorialBuildingInstance(this);
            _isActive = false;
        }

        /// <summary>
        /// 获取当前生效的过载阈值
        /// </summary>
        public float GetEffectiveThreshold()
        {
            if (customOverloadThreshold != 0) return customOverloadThreshold;
            return ResourceManager.Instance != null ? ResourceManager.Instance.globalOverloadThreshold : 0f;
        }
    }
}