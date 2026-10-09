# WinterShuffle

STATUS: AUTHORED AND ROUND-TRIP VERIFIED; Unity import and native playback remain to be run by the integration owner.

This is an original 2.4-second, 30 fps Generic-rig animation for the existing avatar. The separate FBX contains the unchanged 22-bone hierarchy and one take. It contains no duplicate character mesh. `WinterShuffle.blend` preserves the editable action with the existing avatar meshes as reference; `manifest.json` records source/export hashes and all 73 motion samples.

The dance has a left step, right step, knee bounce and two raised mitten claps at frames 22 and 50, then returns to the initial planted pose. Root translation is zero. The fixed-length 0.241 m arms cannot reach over the 0.988 m head; the approved adaptation places the claps in front of the face without stretching the arms. Preview review also adjusted wrist orientation and contact spacing to retain the original mitten shape.

Blender checks every frame for a grounded support boot and bounded leg/arm IK error. It reimports the exported FBX and compares the hierarchy, duration, take count and sampled root/head/hand/knee/foot positions. Current numeric results and SHA-256 values are in the manifest. The original Avatar FBX and its seventeen clips are never written.

The seven PNGs are Blender pose previews using the existing material colours and textures. They are not native Unity evidence. Native tests additionally check that every curve resolves on the original Animator, actual hand/knee motion, floor/root stability, unscaled playback, return to Idle and graph disposal.

Reproduce from the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/author_winter_emote.py -- --source Assets/_Project/Art/Imported/Avatar/Avatar.fbx --output ArtSource/Collections/Winter/Animation --render
```

The Unity copy is `Assets/_Project/Art/Seasonal/Winter/Animations/WinterShuffle.fbx`. `WinterEmoteImporter.Build()` copies changed source bytes and imports this single path. The imported clip is named `WinterShuffle`, non-looping and Generic. `LobbyStage.PlayEmote(AnimationClip)` is independent of the seasonal collection asset; its owner supplies the clip reference.
