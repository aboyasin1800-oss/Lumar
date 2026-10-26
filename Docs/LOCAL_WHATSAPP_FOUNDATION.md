# Local WhatsApp Foundation 1/3

Status: CLOSED / VERIFIED on 2026-10-26. Foundation only; not approved for operational messaging. Mission 2/3 was not started.

Final verdict: **A - LOCAL WHATSAPP CHANNEL FOUNDATION VERIFIED**

## Source of Truth and Audit

- `Backend/LUMAR_ERP_API_V2/Services/MessageWriteService.cs` renders the existing MessageTemplates through MessageTemplateRenderer. It calls CreateCustomerMessageAndNotificationAsync with final Title/Body and literal Channel `WhatsApp`. Missing/disabled WhatsApp still creates the existing application notification.
- `Repositories/MessageRepository.cs` inserts CustomerMessages as Pending, AttemptCount=0. ClaimNextAsync claims by exact channel in a serializable transaction plus application lock, sets Processing, increments AttemptCount, clears FailureReason, and hydrates existing context. Existing Failed/IsRetryable/max-attempt and stale-Processing rules remain untouched.
- MarkSentAsync saves the provider identifier and SentAtUtc without setting DeliveredAtUtc. MarkFailedAsync persists the safe reason (500-character limit) and IsRetryable. Existing BuildIdempotencyKey and duplicate-key/atomic message-notification logic are unchanged.
- `OutboundMessageDispatcher.cs` resolves adapters from `IEnumerable<IOutboundMessageChannel>` by ChannelName. `OutboundMessageBackgroundService.cs` drives the existing dispatcher. No second dispatcher exists.
- `IOutboundMessageChannel.cs` is the proven transport extension point. The new adapter implements it but is registered ONLY as LocalWhatsAppMessageChannel, not in the dispatcher's enumerable. The new gateway also denies sends unless an explicit owner-approved test number is configured.
- `Program.cs` continues to register InAppMessageChannel and WhatsAppCloudMessageChannel as the only operational channels. `WhatsAppCloudMessageChannel.cs` still owns Meta's transport/template contract. The local adapter transports CustomerMessage.Body, not RawCampaignText or a newly rendered template; CampaignImageUrl is transport metadata only.
- `InAppMessageChannel.cs`, `SettingsRepository.cs`, `WhatsAppConnectionSettingsStore.cs`, PushDeviceService, FirebasePushSender and MessageRepository were audited without modification. The original eight mandatory files were read before implementation; direct call sites were followed only as needed.

### Operational State

Read-only SQL on `LUMAR_ERP_ES_VALIDATION` found zero LocalWhatsApp messages and zero rows for MessageMasterEnabled, WhatsAppEnabled, ActiveWhatsAppProvider and WhatsAppProvider. SettingsRepository therefore uses its existing true defaults for the first two keys. There is no persisted provider selector in this path: `WhatsApp -> WhatsAppCloudMessageChannel` is the source-code mapping.

The existing local Meta protected store was present with Enabled=true and phone configured. Its secrets were not printed, returned, decrypted or changed. Evidence contains only configuration booleans and a SHA-256 fingerprint. This proves the stored configuration, not Meta network availability or access-token validity. No live Meta send was attempted.

Evidence: `artifacts/local-whatsapp-foundation/audit-before.json`, `audit-dependency-options.json`, plus the read-only SQL results in this session. No System_Settings value was added or changed. Migration=NO.

## Technology Decision

Registry versions verified on 2026-10-26 (local time), Node v24.21.0, Windows:

