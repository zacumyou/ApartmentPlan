# References

- RoadStyleIntoKorea local source 0.1.267 inspected 2026-10-17: GameTopRight FloatingButton, cs2/api ValueBinding/TriggerBinding, panel styling, icon treatment and layout controls. Generic SVG icons and UI type declarations reused from the user's local project. The RSIK project itself is unchanged.
- Parking Lot Tool: https://github.com/KruemelGames/ParkingLotTool . README inspected for polygon selection and manual entrance workflow. Upstream is GPL-3.0; no upstream implementation code was copied into this project. This mod does not build their real parking lanes or roads. Source-directory/API fetching was unavailable; no claim of a full source review.
- Official Modding Toolchain and UI Modding pages returned HTTP 401 on 2026-10-17. Local guide `../GizmoXYZ/references/official-guidelines.md` records a direct browser review on 2026-09-16. Installed Mod.props/Mod.targets and current Game.dll metadata were inspected on 2026-10-17; latest live wiki contents remain unverified.
- Installed API checks: BuildingData.m_LotSize, ObjectGeometryData.m_Size, BuildingPropertyData.m_ResidentialProperties, ToolBaseSystem actions/raycast, OverlayRenderSystem.Buffer.DrawLine with Projected and zero roundness, custom ISerializable components/buffers. Decompiled fragments and game DLLs are not included in the package.

## Polygon interaction reference, 2026-10-17

Read live public source files from KruemelGames/ParkingLotTool main: Tools/ParkingLotSnapping.cs, ParkingLotSnapping.Targets.cs, ParkingLotPreviewStyle.cs, ParkingLotOverlay.Editing.cs and ParkingLotToolSystem.cs. Source snapshots retained under research/PLT for local reference only, excluded from package. The earlier directory-fetch limitation is resolved for these five files.

ApartmentPlan's independent BoundarySnap constraint solver and spatial-query adapter follow the referenced interaction: 8m snap radius, 12m closure, inherited axes, intersections, corner handles, dashed guides and local world-edge queries. No PLT runtime dependency or actual parking facilities. This is not full PLT feature parity: curves use bounded polyline approximation; saved overlays remain immutable. PLT source is GPL-3.0; no upstream implementation file is shipped.

## Current reference review, 2026-10-18

Official Modding Toolchain was successfully read in the in-app browser (the web-fetch route returned HTTP 401). Its current visible verification badge is 1.1.12 f1; actual build contracts come from the installed SDK. The earlier 401/source-access entries above are historical.

Read installed Parking Lot Tool 161156_13 README and decompiled ParkingLotComfortSystem, ParkingLotFahrprefabSystem, ParkingLotUISystem for local study. The comfort system writes ParkingLane.m_ComfortFactor and parking fee; driving paths clone eligible built-in PathwayPrefab assets; catalogs use native UIObject icons and thumbnail fallback. No implementation code from these files was copied into ApartmentPlan runtime or package. DLL/source snapshots and game data remain local research only. GPL-3.0 licensing of PLT is unchanged.

Inspected installed game's Paths glossary image, RoadsBuildingRoads tutorial image and EU_ResidentialHighSignature02 building image. They show representative built-in appearance, not a survey of every installed asset. Planning dimensions are read dynamically from BuildingData lot size and ObjectGeometryData; full building footprints and terrain performance remain runtime concerns.

## Vehicle access review, 2026-10-18

Read the current public PLT README and locally decompiled installed 161156_13 ParkingLotFahrregeln / ParkingLotToolSystem. In particular, PlaneGassenkurs resolves a real city-road attachment; its post-build audit checks non-virtual ParkingLane and live CarLane LaneConnection. The built-in two-way path names/widths include Invisible Car Path 1xTwoway (3m) and 2xTwoway (6m). These are source observations, not dimensions copied into a game prefab by ApartmentPlan. Research snapshots are excluded from the package. ApartmentPlan's independent raster connectivity/clearance checks do not create or validate real game lanes. No PLT implementation copied.

Installed Game.Buildings.RoadConnectionSystem and Game.Prefabs.BuildingFlags were inspected: NoRoadConnection short-circuits that connection search; the normal branch uses BuildingUtils.CalculateFrontPosition and an 8.4m search threshold. RequireRoad, NoRoadConnection and side/back access flags are distinct. This finding does not establish normal household/service behavior without a road; no global prefab patches are installed.

## Native construction prototype, 2026-10-18

The user's expanded scope now includes native construction. Earlier overlay-only descriptions above refer to 0.1.10 and earlier. 0.1.11 independently creates NetCourse/ObjectDefinition previews, uses the native apply/error pipeline and ParkingLotDecal01, and inspects live lane/utility components. No PLT runtime dependency or copied implementation file is included. This is not evidence of full PLT feature parity or successful traffic/utility simulation; native testing is in progress.

## Landscaping references, 2026-10-19

Inspected installed Parking Lot Tool 161156_13 vegetation catalog and native ObjectDefinition/Tree initial-age workflow. ApartmentPlan uses an independent green-coverage placement implementation and the installed game API; no PLT implementation files are distributed. Trees use native preview/error/Apply and ownership rather than a third-party runtime dependency.

Grass Sprites optional integration references https://github.com/winterirl/Grass-Sprites at commit2b1423fc1b79748d4d24c05bd5bfab9025ce18b4 (MIT), and locally inspected installed148141_3/GrassSprites.dll. Its public PaintAtWorldPosition(Vector3,float,bool,bool) API modifies the existing save-specific mask. A zero-radius circle paints a one-pixel cross at full coverage, not a zero-sized brush. ApartmentPlan reserves that footprint and reads readiness/mask values without modifying private fields. No Grass Sprites sources/binaries are bundled; optional API signatures/fields are checked before use. Upstream private readiness fields may change; incompatible integrations remain disabled. Existing Grass Sprites shared mask is not erased on compound demolition.

## AP road icon presentation, 2026-10-19

Road icons are composed locally at runtime from the user's installed AssetIconLibrary CustomThumbnails SVG for the exact original network name, or the game's own original thumbnail atlas. Original icon files and game assets are not modified or bundled in the package. The AP badge is vector lettering based on the locally installed Segoe UI Bold glyph outlines, not a distributed font file. Preview files under docs use the locally installed icons and are not packaged or published. Native MarkerNet, NetInitializeSystem, NetCompositionSystem, NetToolSystem and BulldozeToolSystem were inspected in the installed Game.dll for selection/replacement behavior; no game implementation files are distributed.
