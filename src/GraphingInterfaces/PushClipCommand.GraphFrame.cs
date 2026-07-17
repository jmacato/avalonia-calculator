using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record PushClipCommand(GraphRect Clip) : GraphFrameCommand(GraphCommandKind.PushClip);
