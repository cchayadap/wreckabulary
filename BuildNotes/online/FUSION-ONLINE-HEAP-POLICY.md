# Shared online Fusion heap policy

STATUS: IMPLEMENTED, COMPILE/NATIVE NOT RUN. Root approved the bounded capacity change after `FUSION-FFA-ALLOCATOR-REVIEW.md` (acb1aa8d). Independent source review is pending. Baseline-only fee597fc aligns MatchClient with root camera/visual-profile support and must not be integrated.

The new project-owned `WordboundFusionConfiguration.Create()` retains the former production/native online settings and prefab registration flow, while selecting 2,048 pages of 32 KiB: 64 MiB per heap. Production MatchClient and native OnlineGameplay both use it. Each session retains that configuration for replacement/recovery runners. Offline/local factories, the global Photon asset and all SDK files remain unchanged. The native live identity check also reads back the running peers' heap policy.

The installed SDK creates two equal heaps, so the cost is approximately 128 MiB per Runner (up 112 MiB from default), approximately 1 GiB for eight Runners in one process before other Unity/managed/graphics memory. This is a capacity correction, not a performance or memory-reduction claim. The rationale and exact allocation quantization are retained in the preceding report.

Two focused tests were added to existing `Wreckabulary.PhotonTests.WordboundFusionWeavingTests`:

- `OnlineHeapPolicyOwnsItsConfigurationAndPreservesRegistrations`: checks actual factory configuration, independent ownership, unchanged global/default heap, registered prefab sources and existing tick/reliable/migration/render settings.
- `BakedBodyAndElectionStateFitTheAdmittedHistoryEnvelope`: reads the actual three baked body prefabs and native woven word counts, retains interpolation, checks the measured 315-word body bound and 6,144-byte carrier bucket, and requires the 512-body / 60-history + 6-auxiliary planning envelope to leave at least 25% of the object heap for SDK state and fragmentation.

These are native Editor tests using the existing Photon test asmdef's Fusion/Rules/Editor references. No recompile-test invocation is needed to select these two methods. The existing explicit compilation coroutine was not changed. The source check does not establish runtime maximum-body occupancy, despawn reclamation or a heap plateau.

Root follow-up gates, in order:

1. R0 plus those two exact native tests (and existing `ConfiguredCarrierHasNativeGeneratedNetworkState` as useful). Preserve root's actual gate runner/cold timeout conventions; no jobs were started here.
2. The unchanged full public FFA method `Wreckabulary.OnlineGameplayTests.WordboundNativeGameplayTests.TwoAuthenticatedPublicFfaIndividualsReachScoredWinAndConfirmDurableResult`, with existing external/full-match flags, memory samples and deadlines. Require all scored-finish assertions, exact durable ACK, no unexpected OOM and cleanup. Read the full time-series, not merely the first nonfailure sample.
3. Separately exercise maximum admitted bodies, history population and despawn/recreate reclamation, followed by outstanding 4/8-peer and recovery acceptance. Until then 64 MiB is an approved candidate backed by a source envelope, not verified worst-case production capacity.

Changes are limited to the new factory, two existing callsites, one live configuration assertion, two native tests and this report. No SDK/kit/protocol/rules/timeouts/service change or hand-authored metadata. Original ModeDefinition/log work remains untouched.
