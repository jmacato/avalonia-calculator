# Calculator browser telemetry host

This local HTTPS host serves Calculator's normal build output through its .NET
static-web-assets manifest and records bounded lockup diagnostics from
telemetry-enabled sessions. It is launched automatically by the browser project.

Run either configuration with an HTTPS certificate that the test device trusts:

```sh
ASPNETCORE_Kestrel__Certificates__Default__Path=/path/to/lan-certificate.pfx \
ASPNETCORE_Kestrel__Certificates__Default__Password=certificate-password \
dotnet run --project src/Calculator.Browser/Calculator.Browser.csproj -c Release
```

Use `-c Debug` for the interpreted development build. No publish directory or
manual asset copy is required.

Add `telemetry=1` and an optional `run` label to the Calculator URL, for
example `/?telemetry=1&run=physical-phone`. The live dashboard is available at
`/telemetry` on the same origin. Batches are also flushed as NDJSON under the
system temporary directory; set `CALCULATOR_TELEMETRY_DIRECTORY` to choose a
different location.

Telemetry contains liveness, timing, pointer/focus counters, canvas state, and
aggregate memory/GC data. It does not record equation text, key values, input
values, or clipboard contents. Both the in-memory timeline and client event
queues are bounded.
