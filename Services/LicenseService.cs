using CleanAimTracker.Services;
using System;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace CleanAimTracker.Services
{
    public static class LicenseService
    {
        private static StoreContext? _context;
        private static bool _initialized;

        public const string PRODUCT_PRO          = "pro_monthly";
        public const string PRODUCT_PRO_TRAINER  = "pro_trainer_monthly";
        public const string PRODUCT_LIFETIME     = "lifetime_pro";

        public static bool HasPro      { get; private set; }
        public static bool HasTrainer  { get; private set; }
        public static bool HasLifetime { get; private set; }
        public static bool IsFree => !HasPro && !HasTrainer && !HasLifetime;

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

                var productsResult = await _context.GetAssociatedStoreProductsAsync(
                    new[] { "Durable", "Subscription" });

                if (productsResult.Products == null) return;

                foreach (var kv in productsResult.Products)
                {
                    var product = kv.Value;
                    string token = product.InAppOfferToken ?? "";

                    // Check if this product has an active license
                    bool isActive = appLicense.AddOnLicenses.TryGetValue(product.StoreId, out var lic)
                        && lic.IsActive;

                    if (!isActive) continue;

                    if (token == PRODUCT_LIFETIME) HasLifetime = true;
                    if (token == PRODUCT_PRO)         HasPro = true;
                    if (token == PRODUCT_PRO_TRAINER) { HasPro = true; HasTrainer = true; }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("License refresh failed", ex);
            }
        }

        public static async Task<bool> PurchaseAsync(string productId)
        {
            if (_context == null)
            {
                LogService.Error("LicenseService not initialized — cannot purchase", null);
                return false;
            }

            try
            {
                StorePurchaseResult result = await _context.RequestPurchaseAsync(productId);

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
