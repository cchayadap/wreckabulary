from pathlib import Path
import difflib, hashlib, json

root = Path(__file__).resolve().parents[4]
out = Path(__file__).resolve().parent
files = {}

def edit(path):
    original = (root / path).read_bytes()
    saved = out / 'baseline' / path
    saved.parent.mkdir(parents=True, exist_ok=True)
    if saved.exists() and saved.read_bytes() != original:
        raise RuntimeError('Baseline changed: ' + path)
    saved.write_bytes(original)
    return original.decode('utf-8').replace('\r\n', '\n')

def put(path, value):
    files[path] = value
    target = out / 'candidate' / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(value.encode('utf-8'))

path = 'Assets/_Project/Editor/Art/AvatarSurfaceCorrectionSetup.cs'
value = edit(path)
needle = '        public static void ValidateSource(Mesh source)\n'
addition = '''        public static AvatarSurfaceProfile VerifyExistingProfile(bool required = false)
        {
            var stored = AssetDatabase.LoadMainAssetAtPath(ProfilePath);
            if (!stored)
            {
                if (required || File.Exists(ProfilePath) || File.Exists(ProfilePath + ".meta"))
                    throw new InvalidOperationException("Create or repair the saved avatar surface profile before this operation.");
                return null;
            }
            if (stored is not AvatarSurfaceProfile profile) throw new InvalidOperationException("Invalid saved avatar surface profile.");
            return VerifyProfile(profile, required);
        }

        public static AvatarSurfaceProfile VerifyProfile(AvatarSurfaceProfile profile, bool required = false)
        {
            if (!profile) throw new InvalidOperationException("Avatar surface profile is missing.");
            if (!profile.EnabledCorrection)
            {
                if (required) throw new InvalidOperationException("This operation requires the saved avatar surface correction to be enabled.");
                return null;
            }
            if (!profile.TryValidate(out _)) throw new InvalidOperationException("Invalid saved avatar surface profile.");
            var library = ModelLibrary.Load();
            var avatar = library ? library.Find("Avatar/Avatar") : null;
            if (!avatar || AssetDatabase.GetAssetPath(avatar) != AvatarPath || profile.SourceAvatar != avatar
                || profile.SourceFingerprint != ExpectedSource
                || AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != profile.CorrectedMesh)
                throw new InvalidOperationException("Avatar correction references no longer match the reviewed saved assets.");
            var heads = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer => renderer.name == "SK_Head").ToArray();
            if (heads.Length != 1 || heads[0].sharedMesh != profile.SourceMesh
                || AnimationUtility.CalculateTransformPath(heads[0].transform, avatar.transform) != profile.RendererPath)
                throw new InvalidOperationException("The exact original avatar head binding changed.");
            ValidateSource(profile.SourceMesh);
            ValidateCorrected(profile.SourceMesh, profile.CorrectedMesh);
            return profile;
        }

        public static void RequireReviewedMeshContent(Mesh mesh)
        {
            if (!mesh || AvatarSurfaceMeshData.Fingerprint(mesh) != ExpectedSource)
                throw new InvalidOperationException("Avatar source content changed, even if its GUID and vertex counts match. Review a new correction version.");
        }

'''
assert value.count(needle) == 1
value = value.replace(needle, addition + needle)
old = '''                || AvatarSurfaceMeshData.Hash(File.ReadAllBytes(AvatarPath + ".meta")) != ExpectedMeta
                || AvatarSurfaceMeshData.Fingerprint(source) != ExpectedSource)'''
new = '''                || AvatarSurfaceMeshData.Hash(File.ReadAllBytes(AvatarPath + ".meta")) != ExpectedMeta)'''
assert old in value
value = value.replace(old, new).replace('            var face = Measure(source, 0);', '            RequireReviewedMeshContent(source);\n            var face = Measure(source, 0);', 1)
put(path, value)

path = 'Assets/_Project/Editor/Authoring/PlayerAuthoring.cs'
value = edit(path)
needle = '                wrapper = new GameObject("ImportedAvatar").transform;'
assert value.count(needle) == 1
value = value.replace(needle, '                AvatarSurfaceCorrectionSetup.VerifyExistingProfile();\n' + needle)
needle = '            var preview = new GameObject(PreviewName) { tag = "EditorOnly" };'
assert value.count(needle) == 1
value = value.replace(needle, '            AvatarSurfaceCorrectionSetup.VerifyExistingProfile();\n' + needle)
put(path, value)

