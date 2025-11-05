using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables {
    /// <summary>
    /// Important NOTE: scriptableObjectName is used as asset identifier
    /// So after your game is shipped you should NOT change the Scriptable Object Name anymore, since this could cause unexpected errors
    /// If you want your own unique IDs, please create a string identifier property and set your identifiers per object
    /// In GetAssetIdentifier please return your identifier instead of the Scriptable object name
    /// </summary>
    [CreateAssetMenu(fileName = "Placeable object", menuName = "SF Studio/Grid System/Placeable", order = 0)]
    public class Placeable : ScriptableObject {
        [field: SerializeField]
        public Vector2 Size { get; private set; } = Vector2.one;

        [field: SerializeField]
        public GameObject Prefab { get; private set; }

        [field: SerializeField]
        public Sprite Icon { get; private set; }

        // --- 在这里添加新属性 ---
        [field: SerializeField]
        public float Cost { get; private set; } = 100f; // 默认价格100
        public float Health = 100f; // 默认血量100

        // --- ⬇️ 在这里添加新代码 ⬇️ ---
        [Header("Game Rules")]
        [field: Tooltip("如果勾选，这个建筑在地图上只能存在一个。")]
        [field: SerializeField]
        public bool IsUnique { get; private set; } = false;
        // --- ⬆️ 新代码结束 ⬆️ ---

        [field: Tooltip("Describes in which gridData the object will be stored.")]
        [field: SerializeField]
        public GridDataType GridType { get; private set; }

        [field: Tooltip("Describes the Group of the placeable object, in order to easier group them in the shop by the defined group")]
        [field: SerializeField]
        public ObjectGroup ObjectGroupInfo { get; private set; }

        [field:
            Tooltip(
                "Objects with dynamic size will automatically adapt the transform scale to match the grid size (Especially useful for 1x1 terrain data in grids with different cell size, but works with every object)")]
        [field: SerializeField]
        public bool DynamicSize { get; private set; }

        private Vector3 _autoCalculatedPivotOffset;

        public string GetAssetIdentifier() {
            return name;
        }

        /// <summary>
        /// Used by the editor script to update the size and offset after precalculating it based on the attached prefab
        /// </summary>
        public void SetObjectSize(Vector2 size) {
            Size = size;
        }
    }
}