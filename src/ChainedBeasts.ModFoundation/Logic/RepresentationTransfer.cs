namespace ChainedBeasts.ModFoundation.Logic;

public static class RepresentationTransfer
{
    // Native origin IDs are not unique physical weapon IDs. They only validate an explicit transfer scope.
    public static bool Matches(bool explicitScope, int sourceType, int nextType, int sourceModel, int nextModel, ulong sourceOrigin, ulong nextOrigin) =>
        explicitScope && sourceType == nextType && sourceModel == nextModel && sourceOrigin == nextOrigin;
}
