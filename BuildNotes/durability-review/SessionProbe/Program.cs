using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

static class Program
{
    const string Player = "11111111-1111-4111-a111-111111111111";
    const string MatchId = "33333333-3333-4333-a333-333333333333";
    const string AppId = "66666666-6666-4666-a666-666666666666";
    const long Now = 1700000000000;
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Members).GetValue(target);
    static T Call<T>(object target, string name, params object[] values) =>
        (T)target.GetType().GetMethod(name, Members).Invoke(target, values);
    static T Seed<T>(string name, params object[] values) =>
        (T)typeof(WordboundOnlineTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, values);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static readonly List<string> Passed = new List<string>();
    static async Task Test(string name, Func<Task> body) { await body(); Passed.Add(name); Console.WriteLine("PASS " + name); }

    sealed class Http : IOnlineHttpTransport
    {
        public Func<OnlineRequest, Task<OnlineResponse>> Reply;
        public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken token) => Reply(request);
    }
    sealed class Authority : IWordboundFusionAuthority
    {
        public int Acks, Validations, Publishes;
        public string IdentityHash => new string('a', 64);
        public long Epoch => 0;
        public void AcceptCommand(string actor, JsonNode body) { }
        public OnlineSnapshotMark Publish(out string json)
        { json = "{}"; return new OnlineSnapshotMark(MatchId, IdentityHash, 0, ++Publishes, 0, new string('b', 64)); }
        public void ValidateSnapshot(string json, OnlineSnapshotMark mark) { Validations++; }
        public bool AcknowledgeDurable(OnlineSnapshotMark mark) { Acks++; return true; }
        public void PauseForMigration() { }
        public void RestoreCommitted(string json, OnlineSnapshotMark mark, long epoch, long clock) { }
        public IReadOnlyList<OnlineSeatPresence> Presence() => Array.Empty<OnlineSeatPresence>();
        public void SetTransportReady(bool ready, bool host) { }
        public void Disconnect(string actor, long clock) { }
        public bool Reconnect(string actor, long clock) => true;
        public void Return(string actor, long clock) { }
    }
    sealed class Fixture : IDisposable
    {
        readonly RSA key = Seed<RSA>("Key");
        public readonly Http Http = new Http();
        public readonly Authority Authority = new Authority();
        public readonly WordboundFusionSession Session;
        public readonly WordboundFusionJoin Join;
        public readonly OnlineCheckpointService Checkpoints;
        public readonly VerifiedOnlineMatch Match;
        public readonly Dictionary<string, object> Writer;
        public readonly List<string> Requests = new List<string>();
        public Func<OnlineRequest, Task<OnlineResponse>> Override;
        public string Manifest, Head;
        public long Clock = Now;
        public int Pushes;
        public Fixture()
        {
            var old = Seed<OnlineMatchExpectation>("Expected", new object[] { null });
            var settings = new OnlineSettings("TimeRun", "Solo", false, "asia", "wordbound-v2", "1");
            var expected = new OnlineMatchExpectation(old.MatchId, old.Generation, old.RosterHash, settings, old.Content);
            var verifier = new OnlineAdmissionVerifier("wordbound-test", Seed<string>("Jwks", key));
            string Sign(Dictionary<string, object> claims) => Seed<string>("Sign", key, claims, null);
            var start = Seed<Dictionary<string, object>>("Start"); start["settings"] = settings.ToValue();
            string certificate = Sign(start);
            Match = verifier.VerifyStart(certificate, expected, Now / 1000);
            var admission = Seed<Dictionary<string, object>>("Admission"); admission["protocolVersion"] = "wordbound-v2";
            string secret = OnlineJson.Base64Url(Enumerable.Repeat((byte)42, 32).ToArray());
            var ticket = new OnlinePhotonTicket(OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["ticket"] = "fixture.photon.ticket", ["appId"] = AppId, ["expiresAt"] = Now + 60000 })));
            Join = new WordboundFusionJoin(Player, certificate, Sign(admission), secret, ticket);
            Writer = new Dictionary<string, object> {
                ["playerId"] = Player, ["claimId"] = AppId, ["term"] = "1", ["epoch"] = "0",
                ["expiresAt"] = Now + 15000, ["baseHeadId"] = null, ["admissionOpen"] = true };
            Http.Reply = request => { Requests.Add(request.Path); return Override == null ? Task.FromResult(Reply(request)) : Override(request); };
            var client = new OnlineCoordinatorClient(Http); client.CreateGuest("Review fixture").GetAwaiter().GetResult();
            Checkpoints = new OnlineCheckpointService(client, expected, Authority.IdentityHash);
            Session = new WordboundFusionSession(() => Runner(), new FusionAppSettings { AppIdFusion = AppId },
                new NetworkProjectConfig(), default, verifier, expected, _ => Task.FromResult(Join), Authority,
                new OnlineConnectionService(client, verifier, expected, () => Clock), Checkpoints, () => Clock);
            Set(Session, "gate", new OnlineSessionGate(Match));
        }
        public NetworkRunner Runner(bool server = true)
        {
            var runner = new NetworkRunner { IsServer = server };
            runner.Users[runner.LocalPlayer] = Player;
            runner.ActivePlayers.Clear(); runner.ActivePlayers.Add(runner.LocalPlayer);
            runner.Push = () => { Pushes++; return Task.FromResult(true); };
            return runner;
        }
        public object Replace(NetworkRunner runner, bool ready = false)
        {
            var run = Call<object>(Session, "BeginRun");
            Set(run, "Runner", runner); Set(run, "Join", Join); Set(Session, "<Runner>k__BackingField", runner);
            Set(run, "Writer", new OnlineCheckpointWriter(OnlineJson.Parse(OnlineJson.Encode(Writer))));
            Set(run, "CheckpointReady", ready); Set(Session, "<GameplayReady>k__BackingField", ready);
            return run;
        }
        public JsonNode Prompt() => OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
            ["roster"] = Match.Match.RosterHash, ["host"] = Player, ["term"] = "1" }));
        public OnlineResponse State() => new OnlineResponse(200, "{\"writer\":" + OnlineJson.Encode(Writer) +
            ",\"head\":" + (Head ?? "null") + ",\"previous\":null}");
        public OnlineResponse Reply(OnlineRequest request)
        {
            if (request.Path == "/v1/guests") return Json(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "fixture.access.token",
                ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 });
            if (request.Path.EndsWith("checkpoint-head")) return State();
            if (request.Path.EndsWith("connection-tickets")) return Json(new Dictionary<string, object> {
                ["ticket"] = OnlineJson.Base64Url(new byte[32]), ["expiresAt"] = Clock + 15000 });
            if (request.Path.EndsWith("checkpoint-begin")) { Manifest = request.JsonBody; return new OnlineResponse(200, "{\"manifest\":" + Manifest + "}"); }
            var body = OnlineJson.Parse(request.JsonBody);
            if (request.Path.EndsWith("checkpoint-chunk")) return Json(new Dictionary<string, object> {
                ["uploadId"] = body["uploadId"].String(), ["index"] = (int)body["index"].Number() });
            if (request.Path.EndsWith("checkpoint-finalize")) { Head = Manifest; return new OnlineResponse(200, "{\"head\":" + Head + "}"); }
            throw new Exception("Unexpected route " + request.Path);
        }
        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));
        public int Count(string suffix) => Requests.Count(p => p.EndsWith(suffix));
        public void Dispose() => key.Dispose();
    }
    static async Task Main()
    {
        await Test("Closed writer never consumes a ticket; prompt lookups are bounded", async () => {
            using var f = new Fixture(); f.Writer["admissionOpen"] = false;
            var run = f.Replace(f.Runner(false));
            for (int i = 0; i < 12; i++) await Call<Task>(f.Session, "ReceiveHostAvailable", run, f.Prompt());
            Require(f.Count("checkpoint-head") == 4 && f.Count("connection-tickets") == 0, "Unbounded or premature ticket flow.");
        });
        await Test("Duplicate prompts coalesce before and after deferred Head", async () => {
            using var f = new Fixture(); var runner = f.Runner(false); var run = f.Replace(runner);
            var response = new TaskCompletionSource<OnlineResponse>();
            f.Override = r => r.Path.EndsWith("checkpoint-head") ? response.Task : Task.FromResult(f.Reply(r));
            var first = Call<Task>(f.Session, "ReceiveHostAvailable", run, f.Prompt());
            await Call<Task>(f.Session, "ReceiveHostAvailable", run, f.Prompt());
            Require(f.Count("checkpoint-head") == 1 && f.Count("connection-tickets") == 0, "Duplicate pending Head.");
            response.SetResult(f.State()); await first;
            await Call<Task>(f.Session, "ReceiveHostAvailable", run, f.Prompt());
            Require(f.Count("connection-tickets") == 1 && runner.Sends == 1, "Duplicate consumed prompt.");
        });
        await Test("Superseded Head response cannot request a ticket", async () => {
            using var f = new Fixture(); var run = f.Replace(f.Runner(false));
            var response = new TaskCompletionSource<OnlineResponse>(); f.Override = _ => response.Task;
            var pending = Call<Task>(f.Session, "ReceiveHostAvailable", run, f.Prompt());
            f.Replace(f.Runner(false)); response.SetResult(f.State()); await pending;
            Require(f.Count("connection-tickets") == 0 && f.Session.Failure == "", "Old Head affected replacement.");
        });
        await Test("Superseded ticket response cannot send into replacement", async () => {
            using var f = new Fixture(); var old = f.Runner(false); var run = f.Replace(old);
            var response = new TaskCompletionSource<OnlineResponse>(); OnlineRequest ticket = null;
            f.Override = r => { if (r.Path.EndsWith("connection-tickets")) { ticket = r; return response.Task; } return Task.FromResult(f.Reply(r)); };
            var pending = Call<Task>(f.Session, "ReceiveHostAvailable", run, f.Prompt());
            var next = f.Runner(false); f.Replace(next); response.SetResult(f.Reply(ticket)); await pending;
            Require(old.Sends == 0 && next.Sends == 0 && f.Session.Failure == "", "Old ticket crossed Runner lifetime.");
        });
        await Test("Coordinator receipt acknowledges without any Photon snapshot push", async () => {
            using var f = new Fixture(); f.Replace(f.Runner(), true);
            Require(await f.Session.PublishCheckpoint(), "Publication failed.");
            Require(f.Authority.Acks == 1 && f.Pushes == 0 && f.Session.Committed != null, "SDK push was used as durability.");
        });
        foreach (bool reject in new[] { false, true })
        await Test("Old finalize " + (reject ? "failure" : "success") + " cannot ACK or unlock replacement", async () => {
            using var f = new Fixture(); f.Replace(f.Runner(), true);
            var response = new TaskCompletionSource<OnlineResponse>(); OnlineRequest finalize = null;
            f.Override = r => { if (r.Path.EndsWith("checkpoint-finalize")) { finalize = r; return response.Task; } return Task.FromResult(f.Reply(r)); };
            var pending = f.Session.PublishCheckpoint();
            Require(finalize != null, "Probe did not reach finalize.");
            var next = f.Replace(f.Runner(), true); Set(next, "Publishing", true);
            if (reject) response.SetException(new IOException("Deferred old request")); else response.SetResult(f.Reply(finalize));
            Require(!await pending && f.Authority.Acks == 0 && Get<bool>(next, "Publishing") && f.Session.Failure == "", "Old finalize escaped its scope.");
        });
        await Test("Exact local writer expiry rejects an otherwise successful late finalize", async () => {
            using var f = new Fixture(); f.Replace(f.Runner(), true);
            var response = new TaskCompletionSource<OnlineResponse>(); OnlineRequest finalize = null;
            f.Override = r => { if (r.Path.EndsWith("checkpoint-finalize")) { finalize = r; return response.Task; } return Task.FromResult(f.Reply(r)); };
            var pending = f.Session.PublishCheckpoint(); f.Clock = Now + 15000; response.SetResult(f.Reply(finalize));
            Require(!await pending && f.Authority.Acks == 0, "Expired writer acknowledged.");
            f.Session.Poll(); Require(!f.Session.GameplayReady && f.Session.Failure != "", "Expired writer kept gameplay ready.");
        });
        await Test("Lost finalize response resolves exact durable Head once", async () => {
            using var f = new Fixture(); f.Replace(f.Runner(), true);
            f.Override = r => { var value = f.Reply(r); return r.Path.EndsWith("checkpoint-finalize") ?
                Task.FromException<OnlineResponse>(new IOException("Lost receipt")) : Task.FromResult(value); };
            Require(await f.Session.PublishCheckpoint() && f.Authority.Acks == 1 && f.Count("checkpoint-head") == 1, "Unknown outcome was not resolved exactly.");
        });
        await Test("Stop fences delayed Head resolution after a lost finalize response", async () => {
            using var f = new Fixture(); f.Replace(f.Runner(), true);
            var head = new TaskCompletionSource<OnlineResponse>();
            f.Override = r => {
                if (r.Path.EndsWith("checkpoint-head")) return head.Task;
                var value = f.Reply(r); return r.Path.EndsWith("checkpoint-finalize") ?
                    Task.FromException<OnlineResponse>(new IOException("Lost receipt")) : Task.FromResult(value);
            };
            var pending = f.Session.PublishCheckpoint(); await f.Session.Stop(); head.SetResult(f.State());
            Require(!await pending && f.Authority.Acks == 0 && !f.Session.GameplayReady, "Stopped publication acknowledged.");
        });
        Console.WriteLine("RESULT " + Passed.Count + "/" + Passed.Count + " PASS; actual Session/client source with HTTP and SDK scheduling doubles; no native, cloud, domain-validity or physics claim.");
    }
}
