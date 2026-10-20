// Game Studio Kit (MIT). Applies AudioImportRules to audio clips on their first import (no .meta yet), so later manual
// tuning in the Inspector survives, and reports deviations on demand:
//  exec GameStudioKit.Editor.Audio.KitAudioImport.Audit  args {"root": "Assets/Audio", "fix": false}
//  → {schema:1, root, files:[{path, category, ok, deviations[]}], checked, deviating, verdict}
// The sample rate is always preserved (the sheet's 48 kHz masters). Folders that match no category are untouched.
using System;
using System.Collections.Generic;
using GameStudioKit.Common;
using UnityEditor;
using UnityEngine;

namespace GameStudioKit.Editor.Audio
{
    public sealed class KitAudioImport : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            if (!assetImporter.importSettingsMissing) return;
            var rule = AudioImportRules.RuleFor(AudioImportRules.CategoryFor(assetPath));
            if (rule == null) return;
            Apply((AudioImporter)assetImporter, rule);
        }

        public static void Apply(AudioImporter importer, AudioRule rule)
        {
            var s = importer.defaultSampleSettings;
            s.loadType = LoadType(rule.LoadType);
            s.compressionFormat = Format(rule.Format);
            s.quality = Mathf.Clamp01(rule.Quality);
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = rule.Preload;
            importer.defaultSampleSettings = s;
            importer.forceToMono = rule.ForceMono;
            importer.loadInBackground = rule.LoadInBackground;
        }

        /// <summary>Lists clips whose settings differ from their folder rule; with "fix": true, re-applies the rule.</summary>
        public static void Audit()
        {
            var args = KitExec.Args();
            string root = KitJson.GetString(args, "root", "Assets");
            bool fix = KitJson.GetBool(args, "fix");
            var files = new List<object>();
            int deviating = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                string category = AudioImportRules.CategoryFor(path);
                var rule = AudioImportRules.RuleFor(category);
                if (rule == null) continue;
                var deviations = Deviations(importer, rule);
                if (deviations.Count > 0)
                {
                    deviating++;
                    if (fix)
                    {
                        Apply(importer, rule);
                        importer.SaveAndReimport();
                    }
                }
                files.Add(new Dictionary<string, object>
                {
                    { "path", path },
                    { "category", category },
                    { "ok", deviations.Count == 0 },
                    { "deviations", deviations },
                });
            }
            KitExec.Result(new Dictionary<string, object>
            {
                { "schema", 1 },
                { "root", root },
                { "files", files },
                { "checked", files.Count },
                { "deviating", deviating },
                { "fixed", fix ? deviating : 0 },
                { "verdict", deviating == 0 || fix ? "PASS" : "FAIL" },
            });
        }

        public static List<object> Deviations(AudioImporter importer, AudioRule rule)
        {
            var d = new List<object>();
            var s = importer.defaultSampleSettings;
            if (s.loadType != LoadType(rule.LoadType)) d.Add("loadType " + s.loadType + " (rule " + rule.LoadType + ")");
            if (s.compressionFormat != Format(rule.Format)) d.Add("compressionFormat " + s.compressionFormat + " (rule " + rule.Format + ")");
            if (s.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate) d.Add("sample rate is not preserved");
            if (importer.forceToMono != rule.ForceMono) d.Add("forceToMono " + importer.forceToMono + " (rule " + rule.ForceMono + ")");
            if (s.preloadAudioData != rule.Preload) d.Add("preloadAudioData " + s.preloadAudioData + " (rule " + rule.Preload + ")");
            return d;
        }

        static AudioClipLoadType LoadType(string s)
        {
            switch (s)
            {
                case "streaming": return AudioClipLoadType.Streaming;
                case "compressed_in_memory": return AudioClipLoadType.CompressedInMemory;
                default: return AudioClipLoadType.DecompressOnLoad;
            }
        }

        static AudioCompressionFormat Format(string s)
        {
            switch (s)
            {
                case "adpcm": return AudioCompressionFormat.ADPCM;
                case "pcm": return AudioCompressionFormat.PCM;
                default: return AudioCompressionFormat.Vorbis;
            }
        }
    }
}
