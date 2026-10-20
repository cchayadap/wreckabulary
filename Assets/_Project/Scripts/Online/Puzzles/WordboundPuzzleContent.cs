using System;
using System.Linq;
using System.Text;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public interface IWordboundPuzzleContent
    {
        ModeDefinition Mode { get; }
        CourseCatalogue Geometry { get; }
        ResolvedCourse Course { get; }
        WordboundPuzzlePlan Plan { get; }
        ItemCatalogue Items { get; }
        string CatalogueHash { get; }
        bool BindsRules { get; }
        GameRules Rules { get; }
        void Validate(MatchBinding binding, GameRules actualRules);
    }

    public sealed class WordboundAuthoredPuzzleContent : IWordboundPuzzleContent
    {
        readonly string itemJson;
        readonly GameRules rules;
        public ModeDefinition Mode { get; }
        public CourseCatalogue Geometry { get; }
        public ResolvedCourse Course { get; }
        public WordboundPuzzlePlan Plan { get; }
        public ItemCatalogue Items => ItemCatalogue.FromJson(itemJson);
        public string ItemSourceHash { get; }
        public string RuleSourceHash { get; }
        public string EffectiveRulesHash { get; }
        public string CatalogueHash { get; }
        public bool BindsRules => true;
        public GameRules Rules => rules.Clone();
        public WordboundAuthoredPuzzleContent(ModeDefinition mode, CourseCatalogue geometry, ResolvedCourse course,
            string itemCatalogueJson, string ruleBookJson)
            : this(mode, geometry, course, new WordboundPuzzlePlan(course), itemCatalogueJson, ruleBookJson) { }

        public WordboundAuthoredPuzzleContent(ModeDefinition mode, CourseCatalogue geometry, ResolvedCourse course,
            WordboundPuzzlePlan plan, string itemCatalogueJson, string ruleBookJson)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            Course = course ?? throw new ArgumentNullException(nameof(course));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (!mode.IsRace && plan.Sites.Count != 0 || course.SourceHash != plan.SourceHash || course.Hash != plan.ManifestHash ||
                !CourseValidator.ValidateResolved(course, geometry, MatchStateValidation.Requirements(mode)).Ok)
                throw new ArgumentException("Authored content requires the exact valid resolved course and mode-specific physical plan.");
            CheckSource(itemCatalogueJson, "Item catalogue");
            CheckSource(ruleBookJson, "Rule book");
            itemJson = itemCatalogueJson;
            if (Items.Validate(64).Count != 0) throw new ArgumentException("Authored item catalogue is invalid.");
            var book = RuleBook.FromJson(ruleBookJson);
            if (book.Validate().Count != 0) throw new ArgumentException("Authored rule book is invalid.");
            rules = mode.CreateGameplayRules(book.For("Dibs"));
            if (rules.Validate().Count != 0) throw new ArgumentException("Authored gameplay rules are invalid.");
            ValidatePhysicalGameplay();
            ItemSourceHash = WordboundIdentity.Sha256(itemCatalogueJson);
            RuleSourceHash = WordboundIdentity.Sha256(ruleBookJson);
            EffectiveRulesHash = WordboundRulesFingerprint.Compute(rules);
            CatalogueHash = WordboundIdentity.Sha256(string.Join("|", "wordbound.authored-physical-content", "1",
                geometry.Hash, ItemSourceHash, Plan.Hash, RuleSourceHash, EffectiveRulesHash));
        }

        public void Validate(MatchBinding binding, GameRules actualRules)
        {
            Plan.Validate(binding);
            if (binding.ModeHash != Mode.Hash || binding.CatalogueHash != CatalogueHash ||
                WordboundRulesFingerprint.Compute(actualRules) != EffectiveRulesHash)
                throw new ArgumentException("Authored physical content or gameplay rules differ from signed admission.");
        }

        void ValidatePhysicalGameplay()
        {
            var catalogue = Items;
            foreach (var site in Plan.Sites)
            {
                var puzzle = Course.Puzzles.Single(p => p.Id == site.PuzzleId);
                if (!Mode.Recipes.Contains(puzzle.RecipeId) || !catalogue.TryGet(puzzle.RecipeId, out var recipe) ||
                    !recipe.Enabled || recipe.Size == null || recipe.Size.Length != 3)
                    throw new ArgumentException("Physical puzzles require an available, dimensioned recipe.");
                if (puzzle.PresetId == "BuildRoute" && (recipe.Deploy == null || recipe.Deploy.Effect != DeployEffect.JumpPad ||
                    recipe.Deploy.Strength <= 0 || recipe.Deploy.LifetimeSeconds != 0))
                    throw new ArgumentException("Traversal requires the actual permanent catalogue spring pad.");
                if (puzzle.PresetId == "RedirectWind" || puzzle.PresetId == "TimedMechanism")
                {
                    var expected = puzzle.PresetId == "RedirectWind" ? DeployEffect.WindField : DeployEffect.SlowField;
                    if (recipe.Deploy == null || recipe.Deploy.Effect != expected)
                        throw new ArgumentException("Physical field recipe has the wrong catalogue effect.");
                    WordboundFieldPolicy.Validate(recipe.Deploy);
                    if (puzzle.PresetId == "TimedMechanism" && (site.Authored.Fixtures.Count == 0 ||
                        recipe.Deploy.LifetimeSeconds > 0 &&
                        site.ActiveWindowTicks >= recipe.Deploy.LifetimeSeconds * ModeDefinition.TickRate))
                        throw new ArgumentException("Timed machinery crossing must fit inside the actual CLOCK lifetime.");
                }
                if (puzzle.PresetId == "RedirectWind" && (!Mode.Recipes.Contains(site.WindReceiverRecipeId) ||
                    !catalogue.TryGet(site.WindReceiverRecipeId, out var receiver) || !receiver.Enabled ||
                    !Course.Placements.Where(p => p.ZoneId == site.WindReceiverZoneId)
                        .All(p => Geometry.Assets[p.AssetId].Letters == site.WindReceiverRecipeId)))
                    throw new ArgumentException("Wind's authored prop and replacement must use the same actual recipe.");
                if (site.Authored.ToolRecipeId != "" && (!Mode.Recipes.Contains(site.Authored.ToolRecipeId) ||
                    !catalogue.TryGet(site.Authored.ToolRecipeId, out var tool) || !tool.Enabled || tool.Melee == null ||
                    tool.Melee.BreakPower <= 0))
                    throw new ArgumentException("Smash instructions require an actual usable break tool.");
            }
        }

        static void CheckSource(string source, string name)
        {
            if (source == null || source.Length > CourseLimits.DocumentBytes ||
                Encoding.UTF8.GetByteCount(source) > CourseLimits.DocumentBytes)
                throw new ArgumentException(name + " exceeds the authored content bound.");
            int depth = 0;
            bool quoted = false, escaped = false;
            foreach (char c in source)
            {
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == '{' || c == '[')
                {
                    if (++depth > 16) throw new ArgumentException(name + " nesting exceeds the authored content bound.");
                }
                else if (c == '}' || c == ']')
                {
                    if (--depth < 0) throw new ArgumentException(name + " has unmatched containers.");
                }
            }
            if (quoted || depth != 0) throw new ArgumentException(name + " is not a complete JSON document.");
        }
    }
}
