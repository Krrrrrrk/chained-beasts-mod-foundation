namespace ChainedBeasts.ModFoundation.Logic;

/// <summary>An absent/null preferred visual never discards a valid fallback.</summary>
public sealed class VisualReferenceSelection<T> where T : class
{
    private T _preferred, _fallback;
    public void Consider(T reference, bool preferred)
    {
        if (reference is null) return;
        _fallback ??= reference;
        if (preferred) _preferred = reference;
    }
    public T Selected => _preferred ?? _fallback;
}
