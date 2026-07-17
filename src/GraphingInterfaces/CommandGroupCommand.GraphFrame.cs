using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record CommandGroupCommand(ImmutableArray<GraphFrameCommand> Commands) : GraphFrameCommand(GraphCommandKind.CommandGroup);
