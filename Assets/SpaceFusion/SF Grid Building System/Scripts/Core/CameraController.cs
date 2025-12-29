using System;
using System.Collections;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    /// <summary>
    /// 相机控制器
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Smooth Settings")]
        public float positionLerpTime = 10f;
        public float rotationLerpTime = 10f;

        [Header("Movement Settings")]
        public float keyboardMoveSpeed = 50f;
        public float sprintMultiplier = 2.5f;
        public float keyboardRotateSpeed = 100f;

        [Header("Adaptive Speed")]
        public bool useAdaptiveSpeed = true;
        public float referenceHeight = 20f;
        public float maxSpeedMultiplier = 4f;

        [Header("Limits (角度与范围限制)")]
        [Tooltip("最小高度 (防止钻地)")]
        public float minHeight = 2.0f;
        [Tooltip("最大高度")]
        public float maxHeight = 500.0f;

        [Tooltip("最小俯仰角 (10度=平视，90度=垂直俯视)")]
        public float minPitchAngle = 10f;
        [Tooltip("最大俯仰角 (建议85度，不要超过90度)")]
        public float maxPitchAngle = 85f;

        [Tooltip("是否启用 X/Z 轴的地图边界限制")]
        public bool enableMapBounds = false;
        [Tooltip("地图边界范围 (MinX, MinZ, MaxX, MaxZ)")]
        public Vector4 mapBounds = new Vector4(-500, -500, 500, 500);

        // --- 核心修改：使用 Euler 角度来控制旋转，而不是 Quaternion 累乘 ---
        private float _targetYaw;   // 水平旋转 (Y轴)
        private float _targetPitch; // 俯仰旋转 (X轴)
        // ----------------------------------------------------------------

        private Vector3 _targetPosition;
        private Quaternion _targetRotation; // 这个现在由 Yaw/Pitch 计算得出

        private Vector3 _groundCamOffset;
        private Camera _sceneCamera;
        private bool _startedHoldingOverUI;
        private bool _isInPlacementState;
        private GameConfig _config;

        private void Start()
        {
            _config = GameConfig.Instance;
            _sceneCamera = GetComponent<Camera>();

            _targetPosition = transform.position;

            // --- 初始化角度 ---
            Vector3 angles = transform.eulerAngles;
            _targetYaw = angles.y;
            _targetPitch = angles.x;
            _targetRotation = transform.rotation;
            // ----------------

            var groundPos = GetWorldPosAtViewportPoint(0.5f, 0.5f);
            _groundCamOffset = _sceneCamera.transform.position - groundPos;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLmbRelease += ResetUIHold;
                InputManager.Instance.OnMmbDrag += HandleDrag;
                InputManager.Instance.OnRmbDrag += HandleRotation;
                InputManager.Instance.OnScroll += HandleZoom;
                InputManager.Instance.OnMouseAtScreenCorner += HandleMouseAtScreenCorner;
            }
            if (PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.OnPlacementStateStart += PlacementStateActivated;
                PlacementSystem.Instance.OnPlacementStateEnd += PlacementStateEnded;
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLmbRelease -= ResetUIHold;
                InputManager.Instance.OnMmbDrag -= HandleDrag;
                InputManager.Instance.OnRmbDrag -= HandleRotation;
                InputManager.Instance.OnScroll -= HandleZoom;
                InputManager.Instance.OnMouseAtScreenCorner -= HandleMouseAtScreenCorner;
            }
            if (PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.OnPlacementStateStart -= PlacementStateActivated;
                PlacementSystem.Instance.OnPlacementStateEnd -= PlacementStateEnded;
            }
        }

        private void Update()
        {
            HandleKeyboardInput();
            UpdateCameraTransform();
        }

        private void HandleKeyboardInput()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            float rotate = 0f;

            // Q/E 旋转现在直接修改 Yaw 值
            if (Input.GetKey(KeyCode.Q)) rotate = -1f;
            if (Input.GetKey(KeyCode.E)) rotate = 1f;

            // 1. 处理移动
            float speedMult = 1f;
            if (Input.GetKey(KeyCode.LeftShift)) speedMult = sprintMultiplier;
            speedMult *= GetHeightSpeedMultiplier();

            // 计算移动方向 (基于当前的 Yaw，忽略 Pitch)
            // 这样无论你看哪里，按W都是向前飞
            Quaternion yawRotation = Quaternion.Euler(0, _targetYaw, 0);
            Vector3 forward = yawRotation * Vector3.forward;
            Vector3 right = yawRotation * Vector3.right;

            Vector3 moveDir = (forward * v + right * h).normalized;

            if (moveDir.sqrMagnitude > 0.01f)
            {
                _targetPosition += moveDir * (keyboardMoveSpeed * speedMult * Time.deltaTime);
                ClampTargetPosition();
            }

            // 2. 处理键盘旋转
            if (Mathf.Abs(rotate) > 0.01f)
            {
                _targetYaw += rotate * keyboardRotateSpeed * Time.deltaTime;
                // 更新目标旋转
                UpdateTargetRotationFromEuler();
            }
        }

        private void UpdateCameraTransform()
        {
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, _targetPosition, positionLerpTime * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, rotationLerpTime * dt);
        }

        // --- 核心方法：根据 Yaw 和 Pitch 重新计算 Quaternion ---
        private void UpdateTargetRotationFromEuler()
        {
            // 1. 限制俯仰角 (Clamp Pitch)
            _targetPitch = Mathf.Clamp(_targetPitch, minPitchAngle, maxPitchAngle);

            // 2. 重建 Quaternion (Z轴 Roll 永远为 0)
            _targetRotation = Quaternion.Euler(_targetPitch, _targetYaw, 0f);
        }
        // -----------------------------------------------------

        private float GetHeightSpeedMultiplier()
        {
            if (!useAdaptiveSpeed) return 1f;
            float currentHeight = transform.position.y;
            float multiplier = currentHeight / Mathf.Max(1f, referenceHeight);
            return Mathf.Clamp(multiplier, 0.5f, maxSpeedMultiplier);
        }

        private Vector3 GetWorldPosAtViewportPoint(float vx, float vy)
        {
            var worldRay = _sceneCamera.ViewportPointToRay(new Vector3(vx, vy, 0));
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            groundPlane.Raycast(worldRay, out var distanceToGround);
            return worldRay.GetPoint(distanceToGround);
        }

        private void ResetUIHold()
        {
            _startedHoldingOverUI = false;
        }

        private void HandleDrag(Vector2 mouseDelta)
        {
            if (_startedHoldingOverUI) return;

            float adaptiveMult = GetHeightSpeedMultiplier();

            // 拖拽时也需要基于当前的 Yaw 计算方向
            Quaternion yawRotation = Quaternion.Euler(0, _targetYaw, 0);
            Vector3 moveDirection = yawRotation * Vector3.right * mouseDelta.x + yawRotation * Vector3.forward * mouseDelta.y;

            _targetPosition -= moveDirection * (_config.DragSpeed * adaptiveMult * Time.deltaTime);
            ClampTargetPosition();
        }

        private void HandleRotation(Vector2 mouseDelta)
        {
            var rotationSpeed = _config.RotationSpeed;

            // 1. 水平旋转 (修改 Yaw)
            _targetYaw += mouseDelta.x * rotationSpeed * Time.deltaTime;

            // 2. 垂直旋转 (修改 Pitch)
            // 注意：鼠标向上推(y>0)，视角应该抬头(Pitch变小/负)，还是低头？
            // 通常鼠标上推视角抬头（Pitch减小），鼠标下推视角低头（Pitch增加）
            // 如果觉得反了，把这里的 '-' 改成 '+'
            _targetPitch -= mouseDelta.y * rotationSpeed * Time.deltaTime;

            // 3. 应用并限制
            UpdateTargetRotationFromEuler();
        }

        private void HandleMouseAtScreenCorner(Vector2 direction)
        {
            if (_config.EnableAutoMove == false) return;
            if (_config.RestrictAutoMoveForPlacement && !_isInPlacementState) return;

            float adaptiveMult = GetHeightSpeedMultiplier();

            // 屏幕边缘移动也需要基于 Yaw
            Quaternion yawRotation = Quaternion.Euler(0, _targetYaw, 0);
            Vector3 moveVec = yawRotation * new Vector3(direction.x, 0, direction.y);

            _targetPosition += moveVec * (_config.DragSpeed * adaptiveMult * Time.deltaTime);
            ClampTargetPosition();
        }

        private void HandleZoom(float scrollDelta)
        {
            if (scrollDelta == 0 || !_sceneCamera) return;

            var mouseRay = _sceneCamera.ScreenPointToRay(Input.mousePosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(mouseRay, out var distance))
            {
                var mouseWorldPos = mouseRay.GetPoint(distance);
                var zoomDirection = (mouseWorldPos - _targetPosition).normalized;

                float adaptiveMult = GetHeightSpeedMultiplier();
                Vector3 diff = zoomDirection * (scrollDelta * _config.ZoomSpeed * adaptiveMult);

                float predictedY = _targetPosition.y + diff.y;

                if (predictedY > minHeight && predictedY < maxHeight)
                {
                    _targetPosition += diff;
                }
            }
        }

        private void ClampTargetPosition()
        {
            if (!enableMapBounds) return;

            float x = Mathf.Clamp(_targetPosition.x, mapBounds.x, mapBounds.z);
            float z = Mathf.Clamp(_targetPosition.z, mapBounds.y, mapBounds.w);

            _targetPosition = new Vector3(x, _targetPosition.y, z);
        }

        private void PlacementStateActivated()
        {
            _isInPlacementState = true;
        }

        private void PlacementStateEnded()
        {
            _isInPlacementState = false;
        }

        public void FocusOnPosition(Vector3 target, float duration)
        {
            _targetPosition = target + _groundCamOffset;
        }

        private void OnDrawGizmosSelected()
        {
            if (enableMapBounds)
            {
                Gizmos.color = Color.yellow;
                float width = mapBounds.z - mapBounds.x;
                float height = mapBounds.w - mapBounds.y;
                float centerX = (mapBounds.x + mapBounds.z) / 2;
                float centerZ = (mapBounds.y + mapBounds.w) / 2;

                Vector3 center = new Vector3(centerX, 0, centerZ);
                Vector3 size = new Vector3(width, 100, height);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}