| Criterion | Baileys 7.0.0-rc14 | whatsapp-web.js 1.34.7 | WPPConnect 2.3.6 / Server |
| --- | --- | --- | --- |
| Session/restart | Persisted cryptographic auth state, application-managed | LocalAuth Chromium profile, destroy retains it | Browser profile/token persistence |
| QR/pairing | QR and pairing code | QR and pairing code; this integration exposes QR only | QR and pairing facilities; API varies by release |
| Reconnect/disconnect/logout | Socket lifecycle and auth invalidation | initialize/destroy/logout; directly verified in installed source | Browser/session lifecycle APIs |
| Text/image/media | Supported | Text and image transport supported here | Supported; broader API surface |
| Status/current number | Socket identity; newer LID handling requires care | ready + ClientInfo.wid, c.us numeric identity only | Connection/status/host identity APIs |
| Runtime/resources | Node >=20, no Chromium, usually lower memory | Node >=18 upstream; Node 24 pinned here, Chromium process | Node plus Chromium/Puppeteer and additional native dependencies |
| Windows/service | Lightweight process; RC/native bridge compatibility risk | Independent foreground process suitable for a dedicated Windows service identity | Larger server/distribution/service surface |
| Persistence | Requires durable auth storage | Requires durable user profile filesystem | Requires durable browser/token storage |
| Automated tests | Mock socket plus real pairing tests | Driver boundary + HTTP/ACL/Chromium tests + real pairing gate | More built-in surface to isolate |
| Maintenance | Latest is release candidate; legacy 6.7.24 also published | Stable latest; transitive Puppeteer security override required | Recent registry publication; additional layers/licenses |
| Breaking changes | WhatsApp protocol + RC/LID/native changes | WhatsApp Web internals + Puppeteer/browser changes | WhatsApp Web + WA-JS + browser/server changes |
| Replacement cost | Hidden behind LUMAR Gateway contract | Hidden behind LUMAR Gateway contract | Hidden behind LUMAR Gateway contract |
| Account risk | Unofficial; restriction/disconnection possible | Unofficial; restriction/disconnection possible | Unofficial; restriction/disconnection possible |

Recommendation: whatsapp-web.js behind the LUMAR-owned gateway because its stable API and LocalAuth lifecycle fit this Windows foundation. Do not equate stable package version with guaranteed session uptime. Baileys is lighter, but adopting its current release candidate would add protocol/auth migration risk. WPPConnect is more extensive than this bounded single-session requirement. The WPPConnect Server scoped name queried is not published on npm (404); do not treat that as absence of its GitHub server distribution.

The initially installed Puppeteer 24.38.0 chain produced 11 high audit findings (extract-zip, basic-ftp, zod and affected parents). A pinned 25.16.0 override passed Chromium launch, real WhatsApp QR generation and all gateway tests; the new npm audit reports zero vulnerabilities. It is an explicit compatibility deviation from whatsapp-web.js's pinned dependency; the owner subsequently verified actual account linking and recovery as recorded below. This does not guarantee future upstream compatibility. Never run npm audit fix --force or downgrade WhatsApp blindly to satisfy an audit recommendation. Lockfile is committed-source material, not a session file.

## Gateway Contract

All routes require `Authorization: Bearer <local gateway token>`, including health. Default binding is `127.0.0.1:5094`. Requests carrying an Origin header are denied; no CORS or cookie authentication. Responses use no-store. No HTTP request/response body logging.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | /health | Gateway health, contractVersion=1, operationalActivation=false |
| GET | /session/status | State, proven currentNumber, linked, safe lastError |
| GET | /session/link-material | QR and expiry only; never credentials |
| POST | /session/link | Start QR linking, body `{}` |
| POST | /session/disconnect | Close browser, retain valid stored session |
| POST | /session/reconnect | Reuse stored session, not resend messages |
| POST | /session/logout | Revoke linked session; reconnect required first if offline |
| POST | /session/replace | Revoke old connected account, then start a new link |
| POST | /messages/send | Final recipient/text/imageUrl transport, test recipient only |

No provider activation endpoint. No gateway database, message queue, retry engine, idempotency engine, template store or business rules. Concurrent lifecycle/send operations are rejected BUSY, not enqueued. A Windows named-pipe lock prevents two processes owning the same data directory.

Session states: NotLinked, Linking, Connected, Disconnected, Reconnecting, LoggedOut, Error. Backend uses Unavailable when it cannot reach the gateway. CurrentNumber comes only from the connected client's numeric c.us identity, never from request input or persisted number text. It is cleared when the connection is lost and only re-established from ready. Connected never means operational activation.

Successful send means the library returned a real sent-message identifier, not delivery/read confirmation. The existing engine may retry only proven pre-send NOT_CONNECTED/BUSY results. An ambiguous network failure or missing provider identifier is permanent/non-retryable to avoid duplicate sends. There is no automatic fallback to Meta.

Images are bounded to 5 MiB, JPEG/PNG/WebP with signature checks. Remote fetch requires an explicitly configured exact media origin; redirects and embedded URL credentials are refused. Other media types are outside this foundation. No customer/media URL is fetched by default.

## Backend Contract

`LocalWhatsAppMessageChannel` receives CustomerMessageDto, validates transport fields, and forwards Body unchanged. It returns the existing OutboundAdapterResult. `LocalWhatsAppGatewayClient` knows only LUMAR DTOs and HTTP paths, not the WhatsApp library.

