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
        [SerializeField]
        private float _currentHealth;

        [Header("General Resource Settings")]
        [Tooltip("正数增加消耗(耗电)，负数增加产出(发电)")]
        public float electricityChange = 0f;

        [Tooltip("正数增加排放，负数增加吸收")]
        public float co2Change = 0f;

        // --- 兼容性属性 ---
        [HideInInspector] public float electricityConsumption { get => electricityChange; set => electricityChange = value; }
        [HideInInspector] public float powerPlantCo2Change { get => co2Change; set => co2Change = value; }

        [Header("House Settings")]
        public int populationCapacity = 5;
        public int initialPopulation = 2;
        public float houseCo2Change = 1f;
        public float foodConsumption = 1f; // 新增：民居产生的食物消耗

        [Header("Farm Settings")]
        public float foodProduction = 2f;
        private float _currentFoodProduction;
        public float farmCo2Change = 2f;

        [Header("Institute Settings")]
        public float instituteCo2Change = 3f;

        [Header("Bank Settings")]
        public float bankCo2Change = 1.5f;

        [Header("PowerPlant Settings (Emitter)")]
        public float powerProduction = 20f;

        [Header("Co2Storage Settings (Absorber)")]
        public float storageConsumption = 5f;
        public float storageCo2Change = 8f;

        private float _currentElectricityChange;
        private float _currentCo2Change = 0f;
        private float _currentPowerProduction = 0f;

        private bool _isActive = false;

        private void Start()
        {
            if (ResourceManager.Instance == null) return;

            _currentHealth = maxHealth;

            // 初始化当前运行时数值
            _currentElectricityChange = electricityChange;
            _currentCo2Change = co2Change;
            _currentFoodProduction = foodProduction;

            if (type == BuildingType.PowerPlant)
            {
                _currentPowerProduction = powerProduction;
            }

            ApplyEffect();
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
            _isActive = true;

            if (ResourceManager.Instance == null) return;

            ResourceManager.Instance.RegisterBuildingInstance(this);
            ResourceManager.Instance.RegisterBuilding(type);

            // 1. 统一电力逻辑
            if (_currentElectricityChange > 0)
                ResourceManager.Instance.AddConsumption(_currentElectricityChange);
            else if (_currentElectricityChange < 0)
                ResourceManager.Instance.AddGeneration(Mathf.Abs(_currentElectricityChange));

            // 2. 统一环境逻辑
            if (_currentCo2Change > 0)
                ResourceManager.Instance.AddPowerPlantEffect(_currentCo2Change);
            else if (_currentCo2Change < 0)
                ResourceManager.Instance.AddCo2Absorption(Mathf.Abs(_currentCo2Change));

            // 3. 统一食物逻辑：Farm 加产出，House 加需求
            if (type == BuildingType.Farm)
            {
                ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
            }
            else if (type == BuildingType.House)
            {
                ResourceManager.Instance.AddHouseEffect(populationCapacity, initialPopulation);
                ResourceManager.Instance.AddFoodDemand(foodConsumption);
            }
            else if (type == BuildingType.Bank)
            {
                ResourceManager.Instance.AddBank();
            }
        }

        public void RemoveEffect()
        {
            if (!_isActive || ResourceManager.Instance == null) return;
            _isActive = false;

            // 移除电力影响
            if (_currentElectricityChange > 0)
                ResourceManager.Instance.RemoveConsumption(_currentElectricityChange);
            else if (_currentElectricityChange < 0)
                ResourceManager.Instance.RemoveGeneration(Mathf.Abs(_currentElectricityChange));

            // 移除环境影响
            if (_currentCo2Change > 0)
                ResourceManager.Instance.RemovePowerPlantEffect(_currentCo2Change);
            else if (_currentCo2Change < 0)
                ResourceManager.Instance.RemoveCo2Absorption(Mathf.Abs(_currentCo2Change));

            // 移除食物逻辑
            if (type == BuildingType.Farm)
            {
                ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
            }
            else if (type == BuildingType.House)
            {
                ResourceManager.Instance.RemoveHouseEffect(populationCapacity, initialPopulation);
                ResourceManager.Instance.RemoveFoodDemand(foodConsumption);
            }
            else if (type == BuildingType.Bank)
            {
                ResourceManager.Instance.RemoveBank();
            }
        }

        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;
            if (_currentHealth <= 0) DestroyBuilding();
        }

        public void Heal(float amount)
        {
            _currentHealth += amount;
            if (_currentHealth > maxHealth) _currentHealth = maxHealth;
        }

        public void UpdateElectricityChange(float newValue)
        {
            if (_isActive)
            {
                if (_currentElectricityChange > 0) ResourceManager.Instance.RemoveConsumption(_currentElectricityChange);
                else if (_currentElectricityChange < 0) ResourceManager.Instance.RemoveGeneration(Mathf.Abs(_currentElectricityChange));
            }

            _currentElectricityChange = newValue;
            electricityChange = newValue;

            if (_isActive)
            {
                if (_currentElectricityChange > 0) ResourceManager.Instance.AddConsumption(_currentElectricityChange);
                else if (_currentElectricityChange < 0) ResourceManager.Instance.AddGeneration(Mathf.Abs(_currentElectricityChange));
            }
        }

        public void UpdateFoodProduction(float newValue)
        {
            if (type != BuildingType.Farm) return;
            if (_isActive) ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
            _currentFoodProduction = newValue;
            if (_isActive) ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
        }

        public float GetCurrentElectricity() => _currentElectricityChange;
        public float GetCurrentFood() => _currentFoodProduction;
        public float GetCurrentCo2Change() => _currentCo2Change;

        public void DestroyBuilding()
        {
            PlacedObject placedObject = GetComponent<PlacedObject>();
            if (placedObject != null && PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.Remove(placedObject);
            }
            else
            {
                RemoveEffect();
                Destroy(gameObject);
            }
        }
    }
}