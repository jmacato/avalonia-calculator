using System.Windows.Input;

namespace MathComposer.Avalonia.Controls;

/// <summary>A small command implementation whose availability is owned by an editor.</summary>
internal sealed class MathEditorCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    : ICommand
{
    private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Predicate<object?> _canExecute = canExecute ?? (_ => true);

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute(parameter);
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
