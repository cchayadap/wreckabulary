#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Qa;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.BossFight;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class ModeGuidesQa : IQaScenario
    {
        public const string Scenario = "mode_guides";
        public const int Width = 1280, Height = 720;
        const string Map = "pinwheel";
        const float ModeSeconds = 95f, CourseSeconds = 160f, BossSeconds = 170f;
        static readonly string[] NotPictured = { ModeGuideBook.TeamRace, ModeGuideBook.BrawlRace, ModeGuideBook.ArenaFreeForAll };

        QaContext context;
        GuideSeat[] seats = Array.Empty<GuideSeat>();
        PlayerController p1, p2;
        PlayerJoinManager joins;
        GameHud hud;
        RoomBuilder room;
        int loads;
        bool launched;
        InputBinding[] originalBindings;
        string originalMap, originalQueue, originalMode, originalLobbyMap, originalPage, originalSolo, originalSoloMap;
        bool originalFamily;
        Func<float> originalRoll;
        LobbyExperienceQa.Preference[] preferences;
        Saved[] extras;
        readonly List<string> failed = new();
        readonly List<string> shots = new();
        readonly List<string> errors = new();

        public string Name => Scenario;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register(Scenario, () => new ModeGuidesQa());

        public IEnumerable<QaStep> BuildSteps(QaContext qa)
        {
            yield return QaStep.Named("mode_guides_open").Timeout(60)
                .When(_ => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning).Do(Setup);
            yield return QaStep.Named("mode_guides_" + ModeGuideBook.TimeTrial).Timeout(CourseSeconds).Do(c => Course(c, ModeGuideBook.TimeTrial));
            yield return QaStep.Named("mode_guides_" + ModeGuideBook.Freeplay).Timeout(CourseSeconds).Do(c => Course(c, ModeGuideBook.Freeplay));
            yield return House(HouseModes.Tutorial, null, 1, null, Tutorial);
            yield return House(HouseModes.Dibs, Map, 2, null, Dibs);
            yield return House(HouseModes.Duos, Map, 2, null, Duos);
            yield return House(HouseModes.FreeForAll, Map, 2, PrepareFreeForAll, FreeForAll);
            yield return House(HouseModes.War, Map, 2, PrepareWar, War);
            yield return House(HouseModes.MovingOut, Map, 2, null, MovingOut);
            yield return House(HouseModes.MovingDay, Map, 2, null, MovingDay);
            yield return House(HouseModes.Escape, Map, 2, null, Escape);
            yield return House(HouseModes.ShoppingList, Map, 2, null, ShoppingList);
            yield return House(HouseModes.RiddleRush, Map, 2, PrepareRiddleRush, RiddleRush);
            yield return House(HouseModes.BossFight, Map, 1, null, BossFight, BossSeconds);
            yield return QaStep.Named("mode_guides_return").Timeout(60).Do(Return);
        }

        QaStep House(string mode, string map, int seatCount, Func<IEnumerator> prepare, Func<int, string, IEnumerator> stage, float seconds = ModeSeconds) =>
            QaStep.Named("mode_guides_" + mode).Timeout(seconds).Do(c => Mode(mode, map, seatCount, prepare, stage));

        IEnumerator Setup(QaContext qa)
        {
            context = qa;
            preferences = LobbyExperienceQa.Preference.Capture();
            extras = Saved.Capture();
            originalBindings = Session.Bindings.ToArray();
            originalMap = Session.MapId;
            var menu = LobbyMenu.Instance;
            originalQueue = menu.Queue; originalMode = menu.Mode; originalLobbyMap = menu.Map; originalPage = menu.Current;
            originalSolo = WordboundSoloEntry.SelectedMode; originalSoloMap = WordboundSoloEntry.SelectedMap;
            originalFamily = menu.Page<PlayPage>().WordboundSolo;
            originalRoll = PlayerHealth.TraitRoll;
            var rolls = new System.Random(qa.Seed);
            PlayerHealth.TraitRoll = () => (float)rolls.NextDouble();
            if (!GameplayPrefs.DamageNumbers) GameplayPrefs.DamageNumbers = true;
            if (!GameplayPrefs.EffectWords) GameplayPrefs.EffectWords = true;
            if (GameplayPrefs.FurnitureNames) GameplayPrefs.FurnitureNames = false;
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += Loaded;
            qa.Note("mode_guides_screen", new Dictionary<string, object> { ["width"] = Screen.width, ["height"] = Screen.height, ["saved"] = Width + "x" + Height });
            qa.Note("mode_guides_scope", "Offline Windows House modes through Session.LoadMode with scripted local seats and the game's own AI seats parked, " +
                "plus both solo courses through the lobby's solo route. Each picture stages one guide step with real game objects; " +
                "some moments are shortened (instant breaks, direct knockouts, packed keepsakes, ticked list items). No network or human-feel claim. " +
                "Not pictured, because they need a live room, so their cards show item art: " +
                string.Join(", ", NotPictured.Select(id => ModeGuideBook.Name(id) + " [" + id + "]")) + ".");
            yield return null;
        }

        void Log(string condition, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert) errors.Add(condition);
        }

        void Loaded(Scene scene, LoadSceneMode mode) => loads++;

        IEnumerator Course(QaContext qa, string mode)
        {
            failed.Clear();
            yield return Home();
            var menu = LobbyMenu.Instance;
            if (!menu) throw new QaFailException("The lobby is not open.");
            if (menu.PartySize != 1) throw new QaFailException("Courses are solo; the lobby party has " + menu.PartySize + " seats.");
            if (!Session.CanLoad(Session.WordboundScene)) throw new QaFailException("Courses are not in this build.");
            PlayerPrefs.SetInt(WordboundMatchHud.IntroSeenKey, 1);
            WordboundSoloEntry.Select(mode);
            menu.OpenWordboundSolo(mode);
            WordboundLocalMatchClient client = null;
            yield return Until(() =>
            {
                client = Object.FindFirstObjectByType<WordboundLocalMatchClient>();
                if (client && client.Gameplay && client.Gameplay.Fault != "") throw new QaFailException("The course faulted: " + client.Gameplay.Fault);
                return client && client.Gameplay && client.Gameplay.IsPresentationReady && !client.Gameplay.IsSimulationPaused;
            }, 80, "the " + mode + " course");
            var view = client.GetComponent<WordboundPlayerView>();
            if (!view) throw new QaFailException("The course has no player view.");
            view.SetMenuOpen(false);
            yield return Realtime(3f);
            var steps = ModeGuideBook.For(mode).Steps;
            bool menuSecond = mode == ModeGuideBook.TimeTrial;
            for (int i = 0; i < steps.Count; i++)
            {
                bool menuShot = menuSecond ? i == 1 : i == steps.Count - 1;
                yield return Attempt(steps[i].Image, CourseShot(view, steps[i].Image, menuShot));
            }
            view.SetMenuOpen(false);
            client.SendMessage("RequestReturn", SendMessageOptions.RequireReceiver);
            yield return Until(() => SceneManager.GetActiveScene().name == Session.HubScene && LobbyMenu.Instance, 45, "the Hub after " + mode);
            yield return Realtime(1f);
            Report(mode);
        }

        IEnumerator CourseShot(WordboundPlayerView view, string image, bool menu)
        {
            view.SetMenuOpen(menu);
            yield return Realtime(menu ? .6f : 2f);
            yield return Shot(image);
            view.SetMenuOpen(false);
        }

        IEnumerator Home()
        {
            if (SceneManager.GetActiveScene().name == Session.HubScene && LobbyMenu.Instance) yield break;
            var client = Object.FindFirstObjectByType<WordboundLocalMatchClient>();
            if (client) _ = client.ReturnToMenu();
            else Session.GoHome();
            yield return Until(() => SceneManager.GetActiveScene().name == Session.HubScene && LobbyMenu.Instance, 45, "the Hub");
            yield return Realtime(1f);
        }

        IEnumerator Mode(string mode, string map, int seatCount, Func<IEnumerator> prepare, Func<int, string, IEnumerator> stage)
        {
            failed.Clear();
            yield return Launch(mode, map, seatCount);
            yield return Until(() => Playing(mode), 30, HouseModes.Name(mode) + " to start");
            Settle();
            if (prepare != null)
            {
                yield return prepare();
                Settle();
            }
            var steps = ModeGuideBook.For(mode).Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                Time.timeScale = 1f;
                yield return Attempt(steps[i].Image, stage(i, steps[i].Image));
            }
            Report(mode);
        }

        void Report(string mode)
        {
            context.Note("mode_guides_" + mode, new Dictionary<string, object>
            {
                ["pictures"] = ModeGuideBook.For(mode).Steps.Select(s => s.Image).Where(shots.Contains).ToArray(),
                ["failed"] = failed.ToArray(),
            });
            if (failed.Count > 0) throw new QaFailException(mode + ": " + string.Join(" | ", failed));
        }

        IEnumerator Launch(string mode, string map, int seatCount)
        {
            Time.timeScale = 1f;
            seats = Enumerable.Range(1, seatCount).Select(i => new GuideSeat("qa-mode-guides-" + i)).ToArray();
            Session.Bindings.Clear();
            foreach (var seat in seats) Session.Remember(seat);
            UnityEngine.Random.InitState(context.Seed);
            if (mode == HouseModes.RiddleRush) RiddleRushDirector.SeedOverride = (ulong)context.Seed;
            int before = loads;
            string scene = Session.SceneForMode(mode);
            if (!Session.CanLoad(scene)) throw new QaFailException(scene + " is not in the build.");
            launched = true;
            p1 = p2 = null;
            Session.LoadMode(mode, map);
            yield return Until(() => loads > before && SceneManager.GetActiveScene().name == scene
                && (joins = Object.FindAnyObjectByType<PlayerJoinManager>()) && (hud = Object.FindAnyObjectByType<GameHud>())
                && (p1 = joins.Players.FirstOrDefault(p => p && p.Binding == seats[0])), 30, HouseModes.Name(mode) + " to load");
            room = Object.FindAnyObjectByType<RoomBuilder>();
            if (seats.Length > 1) yield return Until(() => p2 = joins.Players.FirstOrDefault(p => p && p.Binding == seats[1]), 5, "P2 to join");
            hud.Follow(p1);
        }

        static bool Playing(string mode) => mode switch
        {
            HouseModes.Tutorial => Object.FindAnyObjectByType<TutorialDirector>() != null,
            HouseModes.MovingOut => Object.FindAnyObjectByType<MovingOutDirector>() is { Current: MovingOutDirector.State.Playing },
            HouseModes.MovingDay => Object.FindAnyObjectByType<MovingDayDirector>() is { Current: MovingDayDirector.State.Playing },
            HouseModes.Escape => Object.FindAnyObjectByType<EscapeDirector>() is { Current: EscapeDirector.State.Playing },
            HouseModes.BossFight => Object.FindAnyObjectByType<BossFightDirector>() is { Current: BossFightDirector.State.Preparing, Boss: not null },
            _ => RoundManager.Instance && RoundManager.Instance.Phase == Phase.Playing,
        };

        IReadOnlyList<PlayerController> Players => joins ? joins.Players.Where(p => p).ToList() : new List<PlayerController>();

        IEnumerable<PlayerController> Bots => Players.Where(p => p.Binding is BotBinding);

        void Settle()
        {
            foreach (var p in Players)
            {
                var rules = Match.Rules.Clone();
                rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
                if (p.Binding is not BotBinding bot) continue;
                bot.Commands = default;
                p.Frozen = true;
                if (p.TryGetComponent<BotController>(out var brain)) brain.enabled = false;
            }
        }

        IEnumerator Tutorial(int step, string image)
        {
            var tutorial = Object.FindAnyObjectByType<TutorialDirector>();
            var seat = seats[0];
            switch (step)
            {
                case 0:
                    seat.Next.move = new Vector2(1f, 0f);
                    yield return Until(() => tutorial.StepIndex >= 1, 4, "the walk step");
                    seat.Next.move = Vector2.zero;
                    seat.Next.jump = true;
                    yield return Realtime(.8f);
                    seat.Next.dodge = true;
                    yield return Until(() => tutorial.StepIndex >= 2, 3, "the jump and dodge step");
                    yield return Until(() => Standing(TutorialDirector.FirstFurniture), 3, "the TABLE");
                    yield return Smash(image, Standing(TutorialDirector.FirstFurniture));
                    break;
                case 1:
                    yield return Gather(seat, p1, 1.8f);
                    yield return Shot(image);
                    break;
                case 2:
                    p1.Inventory.Set("BATLE");
                    yield return Realtime(.2f);
                    if (tutorial.StepIndex == 4) yield return Book();
                    yield return Spell(image, seat, p1, TutorialDirector.FirstWord, "LE");
                    break;
                default:
                    Give(p1, TutorialDirector.FirstWord);
                    if (!tutorial.Dummy) throw new QaFailException("The tutorial has no dummy.");
                    yield return Hit(image, seat, p1, tutorial.Dummy, TutorialDirector.FirstWord);
                    break;
            }
        }

        IEnumerator Book()
        {
            var link = hud.transform.Find("Safe HUD/Letter bag/Bag link");
            if (!link || !link.TryGetComponent(out Button button)) yield break;
            button.onClick.Invoke();
            yield return Until(() => hud.BagOpen, 3, "the recipe book");
            yield return Realtime(.3f);
            button.onClick.Invoke();
            yield return Until(() => !hud.BagOpen, 3, "the recipe book to close");
        }

        IEnumerator Dibs(int step, string image)
        {
            switch (step)
            {
                case 0: yield return Smash(image, Furniture(TutorialDirector.FirstFurniture)); break;
                case 1: yield return Spell(image, seats[0], p1, TutorialDirector.FirstWord, "LESO"); break;
                case 2:
                    Give(p1, TutorialDirector.FirstWord);
                    yield return Hit(image, seats[0], p1, p2, TutorialDirector.FirstWord);
                    break;
                default:
                    foreach (var p in Players) if (p != p1 && !p.IsEliminated) p.Health.Eliminate();
                    yield return Result(image);
                    break;
            }
        }

        IEnumerator Duos(int step, string image)
        {
            var partner = Players.FirstOrDefault(p => p != p1 && p.Team == p1.Team);
            var rivals = Players.Where(p => p != p1 && p != partner).ToArray();
            if (!partner) throw new QaFailException("P1 has no Duos partner.");
            switch (step)
            {
                case 0:
                {
                    var centre = p1.transform.position;
                    Stand(partner, FreeSpot(centre, 1.3f), centre);
                    foreach (var rival in rivals) Stand(rival, FreeSpot(centre, 3.4f), centre);
                    Give(p1, TutorialDirector.FirstWord);
                    if (rivals.Length > 0) Aim(p1, World.Flat(rivals[0].transform.position - centre).normalized);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    break;
                }
                case 1:
                {
                    Knock(partner, rivals.FirstOrDefault());
                    yield return Until(() => partner.Health.IsDowned, 2, "the partner to be downed");
                    var at = partner.transform.position;
                    Stand(p1, FreeSpot(at, .9f), at);
                    yield return Realtime(.2f);
                    seats[0].Next.grabHeld = true;
                    p1.Combat.TryRevive();
                    try
                    {
                        yield return Realtime(1.1f);
                        yield return Shot(image);
                    }
                    finally { seats[0].Next.grabHeld = false; }
                    break;
                }
                default:
                    foreach (var rival in rivals) if (!rival.IsEliminated) rival.Health.Eliminate();
                    yield return Result(image);
                    break;
            }
        }

        IEnumerator PrepareFreeForAll()
        {
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            if (!director) throw new QaFailException("Free-for-All has no director.");
            director.Target = 2;
            director.RespawnSeconds = 1.5f;
            director.CountdownSeconds = .5f;
            director.StartMatch();
            yield return Until(() => director.Current == HouseRoundDirector.Stage.Playing, 8, "Free-for-All to restart");
        }

        IEnumerator FreeForAll(int step, string image)
        {
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            if (!director) throw new QaFailException("Free-for-All has no director.");
            switch (step)
            {
                case 0:
                {
                    var centre = p1.transform.position;
                    Give(p1, TutorialDirector.FirstWord);
                    Stand(p2, FreeSpot(centre, 1.2f), centre);
                    foreach (var bot in Bots) Stand(bot, FreeSpot(centre, 2.8f), centre);
                    Aim(p1, World.Flat(p2.transform.position - centre).normalized);
                    yield return Realtime(.4f);
                    p1.PlayPunch();
                    yield return Realtime(.15f);
                    yield return Shot(image);
                    break;
                }
                case 1:
                {
                    var at = p1.transform.position;
                    Stand(p2, FreeSpot(at, 1f), at);
                    Aim(p1, World.Flat(p2.transform.position - at).normalized);
                    p1.PlayPunch();
                    Knock(p2, p1);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    break;
                }
                case 2:
                    yield return Until(() => !p2.IsKnockedOut, 6, "the knockout to come back");
                    yield return Realtime(.5f);
                    yield return Shot(image);
                    break;
                default:
                {
                    var victim = Bots.FirstOrDefault(b => !b.IsKnockedOut) ?? p2;
                    var at = p1.transform.position;
                    Stand(victim, FreeSpot(at, 1f), at);
                    p1.PlayPunch();
                    Knock(victim, p1);
                    yield return Result(image);
                    break;
                }
            }
        }

        IEnumerator PrepareWar()
        {
            var director = Object.FindAnyObjectByType<WarDirector>();
            if (!director) throw new QaFailException("War has no director.");
            director.HoldSeconds = 1.5f;
            director.RespawnSeconds = .4f;
            director.CountdownSeconds = .5f;
            director.StartMatch();
            yield return Until(() => director.Current == HouseRoundDirector.Stage.Playing, 8, "War to restart");
        }

        IEnumerator War(int step, string image)
        {
            var director = Object.FindAnyObjectByType<WarDirector>();
            if (!director) throw new QaFailException("War has no director.");
            switch (step)
            {
                case 0:
                    yield return Realtime(1f);
                    yield return Shot(image);
                    break;
                case 1:
                {
                    var mate = Players.FirstOrDefault(p => p != p1 && p.Team == p1.Team && !p.IsKnockedOut);
                    var foe = Players.FirstOrDefault(p => p.Team != p1.Team && !p.IsKnockedOut);
                    if (!mate || !foe) throw new QaFailException("War needs a teammate and a foe standing.");
                    var at = p1.transform.position;
                    Stand(mate, FreeSpot(at, 1.4f), at);
                    Stand(foe, FreeSpot(mate.transform.position, 1f), mate.transform.position);
                    foe.PlayPunch();
                    Knock(mate, foe);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    break;
                }
                case 2:
                {
                    string hold = director.HoldRoom;
                    var box = hold != null && room ? room.Layout.Room(hold) : null;
                    if (box == null) throw new QaFailException("War has no middle room to hold.");
                    var centre = new Vector3((box.MinX + box.MaxX) * .5f, box.FloorY, (box.MinZ + box.MaxZ) * .5f);
                    foreach (var p in Players)
                        if (p != p1 && p.Team != p1.Team && RoomAt(p.transform.position) == hold) joins.Place(p, director.HomeSpawn(p));
                    var spot = FreeSpot(centre, .6f);
                    Stand(p1, spot, spot + Vector3.forward);
                    if (p2 && p2.Team == p1.Team) Stand(p2, FreeSpot(spot, 1.4f), spot);
                    yield return Until(() => hud.ToastText.Contains("+1 life"), 6, "the middle room to earn a life");
                    yield return Realtime(.2f);
                    yield return Shot(image);
                    break;
                }
                default:
                {
                    float end = Time.realtimeSinceStartup + 30f;
                    while (!hud.ResultShown)
                    {
                        if (Time.realtimeSinceStartup > end) throw new QaFailException("War did not end after knocking out the other team.");
                        foreach (var foe in Players)
                            if (foe.Team != p1.Team && !foe.IsKnockedOut) Knock(foe, p1);
                        yield return Realtime(.2f);
                    }
                    yield return Realtime(.8f);
                    yield return Shot(image);
                    break;
                }
            }
        }

        IEnumerator MovingOut(int step, string image)
        {
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            if (!director) throw new QaFailException("Moving Out has no director.");
            var waiting = Object.FindObjectsByType<Keepsake>().Where(k => k && !k.Packed)
                .OrderBy(k => (k.transform.position - p1.transform.position).sqrMagnitude).ToList();
            var van = director.ExtractionPoint;
            switch (step)
            {
                case 0:
                {
                    if (waiting.Count == 0) throw new QaFailException("Moving Out has no keepsakes.");
                    var at = waiting[0].transform.position;
                    Stand(p1, FreeSpot(at, 1.4f), at);
                    Stand(p2, FreeSpot(p1.transform.position, 1.4f), at);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    break;
                }
                case 1:
                {
                    if (waiting.Count == 0) throw new QaFailException("Moving Out has no keepsake left to carry.");
                    var keepsake = waiting[0];
                    var body = keepsake.GetComponent<Rigidbody>();
                    Stand(p1, FreeSpot(keepsake.transform.position, 1.1f), keepsake.transform.position);
                    yield return Realtime(.3f);
                    bool carried = p1.Combat.TryGrab() && p1.Combat.Held == body;
                    if (!carried && p1.Combat.IsHolding) p1.Combat.Drop();
                    var edge = FreeSpot(van, 3.1f, false, p1.transform.position);
                    if (carried) { p1.Body.position = edge; p1.transform.position = edge; Aim(p1, World.Flat(van - edge).normalized); }
                    else { Stand(p1, edge, van); Move(body, van + World.Flat(edge - van).normalized * 1.2f + Vector3.up * .6f); }
                    Stand(p2, FreeSpot(edge, 1.4f, false), van);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    if (carried) p1.Combat.Drop();
                    Move(body, van + Vector3.up * .6f);
                    yield return Until(() => keepsake.Packed, 3, "the keepsake to be packed");
                    break;
                }
                default:
                {
                    foreach (var keepsake in waiting)
                    {
                        Move(keepsake.GetComponent<Rigidbody>(), van + Vector3.up * .6f);
                        yield return Until(() => keepsake.Packed, 3, keepsake.Word + " to be packed");
                    }
                    Stand(p1, FreeSpot(van, .9f, false), van + Vector3.forward);
                    Stand(p2, FreeSpot(van, 1.3f, false), van);
                    yield return Result(image);
                    break;
                }
            }
        }

        IEnumerator MovingDay(int step, string image)
        {
            var director = Object.FindAnyObjectByType<MovingDayDirector>();
            if (!director) throw new QaFailException("Moving Day has no director.");
            switch (step)
            {
                case 0:
                    yield return Realtime(1.2f);
                    yield return Shot(image);
                    break;
                case 1:
                {
                    Smashable box = null;
                    yield return Until(() => box = Boxes().FirstOrDefault(), 8, "a delivery box");
                    yield return Realtime(.6f);
                    box = Boxes().OrderBy(b => (b.transform.position - p1.transform.position).sqrMagnitude).First();
                    yield return Smash(image, box);
                    break;
                }
                case 2:
                    if (director.Remaining.Count == 0) throw new QaFailException("Moving Day has an empty checklist.");
                    yield return Spell(image, seats[0], p1, director.Remaining[0].word.ToUpperInvariant());
                    break;
                default:
                {
                    if (director.Remaining.Count == 0) throw new QaFailException("Moving Day has an empty checklist.");
                    var item = director.Remaining[0];
                    string word = item.word.ToUpperInvariant();
                    Smashable piece = null;
                    yield return Until(() => piece = Object.FindObjectsByType<Smashable>()
                        .FirstOrDefault(s => s && s.Word == word && !s.Invulnerable && s.GetComponent<LetterBuilt>()), 5, word + " to be spelled");
                    if (p1.Combat.Held == piece.GetComponent<Rigidbody>()) p1.Combat.Drop();
                    var centre = director.RoomNamed(item.room).Centre;
                    Move(piece.GetComponent<Rigidbody>(), centre + Vector3.up * .4f);
                    Stand(p1, FreeSpot(centre, 1.7f, false, p1.transform.position), centre);
                    Stand(p2, FreeSpot(p1.transform.position, 1.3f, false), centre);
                    yield return Until(() => !director.Remaining.Contains(item), 5, word + " to be placed");
                    yield return Realtime(.3f);
                    yield return Shot(image);
                    break;
                }
            }
        }

        static IEnumerable<Smashable> Boxes() =>
            Object.FindObjectsByType<Smashable>().Where(s => s && !s.IsBroken && s.name.StartsWith("Box (", StringComparison.Ordinal));

        IEnumerator Escape(int step, string image)
        {
            var director = Object.FindAnyObjectByType<EscapeDirector>();
            if (!director) throw new QaFailException("Escape has no director.");
            switch (step)
            {
                case 0:
                {
                    var door = Object.FindObjectsByType<Collider>()
                        .Where(c => c && c.enabled && c.name == "Barred door")
                        .OrderBy(c => (c.bounds.center - p1.transform.position).sqrMagnitude).FirstOrDefault();
                    if (!door) throw new QaFailException("Escape has no barred door.");
                    var centre = door.bounds.center;
                    centre.y = door.bounds.min.y + .1f;
                    var spot = FreeSpot(centre, 2.1f, false, p1.transform.position);
                    Stand(p1, spot, centre);
                    Stand(p2, FreeSpot(spot, 1.3f, false), centre);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    break;
                }
                case 1:
                {
                    var keys = Keys();
                    if (keys.Count == 0) throw new QaFailException("Escape has no keys.");
                    var key = keys.OrderBy(k => (k.position - p1.transform.position).sqrMagnitude).First();
                    var spot = FreeSpot(key.position, 2.1f, true, p1.transform.position);
                    Stand(p1, spot, key.position);
                    Stand(p2, FreeSpot(spot, 1.3f), key.position);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    foreach (var other in Keys())
                    {
                        var at = other.position;
                        Ground(ref at);
                        p1.Respawn(at);
                        yield return Realtime(.3f);
                    }
                    break;
                }
                default:
                {
                    yield return Until(() => director.ExitOpen, 4, "the exit to open");
                    var exit = director.ExtractionPoint;
                    var spot = FreeSpot(exit, 3.2f, false, p1.transform.position);
                    if (director.AtExit(spot)) throw new QaFailException("No spot near the exit is outside it.");
                    Stand(p1, spot, exit);
                    Stand(p2, FreeSpot(spot, 1.3f, false), exit);
                    yield return Realtime(1f);
                    yield return Shot(image);
                    break;
                }
            }
        }

        static List<Transform> Keys() =>
            Object.FindObjectsByType<Transform>().Where(t => t && t.name == "Van key" && t.gameObject.activeInHierarchy).ToList();

        IEnumerator ShoppingList(int step, string image)
        {
            var director = Object.FindAnyObjectByType<ShoppingListDirector>();
            if (!director || director.Race == null) throw new QaFailException("Shopping List has no race.");
            var race = director.Race;
            int seat = p1.Index;
            switch (step)
            {
                case 0:
                    yield return Until(() => director.ShowsList, 3, "the shopping list");
                    yield return Realtime(.8f);
                    yield return Shot(image);
                    break;
                case 1:
                {
                    string word = race.Wanted(seat) ?? throw new QaFailException("P1 has nothing on the list.");
                    yield return Spell(image, seats[0], p1, word);
                    break;
                }
                case 2:
                {
                    var item = race.Current(seat) ?? throw new QaFailException("P1 has nothing on the list.");
                    if (!race.CurrentSpelled(seat)) Give(p1, item.Word);
                    int done = race.Done(seat);
                    var victim = Bots.FirstOrDefault(b => !b.IsKnockedOut);
                    if (item.Kind == ShoppingKind.Weapon && victim)
                        yield return Hit(null, seats[0], p1, victim, item.Word);
                    else if (item.Kind == ShoppingKind.Deployable) p1.Combat.DeployHeld();
                    yield return Realtime(.5f);
                    if (race.Done(seat) == done) race.Count(seat, item.Word, item.Act, director.Elapsed);
                    yield return Realtime(.3f);
                    yield return Shot(image);
                    break;
                }
                default:
                {
                    for (int guard = 0; guard < 12 && race.Winner == null; guard++)
                    {
                        var next = race.Current(seat);
                        if (next == null) break;
                        race.Spelled(seat, next.Value.Word);
                        race.Count(seat, next.Value.Word, next.Value.Act, director.Elapsed);
                    }
                    yield return Result(image);
                    break;
                }
            }
        }

        IEnumerator PrepareRiddleRush()
        {
            var director = Object.FindAnyObjectByType<RiddleRushDirector>();
            if (!director) throw new QaFailException("Riddle Rush has no director.");
            director.HintSeconds = 1.5f;
            director.BeatSeconds = .3f;
            director.CountdownSeconds = .5f;
            director.StartMatch();
            yield return Until(() => director.Playing && director.Race?.Current != null, 8, "Riddle Rush to restart");
        }

        IEnumerator RiddleRush(int step, string image)
        {
            var director = Object.FindAnyObjectByType<RiddleRushDirector>();
            if (!director || director.Race == null) throw new QaFailException("Riddle Rush has no race.");
            switch (step)
            {
                case 0:
                    yield return Until(() => director.Panel && director.Panel.Shown, 3, "the riddle");
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    break;
                case 1:
                {
                    yield return Until(() => director.Pulsing.Count > 0, 5, "the hint glow");
                    var glowing = director.Pulsing.Where(s => s).OrderBy(s => (s.transform.position - p1.transform.position).sqrMagnitude).First();
                    var at = glowing.transform.position;
                    Stand(p1, FreeSpot(at, 1.5f), at);
                    Stand(p2, FreeSpot(p1.transform.position, 1.3f), at);
                    yield return Realtime(.6f);
                    yield return Shot(image);
                    break;
                }
                case 2:
                {
                    var riddle = director.Race.Current ?? throw new QaFailException("No riddle is being asked.");
                    ClearHands(p1);
                    yield return Spell(image, seats[0], p1, riddle.Answer);
                    break;
                }
                default:
                {
                    float end = Time.realtimeSinceStartup + 25f;
                    while (!hud.ResultShown)
                    {
                        if (Time.realtimeSinceStartup > end) throw new QaFailException("Riddle Rush did not reach the target.");
                        var riddle = director.Race?.Current;
                        if (riddle != null && director.Race.Stage == RiddleStage.Asking && !p1.Summoner.IsCrafting)
                        {
                            ClearHands(p1);
                            p1.Inventory.Set(riddle.Answer);
                            p1.Summoner.Summon(riddle.Answer);
                        }
                        yield return Realtime(.2f);
                    }
                    yield return Realtime(.8f);
                    yield return Shot(image);
                    break;
                }
            }
        }

        IEnumerator BossFight(int step, string image)
        {
            var director = Object.FindAnyObjectByType<BossFightDirector>();
            if (!director || !director.Boss) throw new QaFailException("Boss Fight has no boss.");
            var boss = director.Boss;
            var words = new BossNameCatalogue(Match.Rules);
            switch (step)
            {
                case 0:
                    yield return Smash(image, Furniture("TABLE"));
                    break;
                case 1:
                    yield return Until(() => director.Current == BossFightDirector.State.DropAndPrevent, 40, "the boss to wake");
                    yield return Realtime(1f);
                    Stand(p1, FreeSpot(boss.transform.position, 3.2f, false, p1.transform.position), boss.Centre);
                    yield return Spell(image, seats[0], p1, words.WordFor(0));
                    break;
                case 2:
                {
                    var circles = Object.FindAnyObjectByType<TelegraphedAoE>();
                    if (!circles) throw new QaFailException("Boss Fight has no red circles.");
                    yield return Until(() => director.Current == BossFightDirector.State.DropAndPrevent && circles.ActiveCircleCount > 0, 30, "a red circle");
                    var at = circles.LastSpawnCentre;
                    Stand(p1, FreeSpot(at, 2.4f, false, p1.transform.position), at);
                    yield return Realtime(.5f);
                    yield return Shot(image);
                    break;
                }
                default:
                {
                    for (int layer = words.LayerCount - boss.LayersRemaining; layer < words.LayerCount; layer++)
                    {
                        yield return Until(() => director.Current == BossFightDirector.State.DropAndPrevent, 20, "shield layer " + (layer + 1));
                        string word = words.WordFor(layer);
                        p1.Inventory.Set(word);
                        if (!p1.Summoner.Summon(word)) throw new QaFailException(word + " did not crack a shield.");
                    }
                    yield return Until(() => director.Current == BossFightDirector.State.Hit && Time.timeScale >= 1f, 20, "the shield to break");
                    yield return Realtime(1.5f);
                    Give(p1, "HAMMER");
                    Stand(p1, FreeSpot(boss.transform.position, 1.6f, false, p1.transform.position), boss.Centre);
                    float before = boss.Health;
                    yield return Realtime(.3f);
                    float end = Time.realtimeSinceStartup + 1.5f;
                    try
                    {
                        while (boss.Health >= before && Time.realtimeSinceStartup < end)
                        {
                            Aim(p1, World.Flat(boss.transform.position - p1.transform.position).normalized);
                            seats[0].Next.attack = true;
                            yield return null;
                        }
                        if (boss.Health >= before) p1.PlayPunch();
                        yield return null;
                        Time.timeScale = 0f;
                        yield return Shot(image);
                    }
                    finally
                    {
                        Time.timeScale = 1f;
                        seats[0].Next = default;
                    }
                    break;
                }
            }
        }

        IEnumerator Smash(string image, Smashable target)
        {
            if (!target) throw new QaFailException("There is no furniture to smash.");
            var at = target.transform.position;
            var spot = FreeSpot(at, 1.3f, true, p1.transform.position);
            Stand(p1, spot, at);
            if (p2) Stand(p2, FreeSpot(spot, 1.4f), at);
            yield return Realtime(.5f);
            p1.PlayPunch();
            yield return Realtime(.12f);
            target.Break();
            yield return Realtime(.9f);
            yield return Shot(image);
        }

        static IEnumerator Gather(GuideSeat seat, PlayerController p, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            try
            {
                while (Time.realtimeSinceStartup < end && TilePool.Instance)
                {
                    var next = TilePool.Instance.Active.Where(t => t && t.gameObject.activeInHierarchy)
                        .OrderBy(t => (t.transform.position - p.transform.position).sqrMagnitude).FirstOrDefault();
                    if (!next) break;
                    var to = World.Flat(next.transform.position - p.transform.position);
                    seat.Next.move = to.sqrMagnitude > 1e-4f ? new Vector2(to.x, to.z).normalized : Vector2.zero;
                    yield return null;
                }
            }
            finally { seat.Next.move = Vector2.zero; }
        }

        IEnumerator Spell(string image, GuideSeat seat, PlayerController p, string word, string extra = "")
        {
            string letters = word + extra;
            if (letters.Length > p.Inventory.Capacity) letters = letters.Substring(0, Mathf.Max(word.Length, p.Inventory.Capacity));
            p.Inventory.Set(letters);
            yield return Until(() => p.CanAct && !p.IsDodging && !p.Summoner.IsCrafting, 3, p.Name + " to be ready to spell");
            seat.Next.spellHeld = true;
            seat.Next.spellDown = true;
            try
            {
                yield return Until(() => p.Summoner.IsSpelling, 3, "the spell wheel");
                int index = p.Summoner.Ready.FindIndex(w => w.word == word);
                if (index < 0) throw new QaFailException(word + " is not on the spell wheel.");
                p.Summoner.Select(index);
                yield return Realtime(.4f);
                yield return Shot(image);
            }
            finally
            {
                seat.Next.spellHeld = false;
                seat.Next.spellUp = true;
            }
            yield return Until(() => !p.Summoner.IsSpelling && !p.Summoner.IsCrafting, 5, word + " to be spelled");
        }

        IEnumerator Hit(string image, GuideSeat seat, PlayerController attacker, PlayerController victim, string item)
        {
            if (!attacker || !victim) throw new QaFailException("A hit needs two players.");
            var from = attacker.transform.position;
            var spot = FreeSpot(from, 1f, true, from + attacker.transform.forward);
            Stand(victim, spot, from);
            var facing = World.Flat(spot - from).normalized;
            Aim(attacker, facing);
            float dealt = 0f;
            Action<PlayerHealth, HitInfo, HitResult> saw = (_, _, r) => { if (r.Damage > 0f) dealt += r.Damage; };
            victim.Health.Damaged += saw;
            try
            {
                yield return Realtime(.3f);
                float end = Time.realtimeSinceStartup + 1.2f;
                while (dealt <= 0f && Time.realtimeSinceStartup < end)
                {
                    Aim(attacker, facing);
                    seat.Next.attack = true;
                    yield return null;
                }
                if (dealt <= 0f)
                {
                    attacker.PlayPunch();
                    Strike(attacker, victim, 18f, item);
                }
                if (image == null) yield break;
                yield return null;
                Time.timeScale = 0f;
                yield return Shot(image);
            }
            finally
            {
                Time.timeScale = 1f;
                if (victim) victim.Health.Damaged -= saw;
                seat.Next = default;
            }
        }

        IEnumerator Result(string image)
        {
            yield return Until(() => hud && hud.ResultShown, 10, "the result card");
            yield return Realtime(.8f);
            yield return Shot(image);
        }

        static Smashable Standing(string word) => Object.FindObjectsByType<Smashable>().FirstOrDefault(s => s && s.Word == word && !s.IsBroken);

        Smashable Furniture(string prefer)
        {
            var from = p1.transform.position;
            return Object.FindObjectsByType<Smashable>()
                .Where(s => s && !s.IsBroken && !s.Invulnerable && s.isActiveAndEnabled && !s.GetComponent<Keepsake>()
                    && !s.name.StartsWith("Box (", StringComparison.Ordinal) && Mathf.Abs(s.transform.position.y - from.y) < 1.2f)
                .OrderBy(s => s.Word == prefer ? 0 : 1)
                .ThenBy(s => Mathf.Round(World.Flat(s.transform.position - from).magnitude))
                .ThenBy(s => s.Word, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        static void Give(PlayerController p, string word)
        {
            if (p.Combat.Weapon && p.Combat.Weapon.word == word) return;
            ClearHands(p);
            p.Inventory.Set(word);
            if (!p.Summoner.Summon(word)) throw new QaFailException(p.Name + " could not hold " + word + ".");
        }

        static void ClearHands(PlayerController p)
        {
            for (int i = 0; i < 3 && !p.Combat.HasFreeGearSlot; i++)
            {
                p.Combat.Drop();
                if (!p.Combat.HasFreeGearSlot) p.Combat.SwitchGear();
            }
        }

        static void Strike(PlayerController attacker, PlayerController victim, float damage, string item = null)
        {
            var from = attacker ? attacker.transform.position : victim.transform.position - Vector3.forward;
            var direction = World.Flat(victim.transform.position - from);
            victim.Health.ApplyDamage(Hits.Of(attacker, direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward,
                HitSource.Melee, damage, 1f, itemId: item));
        }

        static void Knock(PlayerController victim, PlayerController attacker) => Strike(attacker, victim, 1000f);

        static void Move(Rigidbody body, Vector3 at)
        {
            if (!body) return;
            body.isKinematic = false;
            body.position = at;
            body.transform.position = at;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
        }

        string RoomAt(Vector3 at) => room ? room.Layout.RoomAt(at.x, at.y + .1f, at.z) : null;

        Vector3 FreeSpot(Vector3 centre, float distance, bool sameRoom = true, Vector3? prefer = null)
        {
            string here = sameRoom ? RoomAt(centre) : null;
            var found = new List<Vector3>();
            for (int ring = 0; ring < 4 && found.Count == 0; ring++)
                for (int i = 0; i < 12; i++)
                {
                    var at = centre + Quaternion.Euler(0f, i * 30f + ring * 15f, 0f) * Vector3.forward * (distance + ring * .45f);
                    if (!Ground(ref at)) continue;
                    if (here != null && RoomAt(at) != here) continue;
                    if (Physics.CheckCapsule(at + Vector3.up * .45f, at + Vector3.up * 1.3f, .32f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    found.Add(at);
                }
            if (found.Count == 0)
            {
                var fallback = centre;
                Ground(ref fallback);
                return fallback;
            }
            return prefer.HasValue ? found.OrderBy(at => (at - prefer.Value).sqrMagnitude).First() : found[0];
        }

        static bool Ground(ref Vector3 at)
        {
            if (!Physics.Raycast(at + Vector3.up * 1.2f, Vector3.down, out var hit, 3f, World.GroundMask, QueryTriggerInteraction.Ignore)) return false;
            at.y = hit.point.y + .02f;
            return true;
        }

        static void Stand(PlayerController p, Vector3 at, Vector3 look)
        {
            if (!p) return;
            p.Respawn(at);
            var to = World.Flat(look - at);
            if (to.sqrMagnitude > 1e-4f) Aim(p, to.normalized);
        }

        static void Aim(PlayerController p, Vector3 direction)
        {
            if (!p || direction.sqrMagnitude < 1e-6f) return;
            p.FaceTowards(direction);
            p.LookYaw = Mathf.Atan2(direction.x, direction.z);
        }

        static IEnumerator Realtime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) throw new QaFailException("Timed out waiting for " + what + ".");
                yield return null;
            }
        }

        IEnumerator Attempt(string image, IEnumerator body)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                object current = null;
                bool advanced;
                string failure = null;
                try
                {
                    advanced = stack.Peek().MoveNext();
                    if (advanced) current = stack.Peek().Current;
                }
                catch (Exception error)
                {
                    advanced = false;
                    failure = error is QaFailException ? error.Message : error.GetType().Name + ": " + error.Message;
                }
                if (failure != null)
                {
                    while (stack.Count > 0)
                        try { (stack.Pop() as IDisposable)?.Dispose(); }
                        catch (Exception cleanup) { failure += " (cleanup " + cleanup.GetType().Name + ")"; }
                    Time.timeScale = 1f;
                    foreach (var seat in seats) seat.Next = default;
                    failed.Add(image + ": " + failure);
                    break;
                }
                if (!advanced) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
        }

        IEnumerator Shot(string image)
        {
            if (Application.isBatchMode) throw new QaFailException("Guide pictures need a normal window, not -batchmode.");
            string path = context.PathInRun(image + ".png");
            if (File.Exists(path)) throw new QaFailException("Capture never overwrites existing evidence: " + image);
            yield return new WaitForEndOfFrame();
            Texture2D frame = null, picture = null;
            try
            {
                frame = ScreenCapture.CaptureScreenshotAsTexture();
                var pixels = Fit(frame.GetPixels32(), frame.width, frame.height);
                int distinct = pixels.Where((_, i) => i % 97 == 0).Select(p => (p.r >> 3, p.g >> 3, p.b >> 3)).Distinct().Count();
                if (distinct < 24) throw new QaFailException("Capture looks blank: " + image);
                picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                picture.SetPixels32(pixels);
                picture.Apply(false);
                File.WriteAllBytes(path, picture.EncodeToPNG());
            }
            finally
            {
                if (frame) Object.Destroy(frame);
                if (picture) Object.Destroy(picture);
            }
            context.AddCapture(image, path, "png", "screen");
            shots.Add(image);
        }

        static Color32[] Fit(Color32[] source, int width, int height)
        {
            float aspect = Width / (float)Height;
            int cropWidth = width, cropHeight = height;
            if (width > height * aspect) cropWidth = Mathf.RoundToInt(height * aspect);
            else cropHeight = Mathf.RoundToInt(width / aspect);
            int left = (width - cropWidth) / 2, bottom = (height - cropHeight) / 2;
            float stepX = cropWidth / (float)Width, stepY = cropHeight / (float)Height;
            var result = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                int y0 = Mathf.Min(height - 1, bottom + Mathf.FloorToInt(y * stepY));
                int y1 = Mathf.Min(height, Mathf.Max(y0 + 1, bottom + Mathf.FloorToInt((y + 1) * stepY)));
                for (int x = 0; x < Width; x++)
                {
                    int x0 = Mathf.Min(width - 1, left + Mathf.FloorToInt(x * stepX));
                    int x1 = Mathf.Min(width, Mathf.Max(x0 + 1, left + Mathf.FloorToInt((x + 1) * stepX)));
                    int r = 0, g = 0, b = 0, count = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            var c = source[sy * width + sx];
                            r += c.r; g += c.g; b += c.b; count++;
                        }
                    result[y * Width + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
            }
            return result;
        }

        IEnumerator Return(QaContext qa)
        {
            Time.timeScale = 1f;
            foreach (var seat in seats) seat.Next = default;
            PlayerHealth.TraitRoll = originalRoll;
            RiddleRushDirector.SeedOverride = null;
            ModeGuides.Close();
            if (originalBindings != null)
            {
                Session.Bindings.Clear();
                Session.Bindings.AddRange(originalBindings);
            }
            if (launched || SceneManager.GetActiveScene().name != Session.HubScene) yield return Home();
            if (originalMap != null && Session.MapId != originalMap) Session.SelectMap(originalMap);
            if (originalSolo != null) WordboundSoloEntry.Select(originalSolo);
            if (originalSoloMap != null) WordboundSoloEntry.SelectMap(originalSoloMap);
            var lobby = LobbyMenu.Instance;
            if (lobby && originalQueue != null)
            {
                lobby.Choose(originalQueue, originalMode, originalLobbyMap);
                lobby.Page<PlayPage>().WordboundSolo = originalFamily;
                if (originalPage != null) lobby.Open(originalPage);
            }
            var changed = (preferences ?? Array.Empty<LobbyExperienceQa.Preference>()).Where(p => !p.Matches()).ToArray();
            foreach (var preference in changed) preference.Restore();
            var changedExtras = (extras ?? Array.Empty<Saved>()).Where(p => !p.Matches()).ToArray();
            foreach (var preference in changedExtras) preference.Restore();
            if (changed.Length + changedExtras.Length > 0) PlayerPrefs.Save();
            Application.logMessageReceived -= Log;
            SceneManager.sceneLoaded -= Loaded;
            qa.Note("mode_guides_pictures", shots.ToArray());
            qa.Note("mode_guides_missing", ModeGuideBook.Images.Except(shots).ToArray());
            qa.Note("mode_guides_prefs_restored", changed.Select(p => p.Key).Concat(changedExtras.Select(p => p.Key)).ToArray());
            qa.Note("mode_guides_errors", errors.ToArray());
            qa.Note("mode_guides_seconds", qa.ElapsedSeconds);
            if (errors.Count > 0) throw new QaFailException($"The run logged {errors.Count} error(s); first: {errors[0]}");
        }

        sealed class GuideSeat : InputBinding
        {
            readonly string id;
            public PlayerCommands Next;

            public GuideSeat(string id) => this.id = id;

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
            static readonly string[] Strings = { MatchTally.CorruptKey, "wv.outfit.0", "wv.outfit.1", "wv.outfit.2", "wv.outfit.3" };
            static readonly string[] Integers = { ControlTips.LearnedKey, ControlTips.SeenKey, WordboundMatchHud.IntroSeenKey };

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
