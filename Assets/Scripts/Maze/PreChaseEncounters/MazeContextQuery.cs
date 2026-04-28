using System.Collections.Generic;
using UnityEngine;

public class MazeContextQuery : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MazeGenerator mazeGenerator;
    [SerializeField] private Collider initialRoomVolume;
    [SerializeField] private Transform initialRoomRoot;

    [Header("Sampling")]
    [SerializeField] private int forwardProbeCells = 10;
    [SerializeField] private int longHallMinCells = 6;
    [SerializeField] private int cornerLookAheadCells = 2;
    [SerializeField] private bool debugLogs;

    private bool hasInitialRoomBounds;
    private Bounds initialRoomBounds;

    void Awake()
    {
        CacheInitialRoomBounds();
    }

    void OnValidate()
    {
        CacheInitialRoomBounds();
    }

    public bool TryBuildContext(Transform player, out MazeContextSnapshot snapshot)
    {
        snapshot = MazeContextSnapshot.Invalid;
        if (player == null || mazeGenerator == null)
        {
            return false;
        }

        Vector2Int currentCell = GetCurrentCell(player.position);
        if (!IsWalkable(currentCell))
        {
            return false;
        }

        Vector2Int forwardDir = GetCardinalFromForward(player.forward);
        if (forwardDir == Vector2Int.zero)
        {
            forwardDir = Vector2Int.up;
        }

        List<Vector2Int> forwardCells = GetForwardCells(currentCell, forwardDir, forwardProbeCells);
        int straightAhead = CountStraightAhead(currentCell, forwardDir, forwardProbeCells);
        bool intersectionNearby = IsIntersectionNearby(currentCell);
        bool atCorner = IsAtCorner(currentCell);
        bool cornerAhead = TryGetCornerAhead(currentCell, forwardDir, cornerLookAheadCells, out Vector2Int cornerCell, out Vector2Int cornerTurnDir);
        bool inLongHall = IsPlayerInLongHall(currentCell, longHallMinCells);
        bool longHallAhead = straightAhead >= longHallMinCells;
        bool inInitialRoom = IsInitialRoom(player.position);
        bool inSafeZone = SafeSpaceZone.IsPlayerProtectedGlobal(player);

        Vector3 hallSpawnPoint = CellToWorld(currentCell + forwardDir * Mathf.Max(3, straightAhead - 1));
        Vector3 cornerRevealPoint = cornerAhead ? CellToWorld(cornerCell + cornerTurnDir) : Vector3.zero;

        snapshot = new MazeContextSnapshot(
            true,
            currentCell,
            forwardDir,
            forwardCells,
            straightAhead,
            longHallAhead,
            inLongHall,
            atCorner,
            cornerAhead,
            cornerCell,
            cornerTurnDir,
            intersectionNearby,
            inInitialRoom,
            inSafeZone,
            hallSpawnPoint,
            cornerRevealPoint);

        return true;
    }

    public Vector2Int GetCurrentCell(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / Mathf.Max(0.01f, mazeGenerator.cellSize)),
            Mathf.FloorToInt(worldPosition.z / Mathf.Max(0.01f, mazeGenerator.cellSize)));
    }

    public List<Vector2Int> GetForwardCells(Vector2Int startCell, Vector2Int forwardDir, int distance)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        Vector2Int cursor = startCell;
        for (int i = 0; i < distance; i++)
        {
            cursor += forwardDir;
            if (!IsWalkable(cursor))
            {
                break;
            }

            cells.Add(cursor);
        }

        return cells;
    }

    public bool IsLongHallAhead(Vector2Int startCell, Vector2Int forwardDir, int minLength)
    {
        int count = CountStraightAhead(startCell, forwardDir, minLength + 1);
        return count >= minLength;
    }

    public bool IsPlayerInLongHall(Vector2Int cell, int minLength)
    {
        int horizontal = 1 + CountDirection(cell, Vector2Int.left, minLength) + CountDirection(cell, Vector2Int.right, minLength);
        int vertical = 1 + CountDirection(cell, Vector2Int.up, minLength) + CountDirection(cell, Vector2Int.down, minLength);
        return horizontal >= minLength || vertical >= minLength;
    }

    public bool TryGetCornerAhead(Vector2Int startCell, Vector2Int forwardDir, int lookAhead, out Vector2Int cornerCell, out Vector2Int cornerTurnDir)
    {
        cornerCell = Vector2Int.zero;
        cornerTurnDir = Vector2Int.zero;

        Vector2Int cursor = startCell;
        for (int i = 0; i < Mathf.Max(1, lookAhead); i++)
        {
            cursor += forwardDir;
            if (!IsWalkable(cursor))
            {
                if (debugLogs)
                {
                    Debug.Log($"CornerQuery blocked forward at {cursor}.");
                }
                return false;
            }

            Vector2Int right = new Vector2Int(forwardDir.y, -forwardDir.x);
            Vector2Int left = -right;
            bool rightTurn = IsWalkable(cursor + right) && !IsWalkable(cursor + forwardDir);
            bool leftTurn = IsWalkable(cursor + left) && !IsWalkable(cursor + forwardDir);
            if (rightTurn)
            {
                cornerCell = cursor;
                cornerTurnDir = right;
                return true;
            }

            if (leftTurn)
            {
                cornerCell = cursor;
                cornerTurnDir = left;
                return true;
            }
        }

        return false;
    }

    public bool IsAtCorner(Vector2Int cell)
    {
        bool left = IsWalkable(cell + Vector2Int.left);
        bool right = IsWalkable(cell + Vector2Int.right);
        bool up = IsWalkable(cell + Vector2Int.up);
        bool down = IsWalkable(cell + Vector2Int.down);

        bool horizontalOnly = (left || right) && !up && !down;
        bool verticalOnly = (up || down) && !left && !right;
        if (horizontalOnly || verticalOnly)
        {
            return false;
        }

        int exits = 0;
        if (left) exits++;
        if (right) exits++;
        if (up) exits++;
        if (down) exits++;

        return exits == 2;
    }

    public bool IsIntersectionNearby(Vector2Int cell)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int check = cell + new Vector2Int(x, y);
                if (!IsWalkable(check))
                {
                    continue;
                }

                if (GetExitCount(check) >= 3)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsInitialRoom(Vector3 playerPosition)
    {
        if (initialRoomVolume != null)
        {
            return initialRoomVolume.bounds.Contains(playerPosition);
        }

        if (!hasInitialRoomBounds)
        {
            return false;
        }

        return initialRoomBounds.Contains(playerPosition);
    }

    public bool CanTriggerEncounter(Transform player, float runtime, float gracePeriod, out string reason)
    {
        reason = string.Empty;
        if (player == null)
        {
            reason = "Missing player transform";
            return false;
        }

        if (runtime < gracePeriod)
        {
            reason = $"Initial grace period active ({(gracePeriod - runtime):F1}s remaining)";
            return false;
        }

        if (IsInitialRoom(player.position))
        {
            reason = "Player in initial room";
            return false;
        }

        if (SafeSpaceZone.IsPlayerProtectedGlobal(player))
        {
            reason = "Player in safe zone";
            return false;
        }

        return true;
    }

    private int CountStraightAhead(Vector2Int startCell, Vector2Int forwardDir, int maxCells)
    {
        int valid = 0;
        Vector2Int cursor = startCell;
        for (int i = 0; i < maxCells; i++)
        {
            cursor += forwardDir;
            if (!IsWalkable(cursor))
            {
                break;
            }

            if (GetExitCount(cursor) > 2)
            {
                break;
            }

            valid++;
        }

        return valid;
    }

    private int CountDirection(Vector2Int startCell, Vector2Int direction, int maxCells)
    {
        int count = 0;
        Vector2Int cursor = startCell;
        for (int i = 0; i < maxCells; i++)
        {
            cursor += direction;
            if (!IsWalkable(cursor))
            {
                break;
            }
            count++;
        }

        return count;
    }

    private int GetExitCount(Vector2Int cell)
    {
        int exits = 0;
        if (IsWalkable(cell + Vector2Int.left)) exits++;
        if (IsWalkable(cell + Vector2Int.right)) exits++;
        if (IsWalkable(cell + Vector2Int.up)) exits++;
        if (IsWalkable(cell + Vector2Int.down)) exits++;
        return exits;
    }

    private bool IsWalkable(Vector2Int cell)
    {
        if (mazeGenerator == null)
        {
            return false;
        }

        return mazeGenerator.GetMazeCell(cell.x, cell.y) == 0;
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float size = Mathf.Max(0.01f, mazeGenerator.cellSize);
        return new Vector3(cell.x * size + size * 0.5f, 0f, cell.y * size + size * 0.5f);
    }

    private static Vector2Int GetCardinalFromForward(Vector3 forward)
    {
        Vector3 planar = new Vector3(forward.x, 0f, forward.z);
        if (planar.sqrMagnitude < 0.0001f)
        {
            return Vector2Int.zero;
        }

        planar.Normalize();
        if (Mathf.Abs(planar.x) >= Mathf.Abs(planar.z))
        {
            return planar.x >= 0f ? Vector2Int.right : Vector2Int.left;
        }

        return planar.z >= 0f ? Vector2Int.up : Vector2Int.down;
    }

    private void CacheInitialRoomBounds()
    {
        hasInitialRoomBounds = false;
        if (initialRoomRoot == null)
        {
            return;
        }

        Renderer[] renderers = initialRoomRoot.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        initialRoomBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            initialRoomBounds.Encapsulate(renderers[i].bounds);
        }
        hasInitialRoomBounds = true;
    }
}

