# RimTalk Event+

This repository contains the source code and XML assets for the RimWorld mod **RimTalk Event+**.

https://steamcommunity.com/sharedfiles/filedetails/?id=3612632140

## Building for RimWorld 1.5 and 1.6/1.7

The project defaults to `GameVersion=1.6`. To build for 1.5, select that version
and provide matching game references and a matching RimTalk DLL:

```powershell
dotnet build Source/RimTalkEventMemory/RimTalkEventPlus.csproj -c Release `
  -p:GameVersion=1.5 `
  '-p:RimWorldManagedDir=C:/References/RimWorld-1.5/Managed' `
  '-p:RimTalkAssemblyPath=C:/Mods/RimTalk/1.5/Assemblies/RimTalk.dll'
```

Replace the example paths with your own. `RimWorldManagedDir` may point to a
matching game's Managed directory or an extracted `Krafs.Rimworld.Ref` package's
`ref/net472` directory. Restore `Lib.Harmony` from `packages.config` if its local
package directory is missing. The build rejects a game reference whose major
and minor version do not match `GameVersion`.

Use `GameVersion=1.6` with 1.6 references and RimTalk to build the newer shared
code path. Outputs go to `1.5/Assemblies` or `1.6/Assemblies`, with separate
intermediate directories. Game, RimTalk, and Harmony dependencies are not copied
into the output.

`Compatibility/RimWorld15Compat.cs` is included only for 1.5 and uses the
`RW1_5` compilation symbol. It contains the older quest, Lord, and threat APIs;
small conditional calls in the shared files select it. The other builds keep
using their existing native APIs. No runtime version detection is added.

## Shared 1.6/1.7 release

`About/About.xml` declares support for 1.5, 1.6, and 1.7. RimWorld automatically
loads the matching version folder, or the newest available older version folder
when no exact match exists. With this package's layout, it selects:

| RimWorld version | Assembly directory |
| --- | --- |
| 1.5 | `1.5/Assemblies` |
| 1.6 | `1.6/Assemblies` |
| 1.7 | `1.6/Assemblies` (shared 1.6 build) |

All versions also load the root directory for shared language assets.

---

This work is licensed under a
[Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License][cc-by-nc-sa].

[![CC BY-NC-SA 4.0][cc-by-nc-sa-image]][cc-by-nc-sa]

[cc-by-nc-sa]: http://creativecommons.org/licenses/by-nc-sa/4.0/
[cc-by-nc-sa-image]: https://licensebuttons.net/l/by-nc-sa/4.0/88x31.png
[cc-by-nc-sa-shield]: https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg
