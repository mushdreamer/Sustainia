using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

using CoreBuildingType = SpaceFusion.SF_Grid_Building_System.Scripts.Core.BuildingType;

public class MultiZoneCityGenerator : MonoBehaviour
{
    public static MultiZoneCityGenerator Instance;

    [System.Serializable]
    public class GenerationZone
    {
        public string zoneName = "Area 1";
        public Transform originPoint;
        public int width = 5;
        public int height = 5;
        public float visualHeightOffset = 0.0f;
        public bool isOccupied = false;

        // --- 教学高亮支持 ---
        [HideInInspector] public bool isTutorialHighlight = false;
        [HideInInspector] public Color customHighlightColor;

        [HideInInspector] public LineRenderer groundOutline;
        [HideInInspector] public TextMesh statusText;
        [HideInInspector] public Transform arrowObj;
        [HideInInspector] public Material instanceLineMat;

        public bool Contains(Vector3 worldPos, float cellSize)
        {
            if (originPoint == null) return false;
            float diffX = worldPos.x - originPoint.position.x;
            float diffZ = worldPos.z - originPoint.position.z;
            float relativeX = diffX / cellSize;
            float relativeZ = diffZ / cellSize;
            return relativeX >= 0 && relativeX < width && relativeZ >= 0 && relativeZ < height;
        }
    }

    [System.Serializable]
    public struct BuildingType { public string name; public GameObject prefab; public Placeable data; }

    [Header("Global Settings")]
    public float cellSize = 10.0f;
    public List<BuildingType> buildingOptions;
    public float buildingYOffset = 0.0f;

    [Header("Optimization Objectives")]
    public float targetCo2 = 50.0f;
    public float targetCost = 1000.0f;
    public float targetEnergy = 100.0f;
    public float weightCo2 = 1.0f;
    public float weightCost = 1.0f;
    public float weightEnergy = 1.5f;

    [Header("Visual Feedback")]
    public Color validColor = Color.green;
    public Color occupiedColor = new Color(1f, 0.6f, 0f);
    public float lineWidth = 0.05f;
    public float lineYOffset = 0.1f;
    public float baseUiHeight = 12.0f;
    public float animSpeed = 2.0f;
    public int circleSegments = 128;

    [Header("Control")]
    public bool autoStartGeneration = false;

    private float _currentCo2 = 0f;
    private float _currentCost = 0f;
    private float _currentEnergy = 0f;

