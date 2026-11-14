using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Audio;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class BeltRangeDirector : MonoBehaviour
    {
        public const string SceneName = "BeltRange";
        public const string LaunchArgument = "-belt-range";
        const string PracticeMode = "Tutorial";
        const string Keys = "F1 Belt  •  F2 Bat  •  F3 skin  •  F4 reset";
        const float KeysSeconds = 12f;
        static readonly string[] Skins = { Skin.Standard, "Candy", "Arcade" };

        [Serializable]
        public sealed class Target
        {
            [Tooltip("Object word. Words with a model in the Model Library use it; others are built from letter blocks.")]
            public string word = "CRATE";
            public SurfaceMaterial material = SurfaceMaterial.Wood;
            [Tooltip("Where the target stands. Move this marker in the Scene view.")]
            public Transform spot;
            [Min(1f)] public float health = 40f;
            [Tooltip("Seconds before a broken target returns. 0 waits for R.")]
            [Min(0f)] public float respawnSeconds = 2.5f;
            [Tooltip("Tint for letter-block targets only.")]
            public Color color = new Color(0.72f, 0.66f, 0.58f);
        }

        [SerializeField] PlayerJoinManager joins;
        [SerializeField] GameHud hud;
        [SerializeField] FlexibleWeaponProfile belt;
        [SerializeField] List<Target> targets = new List<Target>();
        [Tooltip("Practice dummy for body hits. Leave empty for no dummy.")]
        [SerializeField] Transform dummySpot;

        readonly Dictionary<Target, Smashable> live = new();
        int skinIndex;
        float joinedAt = -1f;
        bool keysUsed;

        public FlexibleWeaponProfile Belt => belt;
        public PlayerJoinManager Joins => joins;
        public IReadOnlyList<Target> Targets => targets;
        public PlayerController Dummy { get; private set; }
        public Smashable LiveTarget(Target target) => target != null && live.TryGetValue(target, out var s) && s ? s : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LaunchFromCommandLine()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg == LaunchArgument && Session.CanLoad(SceneName) && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneName)
                {
                    Session.Load(SceneName);
                    return;
                }
        }

        void Awake() => Match.ModeOverride = PracticeMode;

        void Start()
        {
            joins.RespawnKnockedOut = true;
            joins.Joined += Equip;
            foreach (var p in joins.Players) Equip(p);
            Smashable.AnyBroken += Broken;
            ResetTargets();
            SpawnDummy();
        }

        void OnDestroy()
        {
            if (Match.ModeOverride == PracticeMode) Match.ModeOverride = null;
            Smashable.AnyBroken -= Broken;
            if (joins) joins.Joined -= Equip;
        }

        void Update()
        {
            var player = joins.Players.Count > 0 ? joins.Players[0] : null;
            if (player && joinedAt < 0f) joinedAt = Time.unscaledTime;
            string hint = !player ? ControlHints.Join("join") : !keysUsed && Time.unscaledTime - joinedAt < KeysSeconds ? Keys : null;
            if (hud) hud.SetInstruction(hint != null ? "BELT range" : "", hint ?? "");
            var kb = Keyboard.current;
            if (kb == null || !player) return;
            if (kb.f1Key.wasPressedThisFrame) { keysUsed = true; EquipBelt(player); }
            if (kb.f2Key.wasPressedThisFrame) { keysUsed = true; EquipCatalogue(player, "BAT"); }
            if (kb.f3Key.wasPressedThisFrame) { keysUsed = true; CycleSkin(player); }
            if (kb.f4Key.wasPressedThisFrame) { keysUsed = true; ResetTargets(); }
        }

        void Equip(PlayerController player) => EquipBelt(player);

        public HeldWeapon EquipBelt(PlayerController player) => Give(player, PrototypeGear.CreateBelt(belt));

        public HeldWeapon EquipCatalogue(PlayerController player, string id)
        {
            if (!GameConfig.Current.Items.TryGet(id, out var item) || !item.Enabled) return null;
            return Give(player, CatalogGear.Create(item));
        }

        HeldWeapon Give(PlayerController player, HeldWeapon gear)
        {
            if (!player || !gear) return null;
            var combat = player.Combat;
            if (combat.Weapon) DestroyGear(combat.Weapon, combat);
            if (combat.StoredGear) DestroyGear(combat.StoredGear, combat);
            gear.transform.position = player.transform.position + Vector3.up;
            if (!combat.TryEquip(gear)) { Destroy(gear.gameObject); return null; }
            var flex = gear.GetComponent<BeltFlexDriver>();
            if (flex) flex.SetSkin(Skins[skinIndex]);
            return gear;
        }

        static void DestroyGear(HeldWeapon gear, PlayerCombat combat)
        {
            if (combat.Weapon == gear) combat.ForgetHeld();
            Destroy(gear.gameObject);
        }

        public void Throw(PlayerController player)
        {
            if (player && player.Combat.Weapon) player.Combat.Throw();
        }

        public string CycleSkin(PlayerController player)
        {
            skinIndex = (skinIndex + 1) % Skins.Length;
            var flex = player && player.Combat.Weapon ? player.Combat.Weapon.GetComponent<BeltFlexDriver>() : null;
            if (flex) flex.SetSkin(Skins[skinIndex]);
            return Skins[skinIndex];
        }

        public void ResetTargets()
        {
            foreach (var target in targets) Restore(target, true);
        }

        void Broken(Smashable smashable)
        {
            foreach (var target in targets)
            {
                if (!live.TryGetValue(target, out var current) || current != smashable) continue;
                if (target.respawnSeconds > 0f) StartCoroutine(RestoreLater(target));
                return;
            }
        }

        IEnumerator RestoreLater(Target target)
        {
            yield return new WaitForSeconds(target.respawnSeconds);
            Restore(target, false);
        }

        void Restore(Target target, bool replace)
        {
            if (target == null || !target.spot || string.IsNullOrWhiteSpace(target.word)) return;
            if (live.TryGetValue(target, out var current) && current)
            {
                if (!replace) return;
                Destroy(current.gameObject);
            }
            string word = target.word.Trim().ToUpperInvariant();
            var smash = Furniture.Create(transform, word, target.spot.position, target.spot.eulerAngles.y, Vector3.one * 0.4f, 3,
                target.color, 4f, target.health);
            smash.gameObject.AddComponent<SurfaceAudioTag>().material = target.material;
            live[target] = smash;
        }

        void SpawnDummy()
        {
            if (!dummySpot || Dummy) return;
            Dummy = Instantiate(GameAssets.I.playerPrefab, dummySpot.position, Quaternion.identity, transform);
            Dummy.Setup(9, null, new Color(0.72f, 0.70f, 0.66f), '?', "DUMMY");
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.DownedEnabled = false;
            Dummy.Health.UseRules(rules);
            Dummy.Respawn(dummySpot.position);
            Dummy.FaceTowards(-dummySpot.forward);
            Dummy.Inventory.Set("DUMMY");
            Dummy.Inventory.Collects = false;
            Dummy.Health.KnockedOut += _ => Invoke(nameof(ResetDummy), 1.5f);
        }

        void ResetDummy()
        {
            if (!Dummy || !dummySpot) return;
            Dummy.Combat.ResetForRound();
            Dummy.Health.ResetForRound();
            Dummy.Respawn(dummySpot.position);
            Dummy.FaceTowards(-dummySpot.forward);
            Dummy.Inventory.Set("DUMMY");
        }
    }
}
