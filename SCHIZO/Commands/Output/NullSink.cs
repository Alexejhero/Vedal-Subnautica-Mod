namespace SCHIZO.Commands.Output;

internal sealed class NullSink : ISink
{
    public static readonly NullSink Instance = new();
    private NullSink() { }
    public bool TryConsume(object output) => true;
}
