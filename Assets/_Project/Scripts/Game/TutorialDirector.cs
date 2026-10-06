using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Walks roommates through the core loop one step at a time: move, smash, collect, spell,
    /// hit, throw and knock out. Any roommate can complete a step, so it works for 1–4 players.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] GameHud hud;
        [SerializeField] Transform boxSpot;
        [SerializeField] Transform chairSpot;
        [SerializeField] Transform dummySpot;
        [SerializeField] float finishDelay = 4f;

        class Step
        {
            public string text, hint;
            public Func<bool> done;
            public Action enter, keep;
        }

        List<Step> steps;
        readonly HashSet<PlayerController> watched = new();
        Vector3[] lastPositions = new Vector3[8];
        float walked, finishedAt, nextResupply, nextTether;
        bool batSummoned, thrown, jumped, dodged;
        int dummyHits;
        Smashable box, chair;

        public PlayerController Dummy { get; private set; }
        public int StepIndex { get; private set; }
        public int StepCount => steps.Count;
        public bool Finished => StepIndex >= steps.Count;

        void Awake()
        {
            steps = new List<Step>
            {
                new() { text = "Walk around", hint = ControlHints.Move, done = () => walked > 4f },
                new() { text = "Jump, then dodge", hint = $"Jump: {ControlHints.Jump}  •  dodge: {ControlHints.Dodge}", done = () => jumped && dodged },
                new() { text = "Smash the BAT box", hint = $"Punch it a few times: {ControlHints.Attack}", enter = EnsureBox, done = () => !box },
                new() { text = "Pick up the letters", hint = "Walk over B, A and T", done = () => batSummoned || joins.Players.Any(p => Has(p, "BAT")) },
                new() { text = "Spell BAT", hint = $"Hold spell ({ControlHints.Spell}), then let go to summon", done = () => batSummoned },
                new() { text = "Whack the dummy", hint = $"Attack with your BAT ({ControlHints.Attack})", enter = EnsureDummy, done = () => dummyHits > 0 },
                new() { text = "Throw the CHAIR", hint = $"Grab it ({ControlHints.Grab}), then throw it ({ControlHints.Attack})", enter = EnsureChair, keep = EnsureChair, done = () => thrown },
                new()
                {
                    text = "Knock out the dummy", hint = "Keep hitting it until its health runs out",
                    enter = ResetDummy, done = () => Dummy && Dummy.IsKnockedOut
                },
            };
        }

        void Start()
        {
            joins.RespawnKnockedOut = true;
            SpawnDummy();
            steps[0].enter?.Invoke();
        }

        void Update()
        {
            Watch();
            if (Finished)
            {
                hud.SetInstruction("You're ready!", "Heading back to the house...");
                if (Time.time - finishedAt > finishDelay) Session.GoHome();
                return;
            }

            var step = steps[StepIndex];
            if (joins.Players.Count == 0) hud.SetInstruction(ControlHints.Join("join"), "");
            else hud.SetInstruction($"<color=#FFD24A>{StepIndex + 1}/{steps.Count}</color>  {step.text}", step.hint);

            Resupply();
            step.keep?.Invoke();
            if (Time.time > nextTether && Dummy && !Dummy.IsKnockedOut && !Dummy.IsStaggered && !Dummy.IsHeld)
            {
                nextTether = Time.time + 2f;
                EnsureDummy();
            }
            if (step.done()) Advance();
        }

        void Advance()
        {
            Popup.Show("NICE!", joins.Players.Count > 0 ? joins.Players[0].OverheadPosition + Vector3.up : Vector3.up * 2f, Color.white, 5f);
            StepIndex++;
            if (Finished) { finishedAt = Time.time; return; }
            steps[StepIndex].enter?.Invoke();
        }

        /// <summary>Tracks distance walked and hooks up summon/throw events for new roommates.</summary>
        void Watch()
        {
            for (int i = 0; i < joins.Players.Count; i++)
            {
                var p = joins.Players[i];
                if (watched.Add(p))
                {
                    p.Summoner.Summoned += w => { if (w == "BAT") batSummoned = true; };
                    p.Combat.Thrown += _ => thrown = true;
                    p.Jumped += _ => jumped = true;
                    p.Dodged += _ => dodged = true;
                    lastPositions[i] = p.transform.position;
                }
                var pos = p.transform.position;
                float d = World.Flat(pos - lastPositions[i]).magnitude;
                if (d < 1f) walked += d; // ignore respawn teleports
                lastPositions[i] = pos;
            }
        }

        /// <summary>If the BAT letters got lost (dropped, spent on something else), send another box.</summary>
        void Resupply()
        {
            if (StepIndex < 2 || StepIndex > 4 || batSummoned || box || Time.time < nextResupply) return;
            if (joins.Players.Any(p => p.Summoner.IsCrafting && p.Summoner.CraftWord == "BAT")) return;
            nextResupply = Time.time + 1f;
            var letters = new List<char>();
            if (TilePool.Instance) letters.AddRange(TilePool.Instance.Active.Select(t => t.Letter));
            foreach (var p in joins.Players) letters.AddRange(p.Inventory.Letters);
            if (!WordSolver.CanSpell(WordSolver.Count(letters), "BAT")) EnsureBox();
        }

        static bool Has(PlayerController p, string word) => WordSolver.CanSpell(WordSolver.Count(p.Inventory.Letters), word);

        void EnsureBox()
        {
            if (box) return;
            box = DeliverySpawner.CreateBox("BAT", boxSpot.position + Vector3.up * 2f);
            box.transform.SetParent(transform, true);
        }

        void EnsureChair()
        {
            if (chair) return;
            chair = Furniture.Create(transform, "CHAIR", chairSpot.position + Vector3.up * 0.5f, 20f, Vector3.one * 0.4f, 3,
                                     new Color(0.71f, 0.51f, 0.35f), 4f, 40f);
        }

        void SpawnDummy()
        {
            var prefab = GameAssets.I.playerPrefab;
            Dummy = Instantiate(prefab, dummySpot.position, Quaternion.identity, transform);
            Dummy.Setup(9, null, new Color(0.72f, 0.70f, 0.66f), '?', "DUMMY");
            Dummy.Health.UseRules(DummyRules());
            Dummy.Respawn(dummySpot.position);
            Dummy.FaceTowards(Vector3.back);
            Dummy.Inventory.Set("DUMMY");
            Dummy.Inventory.Collects = false;
            Dummy.Health.Hit += (_, attacker) => { if (attacker) dummyHits++; };
            Dummy.Health.KnockedOut += _ => { if (!Finished) Invoke(nameof(EnsureDummy), 1.5f); };
        }

        static GameRules DummyRules()
        {
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.DownedEnabled = false;
            return rules;
        }

        void EnsureDummy()
        {
            if (!Dummy || Finished) return;
            bool wandered = World.Flat(Dummy.transform.position - dummySpot.position).magnitude > 2.5f;
            if (!Dummy.IsKnockedOut && !wandered) return;
            ResetDummy();
        }

        void ResetDummy()
        {
            if (!Dummy || Finished) return;
            Dummy.Combat.ResetForRound();
            Dummy.Health.ResetForRound();
            Dummy.Respawn(dummySpot.position);
            Dummy.FaceTowards(Vector3.back);
            Dummy.Inventory.Set("DUMMY");
        }
    }
}
