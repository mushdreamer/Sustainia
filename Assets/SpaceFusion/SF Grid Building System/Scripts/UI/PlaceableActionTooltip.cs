using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    public class PlaceableActionTooltip : MonoBehaviour
    {
        [SerializeField]
        private Button moveButton;

        [SerializeField]
        private Button removeButton;

        [Header("Institute Buttons")]
        [SerializeField]
        private Button researchButton;

        // <<< +++ 
        // +++ 1. 新增: 添加对 "升级" 按钮的引用
        // +++ 
        [Header("Building Action Buttons")]
        [SerializeField]
        private Button upgradeButton;
        // <<< +++ -------------------------- +++

        [SerializeField]
        private GameObject tooltipUI;

        [SerializeField]
        private GameObject blockerUI;

        [SerializeField]
        [Tooltip("Margin in pixels for the screen edges")]
        private int margin;

        private static PlaceableActionTooltip _instance;
        private PlacementSystem _placementSystem;
        private PlacedObject _placedObject;
        private Vector2 _tooltipSize;
        private Camera _targetCamera;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
            }
            else
            {
                _instance = this;
                _tooltipSize = GetComponent<RectTransform>().sizeDelta;
                moveButton.onClick.AddListener(MoveObject);
                removeButton.onClick.AddListener(RemoveObject);
                researchButton.onClick.AddListener(FundResearch);

                // <<< +++ 
                // +++ 2. 新增: 绑定新 "升级" 按钮的点击事件
                // +++ 
                if (upgradeButton != null) // (添加一个非空检查更安全)
                {
                    upgradeButton.onClick.AddListener(UpgradeObject);
                }
                // <<< +++ ---------------------------------- +++

                tooltipUI.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            _placementSystem = PlacementSystem.Instance;
            _targetCamera = GameManager.Instance.SceneCamera;
            InputManager.Instance.OnExit += CloseTooltip;
            PlacedObject.holdComplete += ShowTooltip;
        }

        private void OnDestroy()
        {
            InputManager.Instance.OnExit -= CloseTooltip;
            PlacedObject.holdComplete -= ShowTooltip;
        }

        private void CloseTooltip()
        {
            tooltipUI.gameObject.SetActive(false);
            blockerUI.SetActive(false);
        }

        private void Show(PlacedObject caller)
        {
            _placedObject = caller;
            var buildingType = _placedObject.buildingEffect.type;

            moveButton.gameObject.SetActive(true);
            removeButton.gameObject.SetActive(true);

            // <<< --- 
            // --- 3. 修改: 这里的逻辑需要更新
            // ---

            // 旧逻辑:
            // researchButton.gameObject.SetActive(buildingType == BuildingType.Institute);

            // 新逻辑:
            if (buildingType == BuildingType.Institute)
            {
                // 如果是大学，显示 "科研" 按钮，隐藏 "升级" 按钮
                researchButton.gameObject.SetActive(true);
                if (upgradeButton != null)
                {
                    upgradeButton.gameObject.SetActive(false);
                }
            }
            else
            {
                // 如果是其他建筑 (房子, 农场等)，隐藏 "科研" 按钮，显示 "升级" 按钮
                researchButton.gameObject.SetActive(false);
                if (upgradeButton != null)
                {
                    upgradeButton.gameObject.SetActive(true);
                }
            }
            // <<< --- 
            // --- 修改结束
            // --- 

            var screenPosition = _targetCamera.WorldToScreenPoint(caller.transform.position);
            tooltipUI.gameObject.SetActive(true);
            tooltipUI.transform.position = RecalculatePositionWithinBounds(screenPosition);
            blockerUI.SetActive(true);

        }

        private Vector3 RecalculatePositionWithinBounds(Vector3 screenPosition)
        {
            var newPosition = new Vector3(screenPosition.x, screenPosition.y, screenPosition.z);
            if (screenPosition.x < 0)
            {
                newPosition.x = margin;
            }
            else if (screenPosition.x + _tooltipSize.x > Screen.width)
            {
                newPosition.x = Screen.width - _tooltipSize.x - margin;
            }

            if (screenPosition.y < 0)
            {
                newPosition.y = margin;
            }
            else if (screenPosition.y + _tooltipSize.y > Screen.height)
            {
                newPosition.y = Screen.height - _tooltipSize.y - margin;
            }

            return newPosition;
        }

        private void MoveObject()
        {
            _placementSystem.StartMoving(_placedObject);
            HideTooltip();
        }

        private void RemoveObject()
        {
            _placementSystem.Remove(_placedObject);
            HideTooltip();
        }

        private void HideTooltip()
        {
            tooltipUI.gameObject.SetActive(false);
            blockerUI.SetActive(false);
        }

        private void FundResearch()
        {
            ResourceManager.Instance.FundResearch();
            HideTooltip();
        }

        // <<< +++ 
        // +++ 4. 新增: 升级按钮调用的方法
        // +++ 
        private void UpgradeObject()
        {
            // 它会调用我们之前在 BuildingEffect.cs 中创建的 TryUpgradeBuilding 方法
            if (_placedObject != null && _placedObject.buildingEffect != null)
            {
                _placedObject.buildingEffect.TryUpgradeBuilding();
            }
            HideTooltip(); // 无论升级是否成功 (比如钱不够)，都关闭菜单
        }
        // <<< +++ ---------------------------------- +++

        private static void ShowTooltip(PlacedObject caller)
        {
            _instance.Show(caller);
        }
    }
}