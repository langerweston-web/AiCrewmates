# AiCrewmates

A BepInEx / Reactor mod for the **PC (Steam)** version of Among Us that lets the host fill a **local, freeplay, or private** lobby with AI bots and play a real round with them.

**Targeted Among Us version:** Steam build matching **GameLibs `2026.9.29`** (Among Us **17.4.x**, same generation Town of Us Mira / Reactor 2.5.x use).  
**Stack:** BepInEx Unity IL2CPP + Harmony + Reactor (same family of tooling as Town of Us).

> This mod was developed and **compiled** in CI/cloud without running the game. In-game behavior is **untested**. Expect to tweak spawn/RPC details on your machine after the first launch.

---

## What works (implemented in source / compiles)

| Feature | Status |
| --- | --- |
| `dotnet build` → `AiCrewmates.dll` against GameLibs + Reactor | **Works** (verified in this repo) |
| Host `/addbot`, `/removebot`, `/fillbots`, `/clearbots`, `/bots` chat commands | Implemented |
| Lobby **ADD BOT / REMOVE BOT** buttons + **F6 / F7** keybinds | Implemented |
| Bot name + color assignment | Implemented |
| Block bots in **public** matchmaking lobbies | Implemented (config: `AllowOnlyPrivateOrLocal`) |
| Crewmate task pathing + delayed task completion | Implemented (untested in-game) |
| Sabotage reaction (walk to critical tasks / best-effort repair RPCs) | Implemented (untested) |
| Impostor fake tasks, isolated kills, venting, sabotage attempts | Implemented (untested) |
| Sighting memory, body report, meeting chat + votes | Implemented (untested) |
| Optional OpenAI-compatible meeting brain (endpoint / key / model in config) | Implemented (untested; no key shipped) |
| Difficulty + chat toggles in BepInEx config | Implemented |

## Untested / known limitations

- **Cannot be playtested in this environment** (no Among Us binary). First real validation is on Weston’s PC.
- Bot spawning uses host-side dummy/`ClientData` simulation. Private lobbies with **other humans** usually need **every client on the same mod** (Reactor handshake is set to `RequireOnAllClients`). Even then, dummy networking can desync on some builds — freeplay / local host-only is the safest first try.
- Pathfinding is a **coarse landmark graph** (consoles / vents / rooms + line-of-sight), not a full official navmesh. Bots can get stuck on ladders, platforms, Airship moving parts, or Fungle terrain.
- Sabotage start/repair byte codes are **best-effort** and map-specific; some sabotages may not trigger or clear cleanly.
- Minigames are **not** solved visually — crewmates complete tasks after a delay via host task completion RPCs.
- Meeting skip vote uses classic `253` skip id; confirm against your build if skips mis-register.
- Live AI chat requires a reachable OpenAI-compatible HTTP API from the game process (Windows firewall / CORS N/A / local LLM URL).
- Do **not** use this to disturb public matchmaking. The mod refuses public lobbies when the safety option is on (default).

---

## How Weston installs and tries it

### 1) Install BepInEx (IL2CPP)

1. Install / update **Among Us** on Steam to a **17.4.x** build that matches this mod (or update GameLibs in the `.csproj` if your game is newer/older).
2. Download **BepInEx Bleeding Edge — Unity.IL2CPP — win-x64** (or win-x86 if your folder is still x86) from:  
   https://builds.bepinex.dev/projects/bepinex_be  
   A version around **6.0.0-be.735+** matches the project references.
3. Extract the BepInEx zip **into** your Among Us folder so you have:
   - `Among Us/BepInEx/`
   - `Among Us/doorstop_config.ini`
   - `Among Us/winhttp.dll` (or the doorstop bootstrap file from the zip)
4. Launch Among Us once, wait for the main menu, then quit. This generates `BepInEx/plugins` and interop.

Reactor quick start (same ecosystem): https://docs.reactor.gg/

### 2) Install Reactor

1. Download `Reactor.dll` from [NuclearPowered/Reactor releases](https://github.com/NuclearPowered/Reactor/releases) (**2.5.1** / CI builds that list support through **17.4**).
2. Place `Reactor.dll` in `Among Us/BepInEx/plugins/`.

### 3) Install AiCrewmates

**Option A — build from source (recommended while iterating)**

```bash
# .NET 6 SDK required
dotnet build AiCrewmates/AiCrewmates.csproj -c Release
```

Copy:

`AiCrewmates/bin/Release/net6.0/AiCrewmates.dll`

into:

`Among Us/BepInEx/plugins/AiCrewmates.dll`

Or build straight into plugins:

```bash
dotnet build AiCrewmates/AiCrewmates.csproj -c Release -p:AmongUs="C:/Program Files (x86)/Steam/steamapps/common/Among Us"
```

**Option B — use a prebuilt DLL** from `AiCrewmates/bin/Debug/net6.0/AiCrewmates.dll` after `dotnet build`.

### 4) Start a local game with bots

1. Launch Among Us with BepInEx (check `BepInEx/LogOutput.log` for `AiCrewmates 1.0.0 loaded`).
2. Host **Freeplay**, **Local**, or a **private** online lobby. Do **not** make the lobby public.
3. Add bots:
   - Chat: `/addbot` · `/addbot Ada` · `/removebot` · `/removebot red` · `/fillbots 8` · `/clearbots` · `/bots`
   - Lobby buttons: **ADD BOT** / **REMOVE BOT**
   - Keys: **F6** add · **F7** remove
4. Start the round and watch bots path to tasks, report, talk, and vote.
5. Optional live AI: edit `BepInEx/config/com.weston.aicrewmates.cfg` after the first run:

```ini
[OpenAI]
BaseUrl = https://api.openai.com/v1
ApiKey = sk-...          # never commit this
Model = gpt-4o-mini
```

Leave those blank to keep the built-in rule-based meeting logic.

Other useful config keys: `MaxBots`, `ReactionTimeSeconds`, `KillAggressiveness`, `TaskCompleteDelaySeconds`, `Chat.Enabled`, `AllowOnlyPrivateOrLocal`.

---

## How game / Reactor references are obtained

This project does **not** require a local Among Us install to compile.

| Reference | Source |
| --- | --- |
| `AmongUs.GameLibs.Steam` **2026.9.29** | NuGet feed `https://nuget.bepinex.dev/v3/index.json` — dummy game assemblies |
| `BepInEx.IL2CPP.MSBuild` | Generates Il2CppInterop assemblies into the local BepInEx cache on `dotnet build` |
| `BepInEx.Unity.IL2CPP` **6.0.0-be.735** | BepInEx NuGet |
| `Reactor` **2.5.1-ci.400** | `https://nuget.reactor.gg/v3/index.json` |
| `BepInEx.AutoPlugin` | BepInEx NuGet |

`nuget.config` in the repo root registers those feeds. See `AiCrewmates/AiCrewmates.csproj`.

If your installed game is a different date/build, change the `AmongUs.GameLibs.Steam` version to the matching package on the BepInEx NuGet feed and rebuild.

---

## Project layout

```
AiCrewmates/
  AiCrewmatesPlugin.cs     # BepInEx plugin entry
  ModConfig.cs             # BepInEx config bindings
  Bots/                    # spawn manager, brain, memory, names
  AI/                      # pathfinding, meeting brain, OpenAI client
  Patches/                 # chat commands, lobby UI, safety, keybinds
  Util/                    # session helpers
```

---

## Disclaimer

This mod is not affiliated with Among Us or Innersloth LLC. Portions of the materials referenced herein are property of Innersloth LLC. © Innersloth LLC.
