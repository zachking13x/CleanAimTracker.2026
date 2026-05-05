using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace CleanAimTracker.Services
{
    public static class LicenseService
    {
        private static StoreContext _context;
        private static bool _initialized;

        // Product IDs (you will create these in the Microsoft Store)
        public const string PRODUCT_PRO = "pro_monthly";
        public const string PRODUCT_PRO_TRAINER = "pro_trainer_monthly";
        public const string PRODUCT_LIFETIME = "lifetime_pro";

        // Entitlements
        public static bool HasPro { get; private set; }
        public static bool HasTrainer { get; private set; }
        public static bool HasLifetime { get; private set; }
        public static bool IsFree => !HasPro && !HasTrainer && !HasLifetime;

        public static async Task InitializeAsync()
        {
            if (_initialized)
                return;

            _context = StoreContext.GetDefault();
            await RefreshEntitlementsAsync();

            _initialized = true;
        }

        public static async Task RefreshEntitlementsAsync()
        {
            HasPro = false;
            HasTrainer = false;
            HasLifetime = false;

            // Load durable licenses (lifetime)
            StoreAppLicense appLicense = await _context.GetAppLicenseAsync();

            foreach (var addOn in appLicense.AddOnLicenses)
            {
                if (!addOn.Value.IsActive)
                    continue;

                string id = addOn.Key;

                if (id == PRODUCT_LIFETIME)
                    HasLifetime = true;
            }

            // Load subscriptions
            StoreProductQueryResult result =
                await _context.GetAssociatedStoreProductsAsync(new[] { "Durable", "Subscription" });

            if (result.Products != null)
            {
                foreach (var product in result.Products)
                {
                    string id = product.Value.StoreId;

                    if (id == PRODUCT_PRO)
                        HasPro = true;

                    if (id == PRODUCT_PRO_TRAINER)
                    {
                        HasPro = true;
                        HasTrainer = true;
                    }
                }
            }
        }

        public static async Task<bool> PurchaseAsync(string productId)
        {
            StorePurchaseResult result = await _context.RequestPurchaseAsync(productId);

            if (result.Status == StorePurchaseStatus.Succeeded)
            {
                await RefreshEntitlementsAsync();
                return true;
            }

            return false;
        }
    }
}