`LocalWhatsAppController` exposes the matching health/status/link-material/session-operation routes under `/local-whatsapp`. Each route resolves the existing IAuthenticatedUserContext, requires an active Admin or System Administrator, and avoids returning secrets. No public send endpoint is exposed. Session-operation path uses `{operation}`, not MVC's reserved `{action}`.

`LocalWhatsAppFoundationRegistration` adds the typed HTTP client and the concrete adapter only. The default credential path is shared by same-user Backend and Gateway; no credentials are stored in appsettings, System_Settings or Flutter. Backend follows its existing Visual Studio ownership policy and is not launched by this setup.

## Storage and Security

- Data: `%LOCALAPPDATA%\LUMAR\LocalWhatsAppGateway`, outside Git. `auth/session-lumar` contains sensitive browser/session material; `web-cache` contains the web-version cache. Both require persistent storage across restarts.
- `gateway-token` is a cryptographically random 256-bit local API credential, read only by Backend/owner CLI. `state.json` contains linked/connect/loggedOut booleans, no WhatsApp credentials or phone number.
- Startup sets a protected Windows DACL permitting only the operating identity and SYSTEM. It does not disable UAC, antivirus, script execution policy or browser sandbox. Do not run under Administrator unnecessarily.
- Chromium uses its normal OS-backed protections where available. The profile is NOT claimed to be fully encrypted by LUMAR; ACLs do not protect against compromise of the same Windows user, SYSTEM, administrator access or unprotected disk backups. Use OS-managed disk/backup protection where required. No homemade encryption is used.
- QR exists in memory with a short expiry and is exposed only on authenticated no-store responses or the owner's interactive terminal. The CLI refuses redirected QR output. Do not photograph, log or paste QR/session files into tickets or chat.
- Git excludes `.wwebjs_auth`, `.wwebjs_cache`, gateway data and node_modules. Logs contain no message bodies, tokens, QR or full current number.

## Operation

From `D:\YASIN`, Windows PowerShell 5.1, no elevation:

```powershell
npm.cmd --prefix Gateway/LocalWhatsApp ci --no-fund
node Gateway/LocalWhatsApp/src/server.mjs
```

In a second interactive terminal, after account-link authorization:

```powershell
node Gateway/LocalWhatsApp/src/owner.mjs link
```

The owner scans with the authorized TEST account in WhatsApp > Linked Devices, then reports success without sending QR or credentials. Use `owner.mjs status`, `disconnect`, `reconnect`, `logout`, or `replace` as appropriate. Never bind a production account without explicit approval. Foreground Ctrl+C performs scoped browser shutdown. Production service installation is not part of 1/3; a dedicated service identity must retain the same data directory and Backend access must be deliberately provisioned.

Optional process-only settings: `LUMAR_LOCAL_GATEWAY_PORT` (default 5094), `LUMAR_LOCAL_TEST_RECIPIENT` (empty by default; owner-specified international digits only), `LUMAR_LOCAL_MEDIA_ORIGIN` (empty by default). A nondefault port must match Backend `LocalWhatsAppGateway:BaseUrl`. These are readiness/test settings, not active-provider selection. No real test number has been authorized yet and no live message has been sent.

Verification commands:

```powershell
node Gateway/LocalWhatsApp/scripts/verify.mjs
node Gateway/LocalWhatsApp/scripts/verify.mjs --backend
node Gateway/LocalWhatsApp/scripts/verify.mjs --push-baseline
```

The backend verification checks for an operational Backend process before isolated fixtures, forces only `LUMAR_ERP_ES_VALIDATION` in the child environment, and preserves all assertions. It does not start/stop Backend. Test fixtures are temporary and use their existing scoped cleanup. No historical message replay is performed.

## Resolved Push Test Conflict

First failed request: DispatchForOfficialNotificationAsync(customerId:9999, referenceId:C-9001). The fixture contains only a device for MobileAccountId=77, supplies null connections, and expects delivery to 77. The current service's explicit-target fallback resolves 9999; the fake repository filters by that account and returns no devices. Result is NoDevices/Success=false; assertion fails at MobilePushDeviceServiceTests.cs:241.

First failed relation: requested customer 9999 -> fixture device account 77. No operational database row is involved in this failure. Neighboring tests explicitly require delivery to the requested target, not the currently logged-in customer. Reverting service targeting would risk cross-account notification disclosure and violate the closed Push contract.

Evidence: `push-baseline-unchanged.txt` proves both service and test have no diff against HEAD (EXIT_CODE=0); `push-baseline-test.txt` and `push-baseline.trx` reproduce the isolated failure (EXIT_CODE=1, Failed=1). The test directly constructs PushDeviceService, with no Local or Program dependency.

