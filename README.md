# FlyAndQuicksave freeze fix (Baby Steps)

rebuild of FlyAndQuicksave 1.0.0 by Caleb Orchard, with a fix for the freeze after a fly cam click teleport.

all credit for the mod goes to Caleb, this just patches the bug.

## the bug

in fly cam, left click teleports nate to the camera. after that he's stuck for good and you have to restart.

turns out the game leaves `PlayerMovement.enabled = false` and `Animator.speed = 0` once the teleport finishes, so your input never gets read again. physics and the puppet are fine, nate's just not listening.

## the fix

`src/FlyFix.cs`: a harmony prefix on `Menu.Teleport` notes if you were in fly cam. ~0.25s after the teleport's done, it turns `PlayerMovement` back on and sets `anim.speed` back to 1. other teleports are left alone.

F11 does the same thing by hand if you ever get stuck anyway (use it outside fly cam).

everything else works like the original: F1 fly cam, WASD/QE, shift/ctrl speed, scroll, F3/F4, F9/F10.

## install

drop `FlyAndQuicksave.dll` in `Baby Steps/Mods/` (replaces the original). needs MelonLoader 0.7.x.

## build

.NET 6 SDK. edit `GameDir` in `src/FlyAndQuicksave.csproj` to point at your game, then:

```
dotnet build src/FlyAndQuicksave.csproj -c Release
```

## diag

`diag/` is the diagnostic build used to find the bug (state watcher, healthy vs frozen field diff, input probe). not needed to play.

## tested on

Baby Steps 12.12.2025 (e81012ec), MelonLoader 0.7.3, Steam Deck / Proton 11. also had BabyBlocks, multiplayer client, minimap and sunglasses toggle installed. not tested with FlyAndQuicksave alone.
