# Cloud tools and checks

Use the existing checkout; a cloud task is already isolated. Download Git LFS
objects before opening Unity (`git lfs pull`, then `git lfs fsck`).
Reviewed web GLBs, generated UI images and art-review renders are stored directly
in Git; each is below 8 MB. The original supplied art retains its existing LFS
storage. This avoids a separate LFS upload prerequisite for the new derivatives.

The rules harness requires .NET9. The rootless Linux installer is pinned to
SDK9.0.318; its hashes were checked against Microsoft's signed repository metadata.
It preserves TLS and checksum checks:

```sh
python3 Tools/CloudSetup/install-dotnet.py
export DOTNET_ROOT=/workspace/.cloud-setup/dotnet-root/usr/share/dotnet
export PATH="$DOTNET_ROOT:$PATH"
dotnet run --project Tools/RulesHarness
```

An optional C# source check compiles all six assemblies separately against installed
Unity DLLs, using .NET's compiler: `python3 Tools/CloudSetup/compile-sources.py`.
It records the exact package DLL paths. Without a successful project import it uses
editor template-cache DLLs, which may differ from the pinned package versions.
This does not execute Unity or validate its import, physics, rendering or builds.

Unity is pinned by `ProjectSettings/ProjectVersion.txt` to 6000.6.3f1. Use the
official editor and matching platform modules. Unity Personal must be activated
through Unity Hub on the machine where the editor runs. Do not put credentials
or a license in this repository or chat. Linux PC support ships with the editor;
Unity Web, Windows and Android require their matching build-support modules.
iOS builds require a supported Mac toolchain.

```sh
export UNITY_EDITOR_PATH=/path/to/6000.6.3f1/Editor/Unity
bash Tools/CloudSetup/verify-unity.sh
bash Tools/CloudSetup/verify-unity.sh --build
```

The first command sets up imported art/data and runs real EditMode and PlayMode
tests. The second additionally builds Linux PC and Unity Web. Each invocation saves
fresh logs/XML in its own ignored `Logs/Verification/run.*` directory. Graphics captures require a graphics-capable Unity
run; a headless rules pass is not a rendering check. Other targets are available
through the Wreckabulary/Build menu or `ProductionBuild.Windows`/`.Android`.

The standalone HTML edition lives in `Web`; its README gives build, tests and
server instructions. Its output is separate from a Unity Web build. Dependencies
are bundled locally; no runtime CDN or image/model provider keys are needed.
