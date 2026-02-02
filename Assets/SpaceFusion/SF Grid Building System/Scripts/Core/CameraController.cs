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
        public float sprintMultiplier = 2.5f;
        public float keyboardRotateSpeed = 100f;

        [Header("Adaptive Speed")]
        public bool useAdaptiveSpeed = true;
        public float referenceHeight = 20f;
        public float maxSpeedMultiplier = 4f;

        [Header("Limits")]
        public float minHeight = 2.0f;
        public float maxHeight = 500.0f;
        public float minPitchAngle = 10f;
        public float maxPitchAngle = 85f;

        private float _targetYaw;
        private float _targetPitch;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Camera _sceneCamera;
        private GameConfig _config;

        private void Start()
        {
            _config = GameConfig.Instance;
            _sceneCamera = GetComponent<Camera>();
            if (_sceneCamera == null) Debug.LogError("CameraController 必须挂载在带有 Camera 组件的物体上！");
            _targetPosition = transform.position;
            Vector3 angles = transform.eulerAngles;
            _targetYaw = angles.y;
            _targetPitch = angles.x;
            _targetRotation = transform.rotation;
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
            if (Input.GetKey(KeyCode.Q)) rotate = -1f;
            if (Input.GetKey(KeyCode.E)) rotate = 1f;

            float speedMult = GetHeightSpeedMultiplier() * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
            Quaternion yawRotation = Quaternion.Euler(0, _targetYaw, 0);
            Vector3 moveDir = (yawRotation * Vector3.forward * v + yawRotation * Vector3.right * h).normalized;

            if (moveDir.sqrMagnitude > 0.01f) _targetPosition += moveDir * (keyboardMoveSpeed * speedMult * Time.deltaTime);
            if (Mathf.Abs(rotate) > 0.01f) { _targetYaw += rotate * keyboardRotateSpeed * Time.deltaTime; UpdateTargetRotationFromEuler(); }
        }

        private void UpdateCameraTransform()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, positionLerpTime * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, rotationLerpTime * Time.deltaTime);
        }

        private void UpdateTargetRotationFromEuler()
        {
            _targetPitch = Mathf.Clamp(_targetPitch, minPitchAngle, maxPitchAngle);
            _targetRotation = Quaternion.Euler(_targetPitch, _targetYaw, 0f);
        }

        private float GetHeightSpeedMultiplier() => useAdaptiveSpeed ? Mathf.Clamp(transform.position.y / referenceHeight, 0.5f, maxSpeedMultiplier) : 1f;

        // --- 核心修改：教程专用聚焦接口 ---
        public void FocusOnPosition(Vector3 target, float distance, float pitch, float yaw)
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