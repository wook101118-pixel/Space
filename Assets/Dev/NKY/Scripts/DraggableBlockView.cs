using System;
using System.Collections.Generic;
using Dev.CSU._02_Scripts.RocketShooting;
using Dev.NKY.Scripts.Dev.NKY.Scripts;
using SpaceGame.CommonUI.Modal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Dev.NKY.Scripts
{
    [RequireComponent(typeof(CanvasGroup), typeof(BlockVisualizer))]
    public class DraggableBlockView : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private MachinePresent presentPrefab;
        [SerializeField] private SoundDataSO equipSound;
        [SerializeField] private SoundDataSO unEquipSound;
        [SerializeField] private SoundDataSO trashSound;
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private string rotateActionName = "Player/RotateBlock";

        private static readonly Dictionary<InputAction, ActionEnableLease>
            RotateActionLeases =
                new Dictionary<InputAction, ActionEnableLease>();

        private InventoryGrid grid;
        private InventoryGridView gridView;
        private BlockData data;
        public MachinePartsDataSo MachinePartsData { get; set; }
        private Transform homeParent;

        public event Action<DraggableBlockView> OnPlaced;
        public event Action<DraggableBlockView> OnUnplaced;
        public event Action<DraggableBlockView> OnDiscarded;

        private RectTransform rect;
        private CanvasGroup canvasGroup;
        private BlockVisualizer visualizer;
        private BlockInstance instance;

        private Vector2 dragStartAnchoredPos;
        private int dragStartRotation;
        private bool dragStartIsPlaced;
        private Vector2Int dragStartCell;
        private BlockInstance dragStartInstance;
        private Transform originalParent;
        private Vector2 dragOffset;
        private int rotation;
        private bool isDragging;
        private bool isPlaced;
        private PointerEventData currentEventData;
        private RectTransform dragParentRect;
        private InputAction rotateAction;
        private bool hasRotateActionLease;
        private ModalCancelRouter cancelRouter;
        private IDisposable dragCancelRegistration;
        private bool dragCancelled;

        private float CellSize => gridView != null ? gridView.CellSize : 64f;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            visualizer = GetComponent<BlockVisualizer>();

            if (TryGetComponent<Image>(out var rootImg)) rootImg.enabled = false;
        }

        private void OnEnable()
        {
            ResolveRotateAction();
        }

        private void OnDisable()
        {
            ReleaseRotateAction();
            ReleaseDragCancelRegistration();
            rotateAction = null;

            if (_present != null)
            {
                _present.HideSilently();
            }
        }

        public void Initialize(BlockData blockData, MachinePartsDataSo statData, InventoryGrid gridRef, InventoryGridView gridViewRef, Transform home)
        {
            data = blockData;

            // ★ 1. ScriptableObject를 복제하여 이 블록만의 독자적인 스탯 인스턴스를 만듭니다.
            if (statData != null)
            {
                MachinePartsData = Instantiate(statData);
            }

            grid = gridRef;
            gridView = gridViewRef;
            homeParent = home;
            isPlaced = false;

            visualizer.RebuildVisual(rect, data, rotation, CellSize, isPlaced);


            if (MachinePartsData != null && MachinePartsData.statData != null)
            {
                float shapeEfficiency = GetShapeEfficiencyMultiplier(
                    data != null && data.cells != null
                        ? data.cells.Count
                        : 4);

                for (int i = 0; i < MachinePartsData.statData.Count; i++)
                {
                    var stat = MachinePartsData.statData[i];
            
                    // Random.Range (float/int 판별하여 무작위 값 할당)
                    if (stat.isRandom) 
                    {
                        float minimum = Mathf.Min(stat.minValue, stat.maxValue);
                        float maximum = Mathf.Max(stat.minValue, stat.maxValue);
                        stat.value = Random.Range(minimum, maximum);
                    }

                    stat.value *= shapeEfficiency;
            
                    // struct일 경우 대비하여 원본 리스트에 다시 덮어쓰기
                    MachinePartsData.statData[i] = stat;
                }
            }

            // 툴팁 UI 초기화 (최상단 레이어 생성 방식 적용)
            var present = GetOrCreatePresent();
            if (present != null)
            {
                present.Initialize(MachinePartsData);
                present.HideSilently();
            }
        }
        
        public static float GetShapeEfficiencyMultiplier(int occupiedCells)
        {
            return Mathf.Max(1, occupiedCells) / 4f;
        }

        private void Update()
        {
            if (isDragging &&
                rotateAction != null &&
                rotateAction.enabled &&
                rotateAction.WasPressedThisFrame())
            {
                RotateBlock();
            }
        }

        private void ResolveRotateAction()
        {
            rotateAction = null;

            if (inputActionAsset == null)
            {
                Debug.LogWarning(
                    "[Inventory] DraggableBlockView에 InputActionAsset이 "
                    + "할당되지 않아 부품 회전 입력을 사용할 수 없습니다.",
                    this);
                return;
            }

            if (string.IsNullOrWhiteSpace(rotateActionName))
            {
                Debug.LogWarning(
                    "[Inventory] 부품 회전 Input Action 이름이 비어 있습니다.",
                    this);
                return;
            }

            rotateAction = inputActionAsset.FindAction(
                rotateActionName,
                false);
            if (rotateAction == null)
            {
                Debug.LogWarning(
                    $"[Inventory] Input Action '{rotateActionName}'을(를) "
                    + $"'{inputActionAsset.name}'에서 찾지 못했습니다.",
                    this);
            }
        }

        private void AcquireRotateAction()
        {
            if (rotateAction == null || hasRotateActionLease)
            {
                return;
            }

            if (!RotateActionLeases.TryGetValue(
                    rotateAction,
                    out ActionEnableLease lease))
            {
                lease = new ActionEnableLease
                {
                    enabledByViews = !rotateAction.enabled
                };
                RotateActionLeases.Add(rotateAction, lease);

                if (lease.enabledByViews)
                {
                    rotateAction.Enable();
                }
            }

            lease.userCount++;
            hasRotateActionLease = true;
        }

        private void ReleaseRotateAction()
        {
            if (!hasRotateActionLease || rotateAction == null)
            {
                hasRotateActionLease = false;
                return;
            }

            if (RotateActionLeases.TryGetValue(
                    rotateAction,
                    out ActionEnableLease lease))
            {
                lease.userCount--;
                if (lease.userCount <= 0)
                {
                    if (lease.enabledByViews && rotateAction.enabled)
                    {
                        rotateAction.Disable();
                    }

                    RotateActionLeases.Remove(rotateAction);
                }
            }

            hasRotateActionLease = false;
        }

        private void RotateBlock()
        {
            rotation = (rotation + 1) % 4;
            visualizer.RebuildVisual(rect, data, rotation, CellSize, isPlaced);
            Canvas.ForceUpdateCanvases();

            if (currentEventData != null && dragParentRect != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        dragParentRect, currentEventData.position, currentEventData.pressEventCamera, out var localMousePos))
                {
                    dragOffset = rect.anchoredPosition - localMousePos;
                }

                UpdatePreview(currentEventData);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_present != null)
            {
                _present.Hide();
            }
            
            isDragging = true;
            dragCancelled = false;
            AcquireRotateAction();
            RegisterDragCancel();
            currentEventData = eventData;
            dragStartAnchoredPos = rect.anchoredPosition;
            dragStartRotation = rotation;
            dragStartIsPlaced = isPlaced;
            dragStartCell = instance != null ? instance.origin : Vector2Int.zero;
            dragStartInstance = instance;
            originalParent = rect.parent;

            if (instance != null)
            {
                grid.Remove(instance);
                instance = null;
            }

            dragParentRect = GetDragLayer();

            if (isPlaced)
            {
                Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
                isPlaced = false;

                visualizer.RebuildVisual(rect, data, rotation, CellSize, isPlaced);

                rect.SetParent(dragParentRect, worldPositionStays: false);
                rect.position = worldCenter;
            }
            else
            {
                rect.SetParent(dragParentRect, worldPositionStays: true);
            }

            canvasGroup.blocksRaycasts = false;
            rect.SetAsLastSibling();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragParentRect, eventData.position, eventData.pressEventCamera, out var localMousePos))
            {
                dragOffset = rect.anchoredPosition - localMousePos;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            currentEventData = eventData;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragParentRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                rect.anchoredPosition = localPoint + dragOffset;
            }

            UpdatePreview(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragCancelled)
            {
                dragCancelled = false;
                return;
            }

            isDragging = false;
            ReleaseRotateAction();
            ReleaseDragCancelRegistration();
            currentEventData = null;
            canvasGroup.blocksRaycasts = true;
            gridView.ClearPreview();

            Vector2 originScreenPos = BlockLayoutCalculator.GetOriginCellScreenPos(
                rect, BlockShapeUtility.GetRotatedCells(data.cells, rotation), CellSize, isPlaced, eventData.pressEventCamera);

            // 1. 그리드 칸 위이며 장착 가능한 경우 -> 그리드 장착
            if (gridView.ScreenToGridCell(originScreenPos, eventData.pressEventCamera, out var cell))
            {
                var candidate = new BlockInstance(data, MachinePartsData, cell, rotation);

                if (grid.TryPlace(candidate))
                {
                    instance = candidate;
                    isPlaced = true;

                    visualizer.RebuildVisual(rect, data, rotation, CellSize, isPlaced);
                    rect.SetParent(gridView.transform, worldPositionStays: false);

                    Vector2Int topLeftCell = BlockLayoutCalculator.GetTopLeftGridCell(cell, BlockShapeUtility.GetRotatedCells(data.cells, rotation));
                    rect.anchoredPosition = gridView.GridCellToAnchoredPosition(topLeftCell);
                    rect.localScale = Vector3.one;
                    
                    SoundManager.Instance.PlaySFX(equipSound);

                    OnPlaced?.Invoke(this);
                    return;
                }
            }

            // ★ 2. 마우스 아래에 휴지통이 있는지 검사 -> 휴지통으로 버리기
            TrashCanUI trashCan = GetTrashCanUnderPointer(eventData);
            if (trashCan != null)
            {
                SoundManager.Instance.PlaySFX(trashSound);
                trashCan.ResetVisual();
                DiscardBlock();
                return;
            }

            // 3. 실패 시 슬롯으로 복귀
            RestoreDragStartState();
        }

        private void RegisterDragCancel()
        {
            ReleaseDragCancelRegistration();
            if (cancelRouter == null)
            {
                cancelRouter = FindFirstObjectByType<ModalCancelRouter>();
            }

            if (cancelRouter != null)
            {
                dragCancelRegistration = cancelRouter.Push(
                    CancelActiveDrag,
                    500);
            }
        }

        private bool CancelActiveDrag()
        {
            if (!isDragging)
            {
                return false;
            }

            isDragging = false;
            dragCancelled = true;
            ReleaseRotateAction();
            currentEventData = null;
            canvasGroup.blocksRaycasts = true;
            gridView?.ClearPreview();
            RestoreDragStartState();
            ReleaseDragCancelRegistration();
            return true;
        }

        private void ReleaseDragCancelRegistration()
        {
            dragCancelRegistration?.Dispose();
            dragCancelRegistration = null;
        }

        private void RestoreDragStartState()
        {
            rotation = dragStartRotation;

            if (dragStartIsPlaced)
            {
                BlockInstance restored =
                    dragStartInstance
                    ?? new BlockInstance(
                        data,
                        MachinePartsData,
                        dragStartCell,
                        dragStartRotation);

                if (grid.TryPlace(restored))
                {
                    instance = restored;
                    isPlaced = true;
                    visualizer.RebuildVisual(
                        rect,
                        data,
                        rotation,
                        CellSize,
                        isPlaced);
                    rect.SetParent(
                        originalParent != null
                            ? originalParent
                            : gridView.transform,
                        worldPositionStays: false);

                    Vector2Int topLeftCell =
                        BlockLayoutCalculator.GetTopLeftGridCell(
                            dragStartCell,
                            BlockShapeUtility.GetRotatedCells(
                                data.cells,
                                dragStartRotation));
                    rect.anchoredPosition =
                        gridView.GridCellToAnchoredPosition(topLeftCell);
                    rect.localScale = Vector3.one;
                    RocketShootingSoundPlayer.Play(
                        RocketShootingSoundCue.PartEquipFailed);
                    return;
                }

                Debug.LogError(
                    "[Inventory] Could not restore a block to its "
                    + "original cells after an invalid drop. Returning it "
                    + "to the tray to keep grid and stat state coherent.",
                    this);
            }

            RestoreUnplacedDragStart();
        }

        private void RestoreUnplacedDragStart()
        {
            SoundManager.Instance.PlaySFX(unEquipSound);
            instance = null;
            isPlaced = false;
            visualizer.RebuildVisual(
                rect,
                data,
                rotation,
                CellSize,
                isPlaced);
            Transform targetParent = dragStartIsPlaced
                ? homeParent
                : originalParent != null
                    ? originalParent
                    : homeParent;
            rect.SetParent(
                targetParent,
                worldPositionStays: false);
            rect.anchoredPosition = dragStartIsPlaced
                ? Vector2.zero
                : dragStartAnchoredPos;
            rect.localScale = Vector3.one;

            if (dragStartIsPlaced)
            {
                OnUnplaced?.Invoke(this);
            }
        }

        public void ReturnToTray()
        {
            if (instance != null)
            {
                grid.Remove(instance);
                instance = null;
            }

            isPlaced = false;
            rotation = dragStartRotation;
            visualizer.RebuildVisual(rect, data, rotation, CellSize, isPlaced);

            rect.SetParent(homeParent, worldPositionStays: false);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            OnUnplaced?.Invoke(this);
        }

        private void UpdatePreview(PointerEventData eventData)
        {
            Vector2 originScreenPos = BlockLayoutCalculator.GetOriginCellScreenPos(rect, BlockShapeUtility.GetRotatedCells(data.cells, rotation), CellSize, isPlaced, eventData.pressEventCamera);

            if (gridView.ScreenToGridCell(originScreenPos, eventData.pressEventCamera, out var cell))
            {
                var preview = new BlockInstance(data, MachinePartsData, cell, rotation);
                gridView.ShowPreview(preview, grid.CanPlace(preview));
            }
            else
            {
                gridView.ClearPreview();
            }
        }

        private RectTransform GetDragLayer()
        {
            if (dragLayer != null) return dragLayer;
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : originalParent as RectTransform;
        }

        private MachinePresent _present;
        private MachinePresent GetOrCreatePresent()
        {
            // 삭제되었거나 아직 없는 경우 최상단 DragLayer에 생성
            if (_present == null && presentPrefab != null)
            {
                Transform targetLayer = GetDragLayer();
                _present = Instantiate(presentPrefab, targetLayer);
            }
            return _present;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 드래그 중이 아닐 때만 툴팁 표시
            if (isDragging) return;

            var present = GetOrCreatePresent();
            if (present != null)
            {
                present.Show(transform.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_present != null)
            {
                _present.Hide();
            }
        }
        
        private TrashCanUI GetTrashCanUnderPointer(PointerEventData eventData)
        {
            if (eventData.pointerCurrentRaycast.gameObject != null)
            {
                return eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<TrashCanUI>();
            }
            return null;
        }

// ★ 블록 삭제 및 완전히 파괴하는 처리
        public void DiscardBlock()
        {
            // 그리드에 장착되어 있던 블록이면 그리드에서 제거 (스탯 차감도 자동 동작)
            if (instance != null)
            {
                grid.Remove(instance);
                instance = null;
            }

            // 툴팁 UI 정리
            if (_present != null)
            {
                Destroy(_present.gameObject);
            }

            // 슬롯 등 구독자들에게 버려짐 알림
            OnDiscarded?.Invoke(this);

            // 블록 오브젝트 완전 삭제
            Destroy(gameObject);
        }

        // 오브젝트 파괴 시 툴팁도 함께 정리
        private void OnDestroy()
        {
            ReleaseRotateAction();
            ReleaseDragCancelRegistration();

            if (_present != null)
            {
                Destroy(_present.gameObject);
            }
        }

        private sealed class ActionEnableLease
        {
            public int userCount;
            public bool enabledByViews;
        }
        
        
    }
}
