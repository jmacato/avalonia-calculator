namespace Graphing;

public sealed record PushClipCommand(GraphRect Clip) : GraphFrameCommand(GraphCommandKind.PushClip);
