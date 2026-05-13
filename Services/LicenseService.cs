using System;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace CleanAimTracker.Services
{
    public static class LicenseService
    {
        private static StoreContext? _context;
        private static bool _initialized;

        // ── Store IDs (Microsoft-assigned — used for RequestPurchaseAsync) ──
        public const string STOREID_LIFETIME    = "9P9B8QZZTVX1";   // lifetime_unlock — LIVE
        public const string STOREID_PRO         = "9NKB13MNDKF1";   // pro_monthly — LIVE
        public const string STOREID_PRO_TRAINER = "9N1J6FR6BX1N";   // pro_trainer_monthly — LIVE
        public const string STOREID_PROMO       = "9P6Z5PSG984Z";   // promo_pro_access — LIVE

        // ── InAppOfferTokens (developer-defined — used for license checks) ──
        private const string TOKEN_LIFETIME     = "lifetime_unlock";
        private const string TOKEN_PRO          = "pro_monthly";
        private const string TOKEN_PRO_TRAINER  = "pro_trainer_monthly";
        private const string TOKEN_PROMO        = "promo_pro_access";

        public static bool HasPro      { get; private set; }
        public static bool HasTrainer  { get; private set; }
        public static bool HasLifetime { get; private set; }
        public static bool IsFree      => !HasPro && !HasTrainer && !HasLifetime;

        public static async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                _context = StoreContext.GetDefault();
                await RefreshEntitlementsAsync();
                _initialized = true;
            }
            catch (Exception ex)
            {
                LogService.Error("LicenseService init failed", ex);
            }
        }

        public static async Task RefreshEntitlementsAsync()
        {
            if (_context == null) return;

            HasPro      = false;
            HasTrainer  = false;
            HasLifetime = false;

            try
            {
                var appLicense = await _context.GetAppLicenseAsync();

                // ── Durable (lifetime) — check directly by Store ID ──────────
                if (appLicense.AddOnLicenses.TryGetValue(STOREID_LIFETIME, out var lifetimeLic)
                    && lifetimeLic.IsActive)
                {
                    HasLifetime = true;
                }

                // ── Developer-managed consumable (promo reviewer key) ─────────
                // Checks by InAppOfferToken since Store ID is not yet assigned.
                // When promo_pro_access is redeemed via promotional code, treat
                // it identically to lifetime_unlock — grants permanent Pro access
                // on this device for this account.
                if (!HasLifetime)
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
                                        HasLifetime = true;
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

                // ── Subscriptions — match by InAppOfferToken ─────────────────
                var subsResult = await _context.GetAssociatedStoreProductsAsync(
                    new[] { "Subscription" });

                if (subsResult.Products != null)
                {
                    foreach (var kv in subsResult.Products)
                    {
                        var product = kv.Value;
                        string token = product.InAppOfferToken ?? "";

                        bool isActive = appLicense.AddOnLicenses.TryGetValue(
                            product.StoreId, out var subLic) && subLic.IsActive;

                        if (!isActive) continue;

                        if (token == TOKEN_PRO)         HasPro = true;
                        if (token == TOKEN_PRO_TRAINER) { HasPro = true; HasTrainer = true; }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("License refresh failed", ex);
            }
        }

        /// <summary>Purchase an add-on by its Store ID (not InAppOfferToken).</summary>
        public static async Task<bool> PurchaseAsync(string storeId)
        {
            if (_context == null)
            {
                LogService.Error("LicenseService not initialized — cannot purchase", null);
                return false;
            }

            try
            {
                StorePurchaseResult result = await _context.RequestPurchaseAsync(storeId);

                if (result.Status == StorePurchaseStatus.Succeeded)
                {
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
