namespace JsMath.Port;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
public sealed class PortedFromAttribute : Attribute
{
    public PortedFromAttribute(
        string project,
        string sourceFile,
        string commit,
        string license,
        string integrity)
    {
        Project = project;
        SourceFile = sourceFile;
        Commit = commit;
        License = license;
        Integrity = integrity;
    }

    public string Project { get; }

    public string SourceFile { get; }

    public string Commit { get; }

    public string License { get; }

    public string Integrity { get; }
}
