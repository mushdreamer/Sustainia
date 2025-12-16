using System;
using SpaceFusion.SF_Grid_Building_System.Scripts.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    public class ShopTabInitializer : MonoBehaviour
    {
        [SerializeField]
        private int maxTabHolderLength = 600;
        [SerializeField]
        private GridLayoutGroup gridLayoutGroup;
        [SerializeField]
        private GameObject buttonPrefab;
        [SerializeField]
        private ShopSwitcher shopSwitcher;

        private void Start()
        {
            var count = 0;
            Button firstButton = null;

            // 遍历枚举生成按钮
            foreach (ObjectGroup group in Enum.GetValues(typeof(ObjectGroup)))
            {
                var obj = Instantiate(buttonPrefab, transform);
                var button = obj.GetComponent<Button>();
                var text = obj.GetComponentInChildren<TextMeshProUGUI>();

                button.onClick.AddListener(() => {
                    shopSwitcher.ActivateGroup(group);
                });

                text.text = group.ToString();

                // 记录第一个按钮，用于自动选中
                if (count == 0) firstButton = button;

                count++;
            }

            // 调整布局大小
            if (count > 0)
            {
                var usedSpacing = count * gridLayoutGroup.spacing.x;
                var spaceLeft = maxTabHolderLength - usedSpacing;
                // 防止除以0，虽然这里count>0
                gridLayoutGroup.cellSize = new Vector2(Mathf.Ceil(spaceLeft / count), gridLayoutGroup.cellSize.y);

                // --- 优化：自动选中第一个标签页 (Building) ---
                // 这样进入游戏时，商店列表就不会是空的
                if (firstButton != null)
                {
                    // 稍微延迟一点点调用，确保 ShopSwitcher 已经初始化完成
                    // 或者直接在这里调用，因为 ShopSwitcher 的 Start 可能还没跑，
                    // 但 ShopSwitcher 的引用是直接拖拽的，其 Awake/Start 顺序不确定。
                    // 安全起见，我们直接手动调用一次 Switcher 的逻辑，或者依赖 Button 的事件

                    // 这里我们手动调用 ActivateGroup，假设 ObjectGroup 的第一个就是 Building
                    shopSwitcher.ActivateGroup((ObjectGroup)0);
                }
            }
        }
    }
}