using System;
using System.Collections;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Utils;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public class CameraController : MonoBehaviour
    {
        [Header("Smooth Settings")]
        public float positionLerpTime = 10f;
        public float rotationLerpTime = 10f;

        [Header("Movement Settings")]
        public float keyboardMoveSpeed = 50f;
        public float keyboardRotateSpeed = 200f; // 旋转灵敏度
        public float zoomSpeed = 20f;         // 缩放灵敏度

        [Header("Limits")]
        public float minHeight = 5.0f;
        public float maxHeight = 80.0f;
        public float minPitchAngle = 20f;
        public float maxPitchAngle = 80f;

        private float _targetYaw;
        private float _targetPitch;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Camera _sceneCamera;

        private void Start()
        {
            _sceneCamera = GetComponent<Camera>();
            if (_sceneCamera == null) Debug.LogError("Missing Camera Component!");

            _targetPosition = transform.position;
            _targetYaw = transform.eulerAngles.y;
            _targetPitch = transform.eulerAngles.x;
            UpdateTargetRotationFromEuler();
        }

        private void Update()
        {
            HandleMovementInput();
            HandleRotationAndZoomInput();

            // 平滑应用变换
            transform.position = Vector3.Lerp(transform.position, _targetPosition, positionLerpTime * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, rotationLerpTime * Time.deltaTime);
        }

        private void HandleMovementInput()
        {
            float h = Input.GetAxis("Horizontal"); // A/D
            float v = Input.GetAxis("Vertical");   // W/S

            // 基于当前偏航角计算移动方向，使W永远是向前
            Vector3 forward = Quaternion.Euler(0, _targetYaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _targetYaw, 0) * Vector3.right;
            Vector3 moveDir = (forward * v + right * h).normalized;

            _targetPosition += moveDir * (keyboardMoveSpeed * Time.deltaTime);
        }

        private void HandleRotationAndZoomInput()
        {
            // 1. 右键旋转 [替代 Q/E]
            if (Input.GetMouseButton(1)) // 0:左键, 1:右键, 2:中键
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                _targetYaw += mouseX * keyboardRotateSpeed * Time.deltaTime;
                _targetPitch -= mouseY * keyboardRotateSpeed * Time.deltaTime; // 鼠标上滑抬头

                UpdateTargetRotationFromEuler();
            }

            // 2. 鼠标滚轮缩放
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // 计算缩放方向（镜头正对着的方向）
                Vector3 zoomDir = transform.forward;
                Vector3 moveAmount = zoomDir * (scroll * zoomSpeed * 10f);

                Vector3 newPos = _targetPosition + moveAmount;

                // 限制缩放高度，防止钻地或飞出地图
                if (newPos.y >= minHeight && newPos.y <= maxHeight)
                {
                    _targetPosition = newPos;
                }
            }
        }

        private void UpdateTargetRotationFromEuler()
        {
            _targetPitch = Mathf.Clamp(_targetPitch, minPitchAngle, maxPitchAngle);
            _targetRotation = Quaternion.Euler(_targetPitch, _targetYaw, 0f);
        }

        public void SyncTutorialFocus(Vector3 target, float distance, float pitch, float yaw)
        {
            _targetYaw = yaw;
            _targetPitch = pitch;
            float rad = pitch * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(0, Mathf.Sin(rad), -Mathf.Cos(rad)) * distance;
            _targetPosition = target + offset;
            UpdateTargetRotationFromEuler();
        }
    }
}