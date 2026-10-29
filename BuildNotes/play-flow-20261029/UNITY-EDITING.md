# Edit the Play screen in Unity

Open `wordbound-integration` in Unity 6000.6.4f1. Use **Wreckabulary → Open Current Workspace → Play** to preview the actual menu. Stop Play before saving design changes.

| Select this saved asset | Change in the Inspector |
| --- | --- |
| `Assets/_Project/Resources/UI/LobbyPlayLayout.asset` | Mode width/height, map width/height/column count and actual course preview sprites. |
| `Assets/_Project/Prefabs/UI/Play/PlayPage.prefab` | Content inset, header/summary height and centered category navigation width/height on LobbyPlayPageView. |
| The Workspace's shared UI theme and typography profiles | Neutral colours, restrained selection accents and Roboto font roles. |

Mode buttons and map cards are generated from the current choices. Their dimensions are editable in these saved controls; their individual runtime objects are not permanent prefabs. Keep the original Resources layout as a baseline when trying a copied design, and preserve valid references. The grid adapts to available width; focused cards scroll into view while categories, modes and Play stay fixed.

**Practice (Offline)** has the five original House games plus Time Trial and Freeplay. **Create** has Home builder, Map editor and Tutorial. Matchmaking uses the existing separate Wordbound lobby. Internal mode IDs remain unchanged for saves and online compatibility. Escape offers Random; Home builder supports its existing compatible houses; circuit and arena maps retain their real gameplay restrictions.

Use the [separate saved course entries](../../docs/wordbound/SCENES-AND-TESTING.md) for Flagship, Freestyle, Parcel Service or Kitchen. Open an entry through **Wreckabulary → Course entries → Open … for Play** so Play tests that scene. Each course keeps its own bindings and settings.

The arena preview is a faithful static capture of the installed arena. It does not show match activity or establish final artwork quality. BELT and material-specific sound profiles remain planned work; they are not selectable finished items in this build.
