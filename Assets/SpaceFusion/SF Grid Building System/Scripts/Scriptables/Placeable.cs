using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables
{
    /// <summary>
    /// Important NOTE: scriptableObjectName is used as asset identifier
    /// </summary>
    [CreateAssetMenu(fileName = "Placeable object", menuName = "SF Studio/Grid System/Placeable", order = 0)]
    public class Placeable : ScriptableObject
    {
        [field: SerializeField]
        public Vector2 Size { get; private set; } = Vector2.one;

        [field: SerializeField]
        public GameObject Prefab { get; private set; }

        [field: SerializeField]
        public Sprite Icon { get; private set; }

        // --- 核心属性 ---
        [field: SerializeField]
        public float Cost { get; private set; } = 100f;

        public float Health = 100f;

        // --- 注意：已移除 IsUnique 字段，以取消建造数量限制 ---

        [Header("Optimization / Simulation Data")]
        [field: Tooltip("正数=产电(Generation), 负数=耗电(Demand)")]
        [field: SerializeField]
        public float BaseEnergyFlow { get; private set; } = 0f;

        [field: Tooltip("正数=排放(Emission), 负数=移除(Removal)")]
        [field: SerializeField]
        public float CO2Effect { get; private set; } = 0f;

        [field: Tooltip("电池容量 (MWh)")]
        [field: SerializeField]
        public float StorageCapacity { get; private set; } = 0f;

        [field: Tooltip("Describes in which gridData the object will be stored.")]
        [field: SerializeField]
        public GridDataType GridType { get; private set; }

        [field: Tooltip("Describes the Group of the placeable object")]
        [field: SerializeField]
        public ObjectGroup ObjectGroupInfo { get; private set; }

        [field: Tooltip("Objects with dynamic size will automatically adapt the transform scale")]
        [field: SerializeField]
        public bool DynamicSize { get; private set; }

        public string GetAssetIdentifier()
        {
            return name;
        }

        public void SetObjectSize(Vector2 size)
        {
            Size = size;
        }
    }
}