    [Header("Zones Configuration")]
    public List<GenerationZone> zones;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); }
        Instance = this;
    }

    IEnumerator Start()
    {
        InitZoneVisuals();
        yield return null;
        if (autoStartGeneration) BeginGeneration();
    }

    private void Update() => AnimateVisuals();

    // ----------------------
    // [补充逻辑] 修复报错所需的外部接口
    // ----------------------

    public bool IsZoneValidAndEmpty(Vector3 worldPos)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        return zone != null && !zone.isOccupied;
    }

    public bool IsZoneAvailableForBuilding(Vector3 worldPos) => IsZoneValidAndEmpty(worldPos);

    public void SetZoneOccupiedState(Vector3 worldPos, bool isOccupied) => SetZoneOccupiedStatus(worldPos, isOccupied);

    public void SetZoneOccupiedStatus(Vector3 worldPos, bool occupied)
    {
        GenerationZone zone = GetZoneAtPosition(worldPos);
        if (zone != null) zone.isOccupied = occupied;
    }

    private GenerationZone GetZoneAtPosition(Vector3 worldPos)
    {
        foreach (var zone in zones)
        {
            if (zone.Contains(worldPos, cellSize)) return zone;
        }
        return null;
    }

    // ----------------------
    // [教学核心] 强制生成与动画
    // ----------------------

    public void ForceSpawnBuildingInZone(int zoneIndex, CoreBuildingType type)
    {
        if (zoneIndex < 0 || zoneIndex >= zones.Count) return;
        BuildingType targetOption = buildingOptions.Find(b => b.data.Prefab.GetComponent<BuildingEffect>().type == type);
        if (targetOption.prefab != null)
        {
            GenerationZone zone = zones[zoneIndex];
            if (zone.originPoint != null)
            {
                foreach (Transform child in zone.originPoint)
                {
                    if (child.name != "RingOutline" && child.name != "StatusLabel" && child.name != "ArrowIndicator")
                        Destroy(child.gameObject);
                }
            }
            GenerateOneZone(zone, targetOption);
            zone.isOccupied = true;
        }
    }

    private void AnimateVisuals()
    {
        if (zones == null) return;
        float timeVar = Time.unscaledTime;
        float bounceY = Mathf.Sin(timeVar * animSpeed) * 1.5f;
        float rotateAngle = timeVar * 90f;

        foreach (var zone in zones)
        {
            Color targetColor = zone.isTutorialHighlight ? zone.customHighlightColor : (zone.isOccupied ? occupiedColor : validColor);
            float finalHeight = baseUiHeight + zone.visualHeightOffset;
            Vector3 center = GetZoneCenter(zone);

            if (zone.arrowObj != null)
            {
                zone.arrowObj.position = center + Vector3.up * (finalHeight + bounceY);
                zone.arrowObj.rotation = Quaternion.Euler(0, rotateAngle, 180);
                foreach (var r in zone.arrowObj.GetComponentsInChildren<Renderer>()) r.material.color = targetColor;
            }

            if (zone.statusText != null)
            {
                if (Camera.main != null) zone.statusText.transform.rotation = Quaternion.LookRotation(zone.statusText.transform.position - Camera.main.transform.position);
                zone.statusText.transform.position = center + Vector3.up * (finalHeight - 2.5f);
                zone.statusText.color = targetColor;
                zone.statusText.text = zone.isOccupied ? "TUTORIAL\nTARGET" : "OPEN\nSLOT";
            }

            if (zone.instanceLineMat != null)
            {
                zone.instanceLineMat.color = targetColor;
                UpdateCirclePositions(zone);
            }
        }
    }

    private void UpdateCirclePositions(GenerationZone zone)
    {
        if (zone.groundOutline == null) return;
        zone.groundOutline.startWidth = lineWidth;
        zone.groundOutline.endWidth = lineWidth;
        float radiusX = zone.width * cellSize * 0.5f;
        float radiusZ = zone.height * cellSize * 0.5f;
        float centerX = zone.width * cellSize * 0.5f;
        float centerZ = zone.height * cellSize * 0.5f;
        zone.groundOutline.positionCount = circleSegments + 1;
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            zone.groundOutline.SetPosition(i, new Vector3(centerX + Mathf.Cos(angle) * radiusX, lineYOffset, centerZ + Mathf.Sin(angle) * radiusZ));
        }
    }

    private void InitZoneVisuals()
    {
        foreach (var zone in zones)
        {
            if (zone.originPoint == null) continue;
            Vector3 centerPos = GetZoneCenter(zone);
            GameObject container = new GameObject($"{zone.zoneName}_Visuals");
            container.transform.SetParent(this.transform);

            GameObject lineObj = new GameObject("RingOutline");
            lineObj.transform.SetParent(container.transform);
            lineObj.transform.position = zone.originPoint.position;
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zone.instanceLineMat = lr.material;
            zone.groundOutline = lr;
            UpdateCirclePositions(zone);

            GameObject textObj = new GameObject("StatusLabel");
            textObj.transform.SetParent(container.transform);
            textObj.transform.position = centerPos;
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.5f;
            tm.fontSize = 20;
            zone.statusText = tm;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ArrowIndicator";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(container.transform);
            marker.transform.localScale = new Vector3(2f, 2f, 2f);
            zone.arrowObj = marker.transform;
            marker.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    private Vector3 GetZoneCenter(GenerationZone zone) => zone.originPoint.position + new Vector3(zone.width * cellSize / 2f, 0, zone.height * cellSize / 2f);

    public void BeginGeneration() { StopAllCoroutines(); StartCoroutine(GenerateZonesSequence()); }
    IEnumerator GenerateZonesSequence() { yield break; }
    public void ClearAndRestartGeneration() { }

    void GenerateOneZone(GenerationZone zone, BuildingType specificBuildingType)
    {
        if (zone.originPoint == null) return;
        Vector3 centerWorldPos = GetZoneCenter(zone);
        Vector3 spawnPos = new Vector3(centerWorldPos.x, zone.originPoint.position.y + buildingYOffset, centerWorldPos.z);
        GameObject newBuilding = Instantiate(specificBuildingType.prefab, spawnPos, Quaternion.identity, zone.originPoint);
        AttachAndInitialize(newBuilding, specificBuildingType.data, spawnPos);
    }

    void AttachAndInitialize(GameObject building, Placeable placeableData, Vector3 worldPos)
    {
        PlacedObject placedObj = building.GetComponent<PlacedObject>() ?? building.AddComponent<PlacedObject>();
        if (GameManager.Instance != null && GameManager.Instance.PlacementGrid != null)
        {
            Vector3Int gridPos = GameManager.Instance.PlacementGrid.WorldToCell(worldPos);
            placedObj.Initialize(placeableData, gridPos);
        }
    }
}