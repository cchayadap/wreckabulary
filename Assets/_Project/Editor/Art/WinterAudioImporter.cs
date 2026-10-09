using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public sealed class WinterAudioImporter : AssetPostprocessor
    {
        public const string AudioDirectory = "Assets/_Project/Audio/Winter/";
        public const string BankPath = "Assets/_Project/Resources/AudioPacks/Winter.asset";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioDirectory, System.StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            // Sources are already mono; disabling downmix also avoids importer normalization.
            importer.forceToMono = false;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("Wreckabulary/Art/Import Winter Audio")]
        public static void Build()
        {
            const string source = "ArtSource/Collections/Winter/Audio/";
            var names = new[] { "bell-01", "bell-02", "paper-01", "paper-02", "snow-01", "snow-02" };
            foreach (string name in names)
                if (!File.Exists(source + name + ".wav")) throw new FileNotFoundException("Winter source audio is missing", source + name + ".wav");
            Directory.CreateDirectory(AudioDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string name in names)
            {
                string target = AudioDirectory + name + ".wav";
                var bytes = File.ReadAllBytes(source + name + ".wav");
                if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(bytes)) File.WriteAllBytes(target, bytes);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            var bank = AssetDatabase.LoadAssetAtPath<GameSoundBank>(BankPath);
            if (!bank)
            {
                bank = ScriptableObject.CreateInstance<GameSoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            AudioClip[] Pair(string prefix) => new[] { "01", "02" }.Select(id => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDirectory + prefix + "-" + id + ".wav")).ToArray();
            bank.Configure(Pair("bell"), Pair("paper"), Pair("snow"));
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
        }
    }
}
