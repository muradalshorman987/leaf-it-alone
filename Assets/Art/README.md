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

Leaves are 1 unit long with the tip towards `+Z`, like `LeafMeshFactory`, because `LeafField` scales each instance. Flat leaves carry a duplicate reversed face so both sides draw with a single-sided URP material, and those duplicates are included in the counts above.

`FY_CompostBin` has a child, `FY_CompostBin_Lid`, whose pivot is on the rear hinge. Rotate it around its local X axis to open it.

## Rebuilding

`Source~/` holds the source file and the script that builds and exports everything. Unity ignores folders ending in `~`. To rebuild, open `Source~/fy_build_batch1.py` in Blender's Scripting tab and run it.
