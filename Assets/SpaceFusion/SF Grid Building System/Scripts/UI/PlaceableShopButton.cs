using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 1. 引入事件系统命名空间

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    // 2. 实现鼠标进入和离开的接口
    public class PlaceableShopButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private Button button;
        [SerializeField]
        private Image icon;

        private Placeable _placeable; // 3. 缓存数据引用

        public void Initialize(Placeable placeable)
        {
            _placeable = placeable; // 缓存数据

            button.onClick.AddListener(() => PlacementSystem.Instance.StartPlacement(placeable.GetAssetIdentifier()));
            if (placeable.Icon)
            {
                icon.sprite = placeable.Icon;
                icon.color = Color.white;
            }
            else
            {
                // fallback to name if icon not set
                button.GetComponentInChildren<TextMeshProUGUI>().text = placeable.GetAssetIdentifier();
            }
        }

        // 4. 鼠标进入时触发
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_placeable != null && ShopTooltipUI.Instance != null)
            {
                // 显示提示框，传入建筑名称 (AssetIdentifier)
                ShopTooltipUI.Instance.Show(_placeable.GetAssetIdentifier());
            }
        }

        // 5. 鼠标离开时触发
        public void OnPointerExit(PointerEventData eventData)
        {
            if (ShopTooltipUI.Instance != null)
            {
                ShopTooltipUI.Instance.Hide();
            }
        }
    }
}