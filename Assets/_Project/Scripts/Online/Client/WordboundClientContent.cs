using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed class WordboundClientContent
    {
        readonly string itemSource;
        readonly GameRules gameRules;
        public string Seed { get; }
        public ModeDefinition Mode { get; }
        public CourseCatalogue Geometry { get; }
        public ResolvedCourse Course { get; }
        public ItemCatalogue Items => ItemCatalogue.FromJson(itemSource);
        public GameRules Rules => gameRules.Clone();
        public IWordboundPuzzleContent PhysicalPuzzles { get; }
        public OnlineContent PrivateBinding { get; }
        public OnlineContent PublicBinding { get; }

        public WordboundClientContent(string itemJson, string ruleBookJson, string modeId, string format, string seed)
        {
            if (seed == null || seed.Length != 16 || !Regex.IsMatch(seed, "^[a-f0-9]{16}$"))
                throw new ArgumentException("Use a 16-character hexadecimal map seed.");
            if (!Enum.TryParse(format, false, out RaceFormat raceFormat)) throw new ArgumentException("Choose Solo, Duo or Squad.");
            itemSource = itemJson; Seed = seed; Mode = ModeDefinition.For(modeId, raceFormat);
            string catalogueHash;
            string courseId;
            if (Mode.IsRace || Mode.Id == "Freestyle")
            {
                var flagship = WordboundFlagshipContent.Create(Items);
                var resolved = flagship.Resolve(seed, MatchStateValidation.Requirements(Mode));
                if (!resolved.Success) throw new ArgumentException("The selected flagship course cannot be resolved.");
                PhysicalPuzzles = new WordboundAuthoredPuzzleContent(Mode, flagship.Geometry,
                    resolved.Manifest, itemJson, ruleBookJson);
                Geometry = PhysicalPuzzles.Geometry; Course = PhysicalPuzzles.Course;
                gameRules = PhysicalPuzzles.Rules;
                catalogueHash = PhysicalPuzzles.CatalogueHash; courseId = Course.CopyDocument().Id;
            }
            else
            {
                var book = RuleBook.FromJson(ruleBookJson);
                if (Items.Validate(64).Count != 0 || book.Validate().Count != 0) throw new ArgumentException("Game content validation failed.");
                gameRules = Mode.CreateGameplayRules(book.For("Dibs"));
                if (Rules.Validate().Count != 0) throw new ArgumentException("Gameplay rules validation failed.");
                Geometry = CourseTemplates.ProofCatalogue();
                var source = modeId == "FreeForAll" ? CourseTemplates.CombatArena() : CourseTemplates.FreestyleWorld();
                var resolved = CourseResolver.Resolve(source, Geometry,
                    new CourseResolveRequest(seed, MatchStateValidation.Requirements(Mode)));
                if (!resolved.Success) throw new ArgumentException("The selected course cannot be resolved.");
                Course = resolved.Manifest; courseId = source.Id;
                _ = new WordboundEntityBudget(Mode, Course, Geometry, Items, gameRules);
                catalogueHash = OnlineJson.Hash(new Dictionary<string, object> {
                    ["format"] = "wordbound.client-content-v1", ["geometry"] = Geometry.Hash,
                    ["items"] = WordboundIdentity.Sha256(itemJson), ["ruleSource"] = WordboundIdentity.Sha256(ruleBookJson),
                    ["effectiveRules"] = WordboundRulesFingerprint.Compute(Rules) });
            }
            PrivateBinding = Binding("Workshop", courseId, catalogueHash);
            PublicBinding = Binding("BuiltIn", courseId, catalogueHash);
        }
        OnlineContent Binding(string kind, string id, string catalogue) =>
            new OnlineContent(kind, id, Course.SourceHash, Course.Hash, Mode.Hash, catalogue);
        public bool Matches(OnlineContent content) => content != null &&
            OnlineJson.Hash(content.ToValue()) == OnlineJson.Hash((content.Kind == "BuiltIn" ? PublicBinding : PrivateBinding).ToValue());
    }

    public sealed class WordboundPublicCourseRegistry
    {
        readonly HashSet<string> approved;
        public int Count => approved.Count;
        public WordboundPublicCourseRegistry(string approvedContentJson = "[]")
        {
            var node = OnlineJson.Parse(approvedContentJson);
            if (!node.IsArray || node.Items.Count > 128) throw new ArgumentException("Invalid approved public course registry.");
            approved = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in node.Items)
            {
                var content = OnlineContent.Read(entry);
                if (content.Kind != "BuiltIn" || !approved.Add(OnlineJson.Hash(content.ToValue())))
                    throw new ArgumentException("Public course entries must be unique exact BuiltIn bindings.");
            }
        }
        public bool Allows(WordboundClientContent content) => content.Mode.PublicQueue &&
            approved.Contains(OnlineJson.Hash(content.PublicBinding.ToValue()));
        public static string RegistrationJson(IEnumerable<WordboundClientContent> testedCourses) =>
            OnlineJson.Encode(testedCourses.Where(c => c.Mode.PublicQueue).Select(c => c.PublicBinding.ToValue()).ToArray());
    }

    public sealed class WordboundClientTrust
    {
        public const string ProductionOrigin = "https://wreckabulary-wordbound-coordinator.pagna.workers.dev";
        public const string ProductionAppId = "f6fb7f4c-b8a9-44a3-ac63-bee1c5f23552";
        const string Fingerprint = "43ea939c715a181912177bff3e98c647d5690ec2b0b40085a8bb6c047ce41dce";
        public Uri Origin { get; }
        public string PhotonAppId { get; }
        public OnlineAdmissionVerifier Verifier { get; }
        public WordboundClientTrust(string publicTrustJson)
        {
            var data = OnlineJson.Parse(publicTrustJson);
            if (data["origin"].String() != ProductionOrigin || data["photonAppId"].String() != ProductionAppId ||
                data["issuer"].String() != "wreckabulary-wordbound" || data["keyId"].String() != "wordbound-rs256-20261020-01" ||
                data["publicKeyFingerprint"].String() != Fingerprint || data["jwks"]["keys"].Items.Count != 1)
                throw new ArgumentException("The configured coordinator trust is not the approved production service.");
            var key = data["jwks"]["keys"].Items.Single();
            if (key["kid"].String() != data["keyId"].String() || OnlineJson.Hash(new Dictionary<string, object> {
                ["e"] = key["e"].String(), ["kty"] = key["kty"].String(), ["n"] = key["n"].String() }) != Fingerprint)
                throw new ArgumentException("The coordinator public key fingerprint does not match.");
            Origin = new Uri(ProductionOrigin); PhotonAppId = ProductionAppId;
            Verifier = new OnlineAdmissionVerifier(data["issuer"].String(), data["jwks"]);
        }
    }

    public sealed class WordboundPartyLaunchContext
    {
        int initialization;
        public OnlineCoordinatorClient Coordinator { get; }
        public OnlineAdmissionVerifier Verifier { get; }
        public VerifiedOnlineMatch Verified { get; }
        public WordboundClientContent Content { get; }
        public Func<CancellationToken, Task<WordboundFusionJoin>> FreshJoin { get; }
        public bool NoClientInitialized => Volatile.Read(ref initialization) == 2;
        public void MarkClientInitialized()
        {
            if (Interlocked.CompareExchange(ref initialization, 1, 0) != 0)
                throw new InvalidOperationException("The launch initialization outcome is already recorded.");
        }
        public void ConfirmNoClientInitialized()
        {
            if (Interlocked.CompareExchange(ref initialization, 2, 0) != 0)
                throw new InvalidOperationException("An initialized client cannot use the no-client return fallback.");
        }
        public Task ReturnWithoutClient(CancellationToken ct)
        {
            if (!NoClientInitialized) throw new InvalidOperationException("Confirm that no gameplay client was initialized before using this fallback.");
            return Coordinator.MatchAction(Verified.Match, "return", ct);
        }
        internal WordboundPartyLaunchContext(OnlineCoordinatorClient client, OnlineAdmissionVerifier verifier,
            VerifiedOnlineMatch verified, WordboundClientContent content, Func<CancellationToken, Task<WordboundFusionJoin>> fresh)
        { Coordinator = client; Verifier = verifier; Verified = verified; Content = content; FreshJoin = fresh; }
    }
}