put('Assets/_Project/Editor/Art/AvatarSurfaceValidationGuard.cs', '''using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    [InitializeOnLoad]
    public sealed class AvatarSurfaceValidationGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        static AvatarSurfaceValidationGuard()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                try { AvatarSurfaceCorrectionSetup.VerifyExistingProfile(); }
                catch (Exception error)
                {
                    EditorApplication.isPlaying = false;
                    Debug.LogError("Avatar surface validation stopped Play Mode. Preserve the changed source and review its correction. " + error.Message);
                }
            };
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            try { AvatarSurfaceCorrectionSetup.VerifyExistingProfile(); }
            catch (Exception error) { throw new BuildFailedException("Avatar surface validation stopped the build: " + error.Message); }
        }
    }
}
''')

put('Assets/_Project/Tests/Editor/AvatarSurfaceValidationTests.cs', '''using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public sealed class AvatarSurfaceValidationTests
    {
        [Test]
        public void DeliberatelyDisabledCorrectionAllowsSourceEditingButCannotProduceCorrectedPortraits()
        {
            var stored = AssetDatabase.LoadAssetAtPath<AvatarSurfaceProfile>(AvatarSurfaceCorrectionSetup.ProfilePath);
            Assert.IsNotNull(stored);
            byte[] file = File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath);
            byte[] meta = File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath + ".meta");
            bool dirty = EditorUtility.IsDirty(stored);
            string source = AvatarSurfaceMeshData.Fingerprint(stored.SourceMesh);
            var copy = ScriptableObject.CreateInstance<AvatarSurfaceProfile>();
            try
            {
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("enabledCorrection").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(copy.EnabledCorrection);
                Assert.IsNull(AvatarSurfaceCorrectionSetup.VerifyProfile(copy));
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.VerifyProfile(copy, true));
                serialized.FindProperty("enabledCorrection").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.VerifyProfile(copy));
            }
            finally { Object.DestroyImmediate(copy); }
            CollectionAssert.AreEqual(file, File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath));
            CollectionAssert.AreEqual(meta, File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath + ".meta"));
            Assert.AreEqual(dirty, EditorUtility.IsDirty(stored));
            Assert.AreEqual(source, AvatarSurfaceMeshData.Fingerprint(stored.SourceMesh));
        }

        [Test]
        public void SameCountSourceEditIsRejectedWithoutChangingOriginal()
        {
            var profile = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            string original = AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh);
            var copy = Object.Instantiate(profile.SourceMesh);
            try
            {
                Assert.DoesNotThrow(() => AvatarSurfaceCorrectionSetup.RequireReviewedMeshContent(copy));
                var vertices = copy.vertices; vertices[0] += Vector3.right * .001f; copy.vertices = vertices;
                Assert.AreEqual(profile.SourceMesh.vertexCount, copy.vertexCount);
                Assert.AreEqual(profile.SourceMesh.subMeshCount, copy.subMeshCount);
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.RequireReviewedMeshContent(copy));
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.ValidateCorrected(copy, profile.CorrectedMesh));
            }
            finally { Object.DestroyImmediate(copy); }
            Assert.AreEqual(original, AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh));
        }

        [Test]
        public void ExistingValidationReadsWithoutSavingOrRegenerating()
        {
            string[] paths = { AvatarSurfaceCorrectionSetup.AvatarPath, AvatarSurfaceCorrectionSetup.MeshPath, AvatarSurfaceCorrectionSetup.ProfilePath };
            var bytes = Array.ConvertAll(paths, path => File.ReadAllBytes(path));
            var metadata = Array.ConvertAll(paths, path => File.ReadAllBytes(path + ".meta"));
            var assets = Array.ConvertAll(paths, AssetDatabase.LoadMainAssetAtPath);
            var dirty = Array.ConvertAll(assets, EditorUtility.IsDirty);
            var first = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            Assert.AreSame(first, AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true));
            new AvatarSurfaceValidationGuard().OnPreprocessBuild(null);
            for (int i = 0; i < paths.Length; i++)
            {
                CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]));
                CollectionAssert.AreEqual(metadata[i], File.ReadAllBytes(paths[i] + ".meta"));
                Assert.AreSame(assets[i], AssetDatabase.LoadMainAssetAtPath(paths[i]));
                Assert.AreEqual(dirty[i], EditorUtility.IsDirty(assets[i]));
            }
        }
    }
}
''')

