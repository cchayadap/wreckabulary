@echo off
setlocal
rem flip.cmd: NVIDIA FLIP perceptual score for hero shots (game-qa-probes; research_qa_harness 1.7).
rem   Tools\kit\qa\flip.cmd reference.png test.png [out_dir]
rem flip-evaluator (BSD) ships Windows wheels for CPython 3.8-3.13 only, so this pins Python 3.13 through uv. Every
rem package is pinned to an exact version; --no-config and the cleared index variables keep a stray uv.toml or
rem UV_INDEX_URL from pointing uv at another index.
rem Arguments are forwarded with %%* only (CONTRACTS.md 1.1). Exit 0 ok, 2 usage, 3 tool or environment error.
rem flip_eval.py exits 64 (usage) or 65 (tool error), codes uv never uses, so any other code is uv failing to set up.
if "%~2"=="" (
  echo {"ok":false,"exit":2,"error":"usage: flip.cmd reference.png test.png [out_dir]"}
  exit /b 2
)
where uv >nul 2>nul
if errorlevel 1 (
  echo {"ok":false,"exit":3,"error":"uv is not on PATH; install uv (see kit doctor)"}
  exit /b 3
)
for %%V in (UV_INDEX UV_INDEX_URL UV_DEFAULT_INDEX UV_EXTRA_INDEX_URL UV_FIND_LINKS PIP_INDEX_URL PIP_EXTRA_INDEX_URL PIP_FIND_LINKS) do set "%%V="
rem call: uv may be a .cmd shim, and a batch file started without call never returns here.
call uv run --no-config --no-project --python 3.13 --with flip-evaluator==1.7 --with numpy==2.5.3 --with pillow==12.3.0 python "%~dp0flip_eval.py" %*
set "RC=%errorlevel%"
if "%RC%"=="0" exit /b 0
if "%RC%"=="64" exit /b 2
if "%RC%"=="65" exit /b 3
echo {"ok":false,"exit":3,"error":"uv could not start flip_eval.py (uv exit %RC%): Python 3.13 or a pinned package is not available, for example offline; see the uv message on stderr"}
exit /b 3
