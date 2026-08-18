namespace CleanAimTracker.Services
{
    /// <summary>
    /// Single source of truth for DISPLAYED prices across the app and website.
    ///
    /// IMPORTANT: the REAL prices live in Partner Center (the Store add-ons) — these
    /// strings only control what the UI shows and MUST be kept in sync with Partner
    /// Center by hand. If you change a price in Partner Center, change it here too.
    ///
    /// Launch pricing (2026-07): Lifetime $9.99 "Founder's Price" is the HERO (one-time,
    /// matches KovaaK's, dodges subscription fatigue); Monthly $3.99 is the secondary
    /// low-commitment on-ramp. Rationale + math in the pricing research from this session.
    /// Trigger to revisit: raise Lifetime to $14.99 once past ~500-1000 installs/mo with
    /// real reviews. The "Pro + Trainer" bundle tier is retired from the UI (one Pro).
    /// </summary>
    public static class Pricing
    {
        // ── CAT_REGIONAL_PRICING (2026-08-16) ────────────────────────────────
        // Regional pricing is now live in Partner Center, so the Store charges a
        // different, localised amount depending on the customer's market. These
        // hardcoded US strings are therefore NO LONGER TRUE for most users — the
        // majority of CAT's actives are outside the US.
        //
        // Showing "$9.99" to someone who will be charged ₹X at checkout is a false
        // statement in the UI, and a worse one than anything the coach used to say:
        // it's about money, and the user finds out at the till.
        //
        // The Store already knows the right answer. LicenseService fetches the
        // StoreProduct objects, and each carries Price.FormattedPrice — already
        // localised and currency-formatted for that user's market. Display now uses
        // that, and these constants are the FALLBACK for when the Store is
        // unreachable (offline, sign-in issues, init failure).
        public const string LifetimeFallback = "$9.99";
        public const string MonthlyFallback  = "$3.99";

        public const string LifetimeLabel   = "Founder's Price";
        public const string LifetimeCaption = "One-time · yours forever · never billed again";

        /// <summary>Localised lifetime price, or the US fallback if the Store is unreachable.</summary>
        public static string Lifetime => LicenseService.LifetimePrice ?? LifetimeFallback;

        /// <summary>Localised monthly price, or the US fallback if the Store is unreachable.</summary>
        public static string Monthly => LicenseService.MonthlyPrice ?? MonthlyFallback;

        /// <summary>Monthly with the period suffix, e.g. "₹299 / month".</summary>
        public static string MonthlyPer => $"{Monthly} / month";
    }
}
