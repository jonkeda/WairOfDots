# RCA: Remove Oravey Control Instructions Overlay

## Summary

The Wair of Dots play screen shows a `CONTROL INSTRUCTIONS` overlay with camera/help controls such as `F2`, `F3`, arrow keys, `Q/E`, numpad rotation, right mouse rotation, and `H: Reset Camera`.

Those instructions are not part of Wair of Dots. They came from the Oravey-style Stride setup path and should be removed from the Wair UI.

## Impact

- The overlay covers the map and HUD.
- The listed controls do not describe Wair of Dots gameplay.
- It makes the game look like it inherited Oravey debug/player instructions.
- It can confuse UAT because Wair of Dots is a map-command game, not an Oravey-style camera-control game.

## Root Cause

`WairOfDots.Windows` previously initialized the world through the Stride Community Toolkit base 3D scene helper:

- `src/WairOfDots.Windows/WairOfDotsGame.cs:2` imported `Stride.CommunityToolkit.Bepu`.
- `src/WairOfDots.Windows/WairOfDotsGame.cs:161` called `this.SetupBase3DScene()`.
- `src/WairOfDots.Windows/WairOfDots.Windows.csproj:18` referenced `Stride.CommunityToolkit.Bepu`.

The installed toolkit XML docs for `SetupBase3DScene()` say that the helper adds a camera and sets it up with a `MouseLookCamera` component. The visible instruction overlay matches that camera/control helper behavior.

The Oravey reference rule was: use Oravey only as an example for Stride tech and Brinell integration. The camera instruction overlay is visible UX, not tech-only integration, so it should not have carried over.

## Contributing Factors

- The base scene helper was convenient for fast Stride startup.
- Wair of Dots uses a UI map as the main experience, so a 3D mouse-look camera helper is unnecessary.
- Brinell tests currently assert app state and map marker counts, but they do not assert absence of non-Wair overlay text.
- The overlay is likely created outside the Wair UI tree, so normal UI element queries may not see it.

## Corrective Action

- [x] Replace `SetupBase3DScene()` with a minimal Wair-specific setup.
- [x] Keep only the graphics/UI services needed by the Wair HUD and Brinell automation.
- [x] Remove the mouse-look/debug camera component path.
- [x] Remove `using Stride.CommunityToolkit.Bepu` if it is no longer needed.
- [x] Remove the `Stride.CommunityToolkit.Bepu` package reference if nothing else uses it.
- [x] Verify the game still boots, displays the map, and accepts Brinell commands.

## Verification Plan

- [x] Run `dotnet build WairOfDots.slnx`.
- [x] Run `dotnet test tests\WairOfDots.Tests\WairOfDots.Tests.csproj`.
- [x] Run `dotnet test tests\WairOfDots.UITests\WairOfDots.UITests.csproj`.
- [x] Launch the game and confirm no `CONTROL INSTRUCTIONS` overlay is visible.
- [x] Add a UAT checklist item that no Oravey/debug camera instructions appear on the play screen.

## Implementation Notes

- `WairOfDotsGame.SetupWorld()` now uses the non-Bepu `SetupBase3D()` helper.
- The app keeps the render and clean UI stage setup needed by the Wair HUD and Brinell tests.
- The app no longer calls `SetupBase3DScene()`, which was the path that added the mouse-look camera helper.
- `Stride.CommunityToolkit.Bepu` is no longer a top-level package reference in `WairOfDots.Windows`.
- Added Brinell UI smoke tests for booting to a playable map and for absence of Oravey/debug camera instruction text.
- Fixed Brinell Stride screenshot capture to bring the game window to the capture surface and capture the client area from the screen DC. The previous capture path could produce black/occluded DirectX screenshots even when the game was visible.

## Prevention

- [ ] Treat Oravey as a technical reference only: Stride hosting, Brinell wiring, and test patterns are allowed; visible controls, mechanics, overlays, and player instructions are not.
- [ ] Prefer Wair-specific scene setup over sample-game base helpers when the helper creates user-visible behavior.
- [ ] Add a review checkpoint for copied/reference code: "Does this add visible UI or controls from another game?"
