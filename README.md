# Replace Mountain Roof to Normal Roof

A RimWorld mod that lets colonists replace thick overhead-mountain roof with normal
constructed roof — so you can de-mountain a base (and stop infestations from spawning
under it) without mining everything away.

Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3375819442

## How it works (in-game)

1. Draw a **Build Roof** area over mountain roof (or let the auto-area do it —
   see settings).
2. Colonists take the roof job and replace the thick roof with constructed roof.

Settings: master enable; auto-add build-roof area to mountain roof inside rooms;
optionally auto-add to the entire room regardless of roof (respects the remove-roof area).

## Supported versions

| Game | Build | Notes |
|---|---|---|
| 1.5 | `Legacy/Assemblies` — net472, compiled against 1.5 refs | frozen (no new features) |
| 1.6 | `Legacy/Assemblies` — same DLL | maintained |
| 1.7 | `1.7/Assemblies` — net10.0, native Waylith | maintained |

The mod deliberately operates **only on world maps**: pocket maps (Anomaly's void /
metal hell, the pit-gate undercave, Odyssey's pocket dimensions, or any modded
dimension using the pocket-map system) are excluded entirely.

## Implementation notes

- Harmony **postfix-only** patching (no transpilers, no IL rewriting) — patches compose
  safely with other roof mods (notably Vanilla Gravship Expanded) and never depend on
  method-body shape, which is what made the pre-rewrite build break other mods.
- No reflection into private game state; only public APIs plus the patched method names.
- Saves/settings: touches neither. Existing settings keys carry over from older versions.

## Building

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download).

The 1.5/1.6 (net472) build resolves its game references from NuGet automatically.
The 1.7 (net10) build needs three DLLs from a RimWorld 1.7 install
(`Assembly-CSharp.dll`, `UnityStubs.dll`, `Waylith.dll`). Either copy them into a
`References/` folder next to this README, or point the build at the game:

```sh
cd Source
dotnet build                              # uses References/ if present
dotnet build -p:RimWorldDir=/path/to/RimWorld
```

`dotnet build` places the binaries into `Legacy/Assemblies` and `1.7/Assemblies`
automatically — the folder is then directly usable as a local mod / workshop upload.

## Testing

Behavioral coverage is provided by a headless self-test suite
(`RimWorld -Headless -quicktest`) exercising area generation, job availability and
actual in-game roof replacement, with and without Vanilla Gravship Expanded loaded.
Verified: 1.7 native (ALL-PASS, 0 log errors, VGE C1+C2), 1.7 via compat shim (net472),
1.6 native incl. VGE, 1.5 load+patch; 1.5 behavioral is compile-verified only.

## License

[MIT](LICENSE)
