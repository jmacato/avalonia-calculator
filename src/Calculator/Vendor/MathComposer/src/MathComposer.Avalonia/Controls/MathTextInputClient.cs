using Avalonia;
using Avalonia.Input.TextInput;

namespace MathComposer.Avalonia.Controls;

internal sealed class MathTextInputClient : TextInputMethodClient
{
    private readonly MathEditor _owner;

    public MathTextInputClient(MathEditor owner) =>
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public override Visual TextViewVisual => _owner;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => false;

    public override string SurroundingText => string.Empty;

    public override Rect CursorRectangle => _owner.GetImeCursorRectangle();

    public override TextSelection Selection
    {
        get => new(0, 0);
        set { }
    }

    public override void SetPreeditText(string? preeditText) =>
        _owner.SetPreeditText(preeditText ?? string.Empty);

    public override void SetPreeditText(string? preeditText, int? cursorPos) =>
        _owner.SetPreeditText(preeditText ?? string.Empty);

    public void NotifyCursorRectangleChanged() => RaiseCursorRectangleChanged();

    public void NotifySelectionChanged()
    {
        RaiseSelectionChanged();
        RaiseCursorRectangleChanged();
    }

    public void NotifyDocumentChanged()
    {
        RaiseSurroundingTextChanged();
        RaiseSelectionChanged();
        RaiseCursorRectangleChanged();
    }
}
