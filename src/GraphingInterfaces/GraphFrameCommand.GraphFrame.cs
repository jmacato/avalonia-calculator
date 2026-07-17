using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public abstract record GraphFrameCommand(GraphCommandKind Kind);
