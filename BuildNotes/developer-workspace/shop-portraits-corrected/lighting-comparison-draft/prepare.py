from pathlib import Path
import difflib, hashlib, json

out = Path(__file__).resolve().parent
root = out.parents[3]
path = 'Assets/_Project/Editor/Art/ShopPreviewImporter.cs'
base = (root/path).read_bytes()
baseline = out/'baseline'/path
baseline.parent.mkdir(parents=True,exist_ok=True)
if baseline.exists() and baseline.read_bytes() != base: raise RuntimeError('Live baseline changed; inspect before refreshing draft.')
baseline.write_bytes(base)
value = base.decode().replace('\r\n','\n')
value = value.replace('            public string id, name, previousPng, previousMeta, previousGuid, previousImporter;', '            public string control, outfit;\n            public string id, name, previousPng, previousMeta, previousGuid, previousImporter;')
value = value.replace('            public bool ssaoBeforeRender, ssaoAfterRender;', '''            public bool ssaoBeforeRender, ssaoAfterRender;
            public string nativeTargetFormat, rgbaFormat, rgbaSha256;
            public bool nativeTargetSrgb, rgbaIsDataSrgb, alphaVerified;
            public int alphaZero, alphaOpaque, alphaPartial, borderNonzeroAlpha, rgbMaxDelta, rgbChangedPixels;''')
value = value.replace('public bool asyncShaders, ssao, fog;', 'public bool asyncShaders, ssao, fog, srgbWrite;')
value = value.replace('asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"),', 'asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"), srgbWrite = GL.sRGBWrite,')
needle = '        static void Capture(Portrait result, string folder, AvatarSurfaceProfile profile, AnimationClip idle)'
addition = '''        [MenuItem("Wreckabulary/Art/Compare corrected portrait lighting")]
        public static void CompareLighting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline)
                throw new InvalidOperationException("Compare in Edit Mode with the active render pipeline and a graphics device.");
            var profile = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            var library = ModelLibrary.Load(); var avatar = library ? library.Find("Avatar/Avatar") : null;
            var idle = library ? library.FindClip("Avatar/Avatar", "Idle") : null;
            var materials = MaterialLibrary.Load(); var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!avatar || !idle || !materials || !data || CosmeticBundles.Find("courier") == null)
                throw new InvalidOperationException("The original courier portrait inputs are required.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName, colorSpace = QualitySettings.activeColorSpace.ToString(), globalBefore = Global(),
                folder = Evidence + "-lighting/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                scope = "Nonpublishing courier lighting comparison: current preview lighting, neutral front/fill 1.15/0.65, then the same neutral directions at 1.55/0.85. All use the saved corrected head, original materials/tints, Idle at zero, pose, camera and ambient. A to B is a lighting-preset comparison; B to C changes intensities only. Each frame also has native RGBA readback with alpha and RGB-transfer validation. No target portrait, metadata, original material, texture or scene is saved." };
            TrackSources(report, avatar, idle, library, materials, data, profile);
            foreach (string id in Ids)
                foreach (string target in new[] { Target(id), Target(id) + ".meta" })
                    report.files.Add(new FileProof { path = target, before = FileHash(target) });
            string folder = Full(report.folder);
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Preserve existing lighting evidence: " + folder);
            Directory.CreateDirectory(Path.Combine(folder, "Rendered"));
            var active = RenderTexture.active; bool asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                foreach (string control in new[] { "A-current", "B-neutral-front", "C-neutral-front-brighter" })
                {
                    var portrait = new Portrait { id = "courier", name = "Mint Messenger", control = control, previousPng = FileHash(Target("courier")) };
                    report.portraits.Add(portrait); Capture(portrait, folder, profile, idle);
                }
                report.status = "CAPTURED_NOT_VISUALLY_APPROVED";
            }
            catch (Exception error) { report.status = "FAIL"; report.error = error.ToString(); }
            finally
            {
                try { Restore(active, asyncShaders, ssao); } catch (Exception error) { report.error += "\\nRestore: " + error; }
                try { VerifySources(report); } catch (Exception error) { report.error += "\\nPreservation: " + error; }
                if (!report.sourcesAndGlobalPreserved || report.error.Length != 0) report.status = "FAIL";
                WriteNew(Path.Combine(folder, "receipt.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
            }
            if (report.status == "FAIL") throw new InvalidOperationException("Lighting comparison failed; preserve " + folder + "\\n" + report.error);
            Debug.Log("SHOP_PORTRAIT_LIGHTING: comparisons=3; published=0; preserved=true; receipt=" + report.folder + "/receipt.json; visual review pending.");
        }

'''
assert value.count(needle)==1
value=value.replace(needle,addition+needle)
needle='            PreviewRenderUtility preview = null; GameObject holder = null; Texture2D image = null; Scene scene = default; bool begun = false;'
assert value.count(needle)==1
value=value.replace(needle,needle.replace('Texture2D image = null;', 'Texture2D image = null, rgba = null;'))
needle='                PlayerAppearance.Dress(model.GetComponentsInChildren<SkinnedMeshRenderer>(true), outfit);'
assert value.count(needle)==1
value=value.replace(needle, needle+'\n                result.outfit = outfit.Serialize();')
needle='                preview.ambientColor = new Color(.45f, .45f, .45f);'
assert value.count(needle)==1
value=value.replace(needle,needle+'''
                if (result.control == "B-neutral-front" || result.control == "C-neutral-front-brighter")
                {
                    preview.lights[0].color = preview.lights[1].color = Color.white;
                    preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(25f, -25f, 0f);
                    preview.lights[1].transform.rotation = camera.transform.rotation * Quaternion.Euler(10f, 35f, 0f);
                    if (result.control == "C-neutral-front-brighter")
                    { preview.lights[0].intensity = 1.55f; preview.lights[1].intensity = .85f; }
                }''')
