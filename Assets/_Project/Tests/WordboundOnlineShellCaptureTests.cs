using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("WordboundOnlineShellCapture")]
    public sealed class WordboundOnlineShellCaptureTests
    {
        const string Player = "10000000-0000-4000-8000-000000000001";
        const string Guest = "10000000-0000-4000-8000-000000000002";
        const string Party = "20000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef", Code = "1234567890abcdef";
        const string ThemePath = "Assets/_Project/Data/Wordbound/Authoring/Interface.asset";
        readonly List<Frame> frames = new();
        readonly Dictionary<string, string> protectedFiles = new();
        UIRefinementCaptureTests.GameViewScope view;
        GameObject owner;
        WordboundPartyMenu menu;
        WordboundOnlineShellCapturePresenter presenter;
        WordboundPartyCoordinator coordinator;
        MemoryHttp http;
        RSA rsa;
        WordboundUiTheme savedTheme, largeTheme;
        string themeJson, themePreference, currentTheme, directory;
        bool themeHadPreference, themeWasDirty, priorAsync, completed;
        float priorTimeScale;
        OnlineSettings settings;
        bool ready;
        string channel;
        int revision;

        [Serializable] sealed class Frame
        {
            public string file, state, sha256, error;
            public int width, height, repaintFrame;
            public Rect panel;
            public Vector2 scroll;
            public string[] controls;
        }
        [Serializable] sealed class Receipt
        {
            public string status, engine, utc, scope;
            public bool synthetic = true;
            public Frame[] frames;
            public string[] requests, checkedFiles, cleanupErrors;
            public FileProof[] files;
        }
        [Serializable] sealed class FileProof { public string path, sha256; }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.GetFullPath("BuildNotes/completion-20261027/online-shell/capture/run-" +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Assert.IsFalse(Directory.Exists(directory)); Directory.CreateDirectory(directory);
            priorAsync = EditorSettings.asyncShaderCompilation; priorTimeScale = Time.timeScale;
            themeHadPreference = PlayerPrefs.HasKey(LobbyThemes.PreferenceKey);
            themePreference = PlayerPrefs.GetString(LobbyThemes.PreferenceKey, ""); currentTheme = LobbyThemes.Current.Id;
            savedTheme = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(ThemePath);
            Assert.IsNotNull(savedTheme); savedTheme.Validate();
            themeJson = JsonUtility.ToJson(savedTheme); themeWasDirty = EditorUtility.IsDirty(savedTheme);
            foreach (string path in AssetDatabase.GetDependencies(ThemePath, true)) Protect(path);
            Assert.IsNotNull(LobbyThemes.Current.Background); Protect(AssetDatabase.GetAssetPath(LobbyThemes.Current.Background));
            foreach (string path in new[] {
                "Assets/_Project/Scripts/Online/Client/WordboundPartyMenu.cs",
                "Assets/_Project/Scripts/Online/Client/WordboundPartyMenuInput.cs",
                "Assets/_Project/Scripts/Online/Client/WordboundPartyMenuPresentation.cs",
                "Assets/_Project/Scripts/UI/Wordbound/WordboundUiTheme.cs" })
            { Assert.IsTrue(File.Exists(path), path); Protect(path); }
            EditorSettings.asyncShaderCompilation = false;
            view = new UIRefinementCaptureTests.GameViewScope(); view.Select(1280, 720);
            yield return TestScenes.Reset();
            string items = File.ReadAllText("Assets/_Project/Data/Config/items.json");
            string rules = File.ReadAllText("Assets/_Project/Data/Config/rules.json");
            var content = new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed);
            var catalogue = WordboundPublicCourseCatalog.FromTestedContent("ui-capture", new[] { content });
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(
                new Dictionary<string, object> { ["keys"] = new[] { new Dictionary<string, object> {
                    ["kty"] = "RSA", ["kid"] = "ui-fixture", ["use"] = "sig", ["alg"] = "RS256",
                    ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } })));
            http = new MemoryHttp { Handler = Respond };
            coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(http), verifier, items, rules,
                "ui-capture", new WordboundPublicCourseRegistry(catalogue.Json), () => 1000000);
            owner = new GameObject("Online shell UI fixture - no network");
            var camera = owner.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.07f, .11f, .12f); camera.cullingMask = 0;
            menu = owner.AddComponent<WordboundPartyMenu>(); menu.enabled = false; menu.Theme = savedTheme;
            Set("coordinator", coordinator); Set("publicCourses", catalogue);
            Set("itemSource", items); Set("ruleSource", rules); Set("mode", 4); Set("seed", Seed); Set("fill", true);
            menu.BeginMatchRequested = (_, _) => throw new InvalidOperationException("UI fixture cannot launch gameplay.");
            menu.CloseRequested += () => { }; menu.WorkshopRequested += () => { };
            presenter = owner.AddComponent<WordboundOnlineShellCapturePresenter>(); presenter.Menu = menu;
            WriteReceipt("IN PROGRESS", Array.Empty<string>());
        }

        [UnityTest]
        public IEnumerator CaptureActualOnlineShellStatesAtWideAndNarrowSizes()
        {
            yield return Pair("guest", false, "connect");
            yield return Complete(coordinator.Connect("Player")); Assert.IsTrue(coordinator.Authenticated);
            yield return Pair("create", false, "entry:0", "create-party");
            Set("showCreateSettings", true);
            yield return Capture("create-settings", 1280, 720, false, "region:0", "fill", "map-source:0");
            yield return Capture("create-settings-bottom", 854, 480, true, "create-party");
            Set("showCreateSettings", false);
            Set("entryPage", 1);
            yield return Pair("join", false, "join-party", "use-room-settings");
            Set("entryPage", 0);
            yield return Complete(coordinator.CreateParty(coordinator.Settings("Freestyle", "Solo", true, "asia"), Seed));
            yield return Complete(coordinator.SetReady(true)); Assert.IsNull(coordinator.Error);
            Assert.AreEqual(2, coordinator.Party.Members.Count); Assert.IsTrue(coordinator.PublicAvailable);
            yield return Pair("party-ready", false, "ready", "quick-play", "private-actions");
            Set("showPrivateActions", true);
            yield return Pair("private-expanded-bottom", true, "create-private", "join-private", "custom-consent");
            Assert.IsTrue(!Get<bool>("compactPartyLayout") || Get<bool>("compactPartyScroll"),
                "The expanded private form must use the actual production scroll path before the failure transition.");
            Assert.AreEqual("private-expanded-bottom", frames.Last().state);
            Assert.AreEqual(854, frames.Last().width); Assert.AreEqual(480, frames.Last().height);
            Assert.Greater(frames.Last().scroll.y, 0, "The preceding native repaint must show the genuinely scrolled long form.");
            int priorQueueRequests = http.Requests.Count;
            http.FailNext = true; yield return Complete(coordinator.Queue("PrivateCreate"));
            Assert.AreEqual(priorQueueRequests + 1, http.Requests.Count);
            Assert.IsTrue(http.Requests.Last().EndsWith("/queue", StringComparison.Ordinal));
            Assert.IsTrue(coordinator.RetryRequired); Assert.IsNotEmpty(coordinator.Error);
            Assert.AreEqual("Enter queue", coordinator.PendingAction);
            yield return Capture("private-scrolled-retry-onset", 854, 480, null, "retry");
            largeTheme = Object.Instantiate(savedTheme); largeTheme.hideFlags = HideFlags.HideAndDontSave;
            largeTheme.BodySize = 22; largeTheme.HeadingSize = 30;
            largeTheme.RowHeight = Math.Max(largeTheme.RowHeight, 34); largeTheme.ModuleHeight = Math.Max(largeTheme.ModuleHeight, 60);
            largeTheme.Validate(); menu.Theme = largeTheme;
            yield return Capture("private-scrolled-retry-max-font", 854, 480, null, "retry");
            menu.Theme = savedTheme;
            yield return Complete(coordinator.Retry()); Assert.IsFalse(coordinator.RetryRequired);
            Assert.IsTrue(coordinator.Party.HasAttempt); Assert.AreEqual("PrivateCreate", coordinator.Party.Channel);
            yield return Complete(coordinator.CancelQueue()); Assert.IsFalse(coordinator.Party.HasAttempt);
            Set("showPrivateActions", false);
            yield return Complete(coordinator.Queue("QuickPlay")); Assert.IsTrue(coordinator.Party.HasAttempt);
            yield return Pair("searching", false, "cancel-queue", "leave-party");
            yield return Complete(coordinator.CancelQueue());
            http.FailNext = true; yield return Complete(coordinator.Queue("QuickPlay"));
            Assert.IsTrue(coordinator.RetryRequired); Assert.IsNotEmpty(coordinator.Error);
            yield return Capture("failure-onset", 854, 480, null, "retry");
            yield return Pair("failure-retry-bottom", true, "retry");
            yield return Complete(coordinator.Retry()); Assert.IsFalse(coordinator.RetryRequired);
            yield return Complete(coordinator.CancelQueue());
            menu.Theme = largeTheme;
            yield return Capture("party-ready-max-font", 854, 480, false, "ready", "quick-play");
            menu.Theme = savedTheme;
            yield return Complete(coordinator.LeaveParty()); Assert.IsNull(coordinator.Party);
            menu.Theme = largeTheme;
            yield return Capture("create-max-font", 854, 480, false, "create-party", "create-settings");
            menu.Theme = savedTheme;
            Assert.AreEqual(21, frames.Count); completed = true;
        }

        IEnumerator Pair(string state, bool bottom, params string[] required)
        {
            yield return Capture(state, 1280, 720, bottom, required);
            yield return Capture(state, 854, 480, bottom, required);
        }
        IEnumerator Capture(string state, int width, int height, bool? bottom, params string[] required)
        {
            view.Select(width, height);
            if (bottom.HasValue) Set("scroll", bottom.Value ? new Vector2(0, 100000) : Vector2.zero);
            int before = menu.QaCompletedRepaintCount;
            for (int frame = 0; frame < 120 && (Screen.width != width || Screen.height != height ||
                menu.QaCompletedRepaintCount < before + 3); frame++) yield return null;
            Assert.AreEqual(width, Screen.width); Assert.AreEqual(height, Screen.height);
            Assert.GreaterOrEqual(menu.QaCompletedRepaintCount, before + 3); Assert.IsNull(presenter.Failure);
            yield return new WaitForEndOfFrame();
            Assert.IsNull(Get<Action>("pendingPartyAction"), "Capture is state-only; unexpected physical input queued an action.");
            var controls = (List<string>)typeof(WordboundMenuFocus).GetField("controls", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(Get<WordboundMenuFocus>("partyFocus"));
            foreach (string id in required) Assert.Contains(id, controls, "The actual layout must register " + id);
            Rect panel = Get<Rect>("partyRect");
            Assert.GreaterOrEqual(panel.xMin, 0); Assert.GreaterOrEqual(panel.yMin, 0);
            Assert.LessOrEqual(panel.xMax, width); Assert.LessOrEqual(panel.yMax, height);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.AreEqual(width, texture.width); Assert.AreEqual(height, texture.height);
                string file = frames.Count.ToString("00") + "-" + state + "-" + width + "x" + height + ".png";
                byte[] png = texture.EncodeToPNG(); File.WriteAllBytes(Path.Combine(directory, file), png);
                frames.Add(new Frame { file = file, state = state, width = width, height = height,
                    sha256 = Hash(png), panel = panel, scroll = Get<Vector2>("scroll"), controls = controls.ToArray(),
                    repaintFrame = menu.QaLastRepaintFrame, error = coordinator.Error ?? "" });
                WriteReceipt("IN PROGRESS", Array.Empty<string>());
            }
            finally { Object.Destroy(texture); }
        }
        static IEnumerator Complete(Task task)
        {
            for (int frame = 0; frame < 120 && !task.IsCompleted; frame++) yield return null;
            Assert.IsTrue(task.IsCompleted); Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }
        OnlineResponse Respond(OnlineRequest request)
        {
            if (request.Path == "/v1/guests") return Json(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "synthetic.ui.token",
                ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 });
            var body = string.IsNullOrEmpty(request.JsonBody) ? null : OnlineJson.Parse(request.JsonBody);
            if (request.Path == "/v1/parties") settings = OnlineSettings.Read(body["settings"]);
            else if (request.Path.EndsWith("/ready")) ready = body["ready"].Bool();
            else if (request.Path.EndsWith("/queue")) channel = body["channel"].String();
            else if (request.Path.EndsWith("/cancel")) channel = null;
            else if (request.Path.EndsWith("/leave")) return Json(new Dictionary<string, object>());
            else throw new InvalidOperationException("Unexpected UI fixture request: " + request.Path);
            var result = new Dictionary<string, object> {
                ["id"] = Party, ["revision"] = (++revision).ToString(), ["leaderId"] = Player, ["invite"] = Code,
                ["members"] = new[] { new Dictionary<string, object> { ["playerId"] = Player, ["ready"] = ready },
                    new Dictionary<string, object> { ["playerId"] = Guest, ["ready"] = true } },
                ["settings"] = settings.ToValue(), ["state"] = channel == null ? "OPEN" : "QUEUED" };
            if (channel != null) result["activeAttempt"] = new Dictionary<string, object> { ["channel"] = channel };
            return Json(result);
        }
        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        sealed class MemoryHttp : IOnlineHttpTransport
        {
            public readonly List<string> Requests = new();
            public Func<OnlineRequest, OnlineResponse> Handler;
            public bool FailNext;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested(); Requests.Add(request.Path);
                if (FailNext) { FailNext = false; return Task.FromException<OnlineResponse>(
                    new IOException("Connection interrupted. Retry to confirm your last action.")); }
                return Task.FromResult(Handler(request));
            }
        }
        void Set(string name, object value) => typeof(WordboundPartyMenu).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, value);
        T Get<T>(string name) => (T)typeof(WordboundPartyMenu).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
        static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        void Protect(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path)) return;
            protectedFiles[path] = Hash(File.ReadAllBytes(path));
            if (File.Exists(path + ".meta")) protectedFiles[path + ".meta"] = Hash(File.ReadAllBytes(path + ".meta"));
        }
        void WriteReceipt(string status, string[] errors)
        {
            if (directory == null) return;
            File.WriteAllText(Path.Combine(directory, "receipt.json"), JsonUtility.ToJson(new Receipt {
                status = status, engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                scope = "Actual GameView menu rendering with in-memory coordinator responses. No public networking, physical input or match acceptance.",
                frames = frames.ToArray(), requests = http?.Requests.ToArray() ?? Array.Empty<string>(),
                checkedFiles = protectedFiles.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray(), cleanupErrors = errors,
                files = protectedFiles.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new FileProof { path = x.Key, sha256 = x.Value }).ToArray() }, true));
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var errors = new List<string>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error.ToString()); } }
            Attempt(() => { if (owner) Object.DestroyImmediate(owner); });
            Attempt(() => coordinator?.Dispose()); Attempt(() => rsa?.Dispose());
            Attempt(() => { if (largeTheme) Object.DestroyImmediate(largeTheme); });
            Attempt(() => EditorSettings.asyncShaderCompilation = priorAsync);
            Attempt(() => view?.Dispose());
            try { yield return TestScenes.Reset(); }
            finally
            {
                Attempt(() => Time.timeScale = priorTimeScale);
                Attempt(() => {
                    if (savedTheme && themeJson != null) {
                        Assert.AreEqual(themeJson, JsonUtility.ToJson(savedTheme)); Assert.AreEqual(themeWasDirty, EditorUtility.IsDirty(savedTheme)); }
                    Assert.AreEqual(themeHadPreference, PlayerPrefs.HasKey(LobbyThemes.PreferenceKey));
                    Assert.AreEqual(themePreference, PlayerPrefs.GetString(LobbyThemes.PreferenceKey, ""));
                    Assert.AreEqual(currentTheme, LobbyThemes.Current.Id);
                    foreach (var file in protectedFiles) Assert.AreEqual(file.Value, Hash(File.ReadAllBytes(file.Key)), file.Key);
                });
                WriteReceipt(completed && errors.Count == 0 ? "PASS" : "FAIL", errors.ToArray());
            }
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }

    public sealed class WordboundOnlineShellCapturePresenter : MonoBehaviour
    {
        static readonly MethodInfo Draw = typeof(WordboundPartyMenu).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
        public WordboundPartyMenu Menu;
        public Exception Failure;
        void OnGUI()
        {
            if (!Menu) return;
            try { Draw.Invoke(Menu, null); GUI.Label(new Rect(8, 4, 100, 20), "UI FIXTURE"); }
            catch (Exception error) { Failure = error is TargetInvocationException invocation ? invocation.InnerException ?? error : error; }
        }
    }
}
