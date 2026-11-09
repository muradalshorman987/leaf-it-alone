using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FallenYard.EditorTools.Art
{
    /// <summary>
    /// Import presets for everything under Assets/Art, applied in code so they
    /// hold for every new file without anyone picking a preset by hand.
    /// Models: scale 1, no cameras, lights or animation, flat normals from the
    /// file, colliders on environment pieces (not regrowth). Palette textures:
    /// Point filter, no mipmaps, uncompressed. Sky gradients: Bilinear, Clamp.
    /// Models that still carry embedded FY_ materials are remapped to the shared
    /// ones in Assets/Art/Materials once those exist (see ArtMaterialSetup).
    /// </summary>
    public class ArtImportRules : AssetPostprocessor
    {
        const string ArtRoot = "Assets/Art/";
        const string TextureRoot = "Assets/Art/Textures/";

        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.isReadable = false;
            importer.addCollider = WantsCollider(assetPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureRoot)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (assetPath.StartsWith(TextureRoot + "Sky/"))
            {
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
            }
            else
            {
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
            }
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var models = imported.Where(p => p.StartsWith(ArtRoot) && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)).ToArray();
            if (models.Length == 0 || !ArtMaterialSetup.SharedMaterialsExist()) return;
            // Remap after the import pass finishes; remapping reimports the model.
            EditorApplication.delayCall += () => ArtMaterialSetup.RemapModels(models);
        }

        public static bool WantsCollider(string path) =>
            path.StartsWith(ArtRoot + "Environment/") && !path.Contains("/Regrowth/");
    }
}