path = 'Assets/_Project/Scripts/Art/AvatarSurfaceProfile.cs'
value = edit(path)
value = value.replace('        [SerializeField] bool enabledCorrection = true;', '        [SerializeField, Tooltip("Disable automatic correction on new/raw spawns while editing the source. Existing saved renderer overrides stay assigned; change those explicitly if needed. Re-enable after reviewing a matching correction.")] bool enabledCorrection = true;')
value = value.replace('        public GameObject SourceAvatar => sourceAvatar;', '        public bool EnabledCorrection => enabledCorrection;\n        public GameObject SourceAvatar => sourceAvatar;')
old = '''            var target = instance.transform.Find(rendererPath);
            var skin = target ? target.GetComponent<SkinnedMeshRenderer>() : null;
            if (!skin || skin.sharedMesh != correctedMesh) return false;
            int replacements = 0;'''
new = '''            int boundHeads = 0;
            foreach (var modelRoot in instance.GetComponentsInChildren<Transform>(true))
            {
                var target = modelRoot.Find(rendererPath);
                var skin = target ? target.GetComponent<SkinnedMeshRenderer>() : null;
                if (skin && skin.sharedMesh == correctedMesh && ++boundHeads > 1) return false;
            }
            if (boundHeads != 1) return false;
            int replacements = 0;'''
assert value.count(old) == 1
put(path, value.replace(old, new))

path = 'Assets/_Project/Scripts/Online/Presentation/Tests/WordboundPresentationTests.cs'
value = edit(path)
needle = '        [TestCase("BAT"), TestCase("BROOM"), TestCase("HAMMER"), TestCase("SPEAR")]'
addition = '''        [Test]
        public void WrappedPlayerAdmitsOnlyTheExactSavedHeadCorrection()
        {
            var correction = AvatarSurfaceProfile.Load();
            Assert.IsNotNull(correction, "Create the saved avatar surface correction before this native test.");
            var source = ModelLibrary.Load().Find(WordboundVisualResolver.AvatarKey);
            using var visual = resolver.Create("player", new Vector3(.7f, 1.8f, .7f), anchor.transform);
            Mesh[] Read(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
                .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();
            var expected = Read(source);
            Assert.AreNotSame(visual.Root, visual.Model);
            Assert.IsTrue(visual.Model.transform.IsChildOf(visual.Root.transform));
            Assert.IsTrue(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
            var head = visual.Model.transform.Find(correction.RendererPath).GetComponent<SkinnedMeshRenderer>();
            var custom = Object.Instantiate(head.sharedMesh);
            try
            {
                head.sharedMesh = custom;
                Assert.IsFalse(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
                head.sharedMesh = correction.CorrectedMesh;
                var other = visual.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(renderer => renderer != head);
                var originalOther = other.sharedMesh;
                other.sharedMesh = custom;
                Assert.IsFalse(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
                other.sharedMesh = originalOther;
                var impostor = new GameObject("Unrelated head"); impostor.transform.SetParent(visual.Root.transform, false);
                impostor.AddComponent<MeshFilter>().sharedMesh = correction.CorrectedMesh;
                Assert.IsFalse(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
                Object.DestroyImmediate(impostor);
                Assert.IsTrue(correction.MatchesOnlyBoundHeadReplacement(source, visual.Root, expected, Read(visual.Root)));
            }
            finally { head.sharedMesh = correction.CorrectedMesh; Object.DestroyImmediate(custom); }
            Assert.AreSame(correction.SourceMesh, source.transform.Find(correction.RendererPath).GetComponent<SkinnedMeshRenderer>().sharedMesh);
        }

'''
assert needle in value
put(path, value.replace(needle, addition + needle, 1))

patch = ''
manifest = {'status': 'DRAFT_SOURCE_REVIEW_ONLY', 'files': []}
for path, value in files.items():
    base = out / 'baseline' / path
    old = base.read_text(encoding='utf-8').splitlines(keepends=True) if base.exists() else []
    patch += 'diff --git a/' + path + ' b/' + path + '\n'
    if not base.exists(): patch += 'new file mode 100644\n'
    patch += ''.join(difflib.unified_diff(old, value.splitlines(keepends=True), fromfile='a/' + path if base.exists() else '/dev/null', tofile='b/' + path))
    manifest['files'].append({'path': path, 'baseline_sha256': hashlib.sha256(base.read_bytes()).hexdigest() if base.exists() else None,
        'candidate_sha256': hashlib.sha256(value.encode()).hexdigest()})
(out / 'avatar-followup.patch').write_bytes(patch.encode())
manifest['patch_sha256'] = hashlib.sha256(patch.encode()).hexdigest()
(out / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(json.dumps(manifest, indent=2))
