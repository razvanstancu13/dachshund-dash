using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DachshundDash.EditorTools
{
    /// <summary>Import settings by folder, so dropped-in generated assets are game-ready without manual steps.</summary>
    public class ImportRules : AssetPostprocessor
    {
        const string Root = "Assets/_Game/Resources/";

        static readonly HashSet<string> LoopClips = new HashSet<string>
            { "Idle", "Walk", "Run", "Trot", "CrouchRun", "Glide", "Flap", "Eat", "LookAround" };

        /// <summary>9-slice / 3-slice borders as fractions of the image (left, bottom, right, top).</summary>
        public static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "btn_bone", new Vector4(0.22f, 0.0f, 0.22f, 0.0f) },
            { "panel_board", new Vector4(0.12f, 0.12f, 0.12f, 0.12f) },
            { "row_plank", new Vector4(0.08f, 0.0f, 0.08f, 0.0f) },
        };

        public override uint GetVersion() => 6;     // bump to make Unity reimport after a rule change

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root + "Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // the game builds materials from <name>_tex.png
            mi.bakeAxisConversion = true;                                    // upright without a root rotation
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root + "Models/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                string n = c.name;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                c.loopTime = LoopClips.Contains(n);
                c.loopPose = false;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(Root + "UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                var file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                ti.maxTextureSize = file.StartsWith("icon_") || file == "btn_round" ? 256 : file == "menu_bg" ? 2048 : 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
                var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (Borders.TryGetValue(name, out var f))
                {
                    ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                    ti.spriteBorder = new Vector4(f.x * w, f.y * h, f.z * w, f.w * h);
                }
            }
            else if (assetPath.StartsWith(Root + "Textures/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                // fx_line is drawn only a few pixels tall: mipmaps would blur its soft profile away
                ti.mipmapEnabled = System.IO.Path.GetFileNameWithoutExtension(assetPath) != "fx_line";
                ti.wrapMode = TextureWrapMode.Repeat;
                // surface transition patches cover their quad exactly once
                if (System.IO.Path.GetFileName(assetPath).StartsWith("trans_")) ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                // keep the real size: the backdrop layout reads width / height (a 1792 x 672 layer resized to the
                // nearest power of two came in as 2048 x 512 and was drawn 1.5x too wide)
                ti.npotScale = TextureImporterNPOTScale.None;
            }
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root + "Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            bool music = System.IO.Path.GetFileName(assetPath).StartsWith("music") ||
                         System.IO.Path.GetFileName(assetPath).StartsWith("ambience");
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.6f : 0.8f;
            ai.defaultSampleSettings = s;
            ai.forceToMono = !music;
        }
    }
}
