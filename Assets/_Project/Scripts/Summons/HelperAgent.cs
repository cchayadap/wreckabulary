using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class HelperAgent : MonoBehaviour, IDamageable
    {
        public const float ThinkSeconds = .25f;
        public const float PlaceSeconds = .3f;
        public const float HeelDistance = 1.2f;
        public const float BlockedMetres = .12f;
        public const float SideStepSeconds = .5f;
        public const float FallLimit = 2f;
        public const float EndSeconds = .3f;
        public const float LifeGrace = 1f;
        public const float TurnDegreesPerSecond = 360f;
        public const float StrikeStun = .25f;
        public const float EyeHeight = .5f, ChestHeight = .9f;
        public const float Mass = 2f;
        public const float AimLineSeconds = 1f;
        public const float MoverRing = .45f;
        const int SightBuffer = 8, CandidateCapacity = 16, SlowSlots = 4;

        static readonly List<HelperAgent> Active = new();
        static readonly List<HelperGuard> Guards = new();
        static readonly List<HelperFootprint> Blockers = new();
        static readonly List<Vector3> KeySpots = new();
        static readonly List<HelperCandidate> AimCandidates = new(CandidateCapacity);
        static int serials;

        public static event Action<HelperAgent> Launched;
        public static event Action<HelperAgent> Armed;
        public static event Action<HelperAgent> WindingUp;
        public static event Action<HelperAgent, PlayerController> Struck;
        public static event Action<HelperAgent, HelperEnd> Ended;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            Guards.Clear();
            Blockers.Clear();
            AimCandidates.Clear();
            serials = 0;
            Launched = null;
            Armed = null;
            WindingUp = null;
            Struck = null;
            Ended = null;
        }

        public static IReadOnlyList<HelperAgent> All => Active;
        public static int InPlay => Active.Count;

        HelperSpec spec;
        ItemDefinition item;
        PlayerController maker;
        int makerId = -1, makerTeam = Teams.NoTeam;
        HelperState state;
        HelperOrder order;
        Rigidbody body;
        SummonedThing life;
        HelperMotion motion;
        HelperRoute route;
        HouseLayout layout;
        Vector3 home, desired, lastThinkAt, sideStep;
        float nextThink, sideStepUntil, endingSince = -1f, launchedAt, aimLineUntil;
        bool targetInSight, blocked, lost, armedRaised, endRaised, sideLeft;
        int serial;
        LineRenderer aimLine;
        readonly List<HelperCandidate> candidates = new(CandidateCapacity);
        readonly RaycastHit[] sight = new RaycastHit[SightBuffer];
        readonly int[] slowedIds = new int[SlowSlots];
        readonly float[] slowedAt = new float[SlowSlots];

        public HelperSpec Spec => spec;
        public ItemDefinition Item => item;
        public string Word => item != null ? item.Id : spec?.Id;
        public PlayerController Maker => maker;
        public int MakerId => makerId;
        public int MakerTeam => makerTeam;
        public HelperPhase Phase => state.Phase;
        public HelperOrder LastOrder => order;
        public int TargetId => state.Target;
        public int LockedId => state.LockedId;
        public int StrikesLeft => state.StrikesLeft;
        public float LifeLeft => Mathf.Max(0f, state.Life);
        public float HealthLeft => Mathf.Max(0f, state.Health);
        public HelperEnd EndReason => state.Ended;
        public bool IsArmed => state.Phase != HelperPhase.Arming && state.Phase != HelperPhase.Done;
        public bool IsWindingUp => state.Phase == HelperPhase.WindUp;
        public bool IsEnding => endingSince >= 0f || state.Phase == HelperPhase.Done;
        public bool Moves => spec != null && spec.Moves;
        public Vector3 Home => home;
        public float LaunchedAt => launchedAt;

        public bool WouldBite(PlayerController player) =>
            spec != null && player && HelperTargeting.Bites(spec, makerId, makerTeam, player.Index, player.Team);

        public static bool SpecFor(ItemDefinition item, out HelperSpec spec)
        {
            spec = null;
            return item != null && item.Family == HandlingFamily.Helper && GameConfig.Current.Helpers != null && GameConfig.Current.Helpers.TryGet(item.Id, out spec);
        }

        public static bool Offered(ItemDefinition item, string modeId)
        {
            if (item == null || item.Family != HandlingFamily.Helper) return true;
            return SpecFor(item, out var spec) && HelperBook.AllowedIn(modeId, spec);
        }

        public static int MoversOf(PlayerController owner)
        {
            int n = 0;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] && !Active[i].IsEnding && Active[i].maker == owner && Active[i].Moves) n++;
            return n;
        }

        static bool RoundLive()
        {
            var rounds = RoundManager.Instance;
            return !rounds || rounds.Phase == global::Wreckabulary.Phase.Playing;
        }

        static bool RoundOver()
        {
            var rounds = RoundManager.Instance;
            return rounds && (rounds.Phase == global::Wreckabulary.Phase.RoundOver || rounds.Phase == global::Wreckabulary.Phase.MatchOver);
        }

        public static bool RefusesRelease(PlayerController maker, ItemDefinition item, HelperSpec spec)
        {
            string mode = Match.Mode;
            string why = null;
            if (!HelperBook.AllowedIn(mode, spec)) why = "NOT IN THIS MODE";
            else if (!HelperBook.LiveNow(mode, RoundLive())) why = "AFTER THE START";
            else if (DeployedGear.CountFor(maker) >= maker.Health.Rules.MaxDeployed) why = $"{maker.Health.Rules.MaxDeployed} ALREADY PLACED";
            else if (!HelperCaps.Admits(MoversOf(maker), spec.Moves)) why = $"{HelperCaps.MaxMoversPerPlayer} ALREADY OUT";
            else
            {
                var refusal = PlaceRefusal(maker, item, spec.Moves ? HelperPlaceKind.Mover : HelperPlaceKind.Stayer, item.Size[0], item.Size[2]);
                if (refusal != HelperRefusal.None) why = HelperPlacement.Message(refusal);
            }
            if (why == null) return false;
            Popup.Show(why, maker.OverheadPosition + Vector3.up * 0.4f, Color.white, 2.5f);
            return true;
        }

        public static bool RefusesCover(PlayerController maker, ItemDefinition item)
        {
            if (!HelperPlacement.Guards(item)) return false;
            var refusal = PlaceRefusal(maker, item, HelperPlaceKind.Wall, item.Deploy.FootprintX, item.Deploy.FootprintZ);
            if (refusal == HelperRefusal.None) return false;
            Popup.Show(HelperPlacement.Message(refusal), maker.OverheadPosition + Vector3.up * 0.4f, Color.white, 2.5f);
            return true;
        }

        static HelperRefusal PlaceRefusal(PlayerController maker, ItemDefinition item, HelperPlaceKind kind, float sizeX, float sizeZ)
        {
            var at = maker.transform.position + maker.Facing * PlayerCombat.DeployReach;
            float yaw = Mathf.Atan2(maker.Facing.x, maker.Facing.z) * Mathf.Rad2Deg;
            var footprint = HelperFootprint.Around(at.x, at.z, sizeX, sizeZ, yaw);
            Guards.Clear();
            Blockers.Clear();
            foreach (var p in World.Players)
                if (p && p.IsDowned) Guards.Add(new HelperGuard(p.transform.position.x, p.transform.position.z, HelperPlacement.DownedClear, HelperRefusal.Downed));
            string mode = Match.Mode;
            if (mode == HouseModes.Escape)
            {
                var escape = FindAnyObjectByType<EscapeDirector>();
                if (escape)
                {
                    Guards.Add(new HelperGuard(escape.ExtractionPoint.x, escape.ExtractionPoint.z, HelperPlacement.ObjectiveClear, HelperRefusal.Exit));
                    KeySpots.Clear();
                    escape.KeySpots(KeySpots);
                    foreach (var spot in KeySpots) Guards.Add(new HelperGuard(spot.x, spot.z, HelperPlacement.ObjectiveClear, HelperRefusal.Key));
                }
            }
            if (mode == HouseModes.MovingOut)
            {
                var van = FindAnyObjectByType<MovingOutDirector>();
                if (van) Guards.Add(new HelperGuard(van.ExtractionPoint.x, van.ExtractionPoint.z, HelperPlacement.ObjectiveClear, HelperRefusal.Van));
            }
            if (kind == HelperPlaceKind.Wall)
                foreach (var placed in DeployedGear.All)
                {
                    var cover = placed ? placed.Definition : null;
                    if (cover?.Deploy == null || cover.Deploy.Effect != DeployEffect.Cover) continue;
                    var p = placed.transform.position;
                    Blockers.Add(HelperFootprint.Around(p.x, p.z, cover.Deploy.FootprintX, cover.Deploy.FootprintZ, placed.transform.eulerAngles.y));
                }
            var builder = FindAnyObjectByType<RoomBuilder>();
            return HelperPlacement.Check(builder ? builder.Layout : null, kind, at.x, maker.transform.position.y, at.z, footprint, Guards, Blockers);
        }

        public static int AimedLockFor(PlayerController maker, HelperSpec spec)
        {
            if (spec.Pick != HelperPick.Aimed) return -1;
            int pointer = -1;
            if (InteractionAim.TryRay(maker, out var ray) && InteractionAim.Trace(maker, ray, out var hit) && hit.rigidbody
                && hit.rigidbody.TryGetComponent(out PlayerController aimed)) pointer = aimed.Index;
            var from = maker.transform.position;
            AimCandidates.Clear();
            foreach (var p in World.Players)
            {
                if (!p || p == maker || !p.isActiveAndEnabled) continue;
                var at = p.transform.position;
                AimCandidates.Add(new HelperCandidate(p.Index, p.Team, at.x, at.y, at.z, Hittable(p, 0), true));
            }
            return HelperTargeting.AimedLock(spec, maker.Index, maker.Team, from.x, from.y, from.z, maker.Facing.x, maker.Facing.z, pointer, AimCandidates);
        }

        public static HelperAgent Launch(GameObject root, ItemDefinition item, HelperSpec spec, PlayerController maker, int lockedId)
        {
            if (!root || spec == null) return null;
            RetireOldestWhileFull();
            if (root.TryGetComponent(out HeldWeapon gear))
            {
                gear.MarkSpent();
                Destroy(gear);
            }
            var agent = root.AddComponent<HelperAgent>();
            agent.Begin(item, spec, maker, lockedId);
            return agent;
        }

        static void RetireOldestWhileFull()
        {
            while (HelperCaps.RetiresOldest(Active.Count))
            {
                HelperAgent oldest = null;
                for (int i = 0; i < Active.Count; i++)
                    if (Active[i] && !Active[i].IsEnding && (!oldest || Active[i].launchedAt < oldest.launchedAt)) oldest = Active[i];
                if (!oldest) return;
                oldest.Stop(HelperEnd.Life);
                Active.Remove(oldest);
            }
        }

        void Begin(ItemDefinition definition, HelperSpec helper, PlayerController owner, int lockedId)
        {
            item = definition;
            spec = helper;
            maker = owner;
            makerId = owner ? owner.Index : -1;
            makerTeam = owner ? owner.Team : Teams.NoTeam;
            serial = serials++;
            launchedAt = Time.time;
            nextThink = Time.time + ThinkSeconds * (serial % 4) / 4f;
            state = HelperBrain.Start(spec, lockedId);
            home = transform.position;
            lastThinkAt = home;
            for (int i = 0; i < SlowSlots; i++) { slowedIds[i] = -1; slowedAt[i] = float.NegativeInfinity; }
            transform.SetParent(World.Transient, true);
            body = GetComponent<Rigidbody>();
            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = !spec.Moves;
                body.mass = Mass;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                body.rotation = transform.rotation;
            }
            var builder = FindAnyObjectByType<RoomBuilder>();
            layout = builder ? builder.Layout : null;
            route = spec.Moves ? new HelperRoute(layout) : null;
            life = SummonedThing.Attach(gameObject, Word, owner, spec.Seconds + LifeGrace);
            life.ReturnsLetters = false;
            life.EndedWithReason = OnLifeEnded;
            DeployedGear.Register(gameObject, owner);
            if (spec.IsTrap) gameObject.AddComponent<HelperTrap>().Configure(spec);
            motion = gameObject.AddComponent<HelperMotion>();
            motion.Configure(this, RingColor(), spec.Moves ? MoverRing : spec.Radius, spec.Bites == HelperBite.Enemies && maker);
            if (owner) owner.Health.Damaged += MakerHurt;
            if (state.LockedId >= 0) DrawAimLine();
            Active.Add(this);
            Launched?.Invoke(this);
        }

        Color RingColor() => spec.Bites == HelperBite.Anyone ? new Color(.95f, .2f, .2f) : maker ? maker.Color : Color.white;

        void DrawAimLine()
        {
            var holder = new GameObject("Helper aim line");
            holder.transform.SetParent(transform, false);
            aimLine = SummonEffects.Line(holder.transform, RingColor(), new Vector3[2]);
            aimLine.useWorldSpace = true;
            aimLineUntil = Time.time + AimLineSeconds;
        }

        void OnDestroy()
        {
            Active.Remove(this);
            if (maker) maker.Health.Damaged -= MakerHurt;
            RaiseEnded(state.Ended == HelperEnd.None ? HelperEnd.RoundOver : state.Ended);
        }

        void OnLifeEnded(SummonEndReason reason)
        {
            HelperBrain.Stop(ref state, reason switch
            {
                SummonEndReason.OwnerGone => HelperEnd.MakerOut,
                SummonEndReason.Expired => HelperEnd.Life,
                SummonEndReason.Reset => HelperEnd.RoundOver,
                SummonEndReason.Consumed => state.Ended == HelperEnd.None ? HelperEnd.Strikes : state.Ended,
                _ => HelperEnd.Lost,
            });
            Active.Remove(this);
            RaiseEnded(state.Ended);
        }

        void RaiseEnded(HelperEnd end)
        {
            if (endRaised) return;
            endRaised = true;
            Ended?.Invoke(this, end);
        }

        void MakerHurt(PlayerHealth victim, HitInfo hit, HitResult result)
        {
            if (!result.Landed) return;
            HelperBrain.Provoke(ref state, spec, makerId, makerTeam, hit.AttackerId, hit.AttackerTeam);
        }

        public bool ApplyDamage(in HitInfo hit)
        {
            if (IsEnding || !(hit.Damage > 0f)) return false;
            HelperBrain.Provoke(ref state, spec, makerId, makerTeam, hit.AttackerId, hit.AttackerTeam);
            if (!HelperBrain.Hurt(ref state, hit.Damage)) return false;
            Popup.Damage(hit.Damage, transform.position + Vector3.up * 1f);
            if (motion) motion.Hurt();
            if (state.Health <= 0f) Stop(HelperEnd.Smashed);
            return true;
        }

        public void Stop(HelperEnd end)
        {
            if (IsEnding) return;
            HelperBrain.Stop(ref state, end);
            BeginEnding();
        }

        void BeginEnding()
        {
            if (endingSince >= 0f) return;
            endingSince = Time.time;
            desired = Vector3.zero;
            Active.Remove(this);
            if (aimLine) aimLine.enabled = false;
            if (motion) motion.End();
            RaiseEnded(state.Ended);
        }

        void Update()
        {
            if (spec == null || Time.timeScale <= 0f) return;
            if (endingSince >= 0f)
            {
                if (Time.time - endingSince < EndSeconds) return;
                GameFeedback.Burst("Foam_Cloud", transform.position + Vector3.up * .3f, .45f, RingColor(), .35f);
                if (life) life.Finish(EndFor(state.Ended));
                else Destroy(gameObject);
                endingSince = float.PositiveInfinity;
                return;
            }
            if (Time.time >= nextThink) Think();
            var picked = PickedTarget >= 0 ? World.PlayerById(PickedTarget) : null;
            float distance = picked ? Flat(picked.transform.position - transform.position).magnitude : float.PositiveInfinity;
            var sense = new HelperSense(distance, FromHome(), picked && targetInSight, !maker || maker.IsKnockedOut, RoundOver(), blocked, lost);
            var before = state.Phase;
            order = HelperBrain.Step(ref state, spec, sense, PickedTarget, Time.deltaTime);
            Raise(before);
            Obey(order, state.Target == PickedTarget ? picked : state.Target >= 0 ? World.PlayerById(state.Target) : null);
            UpdateAimLine();
        }

        int PickedTarget { get; set; } = -1;

        void Raise(HelperPhase before)
        {
            if (!armedRaised && before == HelperPhase.Arming && state.Phase != HelperPhase.Arming && state.Phase != HelperPhase.Done)
            {
                armedRaised = true;
                if (spec.Bites == HelperBite.Anyone) Popup.Show("!", transform.position + Vector3.up * 1.1f, RingColor(), 3f);
                Armed?.Invoke(this);
            }
            if (state.Phase == HelperPhase.WindUp && before != HelperPhase.WindUp) WindingUp?.Invoke(this);
        }

        void Think()
        {
            float now = Time.time;
            nextThink = now + ThinkSeconds;
            var here = transform.position;
            blocked = spec.Moves && (order == HelperOrder.Approach || order == HelperOrder.ReturnHome)
                && Flat(here - lastThinkAt).sqrMagnitude < BlockedMetres * BlockedMetres;
            lastThinkAt = here;
            lost = here.y < home.y - FallLimit || (layout != null && HelperPlacement.RoomAt(layout, here.x, here.y, here.z) == null);
            Gather(here);
            PickedTarget = HelperTargeting.Choose(spec, ref state, makerId, makerTeam, here.x, here.y, here.z, candidates);
            targetInSight = false;
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Id == PickedTarget) { targetInSight = candidates[i].InSight; break; }
        }

        void Gather(Vector3 here)
        {
            candidates.Clear();
            float reach = spec.Reach + HelperTargeting.StrikeSlack;
            var players = World.Players;
            for (int i = 0; i < players.Count && candidates.Count < CandidateCapacity; i++)
            {
                var p = players[i];
                if (!p || !p.isActiveAndEnabled) continue;
                var at = p.transform.position;
                bool hittable = Hittable(p, Holders(p.Index));
                bool near = Flat(at - here).sqrMagnitude <= reach * reach && Mathf.Abs(at.y - here.y) <= HelperTargeting.SameLevel;
                bool inSight = hittable && near && ClearPath(here + Vector3.up * EyeHeight, p);
                candidates.Add(new HelperCandidate(p.Index, p.Team, at.x, at.y, at.z, hittable, inSight));
            }
        }

        int Holders(int playerId)
        {
            int n = 0;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] && Active[i] != this && Active[i].state.Target == playerId) n++;
            return n;
        }

        static bool Hittable(PlayerController p, int holders)
        {
            var health = p.Health;
            return health && HelperTargeting.Hittable(new HelperVictim(health.State, health.IsInvulnerable, p.IsDodging, p.IsHeld, p.Frozen,
                p.Combat && p.Combat.IsReviving, holders));
        }

        bool ClearPath(Vector3 origin, PlayerController target)
        {
            var to = target.transform.position + Vector3.up * ChestHeight - origin;
            float distance = to.magnitude;
            if (distance < .01f) return true;
            int count = Physics.RaycastNonAlloc(origin, to / distance, sight, distance, World.GroundMask, QueryTriggerInteraction.Ignore);
            if (count >= sight.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hitBody = sight[i].rigidbody;
                if (hitBody == body || hitBody == target.Body) continue;
                if (!hitBody || hitBody.isKinematic) return false;
            }
            return true;
        }

        float FromHome()
        {
            if (spec.HomeIsMaker && maker) return Mathf.Max(0f, Flat(maker.transform.position - transform.position).magnitude - HeelDistance);
            return Flat(home - transform.position).magnitude;
        }

        Vector3 HomePoint => spec.HomeIsMaker && maker ? maker.transform.position : home;

        void Obey(HelperOrder now, PlayerController target)
        {
            switch (now)
            {
                case HelperOrder.Approach:
                    if (target) Steer(target.transform.position);
                    break;
                case HelperOrder.ReturnHome:
                    Steer(HomePoint);
                    break;
                case HelperOrder.SideStep:
                    sideLeft = !sideLeft;
                    var facing = transform.forward;
                    sideStep = new Vector3(sideLeft ? -facing.z : facing.z, 0f, sideLeft ? facing.x : -facing.x) * spec.Speed;
                    sideStepUntil = Time.time + SideStepSeconds;
                    break;
                case HelperOrder.FaceOnly:
                    desired = Vector3.zero;
                    if (target) Face(target.transform.position - transform.position);
                    break;
                case HelperOrder.Release:
                    desired = Vector3.zero;
                    Strike(target);
                    if (state.Phase == HelperPhase.Done) BeginEnding();
                    break;
                case HelperOrder.Vanish:
                    BeginEnding();
                    break;
                default:
                    desired = Vector3.zero;
                    break;
            }
            if (Time.time < sideStepUntil) desired = sideStep;
        }

        void Steer(Vector3 goal)
        {
            var here = transform.position;
            if (route == null || !route.Next(here.x, here.y, here.z, goal.x, goal.y, goal.z, out float x, out float z)) { desired = Vector3.zero; return; }
            var to = new Vector3(x - here.x, 0f, z - here.z);
            if (to.sqrMagnitude < .0004f) { desired = Vector3.zero; return; }
            desired = to.normalized * spec.Speed;
            Face(to);
        }

        void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .0001f) return;
            var turned = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), TurnDegreesPerSecond * Time.deltaTime);
            if (body && !body.isKinematic) body.MoveRotation(turned);
            else transform.rotation = turned;
        }

        void FixedUpdate()
        {
            if (!body || body.isKinematic || spec == null) return;
            var v = body.linearVelocity;
            body.linearVelocity = new Vector3(desired.x, v.y, desired.z);
        }

        void Strike(PlayerController target)
        {
            if (motion) motion.Strike();
            switch (spec.Attack)
            {
                case HelperAttack.Shot:
                    if (target) HelperShot.Fire(this, transform.position + Vector3.up * EyeHeight + transform.forward * .3f, target.transform.position + Vector3.up * ChestHeight);
                    break;
                case HelperAttack.Burst:
                    Burst();
                    break;
                default:
                    if (target && HelperTargeting.StrikeLands(spec, Flat(target.transform.position - transform.position).magnitude, LegalNow(target)))
                        Hit(target, HitSource.Melee);
                    break;
            }
        }

        void Burst()
        {
            var here = transform.position;
            int hits = 0;
            var players = World.Players;
            for (int i = 0; i < players.Count && hits < HelperSpec.BurstTargets; i++)
            {
                var p = players[i];
                if (!p || !LegalNow(p)) continue;
                var at = p.transform.position;
                if (Flat(at - here).magnitude > spec.Range || Mathf.Abs(at.y - here.y) > HelperTargeting.SameLevel) continue;
                if (!ClearPath(here + Vector3.up * EyeHeight, p)) continue;
                Hit(p, HitSource.Explosion);
                hits++;
            }
            GameFeedback.Burst("Impact_Star", here + Vector3.up * .5f, Mathf.Min(2f, spec.Range), RingColor(), .4f);
        }

        public bool LegalNow(PlayerController p) => spec != null && Legal(spec, makerId, makerTeam, p);

        public static bool Legal(HelperSpec spec, int makerId, int makerTeam, PlayerController p) =>
            p && p.isActiveAndEnabled && HelperTargeting.Bites(spec, makerId, makerTeam, p.Index, p.Team) && Hittable(p, 0);

        bool Hit(PlayerController victim, HitSource source) => Hit(this, spec, maker, victim, transform.position, source);

        public static bool Hit(HelperAgent agent, HelperSpec spec, PlayerController maker, PlayerController victim, Vector3 from, HitSource source)
        {
            var hit = Hits.Of(spec.Bites == HelperBite.Anyone ? null : maker, victim.transform.position - from, source,
                spec.Damage, spec.Knockback, StrikeStun, 0f, null);
            if (!victim.Health.ApplyDamage(hit)) return false;
            if (spec.Slows && (!agent || agent.SlowReady(victim.Index))) victim.Slow(spec.Slow, spec.SlowSeconds);
            if (!ReferenceEquals(agent, null)) Struck?.Invoke(agent, victim);
            return true;
        }

        bool SlowReady(int id)
        {
            int slot = -1, oldest = 0;
            for (int i = 0; i < SlowSlots; i++)
            {
                if (slowedIds[i] == id) { slot = i; break; }
                if (slowedAt[i] < slowedAt[oldest]) oldest = i;
            }
            if (slot >= 0 && !HelperCaps.SlowReady(spec, Time.time - slowedAt[slot])) return false;
            if (slot < 0) slot = oldest;
            slowedIds[slot] = id;
            slowedAt[slot] = Time.time;
            return true;
        }

        void UpdateAimLine()
        {
            if (!aimLine || !aimLine.enabled) return;
            var locked = state.LockedId >= 0 ? World.PlayerById(state.LockedId) : null;
            if (Time.time >= aimLineUntil || !locked) { aimLine.enabled = false; return; }
            aimLine.SetPosition(0, transform.position + Vector3.up * EyeHeight);
            aimLine.SetPosition(1, locked.transform.position + Vector3.up * ChestHeight);
        }

        static SummonEndReason EndFor(HelperEnd end) => end switch
        {
            HelperEnd.MakerOut => SummonEndReason.OwnerGone,
            HelperEnd.Life => SummonEndReason.Expired,
            HelperEnd.RoundOver => SummonEndReason.Reset,
            HelperEnd.Stuck or HelperEnd.Lost => SummonEndReason.ConditionEnded,
            _ => SummonEndReason.Consumed,
        };

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
