using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Nekuzaky.Vicinity.Editor.Tests
{
    internal sealed class VicinityGridTests
    {
        [Test]
        public void EmptyGridProducesNoCells()
        {
            using VicinityGrid grid = new VicinityGrid();
            NativeArray<VicinityEntryData> entries = new NativeArray<VicinityEntryData>(0, Allocator.Temp);

            grid.Rebuild(entries, 0, 32f);

            Assert.AreEqual(0, grid.CellCount);
            entries.Dispose();
        }

        [Test]
        public void EntriesInTheSameCellShareOneCell()
        {
            using VicinityGrid grid = new VicinityGrid();
            NativeArray<VicinityEntryData> entries = BuildEntries(
                new float3(1f, 0f, 1f),
                new float3(2f, 0f, 2f),
                new float3(3f, 0f, 3f));

            grid.Rebuild(entries, entries.Length, 32f);

            Assert.AreEqual(1, grid.CellCount);
            Assert.AreEqual(3, grid.Cells[0].EntryCount);
            entries.Dispose();
        }

        [Test]
        public void DistantEntriesLandInDifferentCells()
        {
            using VicinityGrid grid = new VicinityGrid();
            NativeArray<VicinityEntryData> entries = BuildEntries(
                new float3(0f, 0f, 0f),
                new float3(500f, 0f, 0f),
                new float3(0f, 0f, 500f));

            grid.Rebuild(entries, entries.Length, 32f);

            Assert.AreEqual(3, grid.CellCount);
            entries.Dispose();
        }

        [Test]
        public void NegativeCoordinatesGetTheirOwnCell()
        {
            using VicinityGrid grid = new VicinityGrid();
            NativeArray<VicinityEntryData> entries = BuildEntries(
                new float3(-100f, 0f, -100f),
                new float3(100f, 0f, 100f));

            grid.Rebuild(entries, entries.Length, 32f);

            Assert.AreEqual(2, grid.CellCount);
            entries.Dispose();
        }

        [Test]
        public void EveryEntryIsAssignedToExactlyOneCell()
        {
            using VicinityGrid grid = new VicinityGrid();
            const int count = 200;
            NativeArray<VicinityEntryData> entries = new NativeArray<VicinityEntryData>(count, Allocator.Temp);

            for (int i = 0; i < count; i++)
            {
                entries[i] = new VicinityEntryData
                {
                    Position = new float3(i * 7f, 0f, i * 13f),
                    IsActive = 1
                };
            }

            grid.Rebuild(entries, count, 32f);

            int total = 0;
            for (int i = 0; i < grid.CellCount; i++)
            {
                total += grid.Cells[i].EntryCount;
            }

            Assert.AreEqual(count, total);
            Assert.AreEqual(count, grid.EntryOrder.Length);

            for (int i = 0; i < count; i++)
            {
                Assert.GreaterOrEqual(entries[i].CellIndex, 0);
                Assert.Less(entries[i].CellIndex, grid.CellCount);
            }

            entries.Dispose();
        }

        [Test]
        public void RebuildingTwiceProducesTheSameLayout()
        {
            NativeArray<VicinityEntryData> entries = BuildEntries(
                new float3(10f, 0f, 10f),
                new float3(300f, 0f, 40f),
                new float3(-70f, 0f, 5f),
                new float3(11f, 0f, 12f));

            using VicinityGrid first = new VicinityGrid();
            using VicinityGrid second = new VicinityGrid();

            first.Rebuild(entries, entries.Length, 32f);
            second.Rebuild(entries, entries.Length, 32f);

            Assert.AreEqual(first.CellCount, second.CellCount);

            for (int i = 0; i < first.EntryOrder.Length; i++)
            {
                Assert.AreEqual(first.EntryOrder[i], second.EntryOrder[i]);
            }

            entries.Dispose();
        }

        [Test]
        public void ActiveCountNeverGoesBelowZero()
        {
            using VicinityGrid grid = new VicinityGrid();
            NativeArray<VicinityEntryData> entries = BuildEntries(new float3(0f, 0f, 0f));

            grid.Rebuild(entries, entries.Length, 32f);
            grid.AddActive(0, -5);

            Assert.AreEqual(0, grid.CellActiveCount[0]);
            entries.Dispose();
        }

        private static NativeArray<VicinityEntryData> BuildEntries(params float3[] positions)
        {
            NativeArray<VicinityEntryData> entries = new NativeArray<VicinityEntryData>(positions.Length, Allocator.Temp);

            for (int i = 0; i < positions.Length; i++)
            {
                entries[i] = new VicinityEntryData
                {
                    Position = positions[i],
                    IsActive = 1
                };
            }

            return entries;
        }
    }
}
