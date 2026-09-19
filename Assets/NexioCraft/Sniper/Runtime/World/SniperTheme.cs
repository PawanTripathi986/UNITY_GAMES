using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Sniper
{
    /// <summary>Colours, light and fog for one district.</summary>
    public sealed class SniperTheme
    {
        public string Name;
        public bool Night;
        public Color SkyTop, SkyHorizon, SkyGround;
        public Color SunColor;
        public Vector3 SunDirection;
        public Color SunDisc;
        public float SunDiscSize;
        public Color AmbientSky, AmbientGround;
        public Color FogColor;
        public float FogStart, FogEnd, FogMax;
        public Color[] Walls;
        public Color Roof, Trim;
        public Color WindowGlass;
        public Color[] WindowLights;
        public float LitWindowChance;
        public Color Road, RoadLine, Sidewalk, Ground, Grass, Foliage;
        public Color[] Cars;
        public Color[] Neon;
        public Color Water;

        public static SniperTheme ForDistrict(int district)
        {
            switch (district)
            {
                case 1: return Harbour;
                case 2: return NeonCity;
                default: return Downtown;
            }
        }

        static SniperTheme downtown, harbour, neon;

        public static SniperTheme Downtown => downtown ??= new SniperTheme
        {
            Name = "Downtown",
            SkyTop = Palette.Hex(0x3C7BD9),
            SkyHorizon = Palette.Hex(0xCFE7F4),
            SkyGround = Palette.Hex(0xAFC2C9),
            SunColor = Palette.Hex(0xFFF1D6) * 0.95f,
            SunDirection = new Vector3(-0.45f, 0.75f, -0.5f),
            SunDisc = Palette.Hex(0xFFF7E0),
            SunDiscSize = 42f,
            AmbientSky = Palette.Hex(0x8FA9CC) * 0.62f,
            AmbientGround = Palette.Hex(0x7A6E5A) * 0.42f,
            FogColor = Palette.Hex(0xC9DDE8),
            FogStart = 230f, FogEnd = 1100f, FogMax = 0.85f,
            Walls = new[] { Palette.Hex(0xD8C7A6), Palette.Hex(0xB9674E), Palette.Hex(0x9AA3AE), Palette.Hex(0xE3DDD2), Palette.Hex(0x7F95A8), Palette.Hex(0xC98F5E) },
            Roof = Palette.Hex(0x6E6A66), Trim = Palette.Hex(0xEEE8DE),
            WindowGlass = Palette.Hex(0x2C3F5E),
            WindowLights = new[] { Palette.Hex(0xFFE3A1) },
            LitWindowChance = 0.03f,
            Road = Palette.Hex(0x44474D), RoadLine = Palette.Hex(0xF2F0E6), Sidewalk = Palette.Hex(0xB9B4AA),
            Ground = Palette.Hex(0x8F8A80), Grass = Palette.Hex(0x6FA35A), Foliage = Palette.Hex(0x4F8E47),
            Cars = new[] { Palette.Hex(0xD9423B), Palette.Hex(0x2F6FD6), Palette.Hex(0xF2F2F2), Palette.Hex(0x2B2E33), Palette.Hex(0xF2B632), Palette.Hex(0x3C9D6E) },
            Neon = new[] { Palette.Hex(0xFF5A5A), Palette.Hex(0x4FC3F7) },
            Water = Palette.Hex(0x3D78A8)
        };

        public static SniperTheme Harbour => harbour ??= new SniperTheme
        {
            Name = "Harbour",
            SkyTop = Palette.Hex(0x3A3F86),
            SkyHorizon = Palette.Hex(0xFF9E62),
            SkyGround = Palette.Hex(0xB9786A),
            SunColor = Palette.Hex(0xFFB27A),
            SunDirection = new Vector3(0.55f, 0.28f, 0.8f),
            SunDisc = Palette.Hex(0xFFD18A),
            SunDiscSize = 70f,
            // Generous ambient: the low sun backlights everything facing the nest.
            AmbientSky = Palette.Hex(0x9C8BC8) * 0.72f,
            AmbientGround = Palette.Hex(0x8A6056) * 0.55f,
            FogColor = Palette.Hex(0xE9A07C),
            FogStart = 200f, FogEnd = 950f, FogMax = 0.85f,
            Walls = new[] { Palette.Hex(0x3F7F86), Palette.Hex(0xA2573F), Palette.Hex(0xE2D3B5), Palette.Hex(0x5F6B7A), Palette.Hex(0xC7A15B) },
            Roof = Palette.Hex(0x5A4B4B), Trim = Palette.Hex(0xE8D8C0),
            WindowGlass = Palette.Hex(0x3B3552),
            WindowLights = new[] { Palette.Hex(0xFFC77A), Palette.Hex(0xFFE0A8) },
            LitWindowChance = 0.12f,
            Road = Palette.Hex(0x4D4650), RoadLine = Palette.Hex(0xF4E3C4), Sidewalk = Palette.Hex(0xA99A8F),
            Ground = Palette.Hex(0x7F7068), Grass = Palette.Hex(0x7E8F55), Foliage = Palette.Hex(0x5D7746),
            Cars = new[] { Palette.Hex(0xE0663C), Palette.Hex(0x3A6EA5), Palette.Hex(0xE8E2D6), Palette.Hex(0x2E2A33), Palette.Hex(0x6BA36F) },
            Neon = new[] { Palette.Hex(0xFF7A59), Palette.Hex(0xFFD166) },
            Water = Palette.Hex(0x4A5E9C)
        };

        public static SniperTheme NeonCity => neon ??= new SniperTheme
        {
            Name = "Neon City",
            Night = true,
            SkyTop = Palette.Hex(0x070A1F),
            SkyHorizon = Palette.Hex(0x3B1E5E),
            SkyGround = Palette.Hex(0x20163A),
            SunColor = Palette.Hex(0x7F8FD6) * 0.55f,
            SunDirection = new Vector3(-0.3f, 0.8f, 0.4f),
            SunDisc = Palette.Hex(0xDDE6FF),
            SunDiscSize = 26f,
            // City glow: bright enough to read robots by, dark enough for the neon to pop.
            AmbientSky = Palette.Hex(0x6E62B8) * 0.82f,
            AmbientGround = Palette.Hex(0x5A4A7A) * 0.7f,
            FogColor = Palette.Hex(0x2A1F4A),
            FogStart = 170f, FogEnd = 800f, FogMax = 0.9f,
            Walls = new[] { Palette.Hex(0x3A4263), Palette.Hex(0x4B3A63), Palette.Hex(0x2F4F5E), Palette.Hex(0x5A5470), Palette.Hex(0x3D3550) },
            Roof = Palette.Hex(0x28243A), Trim = Palette.Hex(0x6C6A8C),
            WindowGlass = Palette.Hex(0x14162B),
            WindowLights = new[] { Palette.Hex(0xFFD59A), Palette.Hex(0x7FE7FF), Palette.Hex(0xFF8AD8), Palette.Hex(0xFFF2C4) },
            LitWindowChance = 0.42f,
            Road = Palette.Hex(0x23222F), RoadLine = Palette.Hex(0xB9B0E6), Sidewalk = Palette.Hex(0x4B4862),
            Ground = Palette.Hex(0x3A3650), Grass = Palette.Hex(0x2F4A48), Foliage = Palette.Hex(0x2C5A52),
            Cars = new[] { Palette.Hex(0xFF3D7F), Palette.Hex(0x2EC4F1), Palette.Hex(0xD9D6F2), Palette.Hex(0x1B1A26), Palette.Hex(0xFFB547) },
            Neon = new[] { Palette.Hex(0xFF3DB8), Palette.Hex(0x39E6FF), Palette.Hex(0xB06CFF), Palette.Hex(0xFFE14D), Palette.Hex(0x4DFF9A) },
            Water = Palette.Hex(0x1D2B55)
        };

        /// <summary>Pushes this theme's light and fog to the sniper shaders.</summary>
        public void Apply()
        {
            Shader.SetGlobalVector("_NxSunDir", SunDirection.normalized);
            Shader.SetGlobalColor("_NxSunColor", SunColor);
            Shader.SetGlobalColor("_NxSkyAmbient", AmbientSky);
            Shader.SetGlobalColor("_NxGroundAmbient", AmbientGround);
            Shader.SetGlobalColor("_NxFogColor", FogColor);
            Shader.SetGlobalVector("_NxFogParams", new Vector4(FogStart, 1f / Mathf.Max(1f, FogEnd - FogStart), FogMax, 0f));
        }
    }

    /// <summary>Materials for the sniper world, created once from the shaders in Resources/SniperShaders.</summary>
    public static class SniperMaterials
    {
        static Material lit, sky, additive, blended, decal;
        static Texture2D softDot, spark, ring;

        public static Material Lit => lit != null ? lit : lit = Create("SniperLit", "Sniper Lit");
        public static Material Sky => sky != null ? sky : sky = Create("SniperSky", "Sniper Sky");

        /// <summary>Additive glow: sparks, flashes, tracers, the sun.</summary>
        public static Material Additive
        {
            get
            {
                if (additive != null) return additive;
                additive = Create("SniperFx", "Sniper Additive");
                additive.mainTexture = SoftDot;
                additive.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                additive.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                additive.renderQueue = 3100;
                return additive;
            }
        }

        /// <summary>Normal transparency: smoke and dust.</summary>
        public static Material Blended
        {
            get
            {
                if (blended != null) return blended;
                blended = Create("SniperFx", "Sniper Blended");
                blended.mainTexture = SoftDot;
                blended.renderQueue = 3000;
                return blended;
            }
        }

        /// <summary>Blob shadows and bullet marks, pulled towards the camera so they sit on surfaces.</summary>
        public static Material Decal
        {
            get
            {
                if (decal != null) return decal;
                decal = Create("SniperFx", "Sniper Decal");
                decal.mainTexture = SoftDot;
                decal.SetFloat("_DepthOffset", -2f);
                decal.renderQueue = 2900;
                return decal;
            }
        }

        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                var r = new Raster(64, 64, new Color(1f, 1f, 1f, 0f));
                r.Circle(32f, 32f, 8f, Color.white, 46f);
                return softDot = r.ToTexture("SoftDot");
            }
        }

        /// <summary>Hard-edged dot for sharp glints and the tracer core.</summary>
        public static Texture2D Spark
        {
            get
            {
                if (spark != null) return spark;
                var r = new Raster(32, 32, new Color(1f, 1f, 1f, 0f));
                r.Circle(16f, 16f, 9f, Color.white, 10f);
                return spark = r.ToTexture("Spark");
            }
        }

        /// <summary>Bullet hole: dark centre with a soft rim.</summary>
        public static Texture2D HoleRing
        {
            get
            {
                if (ring != null) return ring;
                var r = new Raster(64, 64, new Color(0f, 0f, 0f, 0f));
                r.Circle(32f, 32f, 12f, new Color(0f, 0f, 0f, 0.55f), 22f);
                r.Circle(32f, 32f, 6f, new Color(0.05f, 0.05f, 0.05f, 1f), 3f);
                return ring = r.ToTexture("Hole");
            }
        }

        static Material Create(string shaderFile, string name)
        {
            var shader = Resources.Load<Shader>("SniperShaders/" + shaderFile);
            if (shader == null)
            {
                Debug.LogError("Sniper shader missing: SniperShaders/" + shaderFile);
                shader = Shader.Find("Sprites/Default");
            }
            return new Material(shader) { name = name };
        }
    }
}
