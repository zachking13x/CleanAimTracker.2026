using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_TELEMETRY: anonymous product analytics.
    ///
    /// WHY THIS EXISTS: Partner Center says whether people came back. It cannot say why.
    /// Every open product question — is the reverse trial landing, does anyone read the
    /// coach report, are the bot drills worth more investment, does the paywall ever get
    /// seen — is a funnel question, and a funnel needs in-app events.
    ///
    /// WHAT IS SENT: bucketed counts and enum names. Scenario names, difficulty names,
    /// coarse duration buckets, and a random per-install GUID.
    ///
    /// WHAT IS NEVER SENT: accuracy, reaction times, scores, sensitivity, DPI, file
    /// paths, machine name, user name, email, IP-derived identity, or anything that
    /// could be joined back to a person. The <see cref="Track"/> chokepoint is the only
    /// way an event leaves this app — if a value is not on the allowed shape below, it
    /// does not go out. Adding a raw metric here is a privacy change, not a tweak.
    ///
    /// FAILURE POLICY: analytics must never be able to break the app. Every public
    /// method swallows its own exceptions. A dead network, a bad connection string, or
    /// a disposed client all degrade to "no data", never to a crash.
    /// </summary>
    public static class TelemetryService
    {
        private static readonly object   _gate = new();
        private static TelemetryClient?  _client;
        private static bool              _initTried;
        private static string            _installId = "";
        private static bool              _enabled;

        // ── Event names (one const per event; typos become compile errors) ───
        public const string EvtAppLaunch            = "app_launch";
        public const string EvtDrillCompleted       = "drill_completed";
        public const string EvtCoachReportOpened    = "coach_report_opened";
        public const string EvtCoachReportShown     = "coach_report_shown";
        public const string EvtCoachReportDwell     = "coach_report_dwell";
        public const string EvtCoachReportFailed    = "coach_report_failed";
        public const string EvtFreeTrialExhausted   = "free_trial_exhausted";
        public const string EvtPaywallShown         = "paywall_shown";
        public const string EvtPurchaseStarted      = "purchase_started";
        public const string EvtAimRadarSeen         = "aim_radar_seen";
        public const string EvtLaunchAfterNudge     = "launch_after_nudge";
        public const string EvtTelemetryOptOut      = "telemetry_opt_out";

        /// <summary>
        /// CAT_TEST_EMISSION (2026-08-12): a hard kill switch for automated runs.
        ///
        /// WHY THIS EXISTS: TelemetryServiceTests calls every Track* helper in sequence
        /// to prove they are safe no-ops. That was true only while the connection string
        /// was empty. The moment a real endpoint was configured, the test suite became a
        /// live emitter — nine events per `dotnet test`, stamped with the DEVELOPER'S
        /// install id, landing in production analytics indistinguishably from a real
        /// session (including purchase_started, which briefly looked like a first sale).
        ///
        /// The test assembly sets this via a [ModuleInitializer], so it covers every
        /// test — present and future — rather than the one call site anyone remembers.
        /// </summary>
        public static bool SuppressForTesting { get; set; }

        /// <summary>
        /// True only when a connection string is configured, the user has SEEN the
        /// disclosure, and they have not opted out. All three are required — a build
        /// with no connection string is inert, and nothing is sent before disclosure.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                if (SuppressForTesting) return false;
                EnsureInit();
                return _enabled && _client != null;
            }
        }

        /// <summary>True when the one-time first-run disclosure still needs showing.</summary>
        public static bool NeedsDisclosure()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TelemetryConfig.ConnectionString)) return false;
                return !SettingsService.Load().TelemetryNoticeShown;
            }
            catch { return false; }
        }

        /// <summary>Record that the disclosure has been shown. Call once it is actually on screen.</summary>
        public static void MarkDisclosureShown()
        {
            try
            {
                var s = SettingsService.Load();
                if (s.TelemetryNoticeShown) return;
                s.TelemetryNoticeShown = true;
                SettingsService.Save(s);
                lock (_gate) { _initTried = false; }   // re-evaluate the gate on next use
            }
            catch { }
        }

        /// <summary>
        /// Flip the user's opt-out. Turning it OFF sends one final opt-out event before
        /// shutting down — so the opt-out rate is itself measurable — then discards the
        /// install id, which makes the next opt-in indistinguishable from a new install.
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            try
            {
                var s = SettingsService.Load();
                if (s.TelemetryEnabled == enabled) return;

                if (!enabled && IsEnabled)
                {
                    Track(EvtTelemetryOptOut);
                    Shutdown();
                }

                s.TelemetryEnabled = enabled;
                if (!enabled) s.TelemetryInstallId = "";   // forget who this was
                SettingsService.Save(s);

                lock (_gate)
                {
                    _initTried = false;
                    _installId = "";
                    _client    = null;
                }
            }
            catch { }
        }

        // ── Event helpers ────────────────────────────────────────────────────

        /// <summary>Session start. drillCount is bucketed — the raw count is a fingerprint.</summary>
        public static void TrackAppLaunch(int drillCount, bool isPro)
            => Track(EvtAppLaunch, new Dictionary<string, string>
            {
                ["drill_count_bucket"] = CountBucket(drillCount),
                ["is_pro"]             = isPro ? "1" : "0",
            });

        /// <summary>A finished drill. Answers "what is actually played" vs what was built.</summary>
        public static void TrackDrillCompleted(string scenario, string difficulty, int durationSeconds, bool isAssessment)
            => Track(EvtDrillCompleted, new Dictionary<string, string>
            {
                ["scenario"]        = Safe(scenario),
                ["difficulty"]      = Safe(difficulty),
                ["duration_bucket"] = SecondsBucket(durationSeconds),
                ["is_assessment"]   = isAssessment ? "1" : "0",
            });

        /// <summary>
        /// CAT_COACH_FAILURE (2026-08-15): the report WINDOW opened, before any analysis
        /// has run.
        ///
        /// WHY THIS EXISTS: a real user in Karachi completed 6 drills and produced only 4
        /// coach_report_shown events. That gap had three possible causes — the coach threw,
        /// the score-slam callback that opens the window was dropped, or something else —
        /// and NONE of them emitted anything. A silently failing coach looked identical to
        /// a user simply not opening reports, which is the worst possible blind spot in a
        /// product whose entire value is the coach.
        ///
        /// The pair is the diagnostic:
        ///   opened &lt; drills   → the window never opened (the slam-callback path)
        ///   shown  &lt; opened   → the window opened and the analysis failed
        /// </summary>
        public static void TrackCoachReportOpened(bool isReplay)
            => Track(EvtCoachReportOpened, new Dictionary<string, string>
            {
                ["is_replay"] = isReplay ? "1" : "0",
            });

        /// <summary>
        /// The coach analysis threw and the user saw the error state instead of a report.
        /// Carries the exception TYPE only — never the message, which can contain paths
        /// or data and has no place in analytics.
        /// </summary>
        public static void TrackCoachReportFailed(string exceptionType)
            => Track(EvtCoachReportFailed, new Dictionary<string, string>
            {
                ["error"] = Safe(exceptionType),
            });

        /// <summary>A coach report reached the screen. The reverse trial's aha moment.</summary>
        public static void TrackCoachReportShown(int sessionIndex, bool wasFreeCoached, bool isFullReport)
            => Track(EvtCoachReportShown, new Dictionary<string, string>
            {
                ["session_bucket"]  = CountBucket(sessionIndex),
                ["free_coached"]    = wasFreeCoached ? "1" : "0",
                ["full_report"]     = isFullReport ? "1" : "0",
            });

        /// <summary>
        /// How long the report stayed open. The single most valuable event here: it is
        /// the only thing that separates "we showed them the aha" from "it landed".
        /// </summary>
        public static void TrackCoachReportDwell(int seconds, bool isFullReport)
            => Track(EvtCoachReportDwell, new Dictionary<string, string>
            {
                ["dwell_bucket"] = SecondsBucket(seconds),
                ["full_report"]  = isFullReport ? "1" : "0",
            });

        /// <summary>The free coached drills ran out — the exact conversion moment.</summary>
        public static void TrackFreeTrialExhausted(int drillCount)
            => Track(EvtFreeTrialExhausted, new Dictionary<string, string>
            {
                ["drill_count_bucket"] = CountBucket(drillCount),
            });

        /// <summary>A paywall surface was displayed. trigger names WHICH surface.</summary>
        public static void TrackPaywallShown(string trigger)
            => Track(EvtPaywallShown, new Dictionary<string, string> { ["trigger"] = Safe(trigger) });

        /// <summary>Purchase flow opened. Not a completed sale — Partner Center owns that truth.</summary>
        public static void TrackPurchaseStarted(string sku, string trigger)
            => Track(EvtPurchaseStarted, new Dictionary<string, string>
            {
                ["sku"]     = Safe(sku),
                ["trigger"] = Safe(trigger),
            });

        /// <summary>The aim radar rendered. Does the identity artifact bring people back?</summary>
        public static void TrackAimRadarSeen(int axesWithData)
            => Track(EvtAimRadarSeen, new Dictionary<string, string>
            {
                ["axes_with_data"] = Math.Clamp(axesWithData, 0, 6).ToString(),
            });

        /// <summary>
        /// The app launched shortly after a nudge of this kind fired. Answers "are the
        /// notifications worth keeping?".
        ///
        /// DELIBERATELY NOT CALLED "notification_clicked": it is an INFERENCE, not a
        /// click. Deep-linked toast activation needs a registered COM activator, which
        /// does not work reliably for this package, so there is no true click signal.
        /// What this measures is a launch inside the attribution window after a nudge
        /// fired — which will include coincidences. Read it as a correlation, and read
        /// the DIFFERENCE between nudge kinds rather than the absolute rate.
        /// </summary>
        public static void TrackLaunchAfterNudge(string kind, int minutesSince)
            => Track(EvtLaunchAfterNudge, new Dictionary<string, string>
            {
                ["kind"]           = Safe(kind),
                ["minutes_bucket"] = minutesSince switch
                {
                    < 5  => "0-4",
                    < 15 => "5-14",
                    _    => "15-30",
                },
                ["attribution"]    = "inferred",
            });

        // ── Core ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The single exit point. Every event passes through here, so the privacy
        /// guarantee is enforceable by reading one method rather than auditing callers.
        /// </summary>
        private static void Track(string name, Dictionary<string, string>? props = null)
        {
            try
            {
                if (SuppressForTesting) return;   // belt and braces — never trust one gate
                if (!IsEnabled) return;

                var p = props ?? new Dictionary<string, string>();
                p["schema"]  = TelemetryConfig.SchemaVersion;
                p["app_ver"] = AppVersion();

                _client!.TrackEvent(name, p);
            }
            catch { /* analytics must never surface to the user */ }
        }

        private static void EnsureInit()
        {
            lock (_gate)
            {
                if (_initTried) return;
                _initTried = true;
                _enabled   = false;

                try
                {
                    if (SuppressForTesting) return;
                    if (string.IsNullOrWhiteSpace(TelemetryConfig.ConnectionString)) return;

                    var s = SettingsService.Load();
                    // Both gates matter: opted in AND actually told about it.
                    if (!s.TelemetryEnabled || !s.TelemetryNoticeShown) return;

                    if (string.IsNullOrWhiteSpace(s.TelemetryInstallId))
                    {
                        s.TelemetryInstallId = Guid.NewGuid().ToString("N");
                        SettingsService.Save(s);
                    }
                    _installId = s.TelemetryInstallId;

                    var config = new TelemetryConfiguration
                    {
                        ConnectionString = TelemetryConfig.ConnectionString,
                    };

                    var client = new TelemetryClient(config);

                    // Scrub the context the SDK would otherwise populate from the machine.
                    // RoleInstance in particular defaults to the COMPUTER NAME, which is
                    // frequently a real person's name.
                    client.Context.Cloud.RoleInstance   = "";
                    client.Context.Cloud.RoleName       = "CleanAimTracker";
                    client.Context.Device.Id            = "";
                    client.Context.Device.OperatingSystem = "";
                    client.Context.User.Id              = _installId;
                    client.Context.User.AccountId       = "";
                    client.Context.Session.Id           = Guid.NewGuid().ToString("N");
                    client.Context.Component.Version    = AppVersion();

                    _client  = client;
                    _enabled = true;
                }
                catch
                {
                    _client  = null;
                    _enabled = false;
                }
            }
        }

        /// <summary>Flush buffered events. Call on app exit — InMemoryChannel batches ~30s.</summary>
        public static void Shutdown()
        {
            try
            {
                TelemetryClient? c;
                lock (_gate) { c = _client; }
                if (c == null) return;

                c.Flush();
                // The in-memory channel's flush is fire-and-forget; a short grace period
                // is the documented way to give the send a chance before process exit.
                System.Threading.Thread.Sleep(600);
            }
            catch { }
        }

        // ── Bucketing (raw values never leave) ───────────────────────────────

        /// <summary>Coarse count buckets. A raw session count is a usable fingerprint.</summary>
        public static string CountBucket(int n) => n switch
        {
            <= 0  => "0",
            1     => "1",
            2     => "2",
            3     => "3",
            <= 5  => "4-5",
            <= 10 => "6-10",
            <= 25 => "11-25",
            <= 50 => "26-50",
            <= 100 => "51-100",
            _     => "100+",
        };

        /// <summary>Coarse second buckets, used for both drill length and report dwell.</summary>
        public static string SecondsBucket(int s) => s switch
        {
            < 0    => "unknown",
            < 5    => "0-4",
            < 15   => "5-14",
            < 30   => "15-29",
            < 60   => "30-59",
            < 120  => "60-119",
            < 300  => "120-299",
            _      => "300+",
        };

        /// <summary>
        /// Property values are enum-like by contract. This clamps anything unexpected
        /// rather than trusting callers — a free-text value slipping into a property is
        /// exactly how PII leaks into an analytics pipeline.
        /// </summary>
        public static string Safe(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            var v = value.Trim();
            if (v.Length > 40) v = v.Substring(0, 40);
            foreach (char c in v)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != ' ')
                    return "other";
            return v;
        }

        private static string AppVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly()
                           .GetName().Version?.ToString(3) ?? "unknown";
            }
            catch { return "unknown"; }
        }
    }
}
