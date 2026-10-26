namespace TermsAndContusions;

/// <summary>
/// Stable numeric identities owned by this plugin.
///
/// Each game enum is a separate identifier space. Purchase ID 2499 intentionally
/// stays inside the game's native equipment range (2000 through 2500), while the
/// other IDs live above the highest native value observed in the tested build.
/// </summary>
internal static class CustomContentIds
{
    internal const string BulkUpKey = "terms-and-contusions:skill/bulk-up";
    internal const string RunnersWrapsKey = "terms-and-contusions:equipment/runners-wraps";
    internal const string LeadfootKey = "terms-and-contusions:affliction/leadfoot";
    internal const string AdrenalGraftKey = "terms-and-contusions:surgery/adrenal-graft";
    internal const int BulkUpSkill = 11000;
    internal const int RunnersWrapsPurchase = 2499;
    internal const int LeadfootInjury = 11010;
    internal const int AdrenalGraftSurgery = 11020;
}
