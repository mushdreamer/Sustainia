using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
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

            // 注册到资源管理器，以便在教程逻辑中被识别
            ResourceManager.Instance.RegisterTutorialBuildingInstance(this);

            switch (tutorialType)
            {
                case TutorialBuildingType.LocalGen:
                    ResourceManager.Instance.AddGeneration(energyValue);
                    break;
                case TutorialBuildingType.Battery:
                    // 可以在此添加电池特定的初始化逻辑
                    break;
            }
            Debug.Log($"[Tutorial] 教学建筑已激活并注册: {tutorialType}");
        }

        private void OnDestroy()
        {
            if (!_isActive || ResourceManager.Instance == null) return;

            ResourceManager.Instance.UnregisterTutorialBuildingInstance(this);

            if (tutorialType == TutorialBuildingType.LocalGen)
                ResourceManager.Instance.RemoveGeneration(energyValue);
        }
    }
}