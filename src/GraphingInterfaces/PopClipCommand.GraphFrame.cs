using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record PopClipCommand() : GraphFrameCommand(GraphCommandKind.PopClip);
