# Native runtime libraries for ProjectM AND the JukeboxVisualizations.dll
# managed wrapper (after building this project) go here, flat — no subfolders.
#
# This folder is intentionally empty in the repository. It is populated
# by the fetch-natives script, which downloads the libprojectM + GLEW
# binaries from a GitHub release produced by the build-natives.yml CI
# workflow (see .github/workflows/build-natives.yml).
#
# ── Quick start ────────────────────────────────────────────────────
# Run one of these from the project root:
#
#   Windows:  .\fetch-natives.ps1
#   Linux:    ./fetch-natives.sh
#
# The script is idempotent — safe to re-run. Pass -Force / --force to
# re-download everything.
#
# ── What lives here after fetch-natives runs ───────────────────────
#
#   Windows:
#     libprojectM.dll       — ProjectM visualizer engine (LGPL v2.1+)
#     glew32.dll            — GLEW, required by libprojectM.dll (BSD/MIT)
#     libprojectM-LICENSE.txt   — LGPL license (required for redistribution)
#     glew-LICENSE.txt          — BSD/MIT license (required for redistribution)
#     BUILD-INFO.txt             — records the upstream commit + build time
#
#   Linux:
#     libprojectM.so.4      — ProjectM visualizer engine (LGPL v2.1+)
#     libprojectM-LICENSE.txt   — LGPL license (required for redistribution)
#     BUILD-INFO.txt             — records the upstream commit + build time
#
# After building this project, JukeboxVisualizations.dll (the managed
# wrapper produced by `dotnet publish`) ALSO goes in this same lib/
# folder when deployed to a Jukebox installation — it lives alongside
# the native libprojectM binary, bass.dll, libmpv-2.dll, etc.
#
# Windows .dll and Linux .so files coexist in this same folder; the
# loader code in Native/ProjectMNative.cs picks the right filename per
# OS at runtime.
#
# ── Why we don't commit these to git ───────────────────────────────
# See THIRD_PARTY_LICENSES.md — libprojectM is LGPL v2.1+ and has
# redistribution obligations (include LICENSE, make source available).
# Building from source in CI and downloading via the fetch script
# satisfies those obligations cleanly. The build-natives.yml workflow
# records the exact upstream commit, making the build reproducible.
#
# ── Updating libprojectM ───────────────────────────────────────────
# 1. Edit PROJECTM_REF in .github/workflows/build-natives.yml to point
#    at the new upstream tag/commit.
# 2. Commit and push the workflow file.
# 3. Trigger the workflow (manual dispatch or push a natives-v* tag).
# 4. Once it completes, copy the new SHA-256 from the release's
#    checksums.sha256 into natives.json in both repos.
# 5. Commit and push the manifest updates.
# 6. Re-run fetch-natives locally to get the new binaries.
#
# See README.md and THIRD_PARTY_LICENSES.md for more details.
