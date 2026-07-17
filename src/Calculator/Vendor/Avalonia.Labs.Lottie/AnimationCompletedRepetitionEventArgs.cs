namespace Avalonia.Labs.Lottie;

public sealed class AnimationCompletedRepetitionEventArgs : EventArgs
{
    public AnimationCompletedRepetitionEventArgs(int repetition)
    {
        Repetition = repetition;
    }

    public int Repetition { get; }
}
