using System;
using UnityEngine;

namespace Dev.NKY.Scripts
{
    public class InventoryGrid : MonoBehaviour
    {
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;
 
        public int Width => width;
        public int Height => height;
 
        private BlockInstance[,] cellOwner;
 
        // Grid는 Stats를 전혀 몰라도 됨. 배치/제거만 알림.
        public event Action<BlockInstance> OnBlockPlaced;
        public event Action<BlockInstance> OnBlockRemoved;
 
        private void Awake()
        {
            cellOwner = new BlockInstance[width, height];
        }

#if UNITY_INCLUDE_TESTS
        public void InitializeForTests()
        {
            cellOwner = new BlockInstance[width, height];
        }
#endif
 
        public bool IsInside(Vector2Int cell)
            => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
 
        public bool CanPlace(BlockInstance instance)
        {
            
            foreach (var cell in instance.GetOccupiedCells())
            {
                if (!IsInside(cell)) return false;
                if (cellOwner[cell.x, cell.y] != null) return false;
            }
            return true;
        }
 
        public bool TryPlace(BlockInstance instance)
        {
            if (!CanPlace(instance)) return false;
            
            foreach (var cell in instance.GetOccupiedCells())
                cellOwner[cell.x, cell.y] = instance;

            OnBlockPlaced?.Invoke(instance);
            return true;
        }
 
        public void Remove(BlockInstance instance)
        {
            foreach (var cell in instance.GetOccupiedCells())
            {
                if (IsInside(cell) && cellOwner[cell.x, cell.y] == instance)
                    cellOwner[cell.x, cell.y] = null;
            }
            OnBlockRemoved?.Invoke(instance);
        }
    }
}