Owner approval received on 2026-10-26 within the same mission: only the conflicting test was corrected. It is now named DispatchForOfficialNotification_MissingTargetDevices_DoesNotUseCurrentUserDevices and asserts NoDevices, Success=false, SentCount=0, empty DeviceKeys, no sender calls, and lookup of account 9999 rather than 77. Neighboring positive target-isolation tests are unchanged. PushDeviceService and FirebasePushSender were verified unchanged against HEAD before the isolated run. Evidence: push-corrected.trx (1 passed, Failed=0, EXIT_CODE=0). No live Push send was needed or performed.

## Resolved Owner Startup Failure

Owner attempt at 2026-10-26 00:13:12 UTC stopped before HTTP readiness with GATEWAY_START_FAILED. The original wrapper suppressed the child's diagnostic. Safe IPC telemetry identified STORAGE_ACCESS / STARTUP_OPERATION_FAILED, not a port, token or Chromium-installation failure. The protected token and session booleans were readable/valid, the DACL allowed only the current identity and SYSTEM, and port 5094 had no listener. Historical startup evidence remains in startup-diagnostics.json; gateway-live-link.json now contains the successful owner run rather than the initial failure.

The first broken call was protectDirectory -> Windows PowerShell Set-Acl on an already protected directory. Adding a second protectDirectory call to the existing storage test reproduced SeSecurityPrivilege / PrivilegeNotHeldException (1 failed). Replacing Set-Acl with System.IO.Directory.SetAccessControl applies the modified access-control sections without requesting unrelated audit privileges. The same test then passed, preserving the token and session state. No elevation, ExecutionPolicy change, protection bypass or session-file deletion was used.

A related shutdown defect was proven in the installed libraries: whatsapp-web.js destroy checks browser.isConnected(), while Puppeteer 25 exposes browser.connected. The local WebDriver now supplies the compatibility method before destroy/logout; no dependency source was edited. The runtime test now closes actual sandboxed Chromium through WebDriver, asserts disconnected state, and repeats destroy. The old probe's browser was closed through its own DevTools endpoint only after checking PID 4648, parent gateway PID 39540, parent probe PID 41752, executable path and listening-port ownership. No unrelated process was stopped. Evidence: stale-probe-shutdown.json; the one-use fixed-PID cleanup branch was removed after completion.

Post-fix evidence on 2026-10-26: startup-attempt.json records actual readiness and child exit 0 at 00:27:46 UTC. gateway-live-prelink.json records fresh execution from 00:28:00 to 00:28:11 UTC: authenticated health, unauthenticated rejection, valid unexposed QR, disconnect, shutdownConfirmed=true, sendRequests=0, EXIT_CODE=0. Unlike the earlier probe, success is now written only after child exit. The interactive owner command also preserves allowlisted startup stage/code instead of suppressing every diagnostic. Actual owner linking/current number/recovery subsequently passed at 00:34:47 UTC.

## Verification Status

- Gateway build PASS, 14 tests PASS, Failed=0, EXIT_CODE=0, including actual sandboxed Chromium and Windows ACLs after the dependency upgrade. npm audit: 0 vulnerabilities, EXIT_CODE=0.
- Backend build PASS, EXIT_CODE=0.
- Local Adapter/Admin API tests: 15 passed, Failed=0, EXIT_CODE=0.
- Full selected regression after the authorized test correction: 218 tests, 218 passed, Failed=0, no skips, EXIT_CODE=0. Required MessageWriteService/dispatcher/Meta/InApp/Firebase tests are included, not silently omitted. Historical isolated failure evidence is retained separately.
- Actual owner Live Link, Current Number, Health, Restart and Session Recovery all PASS. The same number was recovered without a new QR. Evidence: gateway-live-link.json, captured at 2026-10-26T00:34:47.800Z, exitCode=0, operationalActivation=false, sendRequests=0, confirmed by the owner. The script writes this result after its final child shutdown. No live send was performed or required for the owner's foundation closure gates.
- The post-fix real pre-link probe passed Gateway Health, unauthenticated rejection, a valid unexposed QR, Disconnect and confirmed child exit with EXIT_CODE=0. It started only its own Gateway child and did not scan QR or send. Evidence: the timestamped `gateway-live-prelink.json`, not the earlier stale result.
- No Meta/Push redesign, no operational cutover, no provider-selection change, no Local operational messages. `audit-after.json` proves protectedFilesUnchanged=true and metaStoreUnchanged=true with EXIT_CODE=0; the only operational adapters remain InAppMessageChannel and WhatsAppCloudMessageChannel.
- Existing .NET dependency warnings: System.Security.Cryptography.Pkcs 9.0.4 advisories are pre-existing and outside the scoped gateway dependency fix.
- Read-only residue check found CustomerMessageID=39 on ValidationInApp-2198ba7995d74544, created 2026-10-14 23:30:27 UTC, predating this task. It was not changed or removed. Local message count and provider-setting row count remained zero after tests.

