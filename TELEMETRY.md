# CAT Telemetry — setup, privacy text, and the queries that answer the questions

**STATUS: live as of v1.0.89 (2026-08-12).** Resource `cleanaimtracker` in resource
group `cleanaimtracker-rg`, East US, workspace-based, subscription "Azure subscription 1".
The connection string is in [`Services/TelemetryConfig.cs`](Services/TelemetryConfig.cs).

Parts 1 and 2 below are kept as a record of how the resource was set up (and what to
redo if it's ever rebuilt). A build with an empty connection string is fully inert —
it never sends, never prompts, and hides the Settings → Privacy toggle entirely — so
clearing that one constant is also the kill switch.

---

## 1. Create the Azure resource

Azure Portal → **Create a resource** → search **Application Insights** → Create.

| Field | Value |
|---|---|
| Subscription / Resource group | whatever you already have |
| Name | `cleanaimtracker` |
| Region | one near you — it only affects ingest latency |
| Resource Mode | **Workspace-based** (Classic is retired) |

After it deploys, open the resource → **Overview** → copy the **Connection String**
(the long `InstrumentationKey=…;IngestionEndpoint=https://…` one, *not* the bare key).

**Cost:** the first 5 GB/month is free. CAT's event volume at current scale is a
rounding error against that — you would need roughly a thousand times your current
MAU to approach it. Set a daily cap anyway: resource → **Usage and estimated costs**
→ **Daily cap** → 1 GB. That makes overspend structurally impossible.

## 2. Paste it in

[`Services/TelemetryConfig.cs`](Services/TelemetryConfig.cs):

```csharp
public const string ConnectionString = "InstrumentationKey=...;IngestionEndpoint=...";
```

Then rebuild. That single line is the on-switch for the whole system.

The string is not a secret in the credential sense — it only authorises *writing*
telemetry, never reading it — but it does ship inside the package, so treat it as
public and don't reuse that resource for anything else.

## 3. Data shows up

First events appear in **Logs** within about 2–5 minutes. Live Metrics is near-instant
if you want to confirm the pipe works while testing.

---

## What is actually sent

| Event | Properties |
|---|---|
| `app_launch` | `drill_count_bucket`, `is_pro` |
| `drill_completed` | `scenario`, `difficulty`, `duration_bucket`, `is_assessment` |
| `coach_report_shown` | `session_bucket`, `free_coached`, `full_report` |
| `coach_report_dwell` | `dwell_bucket`, `full_report` |
| `free_trial_exhausted` | `drill_count_bucket` |
| `paywall_shown` | `trigger` |
| `purchase_started` | `sku`, `trigger` |
| `aim_radar_seen` | `axes_with_data` |
| `launch_after_nudge` | `kind`, `minutes_bucket`, `attribution` |
| `telemetry_opt_out` | — |

Every event also carries `schema`, `app_ver`, and a random per-install GUID as user id.

**Never sent:** accuracy, reaction times, scores, streaks, sensitivity, DPI, machine
name, user name, email, Microsoft account, file paths. `TelemetryService.Track` is the
only exit point, and `TelemetryService.Safe()` rejects any value that isn't enum-like —
both are locked down by tests in `TelemetryServiceTests.cs`.

### One honest caveat

`launch_after_nudge` is **not** a click. Deep-linked toast activation needs a registered
COM activator, which doesn't work reliably for this package, so a tapped toast is
indistinguishable from any other launch. What the event actually measures is "the app
opened within 30 minutes of a nudge firing" — which includes coincidences. Read the
*difference between nudge kinds*, not the absolute rate.

---

## Privacy policy — text to add

Partner Center requires the linked privacy policy to be accurate. Add a section:

> **Usage data**
>
> Clean Aim Tracker collects anonymous usage data to help identify which features are
> working and which are not. This is limited to: which training drills are played and at
> what difficulty, whether screens such as the coaching report are opened and roughly how
> long they stay open, whether upgrade prompts are displayed, the app version, and a
> randomly generated identifier for the installation.
>
> Clean Aim Tracker does **not** collect your training results — scores, accuracy,
> reaction times and sensitivity settings never leave your device. It does not collect
> your name, email address, Microsoft account, computer name, or any other information
> that identifies you personally, and it does not use advertising or cross-site tracking
> identifiers.
>
> Usage data is processed by Microsoft Azure Application Insights and is retained for 90
> days. You can turn this off at any time in Settings → Privacy. Turning it off also
> erases the random identifier associated with your installation.

Set retention to match: resource → **Usage and estimated costs** → **Data Retention** → 90 days.

---

## Known bad data — exclude the dev install

**2026-08-12, roughly 13:30–14:20 UTC.** Before `TelemetryService.SuppressForTesting`
existed, running `dotnet test` on a build with a live connection string fired all nine
event types into this resource, ~9ms apart, once per run. They carry the developer's
install id `a447e8e5bd274d32bc39b3874998deef` and include `purchase_started`, so they
look exactly like a real converting session in any funnel query. Individual rows can't
be deleted from Application Insights.

There is also one `pipe_test` event with no user id, from a manual endpoint check.

**Every query below therefore excludes the dev machine.** Keep that filter in any new
query you write — Zach's own real sessions are also on that install id, and dev usage
shouldn't be in product analytics regardless:

```kusto
let DevInstall = "a447e8e5bd274d32bc39b3874998deef";
customEvents
| where user_Id != DevInstall and name != "pipe_test"
```

To sanity-check that the test emission has actually stopped, look for the signature —
a burst of 8+ distinct event names inside the same 50ms:

```kusto
customEvents
| where timestamp > ago(7d)
| summarize names = dcount(name) by user_Id, bin(timestamp, 50ms)
| where names >= 8
```

That should return nothing for any run after 2026-08-12 14:20 UTC.

## The queries

Application Insights → **Logs**. Custom events land in the `customEvents` table.
Add the `DevInstall` filter above to each one before trusting the numbers.

### Q1 — Is the reverse trial landing? (the one that matters most)

```kusto
customEvents
| where timestamp > ago(30d)
| where name in ("app_launch", "coach_report_shown", "free_trial_exhausted", "paywall_shown", "purchase_started")
| summarize users = dcount(user_Id) by name
| order by users desc
```

Read it as a funnel. If `coach_report_shown` is a small fraction of `app_launch`, people
are installing and never finishing a drill — an onboarding problem. If it's healthy but
`paywall_shown` is tiny, they're getting the value and never being asked.

### Q2 — Do they actually READ the report, or close it?

```kusto
customEvents
| where timestamp > ago(30d) and name == "coach_report_dwell"
| extend bucket = tostring(customDimensions.dwell_bucket),
         full   = tostring(customDimensions.full_report)
| summarize sessions = count() by full, bucket
| order by full, bucket asc
```

This is the aha test. A pile of `0-4` and `5-14` means the report is being dismissed, and
the reverse trial is giving away something nobody is consuming. Compare `full=1` against
`full=0` — if the full report doesn't hold attention longer than the locked one, the
paywall isn't protecting anything people want.

### Q3 — Which drills earn their keep?

```kusto
customEvents
| where timestamp > ago(30d) and name == "drill_completed"
| extend scenario = tostring(customDimensions.scenario)
| where tostring(customDimensions.is_assessment) == "0"
| summarize plays = count(), players = dcount(user_Id) by scenario
| order by plays desc
```

Directly answers whether the bot drills (`HeadshotStrafes`, `PeekClick`, `HeadTrack`)
justify more investment, or whether everyone plays `Flicking` and goes home.

### Q4 — Which prompt actually sells?

```kusto
customEvents
| where timestamp > ago(30d) and name in ("paywall_shown", "purchase_started")
| extend trigger = tostring(customDimensions.trigger)
| summarize shown = countif(name == "paywall_shown"),
            started = countif(name == "purchase_started")
        by trigger
| extend rate = round(100.0 * started / shown, 1)
| order by shown desc
```

Triggers: `nav_go_pro`, `value_moment_card`, `free_limit_card`, `locked_coach_report`,
`pending_reminder`, `feature_gate_*`. Kill the surfaces with volume and no rate; move
the winner earlier in the flow.

### Q5 — Are the nudges worth keeping?

```kusto
customEvents
| where timestamp > ago(30d) and name == "launch_after_nudge"
| extend kind = tostring(customDimensions.kind)
| summarize launches = count(), users = dcount(user_Id) by kind
| order by launches desc
```

Compare kinds against each other (see the caveat above — this is correlation).

### Q6 — Opt-out rate

```kusto
let optouts = toscalar(customEvents | where name == "telemetry_opt_out" | dcount(user_Id));
let total   = toscalar(customEvents | where name == "app_launch" | dcount(user_Id));
print opt_out_pct = round(100.0 * optouts / total, 1)
```

Worth watching. A high number means the disclosure copy is scaring people, and the
disclosure is the thing to fix — not the default.

---

## Reading it honestly

- **The install id is per-install, not per-person.** A reinstall looks like a new user.
  Treat `dcount(user_Id)` as an upper bound on people.
- **Opt-outs are invisible after they opt out.** Everything above describes the
  measured population, which is not the whole population.
- **`OnExit` is best-effort.** Events are flushed on clean exit; a crash or a kill loses
  that session's buffer. Expect a small, roughly constant undercount.
- **Nothing here measures revenue.** Partner Center owns that truth. `purchase_started`
  is intent — how far down the funnel people get — not a sale.
