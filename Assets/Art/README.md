# Fallen Yard art

All art here is original and made for this project in Blender. Nothing is taken from another game or asset pack.

## Conventions

- **Scale:** 1 Blender unit = 1 metre = 1 Unity unit. FBX files import with a root scale of 1 and no rotation.
- **Axes:** Blender `-Y` becomes Unity `+Z` (forward), Blender `+Z` becomes Unity `+Y` (up).
- **Shading:** flat, with no smoothing.
- **Colour:** tools and props use `Textures/FY_Palette.png` (4x4 swatches) through the single `FY_Palette` material, so they can share one material and batch together. Add new colours as swatches instead of new materials.
- **Texture import:** set the palette to Point filter with no mipmaps, so swatches don't bleed.

## Assets

| Asset | Tris | Budget | Pivot |
| --- | --- | --- | --- |
| `FY_Leaf_Maple`, `Oak`, `Birch`, `Ginkgo`, `PineNeedles`, `WetClump` | 12 to 18 | under 20 | Centre of the leaf |
| `FY_Rake` | 336 | 400 | Main grip on the handle |
| `FY_LeafBlower` | 292 | 500 | Top handle grip, nozzle towards `+Z` |
| `FY_LeafBag` | 106 | 300 | Centre of the base |
| `FY_CompostBin` | 510 | 800 | Centre of the base |
| `FY_LeafVacuum` | 452 | 700 | Top handle grip, intake towards `+Z` |
| `FY_Mulcher` | 254 | 700 | Top handle grip, chute towards `+Z` |

Leaves are 1 unit long with the tip towards `+Z`, like `LeafMeshFactory`, because `LeafField` scales each instance. Flat leaves carry a duplicate reversed face so both sides draw with a single-sided URP material, and those duplicates are included in the counts above.

`FY_CompostBin` has a child, `FY_CompostBin_Lid`, whose pivot is on the rear hinge. Rotate it around its local X axis to open it.

## Environment (batch 2)

Everything under `Environment/` uses `Textures/FY_EnvPalette.png`, an 8x8 palette, through the `FY_EnvPalette` material. Batch 1's `FY_Palette.png` is unchanged. Crowns of leafy trees and the round bushes use a second material slot, `FY_Foliage`, on the same texture. Row 7 of the palette holds the foliage colours in pairs, so the material's U offset sets the season: `0` gold, `0.25` orange, `0.5` red, `0.75` green. `ArtMaterialSetup` makes one material per season (`FY_Foliage_Gold`, `_Orange`, `_Red`, `_Green`); swap them per yard. Hedges, pines and berry bushes stay green.

### House kits

Three kits, `Cottage` (blue clapboard, red roof, shutters), `Brick` (charcoal roof, door canopy) and `Manor` (stone with quoins and keystones, slate roof). Each kit has `Wall`, `WallWindow`, `WallDoor`, `Corner`, `Gable`, `Roof`, `Chimney` and `Steps`, named `FY_<Kit>_<Piece>`.

- Grid: walls are 3 m wide, 3 m tall per storey and 0.2 m thick. Every wall is centred on its pivot (bottom centre), with the outside facing `+Z`. Place walls on the module centres and rotate them to face out.
- `Corner` goes on each corner of the grid and hides the wall joints. It is one storey tall; stack it for more.
- Houses are always 2 modules (6 m) deep. `Gable` is the 6 m triangle that sits on top of an end wall. `Roof` is a 3 m slice of the full gable roof, pivot at the centre of the eave line, so put one per module at the top of the walls.
- `Steps` has its pivot on the wall line under the door and comes out towards `+Z`.
- Ready-made houses: `FY_House_Cottage` (3x2 modules, 5,728 tris), `FY_House_Brick` (3x2 with a back door, 6,316 tris) and `FY_House_Manor` (4x2, two storeys, 15,892 tris). Pivot at the centre of the footprint, front door towards `+Z`.

Kit pieces are 24 to 648 tris (the window and door walls are the heaviest).

### Fences

Sections are 2 m long, centred on the pivot, with one post at the `-X` end. Chain them every 2 m and close a run with the matching `_Post`.

| Asset | Tris | Notes |
| --- | --- | --- |
| `FY_Fence_Picket`, `_Post`, `_Gate` | 238, 22, 246 | The gate's child `FY_Fence_Picket_Gate_Door` pivots on its hinge; rotate around local Y to open |
| `FY_Fence_Rail`, `_Post` | 84, 32 | Rustic post and rail |
| `FY_Fence_Stone` | 288 | Low dry stone wall, no posts, ends tile |
| `FY_Fence_Iron`, `_Post` | 376, 44 | Spear-top railings for the church green and manor |
| `FY_Hedge` | 76 | Evergreen, flat ends so sections tile |

