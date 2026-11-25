#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Capture;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.BossFight;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class BossFightQa : IQaScenario
    {
        public const string Scenario = "boss_fight";
        const string Fill = "Danger fill", ErrorShader = "Hidden/InternalErrorShader";
        const float BudgetSeconds = 240f, PhaseSlack = 15f, StandMetres = 1.4f, PunchSeconds = 3f;

        BossSeat seat;
        PlayerController player;
        BossFightDirector director;
        GameHud hud;
        RoomBuilder room;
        bool launched;
        InputBinding[] originalBindings;
        string originalMap;
        LobbyExperienceQa.Preference[] preferences;
        readonly List<string> errors = new();
        readonly List<(string Phase, float Elapsed, float Left)> clock = new();
        readonly Dictionary<string, float> phaseAt = new();

        public string Name => Scenario;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register(Scenario, () => new BossFightQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("boss_fight_open").Timeout(60).When(_ => Object.FindAnyObjectByType<LobbyMenu>() != null).Do(Open);
            yield return QaStep.Named("boss_fight_appear").Timeout(60).Do(Appear);
            yield return QaStep.Named("boss_fight_first_shield").Timeout(60).Do(FirstShield);
            yield return QaStep.Named("boss_fight_hit").Timeout(45).Do(Hit);
            yield return QaStep.Named("boss_fight_return").Timeout(45).Do(Return);
        }

        void Log(string condition, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert) errors.Add(condition);
        }

        IEnumerator Open(QaContext context)
        {
            if (Application.isBatchMode) throw new QaFailException("Boss Fight QA needs a normal window, not -batchmode.");
            if (!Session.CanLoad(Session.SceneForMode(HouseModes.BossFight))) throw new QaFailException("The Boss Fight scene is not in the build.");
            preferences = LobbyExperienceQa.Preference.Capture().Append(LobbyExperienceQa.Preference.Text(MatchTally.CorruptKey)).ToArray();
            originalBindings = Session.Bindings.ToArray();
            originalMap = Session.MapId;
            Application.logMessageReceived += Log;
            seat = new BossSeat("qa-boss-fight-1");
            Session.Bindings.Clear();
            Session.Remember(seat);
            UnityEngine.Random.InitState(context.Seed);
            launched = true;
            Session.LoadMode(HouseModes.BossFight, BossFightTuning.Map);
            PlayerJoinManager joins = null;
            yield return context.WaitUntil(() => (director = BossFightDirector.Instance) && director.Boss && director.Hazards
                && (joins = Object.FindAnyObjectByType<PlayerJoinManager>()) && (hud = Object.FindAnyObjectByType<GameHud>())
                && (player = joins.Players.FirstOrDefault(p => p && p.Binding == seat)), 30, "Boss Fight on Pinwheel to load");
            room = Object.FindAnyObjectByType<RoomBuilder>();
            yield return context.WaitUntil(() => director.Current == BossFightDirector.State.Preparing, 10, "the prepare phase");
            if (Match.Mode != HouseModes.BossFight || Session.MapId != BossFightTuning.Map)
                throw new QaFailException($"Expected {HouseModes.BossFight} on {BossFightTuning.Map}, got {Match.Mode} on {Session.MapId}.");
            hud.Follow(player);
            Mark("prepare");
            yield return context.WaitSeconds(1.5f);
            context.Note("boss_fight_start", new Dictionary<string, object> {
                ["mode"] = Match.Mode, ["map"] = Session.MapId, ["humans"] = joins.HumanCount,
                ["players"] = joins.Players.Count(p => p), ["bots"] = joins.Players.Count(p => p && p.Binding is BotBinding),
                ["round_limit_seconds"] = Match.Rules.RoundTimeLimitSeconds, ["prep_seconds"] = director.Tuning.PrepSeconds,
                ["appear_seconds"] = director.Tuning.AppearSeconds, ["stagger_seconds"] = director.Tuning.StaggerSeconds,
                ["boss_health"] = director.Boss.Health, ["boss_max_health"] = director.Boss.MaxHealth,
                ["boss_visible"] = BossVisible(), ["props"] = director.Props.Count(p => p),
                ["room"] = RoomAt(player.transform.position) ?? "none" });
            context.Note("boss_fight_scope", "Offline Windows Boss Fight on Pinwheel through Session.LoadMode with one scripted local seat. " +
                "Prepare and reveal run on their own timers. The first shield is broken by giving the seat the shield word's letters and spelling it on the real wheel; " +
                "the second and third shields are cracked through BossFightDirector.CrackShield, and the boss is finished with BossController.ApplyDamage after real punches. " +
                "No network, co-op, bot or human-feel claim.");
            Panel(context, "prepare");
            yield return Capture(context, "boss_fight_prepare");
        }

        IEnumerator Appear(QaContext context)
        {
            yield return context.WaitUntil(() => director.Current == BossFightDirector.State.Appear, director.Tuning.PrepSeconds + PhaseSlack, "the boss to appear");
            Mark("appear");
            yield return context.WaitSeconds(.8f);
            if (!BossVisible()) throw new QaFailException("The boss is not visible after it appears.");
            context.Note("boss_fight_appear", new Dictionary<string, object> {
                ["prepare_measured_seconds"] = phaseAt["appear"] - phaseAt["prepare"], ["boss_health"] = director.Boss.Health,
                ["layers"] = director.Boss.LayersRemaining, ["vulnerable"] = director.Boss.Vulnerable, ["boss_visible"] = true,
                ["boss_room"] = RoomAt(director.Boss.transform.position) ?? "none",
                ["boss_to_player_m"] = Vector3.Distance(director.Boss.transform.position, player.transform.position) });
            Panel(context, "appear");
            yield return Capture(context, "boss_fight_appear");
        }

        IEnumerator FirstShield(QaContext context)
        {
            yield return context.WaitUntil(() => director.Current == BossFightDirector.State.DropAndPrevent, director.Tuning.AppearSeconds + PhaseSlack, "the first shield word");
            Mark("shield_1");
            string word = director.ShieldWord;
            if (word != BossFightTuning.Words[0]) throw new QaFailException("The first shield word is " + word + ", not " + BossFightTuning.Words[0] + ".");
            yield return context.WaitUntil(() => Circles("warning").Length > 0, 15, "a red warning circle");
            Shaders(context, "warning");
            yield return Capture(context, "boss_fight_circle_warning");
            yield return context.WaitUntil(() => Circles("strike").Length > 0, director.Tuning.AoeWarnSeconds + 3f, "a warning circle to land");
            Shaders(context, "strike");
            yield return Capture(context, "boss_fight_circle_strike");
            Panel(context, "shield_1");
            yield return context.WaitUntil(() => player.CanAct && !player.IsKnockedOut && !player.Summoner.IsCrafting, 8, "the player to be ready to spell");
            int layers = director.Boss.LayersRemaining;
            player.Inventory.Set(word);
            seat.Next.spellHeld = true;
            seat.Next.spellDown = true;
            try
            {
                yield return context.WaitUntil(() => player.Summoner.IsSpelling, 3, "the spell wheel");
                int index = player.Summoner.Ready.FindIndex(w => w.word == word);
                if (index < 0) throw new QaFailException(word + " is not on the spell wheel.");
                player.Summoner.Select(index);
                yield return context.WaitSeconds(.4f);
                yield return Capture(context, "boss_fight_spell_wheel");
            }
            finally
            {
                seat.Next.spellHeld = false;
                seat.Next.spellUp = true;
            }
            yield return context.WaitUntil(() => director.Boss.LayersRemaining == layers - 1, 8, word + " to crack the first shield");
            Mark("stagger_1");
            context.Note("boss_fight_first_shield", new Dictionary<string, object> {
                ["word"] = word, ["layers_before"] = layers, ["layers_after"] = director.Boss.LayersRemaining,
                ["state"] = director.Current.ToString(), ["boss_health"] = director.Boss.Health,
                ["cracked_by"] = "spelling the word on the real wheel after the QA gave the seat its letters" });
            yield return new WaitForSecondsRealtime(.5f);
            Panel(context, "layer_cracked");
            yield return Capture(context, "boss_fight_layer_cracked");
        }

        IEnumerator Hit(QaContext context)
        {
            var boss = director.Boss;
            int forced = 0;
            for (int guard = 0; guard < BossFightTuning.Phases && !boss.Vulnerable; guard++)
            {
                yield return context.WaitUntil(() => director.Current == BossFightDirector.State.DropAndPrevent,
                    director.Tuning.StaggerSeconds + PhaseSlack, "the next shield word");
                Mark("shield_" + (director.Layer + 1));
                director.CrackShield();
                forced++;
            }
            yield return context.WaitUntil(() => director.Current == BossFightDirector.State.Hit, 5, "the shield to be down");
            Mark("hit");
            yield return new WaitForSecondsRealtime(1f);
            Panel(context, "hit");
            yield return Capture(context, "boss_fight_shield_down");
            float before = boss.Health;
            float end = Time.realtimeSinceStartup + PunchSeconds;
            try
            {
                while (boss.Health >= before && Time.realtimeSinceStartup < end)
                {
                    var at = boss.transform.position;
                    var to = World.Flat(at - player.transform.position);
                    if (to.magnitude > StandMetres) { seat.Next.move = new Vector2(to.x, to.z).normalized; }
                    else
                    {
                        seat.Next.move = Vector2.zero;
                        if (to.sqrMagnitude > 1e-4f) player.FaceTowards(to.normalized);
                        seat.Next.attack = true;
                    }
                    yield return null;
                }
            }
            finally { seat.Next = default; }
            float punched = before - boss.Health;
            boss.ApplyDamage(new HitInfo { Damage = boss.MaxHealth * 10f, Source = HitSource.Melee });
            yield return context.WaitUntil(() => hud.ResultShown && director.Current == BossFightDirector.State.Result, 8, "the result card");
            Mark("result");
            yield return new WaitForSecondsRealtime(.8f);
            bool steady = true;
            for (int i = 1; i < clock.Count; i++)
                steady &= clock[i].Left <= clock[i - 1].Left + .05f && clock[i].Elapsed >= clock[i - 1].Elapsed - .05f;
            context.Note("boss_fight_hit", new Dictionary<string, object> {
                ["shields_forced"] = forced, ["damage_from_punches"] = punched, ["health_before_punches"] = before,
                ["boss_health_end"] = boss.Health, ["won"] = director.Won, ["state"] = director.Current.ToString(),
                ["time_scale"] = Time.timeScale, ["result_shown"] = hud.ResultShown,
                ["finished_by"] = "BossController.ApplyDamage after up to " + PunchSeconds + " s of real punches" });
            context.Note("boss_fight_clock", new Dictionary<string, object> {
                ["phases"] = clock.Select(c => c.Phase).ToArray(),
                ["elapsed_s"] = clock.Select(c => Math.Round(c.Elapsed, 2)).ToArray(),
                ["time_left_s"] = clock.Select(c => Math.Round(c.Left, 2)).ToArray(),
                ["never_reset"] = steady });
            yield return Capture(context, "boss_fight_result");
            if (!steady) throw new QaFailException("The run clock went back between phases.");
            if (!director.Won) throw new QaFailException("The fight did not end in a win.");
            if (!Mathf.Approximately(Time.timeScale, 1f)) throw new QaFailException("Slow motion outlived the fight.");
        }

        IEnumerator Return(QaContext context)
        {
            Time.timeScale = 1f;
            if (seat != null) seat.Next = default;
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
            var changed = preferences == null ? Array.Empty<LobbyExperienceQa.Preference>() : preferences.Where(p => !p.Matches()).ToArray();
            foreach (var preference in changed) preference.Restore();
            if (changed.Length != 0) PlayerPrefs.Save();
            Application.logMessageReceived -= Log;
            float seconds = context.ElapsedSeconds;
            context.Note("boss_fight_prefs_restored", changed.Select(p => p.Key).ToArray());
            context.Note("boss_fight_errors", errors.ToArray());
            context.Note("boss_fight_seconds", seconds);
            if (errors.Count > 0) throw new QaFailException($"The fight logged {errors.Count} error(s); first: {errors[0]}");
            if (seconds > BudgetSeconds) throw new QaFailException($"The run took {seconds:0.0} s; the smoke budget is {BudgetSeconds:0} s.");
        }

        void Mark(string phase)
        {
            phaseAt[phase] = Time.realtimeSinceStartup;
            clock.Add((phase, director.Elapsed, director.TimeLeft));
        }

        bool BossVisible() => director.Boss.GetComponentsInChildren<Renderer>().Any(r => r && r.enabled && r.gameObject.activeInHierarchy);

        string RoomAt(Vector3 at) => room && room.Layout != null ? room.Layout.RoomAt(at.x, at.y + .1f, at.z) : null;

        Renderer[] Circles(string material) => director.Hazards.GetComponentsInChildren<Renderer>()
            .Where(r => r && r.name == Fill && r.sharedMaterial && r.sharedMaterial.name.IndexOf(material, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();

        void Shaders(QaContext context, string material)
        {
            var fills = Circles(material);
            var rings = director.Hazards.GetComponentsInChildren<LineRenderer>().Where(r => r && r.sharedMaterial).ToArray();
            var shaders = fills.Select(r => r.sharedMaterial.shader).Concat(rings.Select(r => r.sharedMaterial.shader)).ToArray();
            bool real = shaders.Length > 0 && shaders.All(s => s && s.isSupported && s.name != ErrorShader);
            context.Note("boss_fight_circle_" + material, new Dictionary<string, object> {
                ["circles"] = director.Hazards.ActiveCircleCount, ["fills"] = fills.Length, ["rings"] = rings.Length,
                ["fill_material"] = fills.Select(r => r.sharedMaterial.name).Distinct().ToArray(),
                ["fill_shader"] = fills.Select(r => r.sharedMaterial.shader ? r.sharedMaterial.shader.name : "missing").Distinct().ToArray(),
                ["ring_shader"] = rings.Select(r => r.sharedMaterial.shader ? r.sharedMaterial.shader.name : "missing").Distinct().ToArray(),
                ["real_shader"] = real, ["centre"] = director.Hazards.LastSpawnCentre.ToString("F2"),
                ["player_to_centre_m"] = Vector3.Distance(World.Flat(player.transform.position), World.Flat(director.Hazards.LastSpawnCentre)),
                ["player_health"] = player.Health ? player.Health.Current : -1f });
            if (!real) throw new QaFailException("A boss circle material did not resolve a real shader.");
        }

        void Panel(QaContext context, string phase)
        {
            var panel = director.Panel;
            context.Note("boss_fight_panel_" + phase, new Dictionary<string, object> {
                ["state"] = director.Current.ToString(), ["shown"] = panel && panel.Shown, ["title"] = panel ? panel.TitleText : "",
                ["tiles"] = panel ? panel.TileText : "", ["clue"] = panel ? panel.ClueText : "", ["health_bar"] = panel && panel.HealthShown,
                ["boss_health"] = director.Boss.Health, ["layers"] = director.Boss.LayersRemaining,
                ["elapsed_s"] = director.Elapsed, ["time_left_s"] = director.TimeLeft });
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
                context.Note(name + "_capture", new Dictionary<string, object> { ["width"] = image.width, ["height"] = image.height });
            }
            finally { Object.Destroy(image); }
            context.AddCapture(name, path, "png", "screen");
        }

        sealed class BossSeat : InputBinding
        {
            readonly string id;
            public PlayerCommands Next;

            public BossSeat(string id) => this.id = id;

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
    }
}
#endif
