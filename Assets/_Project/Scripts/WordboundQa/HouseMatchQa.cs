#if UNITY_EDITOR || GSK_QA
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Capture;
using GameStudioKit.Qa;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class HouseMatchQa : IQaScenario
    {
        public string Name => "house_match";
        const string HouseMode = "Dibs", HouseMap = "pinwheel";
        const float BudgetSeconds = 300f, ApproachMetres = 1.9f, PunchMetres = .7f;
        static readonly string[] ProcWords = { "CRUSH!", "DEEP CUT", "DAZED" };

        LocalSeat attackerSeat, defenderSeat;
        PlayerController attacker, defender, roundWinner;
        PlayerJoinManager joins;
        RoundManager rounds;
        RoomBuilder room;
        GameHud hud;
        Smashable target;
        string targetWord, weaponWord;
        bool launched;
        InputBinding[] originalBindings;
        string originalMap;
        System.Func<float> originalRoll;
        Saved[] preferences;
        readonly List<LetterTile> dropped = new();
        readonly List<string> errors = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register("house_match", () => new HouseMatchQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("house_match_open").Timeout(60).When(_ => Object.FindAnyObjectByType<LobbyMenu>() != null).Do(Open);
            yield return QaStep.Named("house_match_approach").Timeout(20).Do(Approach);
            yield return QaStep.Named("house_match_smash").Timeout(25).Do(Smash);
            yield return QaStep.Named("house_match_collect").Timeout(20).Do(Collect);
            yield return QaStep.Named("house_match_spell_hint").Timeout(10).Do(SpellHint);
            yield return QaStep.Named("house_match_recipe_book").Timeout(12).Do(RecipeBook);
            yield return QaStep.Named("house_match_craft").Timeout(12).Do(Craft);
            yield return QaStep.Named("house_match_hit").Timeout(15).Do(Hit);
            yield return QaStep.Named("house_match_round_end").Timeout(15).Do(RoundEnd);
            yield return QaStep.Named("house_match_return").Timeout(40).Do(Return);
        }

        void Log(string condition, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert) errors.Add(condition);
        }

        void Won(PlayerController winner) => roundWinner = winner;

        IEnumerator Open(QaContext context)
        {
            if (!Session.CanLoad(Session.DibsScene)) throw new QaFailException(Session.DibsScene + " is not in the build.");
            preferences = Saved.Capture();
            originalBindings = Session.Bindings.ToArray();
            originalMap = Session.MapId;
            originalRoll = PlayerHealth.TraitRoll;
            var rolls = new System.Random(context.Seed);
            PlayerHealth.TraitRoll = () => (float)rolls.NextDouble();
            if (!CombatFeedbackOptions.ShowDamage) CombatFeedbackOptions.ShowDamage = true;
            Application.logMessageReceived += Log;
            attackerSeat = new LocalSeat("qa-house-match-1");
            defenderSeat = new LocalSeat("qa-house-match-2");
            Session.Bindings.Clear();
            Session.Remember(attackerSeat);
            Session.Remember(defenderSeat);
            UnityEngine.Random.InitState(context.Seed);
            launched = true;
            Session.LoadMode(HouseMode, HouseMap);
            yield return context.WaitUntil(() => SceneManager.GetActiveScene().name == Session.DibsScene
                && (rounds = RoundManager.Instance) != null && (joins = Object.FindAnyObjectByType<PlayerJoinManager>()) != null
                && (room = Object.FindAnyObjectByType<RoomBuilder>()) != null && (hud = Object.FindAnyObjectByType<GameHud>()) != null, 30, "the Pinwheel house to load");
            yield return context.WaitUntil(() => rounds && rounds.Phase == Phase.Playing, 20, "the House party match to reach Playing");
            attacker = joins.Players.FirstOrDefault(p => p && p.Binding == attackerSeat);
            defender = joins.Players.FirstOrDefault(p => p && p.Binding == defenderSeat);
            if (!attacker || !defender) throw new QaFailException("Both local seats must join the match through Session.Bindings.");
            if (Match.Mode != HouseMode || Session.MapId != HouseMap) throw new QaFailException($"Expected {HouseMode} on {HouseMap}, got {Match.Mode} on {Session.MapId}.");
            var bots = joins.Players.Where(p => p && p.Binding is BotBinding).ToArray();
            foreach (var bot in bots) bot.Frozen = true;
            rounds.RoundWon += Won;
            hud.Follow(attacker);
            yield return context.WaitSeconds(.6f);
            var rig = CameraRig.Instance;
            var lens = rig ? rig.ViewCamera : null;
            context.Note("house_match_mode", Match.Mode);
            context.Note("house_match_map", Session.MapId);
            context.Note("house_match_phase", rounds.Phase.ToString());
            context.Note("house_match_round", rounds.Round);
            context.Note("house_match_players", joins.Players.Where(p => p).Select(p => p.Name + " " + Kind(p.Binding) + " " +
                room.Layout.RoomAt(p.transform.position.x, p.transform.position.y + .1f, p.transform.position.z)).ToArray());
            context.Note("house_match_bots_parked", bots.Select(p => p.Name).ToArray());
            context.Note("house_match_camera", new Dictionary<string, object>
            {
                ["third_person"] = rig && rig.IsThirdPerson, ["orthographic"] = lens && lens.orthographic,
                ["size"] = lens ? (lens.orthographic ? lens.orthographicSize : lens.fieldOfView) : 0f,
            });
            context.Note("house_match_scope", "Offline Windows Dibs on Pinwheel through Session.LoadMode with two scripted human-like local seats. " +
                "The game's own AI fill seats are parked (Frozen) so the run stays deterministic; the defender is staged in the attacker's room; " +
                "the round is closed by eliminating the remaining players after the hit. No network, remote peer or human-feel claim.");
            yield return Capture(context, "house_match_start");
            if (!rig || rig.IsThirdPerson) throw new QaFailException("Two local seats must share the overhead play camera, not a third-person follow.");
        }

        IEnumerator Approach(QaContext context)
        {
            var layout = room.Layout;
            var from = attacker.transform.position;
            string here = layout.RoomAt(from.x, from.y + .1f, from.z);
            var box = layout.Room(here);
            if (box == null) throw new QaFailException("The attacker did not spawn inside a Pinwheel room.");
            target = ChooseTarget(layout, here, box, from);
            if (!target) throw new QaFailException("No reachable furniture in " + here + ".");
            targetWord = target.Word;
            weaponWord = MeleeFrom(targetWord);
            var spot = StagingSpot(layout, here, box, target.transform.position, from);
            defender.Respawn(spot);
            Aim(defender, from - spot);
            yield return context.WaitSeconds(1f);
            bool reached = false;
            yield return WalkTo(attacker, attackerSeat, () => target ? Contact(target, attacker.transform.position) : attacker.transform.position, ApproachMetres, 6f, ok => reached = ok);
            if (!reached) throw new QaFailException($"P1 did not walk up to {targetWord} within 6 s.");
            var labels = VisibleLabels();
            context.Note("house_match_room", here);
            context.Note("house_match_furniture", targetWord);
            context.Note("house_match_planned_weapon", weaponWord ?? "none");
            context.Note("house_match_defender_at", new[] { spot.x, spot.z });
            context.Note("house_match_labels_near_players", labels);
            yield return Capture(context, "house_match_approach_furniture");
            if (labels.Length == 0) throw new QaFailException("No furniture word label was visible on screen near a player.");
        }

        IEnumerator Smash(QaContext context)
        {
            if (!target) throw new QaFailException("No furniture was chosen to smash.");
            var pool = TilePool.Instance;
            if (!pool) throw new QaFailException("The match has no letter tile pool.");
            var before = new HashSet<LetterTile>(pool.Active);
            bool broke = false;
            float started = Time.time;
            System.Action<Smashable> saw = s =>
            {
                if (s != target) return;
                broke = true;
                dropped.AddRange(pool.Active.Where(t => t && !before.Contains(t)));
            };
            Smashable.AnyBroken += saw;
            attacker.Inventory.Collects = false;
            try
            {
                float end = Time.realtimeSinceStartup + 12f;
                while (!broke)
                {
                    if (Time.realtimeSinceStartup > end) throw new QaFailException(targetWord + " did not break within 12 s of punching.");
                    if (!target) throw new QaFailException(targetWord + " vanished without breaking.");
                    var to = World.Flat(Contact(target, attacker.transform.position) - attacker.transform.position);
                    if (to.magnitude > PunchMetres)
                    {
                        attackerSeat.Next.look = Vector2.zero;
                        attackerSeat.Next.move = Flat2(to);
                    }
                    else
                    {
                        attackerSeat.Next.move = Vector2.zero;
                        attackerSeat.Next.look = Flat2(to);
                        Aim(attacker, to);
                        attackerSeat.Next.attack = true;
                    }
                    yield return null;
                }
                attackerSeat.Next = default;
                string letters = new string(dropped.Select(t => t.Letter).OrderBy(c => c).ToArray());
                context.Note("house_match_smash_seconds", Time.time - started);
                context.Note("house_match_letters_spawned", letters);
                if (letters != new string(targetWord.OrderBy(c => c).ToArray()))
                    throw new QaFailException($"Breaking {targetWord} spawned letters '{letters}' instead of its own letters.");
                float settle = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < settle && Haloed() < dropped.Count) yield return null;
                yield return context.WaitSeconds(.2f);
                context.Note("house_match_letters_standing", dropped.Count(t => t && t.GetComponent<LetterPickupView>()));
                context.Note("house_match_letters_with_halo", Haloed());
                yield return Capture(context, "house_match_letters_dropped");
            }
            finally
            {
                Smashable.AnyBroken -= saw;
                if (attacker) attacker.Inventory.Collects = true;
                attackerSeat.Next = default;
            }
        }

        IEnumerator Collect(QaContext context)
        {
            var skipped = new HashSet<LetterTile>();
            float end = Time.realtimeSinceStartup + 30f;
            for (int pass = 0; pass < 2; pass++, skipped.Clear())
                while (Time.realtimeSinceStartup < end && !attacker.Inventory.IsFull)
                {
                    var need = (weaponWord ?? "").ToList();
                    foreach (char c in attacker.Inventory.Letters) need.Remove(c);
                    var next = dropped.Where(t => t && t.gameObject.activeSelf && !skipped.Contains(t))
                        .OrderBy(t => need.Contains(t.Letter) ? 0 : 1)
                        .ThenBy(t => World.Flat(t.transform.position - attacker.transform.position).sqrMagnitude).FirstOrDefault();
                    if (!next) break;
                    yield return WalkTo(attacker, attackerSeat, () => next && next.gameObject.activeSelf ? next.transform.position : attacker.transform.position, .45f, 4f, _ => { });
                    yield return context.WaitSeconds(.15f);
                    if (next && next.gameObject.activeSelf) skipped.Add(next);
                }
            skipped.UnionWith(dropped.Where(t => t && t.gameObject.activeSelf));
            string held = new string(attacker.Inventory.Letters.ToArray());
            weaponWord = MeleeFrom(attacker.Inventory.Letters);
            context.Note("house_match_letters_collected", held);
            context.Note("house_match_letters_missed", skipped.Count);
            context.Note("house_match_weapon", weaponWord ?? "none: fists");
            if (held.Length == 0) throw new QaFailException("P1 could not pick up any of the dropped letters.");
        }

        IEnumerator SpellHint(QaContext context)
        {
            var hint = HudText("Spell hint");
            var inventory = attacker.Inventory;
            var words = Words(attacker);
            bool ready = WordSolver.Spellable(words, inventory.Letters).Count > 0;
            string expected = GameHud.SpellHintText(words, inventory.Letters, inventory.Capacity, GameHud.SpellKeyLabel(attacker.Binding));
            float end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end && expected.Length > 0 && !(hint.gameObject.activeInHierarchy && hint.text.Length > 0)) yield return null;
            yield return context.WaitSeconds(.2f);
            string shown = hint.gameObject.activeInHierarchy ? hint.text : "";
            context.Note("house_match_word_ready", ready);
            context.Note("house_match_spell_hint", shown);
            context.Note("house_match_spell_hint_expected", expected);
            yield return Capture(context, "house_match_hud_spell_hint");
            if (ready && (shown.Length == 0 || !shown.Contains(" ready")))
                throw new QaFailException($"A word is ready but the HUD spell hint shows '{shown}' (rules say '{expected}').");
        }

        IEnumerator RecipeBook(QaContext context)
        {
            var events = EventSystem.current;
            if (!events) throw new QaFailException("The match HUD has no EventSystem to focus a recipe card.");
            Click(HudButton("Bag link"));
            yield return context.WaitUntil(() => hud.BagOpen, 10, "the recipe book to open");
            var root = HudRoot();
            var cards = root.GetComponentsInChildren<Button>(true).Where(b => b.transform.parent && b.transform.parent.name == "Cards").ToList();
            if (cards.Count == 0) throw new QaFailException("The recipe book has no cards.");
            var spellable = new HashSet<string>(WordSolver.Spellable(Words(attacker), attacker.Inventory.Letters).Select(w => w.word));
            var card = cards.Where(c => Effect(c.name).Length > 0).OrderBy(c => spellable.Contains(c.name) ? 0 : 1).FirstOrDefault()
                ?? cards.FirstOrDefault(c => c.name == weaponWord) ?? cards[0];
            string word = card.name;
            var line = HudText("Book line");
            try
            {
                events.SetSelectedGameObject(card.gameObject);
                yield return context.WaitUntil(() => line.text.StartsWith(word + " ·", System.StringComparison.Ordinal), 10, "the book line for " + word);
                yield return context.WaitSeconds(.5f);
                context.Note("house_match_book_card", word);
                context.Note("house_match_book_line", line.text);
                context.Note("house_match_book_card_lit", card.interactable);
                yield return Capture(context, "house_match_recipe_book");
                if (line.text.Trim().Length <= word.Length + 2) throw new QaFailException("The focused recipe card shows no effect line.");
            }
            finally
            {
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                if (hud && hud.BagOpen) Click(HudButton("Close bag"));
            }
            yield return context.WaitUntil(() => !hud.BagOpen, 10, "the recipe book to close");
        }

        IEnumerator Craft(QaContext context)
        {
            if (weaponWord == null)
                throw new QaFailException($"P1 holds '{new string(attacker.Inventory.Letters.ToArray())}', which spells no melee item, so the hit would only test fists.");
            var summoner = attacker.Summoner;
            try
            {
                attackerSeat.Next.spellHeld = true;
                attackerSeat.Next.spellDown = true;
                yield return context.WaitUntil(() => summoner.IsSpelling, 10, "the spell wheel to open");
                for (int i = 0; i < summoner.Ready.Count && summoner.SelectedWord?.word != weaponWord; i++)
                {
                    attackerSeat.Next.down = true;
                    yield return context.WaitFrames(2);
                }
                context.Note("house_match_spell_wheel", summoner.Ready.Select(w => w.word).ToArray());
                if (summoner.SelectedWord?.word != weaponWord) throw new QaFailException(weaponWord + " is not on the spell wheel.");
                attackerSeat.Next.spellHeld = false;
                attackerSeat.Next.spellUp = true;
                yield return context.WaitUntil(() => summoner.IsCrafting || Held(attacker) == weaponWord, 10, weaponWord + " to start crafting");
                yield return context.WaitUntil(() => Held(attacker) == weaponWord || Stored(attacker) == weaponWord, 15, weaponWord + " to be crafted");
                if (Held(attacker) != weaponWord)
                {
                    attackerSeat.Next.swap = true;
                    yield return context.WaitUntil(() => Held(attacker) == weaponWord, 10, weaponWord + " to be in hand");
                }
            }
            finally { attackerSeat.Next.spellHeld = false; }
        }

        IEnumerator Hit(QaContext context)
        {
            var weapon = attacker.Combat.Weapon;
            var melee = weapon && weapon.Definition != null && weapon.Definition.Melee != null ? weapon.Definition.Melee : attacker.Health.Rules.Unarmed;
            string used = weapon ? weapon.word : "FISTS";
            string effect = Effect(used);
            float swing = Mathf.Clamp(melee.Reach * .6f, .7f, 1.1f), dealt = 0f;
            bool staged = false;
            Vector3 facing = Vector3.forward;
            System.Action<PlayerHealth, HitInfo, HitResult> saw = (_, _, r) => { if (r.Damage > 0f) dealt += r.Damage; };
            defender.Health.Damaged += saw;
            try
            {
                float end = Time.realtimeSinceStartup + 10f;
                while (dealt <= 0f)
                {
                    if (Time.realtimeSinceStartup > end) throw new QaFailException($"P1 swung {used} for 10 s without landing a hit on P2 ({World.Flat(defender.transform.position - attacker.transform.position).magnitude:0.00} m apart, staged {staged}, P2 invulnerable {defender.Health.IsInvulnerable}).");
                    if (!defender.Health.IsAlive) throw new QaFailException("P2 was knocked out before the scripted hit.");
                    var to = World.Flat(defender.transform.position - attacker.transform.position);
                    if (!staged && to.magnitude > ApproachMetres)
                    {
                        attackerSeat.Next.look = Vector2.zero;
                        attackerSeat.Next.move = Flat2(to);
                    }
                    else if (!staged)
                    {
                        attackerSeat.Next = default;
                        if (to.sqrMagnitude > 1e-4f) facing = to.normalized;
                        facing = ClearFacing(attacker, facing, swing);
                        defender.Respawn(attacker.transform.position + facing * swing);
                        defender.Body.linearVelocity = Vector3.zero;
                        attacker.Body.linearVelocity = Vector3.zero;
                        Aim(defender, -facing);
                        Aim(attacker, facing);
                        staged = true;
                        yield return context.WaitSeconds(.3f);
                        continue;
                    }
                    else
                    {
                        attackerSeat.Next.move = Vector2.zero;
                        attackerSeat.Next.look = Vector2.zero;
                        Aim(attacker, facing);
                        attackerSeat.Next.attack = true;
                    }
                    yield return null;
                }
                attackerSeat.Next = default;
                yield return null;
                Time.timeScale = 0f;
                try
                {
                    yield return null;
                    var shown = Object.FindObjectsByType<Popup>().Select(p => p.GetComponent<TextMeshPro>()).Where(t => t).Select(t => t.text).ToArray();
                    var procs = shown.Where(t => ProcWords.Contains(t)).ToArray();
                    bool number = shown.Any(t => t.Contains("DMG"));
                    context.Note("house_match_hit", new Dictionary<string, object>
                    {
                        ["weapon"] = used, ["damage"] = dealt, ["effect"] = effect, ["damage_number"] = number,
                        ["proc_words"] = procs, ["popups"] = shown, ["p2_health"] = defender.Health.Current,
                    });
                    yield return Capture(context, "house_match_hit");
                    if (!number) throw new QaFailException("The hit on P2 showed no damage number.");
                }
                finally { Time.timeScale = 1f; }
            }
            finally
            {
                if (defender) defender.Health.Damaged -= saw;
                attackerSeat.Next = default;
            }
        }

        IEnumerator RoundEnd(QaContext context)
        {
            var others = joins.Players.Where(p => p && p != attacker && !p.IsEliminated).ToArray();
            foreach (var p in others) p.Health.Eliminate();
            yield return context.WaitUntil(() => rounds && rounds.Phase is Phase.RoundOver or Phase.MatchOver, 15, "the round to end");
            yield return context.WaitUntil(() => hud && hud.ResultShown, 15, "the round result card");
            float settled = Time.realtimeSinceStartup + .6f;
            yield return context.WaitUntil(() => Time.realtimeSinceStartup >= settled, 10, "the result card to settle");
            context.Note("house_match_round_end", new Dictionary<string, object>
            {
                ["phase"] = rounds.Phase.ToString(), ["round"] = rounds.Round, ["winner"] = roundWinner ? roundWinner.Name : "none",
                ["p1_wins"] = rounds.WinsOf(attacker), ["eliminated_by_qa"] = others.Select(p => p.Name).ToArray(),
            });
            yield return Capture(context, "house_match_round_end");
            if (roundWinner != attacker) throw new QaFailException("P1 should have won the round after the others were eliminated.");
        }

        IEnumerator Return(QaContext context)
        {
            Time.timeScale = 1f;
            if (rounds) rounds.RoundWon -= Won;
            if (attackerSeat != null) attackerSeat.Next = default;
            if (defenderSeat != null) defenderSeat.Next = default;
            PlayerHealth.TraitRoll = originalRoll;
            if (originalBindings != null)
            {
                Session.Bindings.Clear();
                Session.Bindings.AddRange(originalBindings);
            }
            if (launched)
            {
                Session.GoHome();
                yield return context.WaitUntil(() => SceneManager.GetActiveScene().name == Session.HubScene && Object.FindAnyObjectByType<LobbyMenu>() != null, 30, "the Hub");
            }
            if (originalMap != null && Session.MapId != originalMap) Session.SelectMap(originalMap);
            var changed = preferences == null ? new Saved[0] : preferences.Where(p => !p.Matches()).ToArray();
            foreach (var preference in changed) preference.Restore();
            if (changed.Length != 0) PlayerPrefs.Save();
            Application.logMessageReceived -= Log;
            float seconds = context.ElapsedSeconds;
            context.Note("house_match_prefs_restored", changed.Select(p => p.Key).ToArray());
            context.Note("house_match_errors", errors.ToArray());
            context.Note("house_match_seconds", seconds);
            if (errors.Count > 0) throw new QaFailException($"The match logged {errors.Count} error(s); first: {errors[0]}");
            if (seconds > BudgetSeconds) throw new QaFailException($"The run took {seconds:0.0} s; the smoke budget is {BudgetSeconds:0} s.");
        }

        Smashable ChooseTarget(HouseLayout layout, string here, RoomBox box, Vector3 from) => room.Originals
            .Where(s => s && !s.IsBroken && s.isActiveAndEnabled && !s.Invulnerable)
            .Where(s => s.transform.position.y < box.FloorY + 1f && layout.RoomAt(s.transform.position.x, s.transform.position.y + .1f, s.transform.position.z) == here)
            .OrderBy(s => MeleeFrom(s.Word) == null ? 1 : 0)
            .ThenBy(s => Margin(box, s.transform.position) >= 1.5f ? 0 : 1)
            .ThenBy(s => Mathf.Round(World.Flat(s.transform.position - from).magnitude * 10f))
            .ThenBy(s => s.Word, System.StringComparer.Ordinal)
            .ThenBy(s => s.transform.position.x).ThenBy(s => s.transform.position.z)
            .FirstOrDefault();

        Vector3 StagingSpot(HouseLayout layout, string here, RoomBox box, Vector3 avoid, Vector3 from)
        {
            var furniture = room.Originals.Where(s => s && !s.IsBroken).Select(s => s.transform.position).ToArray();
            Vector3? best = null;
            float bestScore = float.NegativeInfinity;
            for (float x = box.MinX + 1.2f; x <= box.MaxX - 1.2f + .01f; x += .5f)
                for (float z = box.MinZ + 1.2f; z <= box.MaxZ - 1.2f + .01f; z += .5f)
                {
                    var at = new Vector3(x, box.FloorY, z);
                    float gap = World.Flat(at - avoid).magnitude;
                    if (gap < 3.5f || gap > 6.5f || World.Flat(at - from).magnitude < 2.5f || OffSegment(at, from, avoid) < 2f) continue;
                    if (layout.RoomAt(x, box.FloorY + .1f, z) != here || furniture.Any(f => World.Flat(f - at).magnitude < 1.5f)) continue;
                    if (Physics.CheckCapsule(at + Vector3.up * .45f, at + Vector3.up * 1.3f, .35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    float score = -Mathf.Abs(gap - 4.5f);
                    if (score > bestScore + .001f) { bestScore = score; best = at; }
                }
            return best ?? ShoulderView.RoomySpot(new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY, (box.MinZ + box.MaxZ) * .5f),
                at => layout.RoomAt(at.x, at.y + .1f, at.z) == here);
        }

        string[] VisibleLabels()
        {
            var rig = CameraRig.Instance;
            var lens = rig && rig.ViewCamera ? rig.ViewCamera : Camera.main;
            if (!lens) return new string[0];
            var seats = new[] { attacker, defender }.Where(p => p).ToArray();
            return Object.FindObjectsByType<SmashableLabel>()
                .Where(l => l && l.Text && l.Text.gameObject.activeInHierarchy && l.Alpha >= .5f)
                .Where(l => seats.Any(p => World.Flat(l.transform.position - p.transform.position).magnitude <= SmashableLabel.NearDistance))
                .Where(l =>
                {
                    var v = lens.WorldToViewportPoint(l.Text.transform.position);
                    return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
                })
                .Select(l => l.Text.text).OrderBy(w => w, System.StringComparer.Ordinal).ToArray();
        }

        int Haloed() => dropped.Count(t => t && t.gameObject.activeInHierarchy &&
            t.GetComponentsInChildren<LineRenderer>().Any(l => l.enabled && l.gameObject.name == "Letter rarity halo"));

        Transform HudRoot() => hud.UiCanvas ? hud.UiCanvas.transform : hud.transform;

        TextMeshProUGUI HudText(string name)
        {
            var part = HudRoot().GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == name);
            if (!part) throw new QaFailException("The match HUD has no " + name + ".");
            return part;
        }

        Button HudButton(string name)
        {
            var part = HudRoot().GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
            if (!part) throw new QaFailException("The match HUD has no " + name + " button.");
            return part;
        }

        static void Click(Button button) =>
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        static IEnumerator WalkTo(PlayerController who, LocalSeat seat, System.Func<Vector3> goal, float stop, float seconds, System.Action<bool> done)
        {
            float end = Time.realtimeSinceStartup + seconds, checkAt = Time.time + .5f, sideUntil = 0f, side = 0f;
            var mark = who.transform.position;
            bool reached = false;
            try
            {
                while (Time.realtimeSinceStartup <= end && who)
                {
                    var to = World.Flat(goal() - who.transform.position);
                    if (to.magnitude <= stop) { reached = true; break; }
                    var direction = to.normalized;
                    if (Time.time >= checkAt)
                    {
                        if (World.Flat(who.transform.position - mark).magnitude < .15f) { side = side > 0f ? -1f : 1f; sideUntil = Time.time + .35f; }
                        mark = who.transform.position;
                        checkAt = Time.time + .5f;
                    }
                    if (Time.time < sideUntil) direction = (direction + Vector3.Cross(Vector3.up, direction) * side * 1.5f).normalized;
                    seat.Next.look = Vector2.zero;
                    seat.Next.move = new Vector2(direction.x, direction.z);
                    yield return null;
                }
            }
            finally { seat.Next.move = Vector2.zero; }
            done(reached);
        }

        static string MeleeFrom(IEnumerable<char> letters)
        {
            var have = WordSolver.Count(letters);
            return GameConfig.Current.Items.Enabled
                .Where(i => i.Melee != null && (i.Family is HandlingFamily.MeleeSwing or HandlingFamily.MeleeThrust) && WordSolver.CanSpell(have, i.Id))
                .OrderBy(i => i.Id.Length).ThenByDescending(i => i.Melee.Damage).ThenBy(i => i.Id, System.StringComparer.Ordinal)
                .Select(i => i.Id).FirstOrDefault();
        }

        static IReadOnlyList<WordEntry> Words(PlayerController player) => player.Summoner.WordsOverride ?? GameAssets.I.words.Words;

        static string Effect(string word) => GameConfig.Current.Tuning?.Describe(word) ?? "";

        static string Held(PlayerController player)
        {
            var weapon = player.Combat.Weapon;
            return weapon ? weapon.word : null;
        }

        static string Stored(PlayerController player)
        {
            var gear = player.Combat.StoredGear;
            return gear ? gear.word : null;
        }

        static string Kind(InputBinding binding) => binding switch
        {
            LocalSeat => "seat",
            BotBinding => "bot",
            null => "none",
            _ => binding.GetType().Name,
        };

        static float Margin(RoomBox box, Vector3 at) =>
            Mathf.Min(Mathf.Min(at.x - box.MinX, box.MaxX - at.x), Mathf.Min(at.z - box.MinZ, box.MaxZ - at.z));

        static float OffSegment(Vector3 at, Vector3 a, Vector3 b)
        {
            var ab = World.Flat(b - a);
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(World.Flat(at - a), ab) / ab.sqrMagnitude) : 0f;
            return World.Flat(at - (a + ab * t)).magnitude;
        }

        static Vector3 Contact(Smashable smashable, Vector3 feet)
        {
            var chest = feet + Vector3.up * .8f;
            var best = smashable.transform.position;
            float nearest = float.PositiveInfinity;
            foreach (var part in smashable.GetComponentsInChildren<Collider>())
            {
                if (!part.enabled || part.isTrigger) continue;
                var point = part is MeshCollider { convex: false } ? part.bounds.ClosestPoint(chest) : part.ClosestPoint(chest);
                float distance = (point - chest).sqrMagnitude;
                if (distance < nearest) { nearest = distance; best = point; }
            }
            return best;
        }

        static Vector2 Flat2(Vector3 v)
        {
            var flat = World.Flat(v);
            return flat.sqrMagnitude > 1e-6f ? new Vector2(flat.x, flat.z).normalized : Vector2.zero;
        }

        static Vector3 ClearFacing(PlayerController attacker, Vector3 facing, float swing)
        {
            int mask = World.GroundMask & Physics.DefaultRaycastLayers;
            var chest = attacker.transform.position + Vector3.up * .8f;
            for (int step = 0; step < 8; step++)
            {
                var direction = Quaternion.Euler(0f, 45f * ((step + 1) / 2) * (step % 2 == 0 ? 1 : -1), 0f) * facing;
                var spot = chest + direction * swing;
                bool blocked = Physics.RaycastAll(chest, direction, swing + .35f, mask, QueryTriggerInteraction.Ignore)
                    .Any(hit => !hit.collider.attachedRigidbody) ||
                    Physics.OverlapSphere(spot, .3f, mask, QueryTriggerInteraction.Ignore).Any(c => !c.attachedRigidbody);
                if (!blocked) return direction;
            }
            return facing;
        }

        static void Aim(PlayerController player, Vector3 direction)
        {
            player.FaceTowards(direction);
            player.LookYaw = Mathf.Atan2(direction.x, direction.z);
        }

        static IEnumerator Capture(QaContext context, string name)
        {
            string path = context.PathInRun(name + ".png"), error = null;
            if (File.Exists(path)) throw new QaFailException("Capture never overwrites existing evidence: " + name);
            yield return StoryCamera.CaptureScreen(path, result => error = result);
            if (error != null) throw new QaFailException("Screen capture failed: " + error);
            var image = new Texture2D(2, 2);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(path))) throw new QaFailException("Capture is not a readable PNG: " + name);
                var pixels = image.GetPixels32();
                int distinct = pixels.Where((_, i) => i % 97 == 0).Select(p => (p.r >> 3, p.g >> 3, p.b >> 3)).Distinct().Count();
                if (distinct < 24) throw new QaFailException("Capture looks blank: " + name);
            }
            finally { Object.Destroy(image); }
            context.AddCapture(name, path, "png", "screen");
        }

        sealed class LocalSeat : InputBinding
        {
            readonly string id;
            public PlayerCommands Next;

            public LocalSeat(string id) => this.id = id;

            public override string Id => id;

            public override void Read(ref PlayerCommands c)
            {
                c = Next;
                Next.grab = Next.attack = Next.jump = Next.dodge = Next.drop = Next.swap = false;
                Next.slot = 0;
                Next.spellDown = Next.spellUp = Next.up = Next.down = Next.start = false;
                Next.lookDelta = Vector2.zero;
            }

            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        sealed class Saved
        {
            static readonly string[] Strings = { MatchTally.CareerKey, MatchTally.CorruptKey, "wv.outfit.0", "wv.outfit.1", "wv.outfit.2", "wv.outfit.3" };
            static readonly string[] Integers = { ControlTips.EnabledKey, ControlTips.LearnedKey, CombatFeedbackOptions.DamageKey, CombatFeedbackOptions.PointsKey, "wv.muted" };

            public string Key { get; private set; }
            bool integer, existed;
            string text;
            int number;

            static Saved Read(string key, bool integer) => new Saved
            {
                Key = key, integer = integer, existed = PlayerPrefs.HasKey(key),
                text = integer ? null : PlayerPrefs.GetString(key), number = integer ? PlayerPrefs.GetInt(key) : 0,
            };

            public static Saved[] Capture() => Strings.Select(k => Read(k, false)).Concat(Integers.Select(k => Read(k, true))).ToArray();

            public bool Matches() => existed == PlayerPrefs.HasKey(Key) &&
                (!existed || (integer ? number == PlayerPrefs.GetInt(Key) : text == PlayerPrefs.GetString(Key)));

            public void Restore()
            {
                if (!existed) PlayerPrefs.DeleteKey(Key);
                else if (integer) PlayerPrefs.SetInt(Key, number);
                else PlayerPrefs.SetString(Key, text);
            }
        }
    }
}
#endif
