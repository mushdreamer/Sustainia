using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public enum BuildingType { House, Farm, Institute, PowerPlant, Co2Storage, Bank }

    public class BuildingEffect : MonoBehaviour
    {
        public BuildingType type;

        [Header("House Settings")]
        public int populationCapacityIncrease = 5;
        public int initialPopulationGain = 2;

        [Header("Farm Settings")]
        public float foodProduction = 2f; // 每秒生产的食物
        public int workersRequired = 2;   // 需要的工人数量

        [Header("PowerPlant Settings")]
        public float electricityProduction = 10f; // 每秒发电量
        public float co2Emission = 2f;            // 每秒二氧化碳排放量

        [Header("Co2 Storage Settings")] // <<< --- 新增设置组 ---
        public float co2AbsorptionRate = 0.5f; // 每秒吸收的CO2量
        public float co2Capacity = 100f;
        // --- 新增：内部状态追踪变量 ---
        private float _currentCo2Stored = 0f;
        private bool _isStorageActive = false; // 用于控制Update逻辑是否执行

        private void Update()
        {
            // 如果这个脚本不是一个激活的储藏罐，就什么也不做
            if (!_isStorageActive)
            {
                return;
            }

            // 随着时间填充储藏量
            // (我们假设它只要开着就在吸收，无论当前排放量多少)
            // (更真实的模拟是吸收“净排放”，但会更复杂，目前这样更直观)
            _currentCo2Stored += co2AbsorptionRate * Time.deltaTime;

            // 检查是否已满
            if (_currentCo2Stored >= co2Capacity)
            {
                _currentCo2Stored = co2Capacity; // 确保不会超过上限
                _isStorageActive = false; // 停止Update循环

                // --- 核心逻辑 ---
                // 储藏罐满了，它不再提供吸收能力
                // 我们通知ResourceManager，把这个建筑的吸收量从全局减去
                ResourceManager.Instance.RemoveCo2Absorption(co2AbsorptionRate);

                // 在这里可以添加视觉提示，比如让建筑变色、冒烟或显示一个“已满”的图标
                Debug.Log("One Co2 Storage is Full!");
            }
        }

        // 当建筑被成功放置时调用
        public void ApplyEffect()
        {
            switch (type)
            {
                case BuildingType.House:
                    ResourceManager.Instance.AddHouseEffect(populationCapacityIncrease, initialPopulationGain);
                    break;
                case BuildingType.Farm:
                    ResourceManager.Instance.AddFoodProduction(foodProduction, workersRequired);
                    break;
                case BuildingType.Bank:
                    ResourceManager.Instance.AddBank();
                    break;
                case BuildingType.PowerPlant:
                    ResourceManager.Instance.AddPowerPlantEffect(electricityProduction, co2Emission);
                    break;
                case BuildingType.Co2Storage:
                    _currentCo2Stored = 0f;
                    _isStorageActive = true;
                    ResourceManager.Instance.AddCo2Absorption(co2AbsorptionRate);
                    break;
                case BuildingType.Institute:
                    // 未来迭代的功能
                    break;
            }
        }

        // 当建筑被移除时调用
        public void RemoveEffect()
        {
            switch (type)
            {
                case BuildingType.House:
                    ResourceManager.Instance.RemoveHouseEffect(populationCapacityIncrease, initialPopulationGain);
                    break;
                case BuildingType.Farm:
                    ResourceManager.Instance.RemoveFoodProduction(foodProduction, workersRequired);
                    break;
                case BuildingType.Bank:
                    ResourceManager.Instance.RemoveBank();
                    break;
                case BuildingType.PowerPlant:
                    ResourceManager.Instance.RemovePowerPlantEffect(electricityProduction, co2Emission);
                    break;
                case BuildingType.Co2Storage:
                    if (_isStorageActive)
                    {
                        ResourceManager.Instance.RemoveCo2Absorption(co2AbsorptionRate);
                    }
                    _isStorageActive = false;
                    break;
                case BuildingType.Institute:
                    break;
            }
        }
    }
}