### Trees and bushes

Pivot at the base of the trunk.

| Asset | Tris | Height |
| --- | --- | --- |
| `FY_Tree_Maple`, `_Bare` | 676, 316 | about 6 m |
| `FY_Tree_Oak`, `_Bare` | 782, 366 | about 5.3 m, 7.8 m wide crown |
| `FY_Tree_Birch`, `_Bare` | 666, 362 | about 6.5 m, white trunk |
| `FY_Tree_Pine` | 112 | about 6 m, evergreen |
| `FY_Bush_Round`, `FY_Bush_Small`, `FY_Bush_Berry` | 240, 240, 268 | 1.1 m, 0.6 m, 0.9 m tall |

### Props

Pivot at the centre of the base.

| Asset | Tris | Notes |
| --- | --- | --- |
| `FY_Mailbox` | 136 | Child `FY_Mailbox_Flag` hinges on the box side; rotate around local X to raise it |
| `FY_Bench`, `FY_Wheelbarrow`, `FY_Birdbath` | 180, 258, 232 | |
| `FY_Pumpkin`, `FY_Pumpkin_Small` | 182, 182 | |
| `FY_HayBale`, `FY_Woodpile`, `FY_DogHouse` | 56, 280, 72 | |
| `FY_TrashCan` | 192 | Child `FY_TrashCan_Lid` hinges at the back |
| `FY_LampPost` | 128 | Lamp glass uses the pale `lamp_glow` swatch; add a light or emission in Unity |
| `FY_Rock_A`, `FY_Rock_B` | 20, 20 | |
| `FY_FlowerPot` | 222 | |

### Regrowth

Small double-sided meshes to pop up on cleaned ground: `FY_Regrow_Grass_A` (18 tris), `FY_Regrow_Grass_B` (24), and `FY_Regrow_Flowers_White`, `_Purple`, `_Yellow` (106 each). Pivot at the centre of the base. They import without colliders.

### Ground

Flat at height 0 on every edge so pieces butt together. Pivot at the centre.

| Asset | Tris | Size |
| --- | --- | --- |
| `FY_Ground_Grass_4m` | 128 | 4 x 4 m, gently uneven inside |
| `FY_Ground_Path_2m` | 152 | 1 x 2 m flagstones along Z |
| `FY_Ground_SteppingStones_2m` | 96 | 2 m along Z |
| `FY_Ground_Driveway_3x6` | 36 | 3 x 6 m concrete slabs |
| `FY_Ground_FlowerBed_2x1` | 80 | Soil bed with a timber edge |

### Skies

`Textures/Sky/FY_Sky_<Season>.png` are vertical gradients (bottom is straight down, the middle row is the horizon, top is straight up) for `EarlyAutumn`, `PeakAutumn`, `LateAutumn` and `FirstFrost`. `ArtMaterialSetup` wraps each in a `Skybox/Panoramic` material (latitude-longitude) in `Materials/Sky/`.

## Unity setup

`Scripts/Editor/Art/ArtImportRules.cs` is the import preset for this folder. It runs on every import, so new files need no manual settings:

- Models: scale 1, file units, no cameras, lights, blend shapes or animation, normals from the file. Colliders are generated for `Environment/` except `Regrowth/`.
- Textures in `Textures/`: no mipmaps, uncompressed. Palettes use Point filter and Repeat, skies use Bilinear and Clamp.

After pulling, run **Fallen Yard > Art > Set Up Materials and Skyboxes** once. It creates `Materials/FY_Palette`, `FY_EnvPalette`, `FY_Foliage` and the season variants (URP Lit, instancing on), the four sky materials, and remaps every model in `Assets/Art` to the shared materials. After that, newly imported models are remapped automatically.

## Rebuilding

`Source~/` holds the source files and the scripts that build and export everything. Unity ignores folders ending in `~`.

- Batch 1 (leaves, rake, blower, bag, compost bin): open `Source~/fy_build_batch1.py` in Blender's Scripting tab and run it.
- Batch 2 (environment, skies, vacuum, mulcher): run `Source~/fy_build_env.py` the same way, or headless with the `bpy` module (`pip install bpy`, then `python fy_build_env.py`). It writes `fy_env.blend` with every asset laid out in rows.