needle='                image = preview.EndStaticPreview(); begun = false; result.ssaoAfterRender = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");'
assert value.count(needle)==1
value=value.replace(needle,'                if (!string.IsNullOrEmpty(result.control)) rgba = ReadNativeRgba(preview.camera.targetTexture, result);\n'+needle)
needle='                WriteNew(Path.Combine(folder, "Rendered", result.id + ".png"), bytes);'
assert value.count(needle)==1
value=value.replace(needle,'''                string imageName = string.IsNullOrEmpty(result.control) ? result.id : result.control + "-" + result.id;
                WriteNew(Path.Combine(folder, "Rendered", imageName + ".png"), bytes);
                if (rgba)
                {
                    byte[] alphaBytes = rgba.EncodeToPNG(); result.rgbaSha256 = AvatarSurfaceMeshData.Hash(alphaBytes);
                    WriteNew(Path.Combine(folder, "Rendered", imageName + "-rgba.png"), alphaBytes);
                    ValidateAlphaAndTransfer(rgba, image, result);
                }''')
needle='                Clean(() => { if (image) Object.DestroyImmediate(image); }, result);'
assert value.count(needle)==1
value=value.replace(needle,needle+'\n                Clean(() => { if (rgba) Object.DestroyImmediate(rgba); }, result);')
value=value.replace('result.remainingOwnedObjects = (holder ? 1 : 0) + (image ? 1 : 0);', 'result.remainingOwnedObjects = (holder ? 1 : 0) + (image ? 1 : 0) + (rgba ? 1 : 0);')
needle='        static void TrackSources(Report report, GameObject avatar, AnimationClip idle, ModelLibrary library, MaterialLibrary materials, GameData data, AvatarSurfaceProfile profile)'
addition='''        static Texture2D ReadNativeRgba(RenderTexture source, Portrait result)
        {
            if (!source || !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(source.graphicsFormat))
                throw new InvalidOperationException("The native preview target has no alpha channel.");
            result.nativeTargetFormat = source.graphicsFormat.ToString(); result.nativeTargetSrgb = source.sRGB;
            RenderTexture encoded = null; Texture2D copy = null; var active = RenderTexture.active; bool srgbWrite = GL.sRGBWrite;
            try
            {
                encoded = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                if (!encoded.Create() || !encoded.sRGB || !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(encoded.graphicsFormat))
                    throw new InvalidOperationException("Could not create a native sRGB alpha output target.");
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, encoded); RenderTexture.active = encoded;
                copy = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                copy.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false); copy.Apply(false, false);
                result.rgbaFormat = copy.graphicsFormat.ToString(); result.rgbaIsDataSrgb = copy.isDataSRGB;
                var output = copy; copy = null; return output;
            }
            finally
            {
                RenderTexture.active = active; GL.sRGBWrite = srgbWrite;
                if (copy) Object.DestroyImmediate(copy);
                if (encoded) { encoded.Release(); Object.DestroyImmediate(encoded); }
            }
        }

        static void ValidateAlphaAndTransfer(Texture2D rgba, Texture2D rgb, Portrait result)
        {
            var alphaPixels = rgba.GetPixels32(); var reference = rgb.GetPixels32();
            if (alphaPixels.Length != reference.Length || rgba.width != Width || rgba.height != Height || !rgba.isDataSRGB
                || !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(rgba.graphicsFormat))
                throw new InvalidOperationException("Invalid alpha readback dimensions or format.");
            for (int i = 0; i < alphaPixels.Length; i++)
            {
                var a = alphaPixels[i]; var b = reference[i];
                if (a.a == 0) result.alphaZero++; else if (a.a == 255) result.alphaOpaque++; else result.alphaPartial++;
                int x = i % Width, y = i / Width;
                if ((x == 0 || x == Width - 1 || y == 0 || y == Height - 1) && a.a != 0) result.borderNonzeroAlpha++;
                int delta = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
                result.rgbMaxDelta = Mathf.Max(result.rgbMaxDelta, delta); if (delta != 0) result.rgbChangedPixels++;
            }
            result.alphaVerified = result.alphaZero > 1000 && result.alphaOpaque > 1000 && result.borderNonzeroAlpha == 0
                && alphaPixels[0].a == 0 && alphaPixels[(Height - 1 - 200) * Width + 128].a == 255 && result.rgbMaxDelta <= 2;
            if (!result.alphaVerified) throw new InvalidOperationException("Native alpha or RGB transfer did not pass; preserve both images and inspect the receipt.");
        }

'''
assert value.count(needle)==1
value=value.replace(needle,addition+needle)
candidate=out/'candidate'/path
candidate.parent.mkdir(parents=True,exist_ok=True); candidate.write_bytes(value.encode())
patch='diff --git a/'+path+' b/'+path+'\n'+''.join(difflib.unified_diff(base.decode().replace('\r\n','\n').splitlines(keepends=True),value.splitlines(keepends=True),fromfile='a/'+path,tofile='b/'+path))
(out/'lighting-comparison.patch').write_bytes(patch.encode())
manifest={'status':'DRAFT_SOURCE_REVIEW_ONLY','files':[{'path':path,'baseline_sha256':hashlib.sha256(base).hexdigest(),'candidate_sha256':hashlib.sha256(value.encode()).hexdigest()}],'patch_sha256':hashlib.sha256(patch.encode()).hexdigest()}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps(manifest,indent=2))
