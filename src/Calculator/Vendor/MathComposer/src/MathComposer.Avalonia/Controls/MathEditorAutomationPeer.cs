using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>Exposes the editor as a named string-valued editable math control.</summary>
public sealed class MathEditorAutomationPeer : ControlAutomationPeer, IValueProvider
{
    /// <summary>Initializes an automation peer for one editor.</summary>
    public MathEditorAutomationPeer(MathEditor owner)
        : base(owner)
    {
    }

    private MathEditor Editor => (MathEditor)Owner;

    bool IValueProvider.IsReadOnly => Editor.IsReadOnly;

    string IValueProvider.Value => Editor.Export(MathTextFormat.UnicodeMath);

    void IValueProvider.SetValue(string? value)
    {
        if (Editor.IsReadOnly)
        {
            throw new InvalidOperationException("The math editor is read-only.");
        }

        Editor.Load(value ?? string.Empty, MathTextFormat.UnicodeMath);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Edit;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(MathEditor);

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        string? name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? "Math equation editor" : name;
    }

    /// <inheritdoc />
    protected override string GetHelpTextCore()
    {
        string errors = string.Join(
            "; ",
            Editor.Diagnostics
                .Where(static diagnostic => diagnostic.Severity >= MathDiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.Message));
        return errors.Length == 0 ? base.GetHelpTextCore() ?? string.Empty : errors;
    }
}