### Owner Live-Link Gate

The owner completed `node Gateway/LocalWhatsApp/scripts/live-link.mjs` in an interactive PowerShell terminal using the authorized test account. It started its own gateway process, displayed QR only in the owner's terminal, verified the actual connected number, gracefully stopped/restarted only its own child process, and verified recovery of the same number without a new QR. It disabled test sending in the child environment and never called a send endpoint. Only a masked number and boolean verification results were saved to `artifacts/local-whatsapp-foundation/gateway-live-link.json`. It stopped its own gateway on completion while retaining the session. The persisted evidence and owner confirmation, not script creation alone, satisfy this gate. Connected PASS describes the verified live run, not an assertion that the gateway remains running after the script exits.

### Final Gate Review

Closure review on 2026-10-26 re-read the existing build logs, TRX/JUnit results, and actual owner evidence. SHA-256 comparisons of all eleven protected production files and the Meta store still matched audit-before.json. Program.cs still registered only InAppMessageChannel and WhatsAppCloudMessageChannel in the operational dispatcher. The local adapter remains a concrete-only registration; its client has no Meta dependency or fallback call.

The timestamp check found only MobilePushDeviceServiceTests.cs newer than regression.trx among the reviewed Backend foundation/registration files and seven selected test classes. Its diff remained the owner-approved test-only correction. Therefore only this class was rebuilt and rerun: push-final.trx, 13 passed, Failed=0, no skips. The earlier 218-test regression remains the evidence for the unchanged slice. No production code was changed during closure; no database test rerun, gateway restart, migration, live message or operational activation was needed.

| Gate | Final Result | Evidence |
| --- | --- | --- |
| Gateway | EXIT_CODE=0 / Failed=0; 14 passed | gateway-build.txt, gateway-tests.txt, gateway-tests.xml |
| Backend | EXIT_CODE=0 / Failed=0; 15 foundation cases passed | backend-build.txt, local-whatsapp-backend.trx; foundation cases also included in regression.trx |
| Regression | EXIT_CODE=0 / Failed=0; 218 passed, plus 13 focused Push revalidated | backend-regression.txt, regression.trx, push-final.trx |
| Connected | PASS | gateway-live-link.json: connected=true |
| Current Number | PASS | gateway-live-link.json: currentNumber=true |
| Health | PASS | gateway-live-link.json: health=true before and after restart |
| Restart | PASS | gateway-live-link.json: restart=true |
| Session Recovery | PASS | gateway-live-link.json: sessionRecovery=true, newQrRequired=false |
| No Operational Cutover | PASS | operationalActivation=false; original operational registrations unchanged |
| No Automatic Fallback | PASS | protected dispatcher unchanged; local client has no alternate-provider path |
| No Meta Regression | PASS | selected Meta regression tests, unchanged production hashes and Meta store |
| No Push Regression | PASS | selected Firebase/Push regression, push-final.trx, unchanged production hashes |
| No Migration | PASS | no schema/migration operation in this mission; closure performed no database writes |

All artifact names in this table are under artifacts/local-whatsapp-foundation. No failed test was skipped or weakened to conceal a defect. The existing Pkcs 9.0.4 vulnerability warnings remain explicit technical debt outside this approval. Live message delivery, production service deployment, and long-duration session reliability are not claimed by this foundation verdict; Meta/Push PASS denotes the audited source/configuration and regression contracts, not a new live delivery test.

## Rollback and Closure

Rollback of foundation wiring is removal of the single AddLocalWhatsAppFoundation registration and stopping only the proven Gateway process. No database rollback is needed. Retain the protected profile until the owner explicitly decides whether to logout/unlink; never delete session files as generic cleanup. Meta, Push and the existing dispatcher remain unchanged throughout.

All owner-specified foundation closure gates are now evidenced. Final verdict: **A - LOCAL WHATSAPP CHANNEL FOUNDATION VERIFIED**. Mission 1/3 is closed; mission 2/3 was not started. This verdict does not authorize operational messaging or provider cutover.

كتب هذا التقرير المطور سعد
