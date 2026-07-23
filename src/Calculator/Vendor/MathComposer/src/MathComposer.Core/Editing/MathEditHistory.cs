namespace MathComposer.Core;

/// <summary>A bounded undo/redo store for immutable editor snapshots.</summary>
public sealed class MathEditHistory
{
    private const int MaximumCapacity = 10_000;
    private readonly List<MathHistoryState> _undo = [];
    private readonly List<MathHistoryState> _redo = [];
    private int _capacity = 100;
    private MathHistoryMergeKind _lastMergeKind;

    /// <summary>Gets or sets the retained undo capacity, clamped to 0 through 10,000.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Clamp(value, 0, MaximumCapacity);
            TrimOldest(_undo);
            TrimOldest(_redo);
            if (_capacity == 0)
            {
                _lastMergeKind = MathHistoryMergeKind.None;
            }
        }
    }

    /// <summary>Gets whether an undo snapshot is available.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets whether a redo snapshot is available.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Gets the retained undo snapshot count.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>Gets the retained redo snapshot count.</summary>
    public int RedoCount => _redo.Count;

    /// <summary>
    /// Records the state before a document-changing command and clears redo.
    /// </summary>
    /// <param name="before">The state to restore on undo.</param>
    /// <param name="after">The state after the command.</param>
    /// <param name="mergeKind">An optional uninterrupted coalescing class.</param>
    public void Record(
        MathHistoryState before,
        MathHistoryState after,
        MathHistoryMergeKind mergeKind = MathHistoryMergeKind.None)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Document == after.Document)
        {
            return;
        }

        _redo.Clear();
        if (_capacity == 0)
        {
            _lastMergeKind = MathHistoryMergeKind.None;
            return;
        }

        bool coalesces = mergeKind != MathHistoryMergeKind.None &&
                         mergeKind == _lastMergeKind &&
                         _undo.Count > 0;
        if (!coalesces)
        {
            _undo.Add(before);
            TrimOldest(_undo);
        }

        _lastMergeKind = mergeKind;
    }

    /// <summary>Ends any active text or IME coalescing group.</summary>
    public void BreakCoalescing()
    {
        _lastMergeKind = MathHistoryMergeKind.None;
    }

    /// <summary>Clears undo, redo, and coalescing state.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _lastMergeKind = MathHistoryMergeKind.None;
    }

    /// <summary>Restores one undo state and records the current state for redo.</summary>
    public bool TryUndo(MathHistoryState current, out MathHistoryState state)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_undo.Count == 0)
        {
            state = current;
            return false;
        }

        state = PopLast(_undo);
        if (_capacity > 0)
        {
            _redo.Add(current);
            TrimOldest(_redo);
        }

        _lastMergeKind = MathHistoryMergeKind.None;
        return true;
    }

    /// <summary>Restores one redo state and records the current state for undo.</summary>
    public bool TryRedo(MathHistoryState current, out MathHistoryState state)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_redo.Count == 0)
        {
            state = current;
            return false;
        }

        state = PopLast(_redo);
        if (_capacity > 0)
        {
            _undo.Add(current);
            TrimOldest(_undo);
        }

        _lastMergeKind = MathHistoryMergeKind.None;
        return true;
    }

    private static MathHistoryState PopLast(List<MathHistoryState> states)
    {
        int index = states.Count - 1;
        MathHistoryState state = states[index];
        states.RemoveAt(index);
        return state;
    }

    private void TrimOldest(List<MathHistoryState> states)
    {
        int excess = states.Count - _capacity;
        if (excess > 0)
        {
            states.RemoveRange(0, excess);
        }
    }
}
