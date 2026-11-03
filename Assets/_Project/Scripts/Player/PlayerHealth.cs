using System;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [RequireComponent(typeof(LetterInventory))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        static readonly ShieldStats TimedShield = new() { FrontArcDegrees = 150f, DamageReduction = 1f, MoveSpeedMultiplier = 1f };
        const float BleedTick = .5f;

        LetterInventory inventory;
        PlayerController controller;
        HealthModel model;
        GameRules rules, rulesOverride;
        HealthModel bubbleOwnerModel;
        int protectionGeneration, bubbleOwner, timedShieldOwner, bleedStacks;
        float frontBlockUntil, bleedUntil, nextBleed;
        HitInfo bleedHit;
        readonly System.Collections.Generic.Dictionary<(int, string), int> blows = new();

        public static Func<float> TraitRoll { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => TraitRoll = null;

        static double Now => Time.timeAsDouble;

        public HealthModel Model => model ??= Build();
        public GameRules Rules { get { if (model == null) Build(); return rules; } }

        public float Current => Model.Current;
        public float Max => Model.Max;
        public float Stamina => Model.Stamina;
        public float StaminaFraction => Model.MaxStamina > 0 ? Model.Stamina / Model.MaxStamina : 0f;
        public float Fraction => Max > 0f ? Current / Max : 0f;
        public LifeState State => Model.State;
        public bool IsAlive => Model.IsAlive;
        public bool IsDowned => Model.State == LifeState.Downed;
        public bool IsEliminated => Model.State == LifeState.Eliminated;
        public bool IsInvulnerable => Now < Model.InvulnerableUntil;
        public float Bubble => Now < Model.BubbleUntil ? Model.Bubble : 0f;
        public float BubbleLeft => Bubble > 0f ? (float)(Model.BubbleUntil - Now) : 0f;
        public float BleedOutLeft => IsDowned ? (float)Math.Max(0.0, Model.BleedOutAt - Now) : 0f;
        public float BleedingLeft => IsAlive && Time.time < bleedUntil ? bleedUntil - Time.time : 0f;
        public int BleedStacks => BleedingLeft > 0f ? bleedStacks : 0;

        public float FrontBlockUntil
        {
            get => frontBlockUntil;
            set
            {
                frontBlockUntil = value;
                timedShieldOwner = 0;
            }
        }
        public ShieldStats RaisedShield { get; set; }

        public event Action<PlayerHealth, HitInfo, HitResult> Damaged;
        public event Action<PlayerHealth, PlayerController> Hit;
        public event Action<PlayerHealth> Downed;
        public event Action<PlayerHealth> Eliminated;
        public event Action<PlayerHealth> Revived;
        public event Action<PlayerHealth> KnockedOut;
        public event Action<PlayerHealth> StateChanged;

        void Awake()
        {
            inventory = GetComponent<LetterInventory>();
            controller = GetComponent<PlayerController>();
        }

        HealthModel Build()
        {
            bubbleOwner = timedShieldOwner = 0;
            bubbleOwnerModel = null;
            frontBlockUntil = 0f;
            RaisedShield = null;
            rules = rulesOverride ?? Match.Rules;
            int id = controller ? controller.Index : -1;
            int team = controller ? controller.Team : Teams.NoTeam;
            model = new HealthModel(rules, id, team);
            return model;
        }

        public void Init()
        {
            Build();
            EnterState(LifeState.Alive, Vector3.zero);
        }

        public void UseRules(GameRules overrideRules)
        {
            rulesOverride = overrideRules;
            Init();
        }

        public void ResetForRound()
        {
            Build().ResetForRound(Now);
            EnterState(LifeState.Alive, Vector3.zero);
        }

        ShieldStats ActiveShield => RaisedShield ?? (Time.time < FrontBlockUntil ? TimedShield : null);

        public bool ApplyDamage(in HitInfo incoming)
        {
            var hit = incoming;
            bool bleeding = hit.Source == HitSource.Bleed;
            bool blowFromItem = hit.Source is HitSource.Melee or HitSource.Thrown && hit.ItemId != null;
            var tuning = GameConfig.Current.Tuning;
            var m = Model;
            var facing = controller ? controller.Facing : transform.forward;
            var shield = ActiveShield;
            bool blocks = shield != null && hit.Blockable && Geometry.InFrontArc(facing.x, facing.z, -hit.DirX, -hit.DirZ, shield.FrontArcDegrees);
            var guard = tuning?.Trait(HeldWord, TraitKind.Guard);
            if (guard != null && hit.Source is not (HitSource.Bleed or HitSource.Hazard) && !blocks) hit.Damage *= 1f - guard.Amount;
            var key = (hit.AttackerId, hit.ItemId);
            int blow = tuning != null && blowFromItem && !blocks ? (blows.TryGetValue(key, out int landed) ? landed : 0) + 1 : 0;
            var crush = blow > 0 && hit.Source == HitSource.Melee ? tuning.Trait(hit.ItemId, TraitKind.Crush) : null;
            bool crushing = crush != null && Lands(crush, blow);
            if (crushing) hit.Damage *= crush.Amount;
            m.SetFacing(facing.x, facing.z);
            m.SetBlocking(shield);
            var r = m.ApplyHit(hit, Now);
            if (!r.Landed) return false;
            if (blow > 0 && !r.Blocked) blows[key] = blow;

            var dir = Hits.Direction(hit);
            if (dir.sqrMagnitude < 0.0001f) dir = -facing;
            var at = controller ? controller.OverheadPosition : transform.position + Vector3.up * 2f;
            var heavy = blowFromItem ? tuning?.Trait(hit.ItemId, TraitKind.Heavy) : null;
            float speed = r.Knockback * Hits.KnockbackSpeed * (heavy != null && !r.Blocked && Lands(heavy, blow) ? heavy.Amount : 1f);
            if (crushing && r.Damage > 0f) Proc("CRUSH!", at, new Color(1f, .62f, .25f));

            if (r.Blocked)
            {
                if (controller) controller.Knock(dir * speed, 0f);
                if (CombatFeedbackOptions.ShowDamage) Popup.Show("BLOCKED", at, Color.white, 2.5f);
                Damaged?.Invoke(this, hit, r);
                return false;
            }

            if (controller && !bleeding) controller.Knock(dir * speed + Vector3.up * speed * 0.35f, r.HitStun);
            if (r.Damage > 0f)
            {
                Popup.Damage(r.Damage, at);
                if (!bleeding) CameraRig.Shake(0.12f);
            }
            else if (r.Absorbed > 0f)
            {
                if (CombatFeedbackOptions.ShowDamage) Popup.Show("SOAKED", at, new Color(0.62f, 0.86f, 1f), 2.5f);
            }

            int drop = rules.LettersDroppedPerHit;
            if (drop > 0 && r.Damage > 0f && !r.BecameEliminated && !bleeding) inventory.DropRandom(drop, transform.position, dir);

            Damaged?.Invoke(this, hit, r);
            Hit?.Invoke(this, Hits.Attacker(hit));

            if (r.BecameDowned) GoDown(dir * speed);
            else if (r.BecameEliminated) GetWrecked(dir * speed, wasDowned: false);
            else if (r.Damage > 0f && blowFromItem && tuning != null) Afflict(tuning, hit, blow, at);
            return true;
        }

        string HeldWord => controller && controller.Combat && controller.Combat.Weapon ? controller.Combat.Weapon.word : null;

        static bool Lands(ItemTrait trait, int blow) =>
            (trait.Every <= 1 || (blow > 0 && blow % trait.Every == 0)) && (trait.Chance >= 1f || (TraitRoll?.Invoke() ?? UnityEngine.Random.value) < trait.Chance);

        static void Proc(string word, Vector3 at, Color colour)
        {
            if (CombatFeedbackOptions.ShowDamage) Popup.Show(word, at + Vector3.up * .45f, colour, 2.5f);
        }

        void Afflict(HouseTuning tuning, in HitInfo hit, int blow, Vector3 at)
        {
            var bleed = tuning.Trait(hit.ItemId, TraitKind.Bleed);
            if (bleed != null && Lands(bleed, blow))
            {
                bleedStacks = Time.time < bleedUntil ? Math.Min(bleedStacks + 1, bleed.Stacks) : 1;
                bleedHit = new HitInfo
                {
                    Damage = bleed.Amount * bleedStacks * BleedTick, AttackerId = hit.AttackerId, AttackerTeam = hit.AttackerTeam,
                    ItemId = hit.ItemId, Source = HitSource.Bleed,
                };
                if (Time.time >= bleedUntil) nextBleed = Time.time + BleedTick;
                bleedUntil = Time.time + bleed.Seconds;
                if (bleedStacks > 1) Proc("DEEP CUT", at, new Color(1f, .35f, .35f));
            }
            var daze = tuning.Trait(hit.ItemId, TraitKind.Daze);
            if (daze != null && controller && Lands(daze, blow))
            {
                controller.Slow(daze.Amount, daze.Seconds);
                if (daze.Delivery is TraitDelivery.Chance or TraitDelivery.BuildUp) Proc("DAZED", at, new Color(.72f, .7f, 1f));
            }
        }

        void Update()
        {
            if (model != null && model.Tick(Now)) GetWrecked(Vector3.zero, wasDowned: true);
            if (bleedUntil <= 0f || Time.time < nextBleed) return;
            if (!IsAlive || nextBleed > bleedUntil + 1e-3f) { bleedUntil = 0f; return; }
            nextBleed += BleedTick;
            ApplyDamage(bleedHit);
        }

        void GoDown(Vector3 push)
        {
            Popup.Show("DOWN!", controller ? controller.OverheadPosition : transform.position, controller ? controller.Color : Color.white, 5f);
            CameraRig.Shake(0.2f);
            EnterState(LifeState.Downed, push);
            Downed?.Invoke(this);
            KnockedOut?.Invoke(this);
        }

        void GetWrecked(Vector3 push, bool wasDowned)
        {
            if (!IsEliminated) Model.Eliminate();
            if (controller && controller.Summoner) controller.Summoner.CancelCraft();
            Popup.Show("WRECKED!", controller ? controller.OverheadPosition : transform.position, controller ? controller.Color : Color.white, 5f);
            CameraRig.Shake(0.3f);
            if (inventory.Count > 0) inventory.DropRandom(inventory.Count, transform.position, push.sqrMagnitude > 0.01f ? push.normalized : Vector3.zero);
            EnterState(LifeState.Eliminated, push);
            Eliminated?.Invoke(this);
            if (!wasDowned) KnockedOut?.Invoke(this);
        }

        void EnterState(LifeState state, Vector3 push)
        {
            bleedUntil = 0f;
            bleedStacks = 0;
            blows.Clear();
            if (controller) controller.ApplyLifeState(state, push);
            StateChanged?.Invoke(this);
        }

        public void Eliminate()
        {
            if (IsEliminated) return;
            GetWrecked(Vector3.zero, IsDowned);
        }

        public float ReviveProgress =>
            IsDowned && Model.ReviverId >= 0 && Rules.ReviveSeconds > 0f
                ? Mathf.Clamp01((float)((Now - Model.ReviveStartedAt) / Rules.ReviveSeconds))
                : 0f;

        public bool BeginRevive(PlayerController reviver) =>
            reviver && reviver.Health.IsAlive && Model.BeginRevive(reviver.Index, reviver.Team, Now);

        public void CancelRevive(PlayerController reviver)
        {
            if (reviver) Model.CancelRevive(reviver.Index);
        }

        public bool TryFinishRevive(PlayerController reviver)
        {
            if (!reviver || !Model.TryFinishRevive(reviver.Index, Now)) return false;
            Popup.Show("BACK UP!", controller ? controller.OverheadPosition : transform.position, Color.white, 4f);
            EnterState(LifeState.Alive, Vector3.zero);
            Revived?.Invoke(this);
            return true;
        }

        public float Heal(float amount) => Model.Heal(amount);

        public void GiveBubble(float amount, float seconds) => GiveOwnedBubble(amount, seconds);

        public int GiveOwnedBubble(float amount, float seconds)
        {
            var m = Model;
            bubbleOwner = NextProtectionToken();
            bubbleOwnerModel = m;
            m.GiveBubble(amount, Now + seconds);
            return bubbleOwner;
        }

        public bool OwnsBubble(int token) =>
            token != 0 && token == bubbleOwner && ReferenceEquals(bubbleOwnerModel, model)
                && Now < model.BubbleUntil && model.Bubble > 0f;

        public void ClearOwnedBubble(int token)
        {
            if (token == 0 || token != bubbleOwner || !ReferenceEquals(bubbleOwnerModel, model)) return;
            ClearBubble();
        }

        public void ClearBubble()
        {
            bubbleOwner = 0;
            bubbleOwnerModel = null;
            Model.ClearBubble();
        }

        public int GiveTimedShield(float seconds)
        {
            _ = Model;
            timedShieldOwner = NextProtectionToken();
            frontBlockUntil = (float)(Now + seconds);
            return timedShieldOwner;
        }

        public bool OwnsTimedShield(int token) =>
            token != 0 && token == timedShieldOwner && Now < frontBlockUntil;

        public void ClearTimedShield(int token)
        {
            if (token == 0 || token != timedShieldOwner) return;
            timedShieldOwner = 0;
            frontBlockUntil = 0f;
        }

        int NextProtectionToken()
        {
            protectionGeneration = unchecked(protectionGeneration + 1);
            if (protectionGeneration == 0) protectionGeneration = 1;
            return protectionGeneration;
        }

        public bool CanDodge => Model.CanDodge(Now);

        public bool Dodge() => Model.Dodge(Now);
    }
}
