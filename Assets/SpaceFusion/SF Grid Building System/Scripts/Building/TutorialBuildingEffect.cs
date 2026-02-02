using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    // 定义教学专用的扩展类型，不干扰原有的 BuildingType 枚举
    public enum TutorialBuildingType { LocalGen, Battery, NegativeHouse, CCHouse }

    public class TutorialBuildingEffect : MonoBehaviour
    {
        public TutorialBuildingType tutorialType;

        [Header("Tutorial Stats")]
        public float energyValue = 20f; // 发电或耗电量
        public float storageCapacity = 100f; // 电池容量
        public float co2Effect = 0f;

        private bool _isActive = false;

        private void Start()
        {
            ApplyTutorialEffect();
        }

        public void ApplyTutorialEffect()
        {
            if (_isActive || ResourceManager.Instance == null) return;
            _isActive = true;

            // 这里直接与 ResourceManager 对接数值
            switch (tutorialType)
            {
                case TutorialBuildingType.LocalGen:
                    ResourceManager.Instance.AddGeneration(energyValue);
                    break;
                case TutorialBuildingType.Battery:
                    break;
            }
            Debug.Log($"[Tutorial] 教学建筑已激活: {tutorialType}");
        }

        private void OnDestroy()
        {
            if (!_isActive || ResourceManager.Instance == null) return;
            // 销毁时同步扣除数值
            if (tutorialType == TutorialBuildingType.LocalGen)
                ResourceManager.Instance.RemoveGeneration(energyValue);
        }
    }
}