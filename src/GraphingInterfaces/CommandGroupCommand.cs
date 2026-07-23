using System.Collections.Immutable;

namespace Graphing;

public sealed record CommandGroupCommand(ImmutableArray<GraphFrameCommand> Commands) : GraphFrameCommand(GraphCommandKind.CommandGroup);
