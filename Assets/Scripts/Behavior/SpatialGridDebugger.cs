#if UNITY_EDITOR

using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SpatialGridDebugger : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float cellSize = 10f;

    [Header("Test")]
    [SerializeField] private int enemyCount = 1000;
    [SerializeField] private float spawnRange = 100f;
    [SerializeField] private float searchRadius = 50f;

    [Header("Movement")]
    [SerializeField] private bool moveEnemies = true;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Debug")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private bool drawSearchRadius = true;

    private readonly List<Transform> enemies = new();

    private Transform seeker;
    private Transform nearest;

    private readonly Stopwatch stopwatch = new();

    private void Start()
    {
        SpatialGrid.Initialize(cellSize);

        CreateSeeker();
        CreateEnemies();
    }

    private void Update()
    {
        MoveEnemies();

        if (Input.GetKeyDown(KeyCode.Space))
            TestNearestSearch();

        if (Input.GetKeyDown(KeyCode.R))
            ResetEnemies();
    }

    // =========================================================
    // Setup
    // =========================================================

    private void CreateSeeker()
    {
        GameObject obj =
            GameObject.CreatePrimitive(
                PrimitiveType.Capsule
            );

        obj.name = "Seeker";
        obj.transform.position = Vector3.zero;

        seeker = obj.transform;
    }

    private void CreateEnemies()
    {
        for (int i = 0; i < enemyCount; i++)
        {
            GameObject obj =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube
                );

            obj.name = $"Enemy_{i}";

            obj.transform.position =
                GetRandomPosition();

            enemies.Add(obj.transform);

            SpatialGrid.Register(
                obj.transform
            );
        }
    }

    private Vector3 GetRandomPosition()
    {
        return new Vector3(
            Random.Range(
                -spawnRange,
                spawnRange
            ),
            0f,
            Random.Range(
                -spawnRange,
                spawnRange
            )
        );
    }

    // =========================================================
    // Movement
    // =========================================================

    private void MoveEnemies()
    {
        if (!moveEnemies)
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            Transform enemy = enemies[i];

            Vector3 direction =
                new Vector3(
                    Mathf.Sin(
                        Time.time * 0.7f + i
                    ),
                    0f,
                    Mathf.Cos(
                        Time.time * 0.5f +
                        i * 0.37f
                    )
                );

            enemy.position +=
                direction *
                moveSpeed *
                Time.deltaTime;

            WrapPosition(enemy);

            SpatialGrid.UpdatePosition(
                enemy
            );
        }
    }

    private void WrapPosition(
        Transform target)
    {
        Vector3 position =
            target.position;

        if (position.x > spawnRange)
            position.x = -spawnRange;

        if (position.x < -spawnRange)
            position.x = spawnRange;

        if (position.z > spawnRange)
            position.z = -spawnRange;

        if (position.z < -spawnRange)
            position.z = spawnRange;

        target.position = position;
    }

    // =========================================================
    // Test
    // =========================================================

    private void TestNearestSearch()
    {
        Transform bruteForceTarget =
            FindNearestBruteForce();

        stopwatch.Restart();

        Transform spatialTarget =
            SpatialGrid.FindNearest(
                seeker.position,
                searchRadius
            );

        stopwatch.Stop();

        double spatialTime =
            stopwatch.Elapsed.TotalMilliseconds;

        nearest = spatialTarget;

        bool sameResult =
            bruteForceTarget ==
            spatialTarget;

        float distance =
            nearest != null
                ? Vector3.Distance(
                    seeker.position,
                    nearest.position
                )
                : -1f;

        UnityEngine.Debug.Log(
            $"===== Spatial Grid Test =====\n" +
            $"Enemy Count   : {enemyCount}\n" +
            $"Cell Size     : {cellSize}\n" +
            $"Search Radius : {searchRadius}\n" +
            $"Spatial Time  : {spatialTime:F4} ms\n" +
            $"Same Result   : {sameResult}\n" +
            $"Distance      : {distance:F3}"
        );
    }

    private Transform FindNearestBruteForce()
    {
        float nearestDistanceSqr =
            searchRadius *
            searchRadius;

        Transform result = null;

        for (int i = 0; i < enemies.Count; i++)
        {
            Transform enemy =
                enemies[i];

            Vector3 offset =
                enemy.position -
                seeker.position;

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

            result = enemy;
        }

        return result;
    }

    // =========================================================
    // Reset
    // =========================================================

    private void ResetEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            Transform enemy =
                enemies[i];

            enemy.position =
                GetRandomPosition();

            SpatialGrid.UpdatePosition(
                enemy
            );
        }

        nearest = null;
    }

    // =========================================================
    // Gizmos
    // =========================================================

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying ||
            seeker == null)
        {
            return;
        }

        if (drawGrid)
            DrawGrid();

        if (drawSearchRadius)
        {
            Gizmos.DrawWireSphere(
                seeker.position,
                searchRadius
            );
        }

        if (nearest != null)
        {
            Gizmos.DrawLine(
                seeker.position,
                nearest.position
            );

            Gizmos.DrawWireSphere(
                nearest.position,
                1f
            );
        }
    }

    private void DrawGrid()
    {
        int range =
            Mathf.CeilToInt(
                searchRadius /
                cellSize
            ) + 1;

        Vector2Int center =
            SpatialGrid.WorldToCell(
                seeker.position
            );

        for (int y = -range;
             y <= range;
             y++)
        {
            for (int x = -range;
                 x <= range;
                 x++)
            {
                Vector2Int cell =
                    center +
                    new Vector2Int(x, y);

                Vector3 centerPosition =
                    new Vector3(
                        (cell.x + 0.5f) *
                        cellSize,
                        0f,
                        (cell.y + 0.5f) *
                        cellSize
                    );

                Gizmos.DrawWireCube(
                    centerPosition,
                    new Vector3(
                        cellSize,
                        0f,
                        cellSize
                    )
                );
            }
        }
    }
}

#endif