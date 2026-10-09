using System;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Outcome of an entitlement refresh.
    ///
    /// AUDIT A6: callers used to read only the HasPro/HasLifetime booleans after a
    /// refresh, which cannot tell "the Store says this account owns nothing" apart from
    /// "we never reached the Store". Restore therefore told users with a valid purchase
    /// that nothing existed to restore whenever the network was down — the worst possible
    /// moment to be wrong, since that is exactly when someone is reinstalling.
    /// </summary>
    public enum EntitlementRefreshResult
    {
        /// <summary>Store reached; this account owns at least one entitlement.</summary>
        Entitled,

        /// <summary>Store reached and answered; this account genuinely owns nothing.</summary>
        NotEntitled,

        /// <summary>
        /// Store could not be reached or did not answer. Entitlement state is UNKNOWN.
        /// Never present this to the user as "you have not purchased".
        /// </summary>
        StoreUnavailable,
    }

    public static class LicenseService
    {
        private static StoreContext? _context;
        private static bool _initialized;
        private static bool _initFailed;

        /// <summary>
        /// True if the Store context could not be initialized (e.g. no internet,
        /// Store service unavailable). When InitFailed is true, IsFree returning
        /// true does NOT mean the user is unlicensed — it means the license check
        /// could not complete. UI should surface a warning rather than treating
        /// the user as a free-tier user.
        /// </summary>
        public static bool InitFailed => _initFailed;

        // ── Store IDs (Microsoft-assigned — used for RequestPurchaseAsync) ──
        public const string STOREID_LIFETIME    = "9P9B8QZZTVX1";   // lifetime_unlock — LIVE
        public const string STOREID_PRO         = "9NKB13MNDKF1";   // pro_monthly — LIVE
        public const string STOREID_PRO_TRAINER = "9N1J6FR6BX1N";   // pro_trainer_monthly — LIVE
        public const string STOREID_PROMO       = "9P6Z5PSG984Z";   // promo_pro_access — LIVE
        public const string STOREID_APP         = "9MVBDZBQ01DM";   // the paid app itself

        /// <summary>
        /// CAT_PAID_APP: true when the Store says this copy is running on a FREE TRIAL of the
        /// paid app. False until the Store answers, so an offline buyer is never locked out.
        /// </summary>
        public static bool IsAppTrial { get; private set; }

        /// <summary>The app's own localised price, for the trial's buy button. Null until fetched.</summary>
        public static string? AppPrice { get; private set; }

        /// <summary>
        /// What the one-time buy button purchases: the app itself for a trial user (add-ons are
        /// retired), otherwise the legacy Lifetime add-on.
        /// </summary>
        public static string OneTimeStoreId => IsAppTrial ? STOREID_APP : STOREID_LIFETIME;

        /// <summary>
        /// A real Store trial always names its SKU, whether it's running or expired. When the
        /// Store has NO licence for this copy (sideloaded build, or a Store hiccup) it still
        /// reports IsTrial=true, with an empty SKU and an endless trial. Seen live 2026-10-09:
        /// "IsActive=False IsTrial=True Sku= Expires=9999-12-31". Reading IsTrial alone
        /// (1.0.103) would lock a paying customer out whenever that happened.
        /// </summary>
        public static bool IsTrialLicense(bool isTrial, string? skuStoreId)
            => isTrial && !string.IsNullOrWhiteSpace(skuStoreId);

        /// <summary>Test seam: the trial state otherwise only comes from a live Store read.</summary>
        internal static void SetAppTrialForTests(bool isTrial, string? appPrice = null)
        {
            IsAppTrial = isTrial;
            AppPrice   = appPrice;
        }

        // ── InAppOfferTokens (developer-defined — used for license checks) ──
        /// <summary>
        /// CAT_REGIONAL_PRICING: the customer's REAL localised prices, straight from the
        /// Store, already currency-formatted for their market. Null until the Store has
        /// been reached — callers fall back to the US strings in <see cref="Pricing"/>.
        /// Never assume these are dollars.
        /// </summary>
        public static string? LifetimePrice { get; private set; }
        public static string? MonthlyPrice  { get; private set; }

        private static string? NullIfBlank(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s;

        private const string TOKEN_LIFETIME     = "lifetime_unlock";
        private const string TOKEN_PRO          = "pro_monthly";
        private const string TOKEN_PRO_TRAINER  = "pro_trainer_monthly";
        private const string TOKEN_PROMO        = "promo_pro_access";

        public static bool HasPro      { get; private set; }
        public static bool HasTrainer  { get; private set; }
        public static bool HasLifetime { get; private set; }
        public static bool IsFree      => !HasPro && !HasTrainer && !HasLifetime;

        // Guard against concurrent InitializeAsync calls (e.g. PurchaseAsync calling it
        // while App.OnStartup's fire-and-forget is still running).
        private static Task? _initTask;

        public static Task InitializeAsync()
        {
            if (_initialized) return Task.CompletedTask;
            return _initTask ??= InitializeCoreAsync();
        }

        private static async Task InitializeCoreAsync()
        {
            try
            {
                // RefreshEntitlementsAsync builds the StoreContext itself now, and reports
                // failure by return value rather than by throwing.
                var result = await RefreshEntitlementsAsync();

                if (result == EntitlementRefreshResult.StoreUnavailable)
                {
                    _initFailed = true;
                    // AUDIT A5: drop the cached task so the NEXT InitializeAsync() actually
                    // retries. `_initTask ??=` used to pin the first (failed) task forever,
                    // which is half of why a transient Store outage was unrecoverable
                    // without restarting the app.
                    _initTask = null;
                    LogService.Error("LicenseService init: Store unavailable, will retry on next attempt", null);
                    return;
                }

                LogService.Info($"LicenseService initialized ({result})");
            }
            catch (Exception ex)
            {
                _initFailed = true;
                _initTask   = null;
                LogService.Error("LicenseService init failed", ex);
            }
        }

        public static async Task<EntitlementRefreshResult> RefreshEntitlementsAsync()
        {
            // AUDIT A5: _context was created once, in InitializeCoreAsync. If that throw'd
            // (no network, Store service down, signed-out account) _context stayed null and
            // _initFailed stayed true for the whole process — every later refresh returned
            // on the line below, so a paid user stayed in a broken licence state until they
            // restarted the app, even after connectivity came back. Rebuild on demand so a
            // later refresh or purchase attempt can actually recover.
            if (_context == null)
            {
                try
                {
                    _context = StoreContext.GetDefault();
                }
                catch (Exception ex)
                {
                    LogService.Error("LicenseService: StoreContext unavailable", ex);
                    return EntitlementRefreshResult.StoreUnavailable;
                }
            }

            if (_context == null) return EntitlementRefreshResult.StoreUnavailable;

            // AUDIT A5: these three used to be zeroed HERE, before the Store call. If the
            // call then threw, a paying customer was silently downgraded to free for the
            // rest of the session. Resolve into locals and commit only after a clean read,
            // so a failed refresh leaves the last known-good entitlements untouched.
            bool hasPro = false, hasTrainer = false, hasLifetime = false, isAppTrial = false;

            try
            {
                var appLicense = await _context.GetAppLicenseAsync();
                isAppTrial = IsTrialLicense(appLicense.IsTrial, appLicense.SkuStoreId);

                // The trial flag now decides whether the coach is locked, so record exactly
                // what the Store said. No account data, just the licence shape.
                LogService.Info($"App licence: IsActive={appLicense.IsActive} IsTrial={appLicense.IsTrial} " +
                                $"Sku={appLicense.SkuStoreId} Expires={appLicense.ExpirationDate:u} " +
                                $"TrialLeft={appLicense.TrialTimeRemaining} AddOns={appLicense.AddOnLicenses.Count}");

                if (isAppTrial)
                {
                    try
                    {
                        var app = await _context.GetStoreProductForCurrentAppAsync();
                        AppPrice = NullIfBlank(app?.Product?.Price?.FormattedPrice);
                    }
                    catch (Exception ex) { LogService.Error("App price lookup failed", ex); }
                }

                // ── Durable (lifetime) — check directly by Store ID ──────────
                if (appLicense.AddOnLicenses.TryGetValue(STOREID_LIFETIME, out var lifetimeLic)
                    && lifetimeLic.IsActive)
                {
                    hasLifetime = true;
                }

                // ── Developer-managed consumable (promo reviewer key) ─────────
                // Checks by InAppOfferToken since Store ID is not yet assigned.
                // When promo_pro_access is redeemed via promotional code, treat
                // it identically to lifetime_unlock — grants permanent Pro access
                // on this device for this account.
                if (!hasLifetime)
                {
                    try
                    {
                        var consumablesResult = await _context.GetAssociatedStoreProductsAsync(
                            new[] { "Consumable", "UnmanagedConsumable" });

                        if (consumablesResult.Products != null)
                        {
                            foreach (var kv in consumablesResult.Products)
                            {
                                var product = kv.Value;
                                string token = product.InAppOfferToken ?? "";

                                if (token == TOKEN_PROMO)
                                {
                                    bool isOwned = appLicense.AddOnLicenses.TryGetValue(
                                        product.StoreId, out var promoLic) && promoLic.IsActive;

                                    if (isOwned)
                                    {
                                        hasLifetime = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Error("Promo consumable check failed", ex);
                    }
                }

                // ── CAT_REGIONAL_PRICING: capture the REAL localised prices ──
                // Partner Center now sets a different price per market, so the app can
                // no longer display a hardcoded "$9.99". StoreProduct.Price.FormattedPrice
                // is already localised and currency-formatted for this customer's market,
                // which is the only string that will match what they see at checkout.
                try
                {
                    var durables = await _context.GetAssociatedStoreProductsAsync(
                        new[] { "Durable" });

                    if (durables.Products != null)
                        foreach (var kv in durables.Products)
                            if (kv.Value.StoreId == STOREID_LIFETIME)
                                LifetimePrice = NullIfBlank(kv.Value.Price?.FormattedPrice);
                }
                catch (Exception ex) { LogService.Error("Lifetime price lookup failed", ex); }

                // ── Subscriptions — match by InAppOfferToken ─────────────────
                var subsResult = await _context.GetAssociatedStoreProductsAsync(
                    new[] { "Subscription" });

                if (subsResult.Products != null)
                {
                    foreach (var kv in subsResult.Products)
                    {
                        var product = kv.Value;
                        string token = product.InAppOfferToken ?? "";

                        // CAT_REGIONAL_PRICING: capture the monthly price regardless of
                        // whether it's owned — a free user is exactly who needs to see it.
                        if (token == TOKEN_PRO)
                            MonthlyPrice = NullIfBlank(product.Price?.FormattedPrice);

                        bool isActive = appLicense.AddOnLicenses.TryGetValue(
                            product.StoreId, out var subLic) && subLic.IsActive;

                        if (!isActive) continue;

                        if (token == TOKEN_PRO)         hasPro = true;
                        if (token == TOKEN_PRO_TRAINER) { hasPro = true; hasTrainer = true; }
                    }
                }
            }
            catch (Exception ex)
            {
                // A partial read is not a licence answer. Keep whatever we last knew to be
                // true and report that the Store could not be reached.
                LogService.Error("License refresh failed", ex);
                return EntitlementRefreshResult.StoreUnavailable;
            }

            // Clean read — commit, and clear the sticky failure flag so the UI stops
            // warning once the Store is reachable again.
            HasPro       = hasPro;
            HasTrainer   = hasTrainer;
            HasLifetime  = hasLifetime;
            IsAppTrial   = isAppTrial;
            _initFailed  = false;
            _initialized = true;

            // Owning the paid app (any non-trial licence) is an entitlement on its own now;
            // only a trial copy with no add-on genuinely owns nothing.
            return IsFree && IsAppTrial
                ? EntitlementRefreshResult.NotEntitled
                : EntitlementRefreshResult.Entitled;
        }

        /// <summary>Purchase an add-on by its Store ID (not InAppOfferToken).</summary>
        public static async Task<bool> PurchaseAsync(string storeId)
        {
            // If init hasn't finished yet (fire-and-forget from App startup), wait for it now.
            // This prevents NullReferenceException when the user opens the Upgrade window
            // within the first second of the app launching.
            if (!_initialized && _initTask != null)
            {
                LogService.Info("PurchaseAsync: waiting for LicenseService init to complete");
                await _initTask;
            }

            if (_context == null)
            {
                LogService.Error("LicenseService: _context still null after init — cannot purchase", null);
                return false;
            }

            try
            {
                StorePurchaseResult? result = await _context.RequestPurchaseAsync(storeId);

                if (result == null)
                {
                    LogService.Error("PurchaseAsync: RequestPurchaseAsync returned null", null);
                    return false;
                }

                LogService.Info($"PurchaseAsync: status={result.Status} for {storeId}");

                if (result.Status == StorePurchaseStatus.Succeeded)
                {
                    await RefreshEntitlementsAsync();
                    return true;
                }

                if (result.Status == StorePurchaseStatus.AlreadyPurchased)
                {
                    // Treat as success — refresh to pick up the license
                    await RefreshEntitlementsAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogService.Error("Purchase failed", ex);
                return false;
            }
        }
    }
}
