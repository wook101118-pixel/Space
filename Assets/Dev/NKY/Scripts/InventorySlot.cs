using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Dev.NKY.Scripts
{
    [RequireComponent(typeof(RectTransform))]
    public class InventorySlot : MonoBehaviour
    {
        [SerializeField] private DraggableBlockView blockPrefab;
        [SerializeField] private InventoryTray tray;
        [SerializeField] private UnityEngine.UI.Image background;
        [SerializeField] private MachinePresent present;
        [SerializeField] private TextMeshProUGUI numberText;

        [SerializeField] private InventoryGrid grid;
        [SerializeField] private InventoryGridView gridView;

        private readonly List<DraggableBlockView> blockList = new List<DraggableBlockView>();

        public void SetGridReferences(InventoryGrid gridRef, InventoryGridView gridViewRef)
        {
            grid = gridRef;
            gridView = gridViewRef;
        }

        private void Start()
        {
            SpawnNewBlock();
        }

        public void SpawnNewBlock()
        {
            if (tray == null) return;
            if (!tray.TryTakeRandom(out var blockData, out var statData)) return;

            var newBlock = Instantiate(blockPrefab, transform);
            newBlock.Initialize(blockData, statData, grid, gridView, transform);

            RegisterBlock(newBlock);
        }

        private void RegisterBlock(DraggableBlockView block)
        {
            if (!blockList.Contains(block))
            {
                blockList.Add(block);
            }

            block.OnPlaced -= HandleBlockPlaced;
            block.OnPlaced += HandleBlockPlaced;
            block.OnUnplaced -= HandleBlockUnplaced;
            block.OnUnplaced += HandleBlockUnplaced;
    
            // ★ 삭제 이벤트 연결
            block.OnDiscarded -= HandleBlockDiscarded;
            block.OnDiscarded += HandleBlockDiscarded;

            UpdateSlotDisplay();
        }

        // 그리드 배치 성공 -> 슬롯 목록에서만 제거
        private void HandleBlockPlaced(DraggableBlockView view)
        {
            view.OnPlaced -= HandleBlockPlaced; 
            // ★ 중요: OnUnplaced 해제 구문을 제거했습니다!
            // 나중에 그리드에서 슬롯으로 다시 되돌아올 때 OnUnplaced 이벤트를 받아야 하기 때문입니다.

            blockList.Remove(view);
            
            UpdateSlotDisplay();
        }

        // 배치 실패 또는 그리드에서 슬롯으로 복귀했을 때 실행
        private void HandleBlockUnplaced(DraggableBlockView view)
        {
            if (!blockList.Contains(view))
            {
                blockList.Add(view);
            }

            // 슬롯으로 되돌아왔으므로 다시 그리드에 장착할 수 있도록 OnPlaced 재연결
            view.OnPlaced -= HandleBlockPlaced;
            view.OnPlaced += HandleBlockPlaced;

            UpdateSlotDisplay();
        }

        private int currentIndex = 0; // ★ 현재 보고 있는 블록의 번호 (0부터 시작)

        private void UpdateSlotDisplay()
        {
            if (blockList.Count == 0)
            {
                numberText.text = "0 / 0";
                present.NothingPart();
                return;
            }

            // 블록이 삭제되거나 해서 인덱스가 범위를 벗어나는 것 방지
            if (currentIndex >= blockList.Count)
            {
                currentIndex = blockList.Count - 1;
            }
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            for (int i = 0; i < blockList.Count; i++)
            {
                if (blockList[i] == null) continue;

                // ★ 현재 선택된 currentIndex 위치의 블록만 활성화
                bool isTop = (i == currentIndex);
                blockList[i].gameObject.SetActive(isTop);

                if (isTop)
                {
                    var rt = blockList[i].GetComponent<RectTransform>();
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = Vector3.one;
                    present.Initialize(blockList[i].MachinePartsData);
                }
            }
            

            numberText.text = $"{currentIndex + 1} / {blockList.Count}";
        }

        public void OnClickNextBlock()
        {
            if (blockList.Count <= 1) return;

            // 다음 위치로 이동 (마지막에 도달하면 0으로 돌아옴)
            currentIndex = (currentIndex + 1) % blockList.Count;

            UpdateSlotDisplay();
        }

        public void OnClickPreviousBlock()
        {
            if (blockList.Count <= 1) return;

            // 이전 위치로 이동 (0보다 작아지면 마지막 번호로 돌아옴)
            currentIndex = (currentIndex - 1 + blockList.Count) % blockList.Count;

            UpdateSlotDisplay();
        }
        
        private void HandleBlockDiscarded(DraggableBlockView view)
        {
            view.OnPlaced -= HandleBlockPlaced;
            view.OnUnplaced -= HandleBlockUnplaced;
            view.OnDiscarded -= HandleBlockDiscarded;

            blockList.Remove(view);
            
            UpdateSlotDisplay();
        }
    }
}
