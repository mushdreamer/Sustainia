// --- BuildingEffect.cs 完整代码 ---
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public enum BuildingType { House, Farm, Institute, PowerPlant, Co2Storage, Bank }

    public class BuildingEffect : MonoBehaviour
    {
        public BuildingType type;

        [Header("Health & Combat")]
        public float maxHealth = 100f;
        private float _currentHealth;

        [Header("General Resource Settings")]
        public float electricityChange = 0f;
        public float co2Change = 0f;

        [Header("House Settings")]
        public int populationCapacity = 5;
        public int initialPopulation = 2;
        public float foodConsumption = 1f;

        [Header("Farm Settings")]
        public float foodProduction = 2f;

        [Header("PowerPlant Settings")]
        public float powerProduction = 20f;

        private float _currentElectricityChange;
        private float _currentCo2Change;
        private float _currentFoodProduction;
        private bool _isActive = false;
        private bool _isInitialized = false;

        // --- 新增：显式初始化方法 ---
        private void InitializeData()
        {
            if (_isInitialized) return;
            _currentHealth = maxHealth;

            // 如果是发电机，直接把 powerProduction 赋给电力变更（假设负数是发电）
            if (type == BuildingType.PowerPlant && electricityChange == 0)
                _currentElectricityChange = -powerProduction;
            else
                _currentElectricityChange = electricityChange;

            _currentCo2Change = co2Change;
            _currentFoodProduction = foodProduction;
            _isInitialized = true;
        }

        private void Start()
        {
            if (!_isActive) ApplyEffect();
        }

        private void OnDestroy()
        {
            if (_isActive && ResourceManager.Instance != null)
            {
                RemoveEffect();
                ResourceManager.Instance.UnregisterBuildingInstance(this);
                ResourceManager.Instance.UnregisterBuilding(type);
            }
        }

        public void ApplyEffect()
        {
            if (_isActive) return;
            InitializeData(); // 确保注册前数值已同步

            if (ResourceManager.Instance == null) return;
            _isActive = true;

            ResourceManager.Instance.RegisterBuildingInstance(this);
            ResourceManager.Instance.RegisterBuilding(type);

            if (_currentElectricityChange > 0)
                ResourceManager.Instance.AddConsumption(_currentElectricityChange);
            else if (_currentElectricityChange < 0)
                ResourceManager.Instance.AddGeneration(Mathf.Abs(_currentElectricityChange));

            if (_currentCo2Change > 0)
                ResourceManager.Instance.AddPowerPlantEffect(_currentCo2Change);
            else if (_currentCo2Change < 0)
                ResourceManager.Instance.AddCo2Absorption(Mathf.Abs(_currentCo2Change));

            if (type == BuildingType.Farm)
                ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
            else if (type == BuildingType.House)
            {
                ResourceManager.Instance.AddHouseEffect(populationCapacity, initialPopulation);
                ResourceManager.Instance.AddFoodDemand(foodConsumption);
            }
            else if (type == BuildingType.Bank)
                ResourceManager.Instance.AddBank();
        }

        public void RemoveEffect()
        {
            if (!_isActive || ResourceManager.Instance == null) return;

            if (_currentElectricityChange > 0)
                ResourceManager.Instance.RemoveConsumption(_currentElectricityChange);
            else if (_currentElectricityChange < 0)
                ResourceManager.Instance.RemoveGeneration(Mathf.Abs(_currentElectricityChange));

            if (_currentCo2Change > 0)
                ResourceManager.Instance.RemovePowerPlantEffect(_currentCo2Change);
            else if (_currentCo2Change < 0)
                ResourceManager.Instance.RemoveCo2Absorption(Mathf.Abs(_currentCo2Change));

            if (type == BuildingType.Farm)
                ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
            else if (type == BuildingType.House)
            {
                ResourceManager.Instance.RemoveHouseEffect(populationCapacity, initialPopulation);
                ResourceManager.Instance.RemoveFoodDemand(foodConsumption);
            }
            else if (type == BuildingType.Bank)
                ResourceManager.Instance.RemoveBank();

            _isActive = false;
        }

        public float GetCurrentElectricity() { InitializeData(); return _currentElectricityChange; }
        public float GetCurrentFood() { InitializeData(); return _currentFoodProduction; }
        public float GetCurrentCo2Change() { InitializeData(); return _currentCo2Change; }
    }
}