from pathlib import Path
import difflib, hashlib, json
out=Path(__file__).resolve().parent
root=out.parents[3]
path='Assets/_Project/Editor/Art/ShopPreviewImporter.cs'
base=(root/path).read_bytes()
baseline=out/'baseline'/path; baseline.parent.mkdir(parents=True,exist_ok=True)
if baseline.exists() and baseline.read_bytes()!=base: raise RuntimeError('Live baseline changed; inspect before refreshing.')
baseline.write_bytes(base)
value=base.decode().replace('\r\n','\n')

def replace(before,after):
    global value
    assert value.count(before)==1, before
    value=value.replace(before,after)

replace('        const int Width = 256, Height = 320;', '        const int Width = 256, Height = 320;\n        const string ProductionControl = "D-neutral-front-soft";')
replace('            public string control, outfit;', '            public string control, outfit, rgbReferenceSha256, importedGraphicsFormat;\n            public bool publishRgba, importedHasAlpha, importedSrgb, importedCompressed;')
replace('Six existing shop looks through ModelVisual.Spawn and the saved exact head correction; original Idle, wardrobe, materials, camera, numerical lights and framing. Existing target PNG/meta backed up before rendering. Original source files/meta and memory preserved. Sequential target-only publication after staging; art acceptance remains pending.',
    'Six existing shop looks through ModelVisual.Spawn and the saved exact head correction; selected D neutral front lights at 0.8/0.4, original Idle, wardrobe, materials, camera and framing. Native RGBA output is validated against an RGB reference and imported with input alpha, sRGB and no compression. Existing target PNG/meta backed up before rendering. Original source files/meta and memory preserved. Sequential target-only publication after staging; actual Shop edge review and art acceptance remain pending.')
replace('report.portraits.Add(new Portrait { id = look.Id, name = look.Name, previousPng = FileHash(path), previousMeta = FileHash(path + ".meta"),',
    'report.portraits.Add(new Portrait { id = look.Id, name = look.Name, control = ProductionControl, publishRgba = true, previousPng = FileHash(path), previousMeta = FileHash(path + ".meta"),')
replace('                    importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;', '''                    importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.sRGBTexture = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;''')
replace('                    portrait.importedWidth = image ? image.width : 0; portrait.importedHeight = image ? image.height : 0;', '''                    portrait.importedWidth = image ? image.width : 0; portrait.importedHeight = image ? image.height : 0;
                    portrait.importedGraphicsFormat = image ? image.graphicsFormat.ToString() : "MISSING";
                    portrait.importedHasAlpha = image && UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(image.graphicsFormat);
                    portrait.importedSrgb = image && image.isDataSRGB;
                    portrait.importedCompressed = image && UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsCompressedFormat(image.graphicsFormat);''')
replace('                        && portrait.importedWidth == Width && portrait.importedHeight == Height;', '''                        && portrait.importedWidth == Width && portrait.importedHeight == Height && portrait.alphaVerified
                        && portrait.importedHasAlpha && portrait.importedSrgb && !portrait.importedCompressed
                        && importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.sRGBTexture
                        && importer.textureCompression == TextureImporterCompression.Uncompressed;''')
replace('SHOP_PORTRAITS: count=6; source=original-avatar; saved_head_correction=true; width=256; height=320; receipt=',
    'SHOP_PORTRAITS: count=6; source=original-avatar; saved_head_correction=true; lighting=D-neutral-front-soft; rgba_verified=true; width=256; height=320; receipt=')
before='''                byte[] bytes = image.EncodeToPNG(); result.bytes = bytes.Length; result.stagedPng = AvatarSurfaceMeshData.Hash(bytes);
                string imageName = string.IsNullOrEmpty(result.control) ? result.id : result.control + "-" + result.id;
                WriteNew(Path.Combine(folder, "Rendered", imageName + ".png"), bytes);
                if (rgba)
                {
                    byte[] alphaBytes = rgba.EncodeToPNG(); result.rgbaSha256 = AvatarSurfaceMeshData.Hash(alphaBytes);
                    WriteNew(Path.Combine(folder, "Rendered", imageName + "-rgba.png"), alphaBytes);
                    ValidateAlphaAndTransfer(rgba, image, result);
                }'''
after='''                byte[] bytes = image.EncodeToPNG(); result.bytes = bytes.Length; result.stagedPng = AvatarSurfaceMeshData.Hash(bytes);
                result.rgbReferenceSha256 = result.stagedPng;
                string imageName = result.publishRgba || string.IsNullOrEmpty(result.control) ? result.id : result.control + "-" + result.id;
                WriteNew(Path.Combine(folder, "Rendered", imageName + (result.publishRgba ? "-rgb-reference.png" : ".png")), bytes);
                if (result.publishRgba && !rgba) throw new InvalidOperationException("Native RGBA is required before portrait publication.");
                if (rgba)
                {
                    byte[] alphaBytes = rgba.EncodeToPNG(); result.rgbaSha256 = AvatarSurfaceMeshData.Hash(alphaBytes);
                    WriteNew(Path.Combine(folder, "Rendered", imageName + (result.publishRgba ? ".png" : "-rgba.png")), alphaBytes);
                    ValidateAlphaAndTransfer(rgba, image, result);
                    if (result.publishRgba)
                    {
                        result.stagedPng = result.rgbaSha256; result.bytes = alphaBytes.Length;
                        result.imageGraphicsFormat = rgba.graphicsFormat.ToString(); result.imageTextureFormat = rgba.format.ToString(); result.imageIsDataSRGB = rgba.isDataSRGB;
                    }
                }'''
replace(before,after)
candidate=out/'candidate'/path; candidate.parent.mkdir(parents=True,exist_ok=True); candidate.write_bytes(value.encode())
patch='diff --git a/'+path+' b/'+path+'\n'+''.join(difflib.unified_diff(base.decode().replace('\r\n','\n').splitlines(keepends=True),value.splitlines(keepends=True),fromfile='a/'+path,tofile='b/'+path))
(out/'production-adoption.patch').write_bytes(patch.encode())
manifest={'status':'DRAFT_SOURCE_REVIEW_ONLY','selected_control':'D-neutral-front-soft','native_basis':'20261026T121705173Z-54f5a1c3','files':[{'path':path,'baseline_sha256':hashlib.sha256(base).hexdigest(),'candidate_sha256':hashlib.sha256(value.encode()).hexdigest()}],'patch_sha256':hashlib.sha256(patch.encode()).hexdigest()}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps(manifest,indent=2))
