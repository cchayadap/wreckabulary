using System;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// A roommate's 100 HP (brief §7). The numbers and the rules live in <see cref="HealthModel"/>;
    /// this component feeds it hits, turns the results into knockback, stagger and popups, and
    /// tells the rest of the game when the player goes down, is wrecked or gets back up.
    /// Letters are loot, not health: they stay put unless the mode drops some per hit, and all
    /// of them spill out when the player is wrecked.
    /// </summary>
    [RequireComponent(typeof(LetterInventory))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        static readonly ShieldStats TimedShield = new() { FrontArcDegrees = 150f, DamageReduction = 1f, MoveSpeedMultiplier = 1f };

        LetterInventory inventory;
        PlayerController controller;
        HealthModel model;
        GameRules rules, rulesOverride;
        HealthModel bubbleOwnerModel;
        int protectionGeneration, bubbleOwner, timedShieldOwner;
        float frontBlockUntil;

        static double Now => Time.timeAsDouble;

        public HealthModel Model => model ??= Build();
        public GameRules Rules { get { if (model == null) Build(); return rules; } }

        public float Current => Model.Current;
        public float Max => Model.Max;
        public float Fraction => Max > 0f ? Current / Max : 0f;
        public LifeState State => Model.State;
        public bool IsAlive => Model.IsAlive;
        public bool IsDowned => Model.State == LifeState.Downed;
        public bool IsEliminated => Model.State == LifeState.Eliminated;
        public bool IsInvulnerable => Now < Model.InvulnerableUntil;
        public float Bubble => Now < Model.BubbleUntil ? Model.Bubble : 0f;
        public float BubbleLeft => Bubble > 0f ? (float)(Model.BubbleUntil - Now) : 0f;
        public float BleedOutLeft => IsDowned ? (float)Math.Max(0.0, Model.BleedOutAt - Now) : 0f;

        /// <summary>SHIELD: hits from the front are blocked until this time.</summary>
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

        public bool ApplyDamage(in HitInfo hit)
        {
            var m = Model;
            var facing = controller ? controller.Facing : transform.forward;
            m.SetFacing(facing.x, facing.z);
            m.SetBlocking(ActiveShield);
            var r = m.ApplyHit(hit, Now);
            if (!r.Landed) return false;

            var dir = Hits.Direction(hit);
            if (dir.sqrMagnitude < 0.0001f) dir = -facing;
            var at = controller ? controller.OverheadPosition : transform.position + Vector3.up * 2f;
            float speed = r.Knockback * Hits.KnockbackSpeed;

            if (r.Blocked)
            {
                if (controller) controller.Knock(dir * speed, 0f);
                Popup.Show("BLOCKED", at, Color.white, 2.5f);
                Damaged?.Invoke(this, hit, r);
                return false;
            }

            if (controller) controller.Knock(dir * speed + Vector3.up * speed * 0.35f, r.HitStun);
            if (r.Damage > 0f)
            {
                Popup.Show(Mathf.CeilToInt(r.Damage).ToString(), at, new Color(1f, 0.42f, 0.3f), 3f);
                CameraRig.Shake(0.12f);
            }
            else if (r.Absorbed > 0f)
            {
                Popup.Show("SOAKED", at, new Color(0.62f, 0.86f, 1f), 2.5f);
            }

            int drop = rules.LettersDroppedPerHit;
            if (drop > 0 && r.Damage > 0f && !r.BecameEliminated) inventory.DropRandom(drop, transform.position, dir);

            Damaged?.Invoke(this, hit, r);
            Hit?.Invoke(this, Hits.Attacker(hit));

            if (r.BecameDowned) GoDown(dir * speed);
            else if (r.BecameEliminated) GetWrecked(dir * speed, wasDowned: false);
            return true;
        }

        void Update()
        {
            if (model != null && model.Tick(Now)) GetWrecked(Vector3.zero, wasDowned: true);
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
