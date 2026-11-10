using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DachshundDash.EditorTools
{
    /// <summary>Creates everything that is not code: template materials, generated helper textures, the two scenes,
    /// build and player settings. Safe to run again. Batch mode:
    ///   Unity -batchmode -projectPath . -executeMethod DachshundDash.EditorTools.ProjectBuilder.Setup -quit
    ///   ... -executeMethod DachshundDash.EditorTools.ProjectBuilder.BuildWebGL</summary>
    public static class ProjectBuilder
    {
        const string Res = "Assets/Resources/";
        const string Scenes = "Assets/Scenes/";
        const int GroundQueue = 1990;

        [MenuItem("Dachshund Dash/Setup Project")]
        public static void Setup()
        {
            foreach (var d in new[] { "Materials", "Textures", "Models", "UI", "Audio", "Fonts" })
                Directory.CreateDirectory(Res + d);
            Directory.CreateDirectory(Scenes);
            AssetDatabase.Refresh();
            MakeHelperTextures();
            MakeMaterials();
            MakeConfig();
            MakePostFx();
            MakeScenes();
            PlayerSetup();
            AssetDatabase.SaveAssets();
            Debug.Log("[DD] setup done");
        }

        // -------------------------------------------------------------- textures

        static void MakeHelperTextures()
        {
            WritePng(Res + "Textures/fx_glow.png", SoftDot(128, 1f));
            WritePng(Res + "Textures/fx_shaft.png", Shaft(64, 128));
            WritePng(Res + "Textures/fx_line.png", Line(16, 64));
            AssetDatabase.Refresh();
        }

        static void WritePng(string path, Texture2D t)
        {
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        static Texture2D SoftDot(int n, float power)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    a = Mathf.Pow(a * a * (3 - 2 * a), power);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Glowing line across v: a solid bright core with soft edges, uniform along u.</summary>
        static Texture2D Line(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float d = Mathf.Abs((y + 0.5f) / h * 2 - 1);
                float a = d < 0.3f ? 1f : Mathf.SmoothStep(1f, 0f, (d - 0.3f) / 0.7f);
                for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply();
            return t;
        }

        /// <summary>Light shaft: bright at the top (v = 1), fading down; soft at the sides (u wraps around the cone).</summary>
        static Texture2D Shaft(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = (y + 0.5f) / h;
                    float a = Mathf.Pow(v, 1.6f) * 0.9f + 0.05f * v;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        // -------------------------------------------------------------- materials

        static Shader S(string name) => Shader.Find(name);

        static Material Mat(string name, Shader sh)
        {
            string path = Res + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = sh;
            return m;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
        }

        static void Tex(Material m, string texPath)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (t == null) return;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            m.mainTexture = t;
        }

        /// <summary>URP surface setup without the inspector: 0 opaque, 1 alpha blend, 2 additive.</summary>
        static void Surface(Material m, int mode, bool doubleSided = false)
        {
            m.SetFloat("_Cull", doubleSided ? 0 : 2);
            if (mode == 0)
            {
                m.SetFloat("_Surface", 0);
                m.SetOverrideTag("RenderType", "Opaque");
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = -1;
                return;
            }
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", mode == 2 ? 2 : 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(mode == 2 ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)(mode == 2 ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static void MakeMaterials()
        {
            var lit = S("Universal Render Pipeline/Simple Lit");      // mobile / WebGL friendly
            var unlit = S("Universal Render Pipeline/Unlit");
            var particles = S("Universal Render Pipeline/Particles/Unlit");

            var m = Mat("Lit", lit); Surface(m, 0); m.SetFloat("_Smoothness", 0.1f);
            // enemies and bones: lit plus a little emission (keyword on here, so the variant ships in the build)
            m = Mat("LitGlow", lit); Surface(m, 0); m.SetFloat("_Smoothness", 0.1f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.4f, 0.4f, 0.4f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m = Mat("Unlit", unlit); Surface(m, 0);

            m = Mat("ParticleAlpha", particles); Surface(m, 1);
            m = Mat("ParticleAdd", particles); Surface(m, 2);

            m = Mat("Backdrop", unlit); Surface(m, 1); m.renderQueue = 2900;   // before other transparents
            m = Mat("Bubble", unlit); Surface(m, 1);
            m = Mat("Icon", unlit); Surface(m, 1);
            m = Mat("LightCone", unlit); Surface(m, 2, true); Tex(m, Res + "Textures/fx_shaft.png"); SetColor(m, new Color(1f, 0.7f, 0.35f, 0.22f));
            m = Mat("Glow", unlit); Surface(m, 2); Tex(m, Res + "Textures/fx_glow.png"); SetColor(m, new Color(1f, 0.65f, 0.3f, 0.55f));
            m = Mat("Halo", unlit); Surface(m, 2); Tex(m, Res + "Textures/fx_glow.png"); SetColor(m, new Color(1f, 0.8f, 0.45f, 0.9f));
            m = Mat("Shadow", unlit); Surface(m, 1); Tex(m, Res + "Textures/fx_glow.png"); SetColor(m, new Color(0, 0, 0.05f, 0.55f));
            m = Mat("LaneLine", unlit); Surface(m, 0); SetColor(m, new Color(0.75f, 0.72f, 0.6f));
            // draw order: ground (Geometry-10) -> path lines (Geometry-5, no depth test) -> everything else (Geometry)
            m = Mat("LaneGlow", unlit); Surface(m, 2, true); Tex(m, Res + "Textures/fx_line.png"); SetColor(m, new Color(0.6f, 0.9f, 1f, 1f));
            m = Mat("LitFade", lit); Surface(m, 1); m.SetFloat("_Smoothness", 0.1f);      // invincibility pulse

            Ground("Asphalt", "tex_asphalt", new Color(0.2f, 0.2f, 0.24f));
            Ground("Cobble", "tex_cobble", new Color(0.36f, 0.34f, 0.38f));
            Ground("Grass", "tex_grass", new Color(0.2f, 0.36f, 0.18f));
            Ground("Brick", "tex_brick", new Color(0.55f, 0.25f, 0.2f));
            m = Mat("Curb", lit); Surface(m, 0); SetColor(m, new Color(0.45f, 0.45f, 0.5f)); m.SetFloat("_Smoothness", 0.05f);
            m.renderQueue = GroundQueue;
        }

        static void Ground(string name, string tex, Color fallback)
        {
            var m = Mat(name, S("Universal Render Pipeline/Simple Lit"));
            Surface(m, 0);
            m.SetFloat("_Smoothness", 0.08f);
            var path = new[] { ".png", ".jpg" }.Select(e => Res + "Textures/" + tex + e).FirstOrDefault(File.Exists);
            if (path != null) { Tex(m, path); SetColor(m, Color.white); }
            else SetColor(m, fallback);
            if (name != "Brick") m.renderQueue = GroundQueue;       // ground surfaces draw before the path lines
        }

        static void MakeConfig()
        {
            const string p = Res + "GameConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(p) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), p);
        }

        /// <summary>Resources/PostFx.asset: the base look with every override PostFx animates. Not loaded at run time;
        /// it keeps those post-processing shader variants in builds (unused ones are stripped).</summary>
        static void MakePostFx()
        {
            const string p = Res + "PostFx.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(p);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, p);
            }
            PostFx.Configure(profile, false);
            // overrides that rest at zero in game still count as used here
            if (profile.TryGet(out UnityEngine.Rendering.Universal.LensDistortion lens)) lens.intensity.Override(-0.01f);
            foreach (var c in profile.components)
                if (!AssetDatabase.IsSubAsset(c))
                {
                    c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
            EditorUtility.SetDirty(profile);
        }

        // -------------------------------------------------------------- scenes and settings

        static void MakeScenes()
        {
            MakeScene("Menu", typeof(MenuRoot));
            MakeScene("Game", typeof(GameRoot));
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Scenes + "Menu.unity", true),
                new EditorBuildSettingsScene(Scenes + "Game.unity", true),
            };
        }

        static void MakeScene(string name, System.Type root)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject(name + "Root");
            go.AddComponent(root);
            EditorSceneManager.SaveScene(scene, Scenes + name + ".unity");
        }

        static void PlayerSetup()
        {
            PlayerSettings.companyName = "Razvan";
            PlayerSettings.productName = "Dachshund Dash";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.razvan.dachshunddash");
            // 64-bit only phones (e.g. Pixel 9a) need IL2CPP + ARM64; Mono builds are 32-bit only
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:FullWindow";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            PlayerSettings.strictShaderVariantMatching = false;    // a missing variant falls back to the nearest one
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "UI/app_icon.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        /// <summary>Diagnostics: clips, animator and avatar of every imported model.</summary>
        public static void DumpModels()
        {
            foreach (var path in Directory.GetFiles(Res + "Models", "*.fbx"))
            {
                var p = path.Replace('\\', '/');
                var all = AssetDatabase.LoadAllAssetsAtPath(p);
                var clips = all.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var anim = go ? go.GetComponentInChildren<Animator>() : null;
                var smr = go ? go.GetComponentsInChildren<SkinnedMeshRenderer>().Length : 0;
                Debug.Log($"[DD] {Path.GetFileName(p)}: clips [{string.Join(", ", clips.Select(c => $"{c.name} {c.length:F2}s legacy={c.legacy} loop={c.isLooping} curves={AnimationUtility.GetCurveBindings(c).Length}"))}] " +
                          $"animator={(anim != null)} avatar={(anim && anim.avatar ? anim.avatar.name + " valid=" + anim.avatar.isValid : "none")} skinned={smr} rot={go?.transform.localRotation.eulerAngles}");
            }
        }

        /// <summary>Opens the game scene and plays the prop showcase test level.</summary>
        [MenuItem("Dachshund Dash/Play Prop Showcase")]
        public static void PlayShowcase()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerPrefs.SetInt("dd_showcase", 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(Scenes + "Game.unity");
            EditorApplication.EnterPlaymode();
        }

        // -------------------------------------------------------------- builds

        [MenuItem("Dachshund Dash/Build WebGL")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

        public static void BuildWebGLDev() => Build(BuildTarget.WebGL, "Builds/WebGLDev", BuildOptions.Development);

        [MenuItem("Dachshund Dash/Build Windows (test)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/DachshundDash.exe");

        [MenuItem("Dachshund Dash/Build Android")]
        public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/Android/DachshundDash.apk");

        static void Build(BuildTarget target, string path, BuildOptions extra = BuildOptions.None)
        {
            Setup();
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                options = extra,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[DD] build {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024f):F1} MB, " +
                      $"{report.summary.totalErrors} errors, {report.summary.totalTime}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
