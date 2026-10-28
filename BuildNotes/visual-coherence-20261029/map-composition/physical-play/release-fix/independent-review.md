# Independent carried-item release review

STATUS: COMPLETE — NO BLOCKING SOURCE FINDING. Native execution remains root-owned and pending this review.

Reviewed exact manifest SHA256 `0a36258bf533f7a32160c264e6cc6c3f1be16088121010aea4cb6ad6169dd0fc`:

- Physics world candidate `9bee17321fbe558487e17faf38378bb817b25f1cba22b540b6782fc74dacdc48`.
- New component lifecycle test `2c89865222fdd661b4c8928ebaed2f912e9a43882a3a75f01add61757d043bca`.
- Both candidate hashes and the existing baseline hash matched the manifest. The live world still matched its baseline and the new test was absent at review time. The supplied semantic compile receipt reports zero errors in both physics assemblies; I did not rerun the compiler or Unity.

## Cause and retained boundaries

`TestResults/editmode_20261028T201013Z.xml:77` records BlockedRelease on the real carried TABLE drop. The item is inactive/kinematic, inventory is Carried by seat 0, actor pose and collider centers/scales are valid, boxAttached is false, capsuleAttached is true, and reported endpoints/sweeps show no external blocker. `WordboundNetworkBody.CacheComponents` validates attached ownership at initial binding; `ApplyVisibility(false)` then disables owned colliders and Rigidbody.detectCollisions. The unconditional attachment equality in the original TryRelease therefore rejects that recorded inactive state before clearance traversal.

The replacement at candidate `WordboundPhysicsWorld.cs:336–339` also requires the exact world's collider-to-item registration and original OwnedColliders membership. Register populates both after BindHost/CacheComponents and Unregister removes them. An unregistered or replaced root collider cannot borrow the fallback.

`ReleaseBodyMatches` at lines 365–371 requires the same GameObject and that object's exact Rigidbody component. A child collider, nested independent Rigidbody, unrelated body, destroyed reference or nonnull foreign attachedRigidbody cannot pass. Its null-attachment exception additionally requires a disabled collider, native kinematic body and collision detection disabled. Existing earlier canonical inactive/kinematic and authenticated inventory Held/Carried checks still apply. The actor capsule remains subject to its original strict attachment check.

The diff does not modify candidate positions, authority/movement checks, holder validation, collider dimensions/center/scale guards, start clearance, endpoint clearance, query layers, or the BoxCast sweep. The helper accepts normal attached ownership as before and does not manipulate any body or collider.

## Regression and limitations

The new Unity test directly enters Play Mode, creates only transient objects, reproduces active-to-disabled root attachment behavior, rejects unrelated and descendant/nested ownership, checks restricted fallback states, then restores normal active attachment. Its teardown destroys only its own objects in the current mode and exits Play Mode even when a body assertion fails. The source-level test of the private predicate does not independently exercise the registered-collider map or an authoritative Drop/Throw; those boundaries were inspected in the production caller.

The exact installed Unity version must run this lifecycle test because null attachedRigidbody behavior is the failure's engine-dependent premise. Root should then rerun the unchanged real Freestyle authority test through Drop, wrong-item rejection and all six stations, plus the existing interaction/release policy coverage. A successful static review does not establish that full progression, cleanup, Windows rendering or broader multiplayer behavior passes.

No production source, native assets, metadata, tests, or old evidence were changed by this review.
