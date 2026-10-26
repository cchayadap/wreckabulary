# Independent review: borrowed-input fixture revision 2

STATUS: COMPLETE — no blocking source finding in test-only patch `1feb731b6968d5ce0a2a26a7a4b891aeb4d6fa6a6c8bf03741ddd4ed8758252f`. Clear for native rerun. No live edits or native jobs by reviewer.

Independently verified the single exact baseline/candidate hashes, strict apply against the owned baseline, normalized live/candidate equality after root integration, and both preserved205241 failure XML/log hashes. Candidate SHA `8f4bb4b64a658ab7f7f775e22049ad0d558dbbbbcbafe6d857fe5162b9b7d10a`. See `independent-integrity.json`.

The controller-label correction tests the actual contract: short label when supplied, display label next, then a stable Latin fallback. It no longer expects a null `startButton.shortDisplayName` to be the visible menu hint. Mappings remain checked per action; production code is unchanged.

The lifecycle correction invokes the production PlayerView.OnDisable and OnDestroy handlers, never adapter.Dispose directly. It verifies ownedBinding is cleared, invokes the destroy handler a second time for idempotence, and guarantees owned object destruction in nested finally. Each affected fixture supplies its own Camera, so the helper does not introduce an EditMode Destroy call for an owned player camera. Exact original-source/adapter ownership, post-handler empty adapter hints, original map/device usability, unchanged overrides/KeyBindings version/Session bindings and input/menu/release assertions remain. Relevant test names explicitly say DestroyHandler.

This is bounded production-handler coverage. The initial48/51 failure only showed that an immediate EditMode destruction did not produce the expected disposal state; it did not measure callback invocation history. Neither the revised handler fixture nor previous entry/return passes alone proves actual Unity shutdown dispatch for the adapter. Root's separate Windows Solo QA now retains the real wrapper through RequestReturn→Hub and observes its private disposed state, empty hints and preserved caller map. That actual-player gate must pass before claiming player-lifecycle acceptance.

No original deadline, input lease policy, runtime ownership behavior, source assertion or existing entry/return test is removed. Native rerun and fresh actual Windows screenshots/input behavior remain pending at review time.