public readonly struct MazeContextSnapshot
{
    public static readonly MazeContextSnapshot Invalid = new MazeContextSnapshot(false, Vector2Int.zero, Vector2Int.zero, null, 0, false, false, false, false, Vector2Int.zero, Vector2Int.zero, false, false, false, Vector3.zero, Vector3.zero);

    public readonly bool IsValid;
    public readonly Vector2Int CurrentCell;
    public readonly Vector2Int ForwardDirection;
    public readonly IReadOnlyList<Vector2Int> ForwardCells;
    public readonly int StraightCellsAhead;
    public readonly bool IsLongHallAhead;
    public readonly bool IsPlayerInLongHall;
    public readonly bool IsAtCorner;
    public readonly bool IsCornerAhead;
    public readonly Vector2Int CornerCell;
    public readonly Vector2Int CornerTurnDirection;
    public readonly bool IsIntersectionNearby;
    public readonly bool IsInitialRoom;
    public readonly bool IsSafeZone;
    public readonly Vector3 SuggestedHallSpawnPoint;
    public readonly Vector3 SuggestedCornerRevealPoint;

    public MazeContextSnapshot(
        bool isValid,
        Vector2Int currentCell,
        Vector2Int forwardDirection,
        IReadOnlyList<Vector2Int> forwardCells,
        int straightCellsAhead,
        bool isLongHallAhead,
        bool isPlayerInLongHall,
        bool isAtCorner,
        bool isCornerAhead,
        Vector2Int cornerCell,
        Vector2Int cornerTurnDirection,
        bool isIntersectionNearby,
        bool isInitialRoom,
        bool isSafeZone,
        Vector3 suggestedHallSpawnPoint,
        Vector3 suggestedCornerRevealPoint)
    {
        IsValid = isValid;
        CurrentCell = currentCell;
        ForwardDirection = forwardDirection;
        ForwardCells = forwardCells;
        StraightCellsAhead = straightCellsAhead;
        IsLongHallAhead = isLongHallAhead;
        IsPlayerInLongHall = isPlayerInLongHall;
        IsAtCorner = isAtCorner;
        IsCornerAhead = isCornerAhead;
        CornerCell = cornerCell;
        CornerTurnDirection = cornerTurnDirection;
        IsIntersectionNearby = isIntersectionNearby;
        IsInitialRoom = isInitialRoom;
        IsSafeZone = isSafeZone;
        SuggestedHallSpawnPoint = suggestedHallSpawnPoint;
        SuggestedCornerRevealPoint = suggestedCornerRevealPoint;
    }
}
