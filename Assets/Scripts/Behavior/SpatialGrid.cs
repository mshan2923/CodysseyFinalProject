using System.Collections.Generic;
using UnityEngine;

public static class SpatialGrid
{
    [SerializeField] private static float cellSize;

    private static readonly Dictionary<
        Vector2Int,
        HashSet<Transform>
    > cells = new();

    private static readonly Dictionary<
        Transform,
        Vector2Int
    > objectCells = new();

    public static int Count => objectCells.Count;
    public static float CellSize => cellSize;


    // =========================================================
    // Initialize
    // =========================================================
    public static bool IsCreate => cells != null && cellSize > 0;
    public static void Initialize(float size)
    {
        if (size <= 0f)
        {
            Debug.LogError(
                $"[SpatialGrid] Invalid Cell Size : {size}"
            );

            return;
        }

        cellSize = size;

        Clear();
    }

    public static void Clear()
    {
        cells.Clear();
        objectCells.Clear();
    }

    // =========================================================
    // Cell
    // =========================================================

    public static Vector2Int WorldToCell(
        Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(
                worldPosition.x / cellSize
            ),
            Mathf.FloorToInt(
                worldPosition.z / cellSize
            )
        );
    }

    // =========================================================
    // Register
    // =========================================================

    public static void Register(
        Transform target)
    {
        if (target == null)
            return;

        Vector2Int cell =
            WorldToCell(target.position);

        AddToCell(target, cell);

        objectCells[target] = cell;
    }

    // =========================================================
    // Unregister
    // =========================================================

    public static void Unregister(
        Transform target)
    {
        if (target == null)
            return;

        if (!objectCells.TryGetValue(
                target,
                out Vector2Int cell))
        {
            return;
        }

        RemoveFromCell(target, cell);

        objectCells.Remove(target);
    }

    // =========================================================
    // Update Position
    // =========================================================

    public static void UpdatePosition(
        Transform target)
    {
        if (target == null)
            return;

        Vector2Int newCell =
            WorldToCell(target.position);

        if (!objectCells.TryGetValue(
                target,
                out Vector2Int oldCell))
        {
            AddToCell(target, newCell);

            objectCells[target] = newCell;

            return;
        }

        if (oldCell == newCell)
            return;

        RemoveFromCell(target, oldCell);
        AddToCell(target, newCell);

        objectCells[target] = newCell;
    }

    // =========================================================
    // Cell Management
    // =========================================================

    private static void AddToCell(
        Transform target,
        Vector2Int cellPosition)
    {
        if (!cells.TryGetValue(
                cellPosition,
                out HashSet<Transform> cell))
        {
            cell = new HashSet<Transform>();

            cells.Add(
                cellPosition,
                cell
            );
        }

        cell.Add(target);
    }

    private static void RemoveFromCell(
        Transform target,
        Vector2Int cellPosition)
    {
        if (!cells.TryGetValue(
                cellPosition,
                out HashSet<Transform> cell))
        {
            return;
        }

        cell.Remove(target);

        if (cell.Count == 0)
            cells.Remove(cellPosition);
    }

    // =========================================================
    // Find Nearest
    // =========================================================

    public static Transform FindNearest(
        Vector3 position,
        float maxDistance)
    {
        if (cellSize <= 0f)
        {
            Debug.Log("0000");
                     return null;
   
        }
        Vector2Int centerCell =
            WorldToCell(position);

        float maxDistanceSqr =
            maxDistance * maxDistance;

        float nearestDistanceSqr =
            maxDistanceSqr;

        Transform nearest = null;

        int maxRing =
            Mathf.CeilToInt(
                maxDistance / cellSize
            );

        for (int ring = 0; ring <= maxRing; ring++)
        {
            SearchRing(
                position,
                centerCell,
                ring,
                ref nearest,
                ref nearestDistanceSqr
            );

            /*
            * 아직 아무것도 못 찾았다면
            * 다음 Ring을 계속 탐색해야 한다.
            */
            if (nearest == null)
                continue;

            /*
            * 다음 Ring에 현재 최단거리보다
            * 가까운 위치가 존재할 가능성이 없다면 종료.
            */
            if (!HasCloserCell(
                    position,
                    centerCell,
                    ring + 1,
                    maxRing,
                    nearestDistanceSqr))
            {
                break;
            }
        }

        return nearest;
    }

    // =========================================================
    // Ring Search
    // =========================================================

    private static void SearchRing(
        Vector3 position,
        Vector2Int centerCell,
        int ring,
        ref Transform nearest,
        ref float nearestDistanceSqr)
    {
        int minX = centerCell.x - ring;
        int maxX = centerCell.x + ring;

        int minY = centerCell.y - ring;
        int maxY = centerCell.y + ring;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!IsRingCell(
                        x,
                        y,
                        minX,
                        maxX,
                        minY,
                        maxY))
                {
                    continue;
                }

                Vector2Int cellPosition =
                    new Vector2Int(x, y);

                float cellDistanceSqr =
                    DistanceToCellSqr(
                        position,
                        cellPosition
                    );

                if (!cells.TryGetValue(
                        cellPosition,
                        out HashSet<Transform> targets))
                {
                    continue;
                }

                Debug.Log(
                    $"[Grid] Ring:{ring} " +
                    $"Cell:{cellPosition} " +
                    $"Targets:{targets.Count} " +
                    $"CellDist:{Mathf.Sqrt(cellDistanceSqr):F2}"
                );

                if (cellDistanceSqr >
                    nearestDistanceSqr)
                {
                    continue;
                }

                foreach (Transform target in targets)
                {
                    Vector3 offset =
                        target.position - position;

                    float distanceSqr =
                        offset.x * offset.x +
                        offset.y * offset.y +
                        offset.z * offset.z;

                    if (distanceSqr >
                        nearestDistanceSqr)
                    {
                        continue;
                    }

                    nearestDistanceSqr =
                        distanceSqr;

                    nearest = target;
                }
            }
        }
    }

    // =========================================================
    // Early Exit
    // =========================================================

    private static bool HasCloserCell(
        Vector3 position,
        Vector2Int centerCell,
        int ring,
        int maxRing,
        float nearestDistanceSqr)
    {
        if (ring > maxRing)
            return false;

        int minX = centerCell.x - ring;
        int maxX = centerCell.x + ring;

        int minY = centerCell.y - ring;
        int maxY = centerCell.y + ring;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!IsRingCell(
                        x,
                        y,
                        minX,
                        maxX,
                        minY,
                        maxY))
                {
                    continue;
                }

                Vector2Int cell =
                    new Vector2Int(x, y);

                float distanceSqr =
                    DistanceToCellSqr(
                        position,
                        cell
                    );

                if (distanceSqr <=
                    nearestDistanceSqr)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // =========================================================
    // Utilities
    // =========================================================

    private static bool IsRingCell(
        int x,
        int y,
        int minX,
        int maxX,
        int minY,
        int maxY)
    {
        return
            x == minX ||
            x == maxX ||
            y == minY ||
            y == maxY;
    }

    private static float DistanceToCellSqr(
        Vector3 position,
        Vector2Int cell)
    {
        float minX =
            cell.x * cellSize;

        float maxX =
            minX + cellSize;

        float minZ =
            cell.y * cellSize;

        float maxZ =
            minZ + cellSize;

        float dx = 0f;
        float dz = 0f;

        if (position.x < minX)
            dx = minX - position.x;
        else if (position.x > maxX)
            dx = position.x - maxX;

        if (position.z < minZ)
            dz = minZ - position.z;
        else if (position.z > maxZ)
            dz = position.z - maxZ;

        return dx * dx + dz * dz;
    }
}