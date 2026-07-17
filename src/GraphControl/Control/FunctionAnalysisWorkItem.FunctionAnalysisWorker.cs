using System.Threading.Channels;

namespace GraphControl;

internal sealed record FunctionAnalysisWorkItem(
    FunctionAnalysisRequest Request,
    ChannelWriter<FunctionAnalysisOutcome> Response,
    CancellationToken CancellationToken);
