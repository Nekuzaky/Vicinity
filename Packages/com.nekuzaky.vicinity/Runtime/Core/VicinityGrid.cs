using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Nekuzaky.Vicinity
{
    internal struct VicinityCell
    {
        public float3 Center;
        public float3 Extents;
        public int EntryStart;
        public int EntryCount;
    }

    internal sealed class VicinityGrid : IDisposable
    {
        #region Main Methods

        internal VicinityGrid()
        {
            _cells = new NativeList<VicinityCell>(64, Allocator.Persistent);
            _entryOrder = new NativeList<int>(256, Allocator.Persistent);
            _cellActiveCount = new NativeArray<int>(0, Allocator.Persistent);
        }

        internal NativeArray<VicinityCell> Cells => _cells.AsArray();

        internal NativeArray<int> EntryOrder => _entryOrder.AsArray();

        internal NativeArray<int> CellActiveCount => _cellActiveCount;

        internal int CellCount => _cells.Length;

        internal float CellSize => _cellSize;

        internal void Rebuild(NativeArray<VicinityEntryData> entries, int entryCount, float cellSize)
        {
            _cellSize = math.max(cellSize, MinimumCellSize);
            _cells.Clear();
            _entryOrder.Clear();
            _cellActiveCount.Dispose();

            if (entryCount <= 0)
            {
                _cellActiveCount = new NativeArray<int>(0, Allocator.Persistent);
                return;
            }

            EnsureSortCapacity(entryCount);
            FillSortBuffer(entries, entryCount);

            if (_staticCount == 0)
            {
                _cellActiveCount = new NativeArray<int>(0, Allocator.Persistent);
                return;
            }

            NativeArray<CellSortItem> ordered = _sortBuffer.GetSubArray(0, _staticCount);
            ordered.Sort(new CellSortComparer());

            BuildCells(ordered, entries, _staticCount);
            _cellActiveCount = new NativeArray<int>(_cells.Length, Allocator.Persistent);
        }

        internal void AddActive(int cellIndex, int delta)
        {
            if (cellIndex < 0 || cellIndex >= _cellActiveCount.Length)
            {
                return;
            }

            _cellActiveCount[cellIndex] = math.max(0, _cellActiveCount[cellIndex] + delta);
        }

        public void Dispose()
        {
            if (_cells.IsCreated)
            {
                _cells.Dispose();
            }

            if (_entryOrder.IsCreated)
            {
                _entryOrder.Dispose();
            }

            if (_cellActiveCount.IsCreated)
            {
                _cellActiveCount.Dispose();
            }

            if (_sortBuffer.IsCreated)
            {
                _sortBuffer.Dispose();
            }
        }

        #endregion

        #region Privates

        private const int AxisBits = 21;
        private const int AxisOffset = 1 << (AxisBits - 1);
        private const long AxisMask = (1L << AxisBits) - 1L;
        private const float MinimumCellSize = 0.01f;

        private NativeList<VicinityCell> _cells;
        private NativeList<int> _entryOrder;
        private NativeArray<int> _cellActiveCount;
        private NativeArray<CellSortItem> _sortBuffer;
        private float _cellSize;
        private int _staticCount;

        private struct CellSortItem
        {
            public long CellKey;
            public int EntryIndex;
            public int3 Coordinates;
        }

        private struct CellSortComparer : IComparer<CellSortItem>
        {
            public int Compare(CellSortItem left, CellSortItem right)
            {
                int byKey = left.CellKey.CompareTo(right.CellKey);
                return byKey != 0 ? byKey : left.EntryIndex.CompareTo(right.EntryIndex);
            }
        }

        private static long EncodeCoordinates(int3 coordinates)
        {
            long x = math.clamp(coordinates.x + AxisOffset, 0, (int)AxisMask);
            long y = math.clamp(coordinates.y + AxisOffset, 0, (int)AxisMask);
            long z = math.clamp(coordinates.z + AxisOffset, 0, (int)AxisMask);
            return (x << (AxisBits * 2)) | (y << AxisBits) | z;
        }

        private void EnsureSortCapacity(int entryCount)
        {
            if (_sortBuffer.IsCreated && _sortBuffer.Length >= entryCount)
            {
                return;
            }

            if (_sortBuffer.IsCreated)
            {
                _sortBuffer.Dispose();
            }

            _sortBuffer = new NativeArray<CellSortItem>(math.ceilpow2(math.max(entryCount, 16)), Allocator.Persistent);
        }

        private void FillSortBuffer(NativeArray<VicinityEntryData> entries, int entryCount)
        {
            float inverseCellSize = 1f / _cellSize;
            _staticCount = 0;

            for (int i = 0; i < entryCount; i++)
            {
                if (entries[i].IsMobile != 0)
                {
                    continue;
                }

                int3 coordinates = (int3)math.floor(entries[i].Position * inverseCellSize);
                _sortBuffer[_staticCount] = new CellSortItem
                {
                    CellKey = EncodeCoordinates(coordinates),
                    EntryIndex = i,
                    Coordinates = coordinates
                };

                _staticCount++;
            }
        }

        private void BuildCells(NativeArray<CellSortItem> ordered, NativeArray<VicinityEntryData> entries, int entryCount)
        {
            float half = _cellSize * 0.5f;
            float3 extents = new float3(half, half, half);

            int runStart = 0;
            for (int i = 1; i <= entryCount; i++)
            {
                bool endOfRun = i == entryCount || ordered[i].CellKey != ordered[runStart].CellKey;
                if (!endOfRun)
                {
                    continue;
                }

                int cellIndex = _cells.Length;
                float3 minimum = (float3)ordered[runStart].Coordinates * _cellSize;

                _cells.Add(new VicinityCell
                {
                    Center = minimum + extents,
                    Extents = extents,
                    EntryStart = _entryOrder.Length,
                    EntryCount = i - runStart
                });

                for (int slot = runStart; slot < i; slot++)
                {
                    int entryIndex = ordered[slot].EntryIndex;
                    _entryOrder.Add(entryIndex);

                    VicinityEntryData data = entries[entryIndex];
                    data.CellIndex = cellIndex;
                    entries[entryIndex] = data;
                }

                runStart = i;
            }
        }

        #endregion
    }
}
