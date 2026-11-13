// The lit world shader (D-01, ENVIRONMENT_ROADMAP ED-1) for the chunk meshes: ground, roads and
// buildings. URP PBR lighting (UniversalFragmentPBR): main-light shadows, Forward+ additional
// lights, SSAO and fog. SRP Batcher compatible.
//
// The meshes carry only positions and an RGBA8 colour (sRGB albedo, WorldMaterial id in alpha),
// as they did unlit, so the mesh format and the apply step are unchanged. The normal is the
// triangle's own, from screen-space derivatives of the world position, turned to face the camera:
// exact for these flat-faceted meshes. World UVs are in metres: plan view (east, north) on flat
// faces, box-projected on walls, offset by _DadWorldWrap (FloatingOrigin) so they hold across
// origin shifts.
//
// Materials (D-02, ED-2): each WorldMaterial picks a slice of the generated texture arrays
// (Editor/Materials/MaterialLibrary): albedo, a normal map and a mask (metallic, occlusion, cavity,
// smoothness). The layer's albedo is scaled so its mean is the vertex colour: builders still choose
// the colour, the texture adds the detail. Tiling is broken three ways:
// - stochastic sampling: three samples per pixel with random offsets (and rotations, for materials
//   with no direction), blended over a triangle lattice that repeats every 4,000 m;
// - macro variation from a texture tiled every 500 m and again every 50 m: brightness, grime, fading;
// - repair patches on asphalt and concrete ground, from a hashed 8 m grid.
// Dirt gathers in the cavity mask (cracks, joints, hollows) and where the macro grime is high.
//
// Shadows: buildings cast with front faces culled (_ShadowCull), so only the faces turned away
// from the light reach the shadow map. Buildings are closed solids, so no shadow is lost, and a
// lit face is never compared with itself: no acne, without the normal bias these meshes cannot give.
//
// Far-tier facades (D-06a), with _DAD_FACADE (WorldFacades.mat, the building meshes): the builder
// writes facade coordinates in TEXCOORD0 (BuildingBuilder: u bays along the wall, v floors up it, w
// the family, a shop flag, a seed, a wall flag and the floors below a parapet, s metres along the
// wall), and the walls are painted with their family's windows, shops, signboards and floor lines;
// a parapet (D-06b) is plain above its roof's slab line. Nothing is added to the
// mesh. Walls with the wall flag take texture UVs from s and the height instead of the box
// projection, so diagonal walls are no longer stretched (K-21). Each piece is a box in its bay-and-floor
// cell, antialiased by the coordinates' screen derivatives; once a bay is only a few pixels wide the
// pattern fades to its average cover, so distant blocks read as rows of windows without moire.
// Signboards are blank colour panels (no lettering, as M2). Glass is dark, or lighter on a hashed few
// (a lit room or curtains). At night (D-13) a window, shop or sign has a rank of its own, from the same hash, and glows
// while _DadLights' share is above its rank, so lights come on and go off one by one, each over a few minutes.
//
// Near facades (D-06c), with _DAD_NEAR (WorldNear.mat, the near facades of a cell's near kit): drawn as they
// are, but dissolved in by a 4x4 screen-door dither (Bayer) as the focus comes from _DadNearFade.y (none of a
// pixel's 16 drawn) to _DadNearFade.x (all of them), the distance measured as StreetLod measures it (on the
// ground plan from _DadNearFocus, and the focus's height above 10 m); all four passes drop the same pixels, so
// shadows and depth agree. The streamer sets both globals every frame; with the fade unset (y <= x) all is drawn.
//
// _DadWorldDebug (global, DebugViews F5): 0 lit, 1 normals, 2 material ids, 3 metre grid, 4 albedo,
// 5 facade families (WorldFacades only; other meshes draw grey).
Shader "Dad/World"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _ShadowCull("Shadow caster cull", Float) = 1
        [NoScaleOffset] _WorldAlbedo("Albedo layers", 2DArray) = "" {}
        [NoScaleOffset] _WorldNormal("Normal layers (rg normal, b height)", 2DArray) = "" {}
        [NoScaleOffset] _WorldMask("Mask layers (r metallic, g occlusion, b cavity, a smoothness)", 2DArray) = "" {}
        [NoScaleOffset] _WorldMacro("Macro variation (r brightness, g grime, b fading)", 2D) = "grey" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
        CBUFFER_END

        float4 _DadWorldWrap;  // xz: the origin offset wrapped to 4,000 m (WorldMaterials.Wrap)
        float _DadWorldDebug;
        float4 _DadNearFocus;  // xyz: the streaming focus (render space)
        float4 _DadNearFade;   // x: all drawn nearer than this, y: none beyond (ChunkBuildings.FadeStart, FadeEnd)
        float4 _DadLights;     // NightLife, each a share 0 to 1: x windows, y lamps, z shops, w signs

        // The near facades' dither (see the header): drops this pixel unless the fade covers it.
        void NearDither(float3 positionWS, float4 positionCS)
        {
            if (_DadNearFade.y <= _DadNearFade.x) return;
            float2 d = positionWS.xz - _DadNearFocus.xz;
            float up = max(0, _DadNearFocus.y - 10);
            float cover = saturate((_DadNearFade.y - sqrt(dot(d, d) + up * up)) / (_DadNearFade.y - _DadNearFade.x));
            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            uint2 p = (uint2)positionCS.xy & 3u;
            clip(cover - (bayer[p.y * 4 + p.x] + 0.5) / 16);
        }

        #define MATERIAL_COUNT 18
        // WorldMaterial table: by Dad.World.WorldMaterial id, (texture layer = MaterialLayer, tile
        // period in metres, sampling: 0 plain, 1 stochastic offsets, 2 offsets and rotations,
        // normal strength). Keep the names and order in step with the enum, the layers with
        // WorldMaterials.LayerOf and the periods with LayerRecipes (WorldMaterialTests compares them).
        // Sampling 1 is for materials with a direction: streaks on plaster, rollers, brushing,
        // ripples. Patterns with joints (bricks, tiles, slabs, sheets) sample plainly.
        static const float4 MaterialTable[MATERIAL_COUNT] =
        {
            float4(1, 2.5, 2, 1), // Default
            float4(11, 2, 2, 1),  // Soil
            float4(12, 2, 2, 1),  // Grass
            float4(14, 8, 2, 1),  // Water
            float4(10, 2, 0, 1),  // Paving
            float4(0, 2, 2, 1),   // Asphalt
            float4(1, 2.5, 2, 1), // Concrete
            float4(3, 2, 1, 1),   // Plaster
            float4(1, 2.5, 2, 1), // Roof
            float4(2, 1.25, 0, 1), // Brick
            float4(4, 2, 1, 1),   // Paint
            float4(5, 0.5, 1, 1), // Metal
            float4(6, 1, 2, 1),   // Rust
            float4(7, 2, 2, 1),   // Glass
            float4(8, 2, 0, 1),   // RoofSheet
            float4(9, 2, 0, 1),   // Tiles
            float4(13, 4, 1, 1),  // Sand
            float4(15, 2, 2, 1),  // Marking
        };
        // Wear by WorldMaterial id: (macro brightness variation, dirt, fading, repair patches).
        static const half4 MaterialWear[MATERIAL_COUNT] =
        {
            half4(0.10, 0.30, 0.00, 0),  // Default
            half4(0.18, 0.20, 0.15, 0),  // Soil
            half4(0.22, 0.10, 0.35, 0),  // Grass
            half4(0.08, 0.00, 0.00, 0),  // Water
            half4(0.20, 0.62, 0.20, 0),  // Paving
            half4(0.22, 0.55, 0.35, 1),  // Asphalt
            half4(0.18, 0.60, 0.15, 1),  // Concrete
            half4(0.16, 0.62, 0.35, 0),  // Plaster
            half4(0.18, 0.85, 0.15, 0),  // Roof
            half4(0.14, 0.48, 0.15, 0),  // Brick
            half4(0.10, 0.40, 0.55, 0),  // Paint
            half4(0.08, 0.42, 0.05, 0),  // Metal
            half4(0.10, 0.10, 0.00, 0),  // Rust
            half4(0.03, 0.20, 0.00, 0),  // Glass
            half4(0.10, 0.30, 0.10, 0),  // RoofSheet
            half4(0.05, 0.30, 0.00, 0),  // Tiles
            half4(0.12, 0.05, 0.10, 0),  // Sand
            half4(0.10, 0.60, 0.60, 0),  // Marking
        };

        uint MaterialId(half alpha)
        {
            uint id = (uint)round(alpha * 255.0) & 31u;
            return id < MATERIAL_COUNT ? id : 0;
        }

        // WorldMaterials.Night's mark, in alpha's top three bits.
        uint NightMark(half alpha) { return (uint)round(alpha * 255.0) >> 5; }

        // The triangle's normal from screen-space derivatives, turned towards the camera.
        float3 FaceNormal(float3 positionWS)
        {
            float3 n = normalize(cross(ddy(positionWS), ddx(positionWS)));
            return dot(n, GetWorldSpaceViewDir(positionWS)) < 0 ? -n : n;
        }

        // World UVs in metres and their tangent frame (u along tu, v along tv): (east, north) on
        // flat faces; on walls, east or north (whichever the wall runs along more) and up. Box
        // projection, not a direction along the wall: the derivative normal carries noise of about
        // 1e-3, and dotted with positions kilometres from the world origin that became metres of
        // noise per pixel (D-01 metre-grid view). Walls at 45 degrees stretch up to 1.41x (K-21) here;
        // building walls take the builder's metres along the wall instead (_DAD_FACADE, D-06a).
        void WorldFrame(float3 positionWS, float3 n, out float2 uv, out float3 tu, out float3 tv)
        {
            float3 p = positionWS + float3(_DadWorldWrap.x, 0, _DadWorldWrap.z);
            if (abs(n.y) > 0.7)
            {
                uv = p.xz; tu = float3(1, 0, 0); tv = float3(0, 0, 1);
            }
            else if (abs(n.z) >= abs(n.x))
            {
                float s = n.z >= 0 ? 1 : -1;
                uv = float2(p.x * s, p.y); tu = float3(s, 0, 0); tv = float3(0, 1, 0);
            }
            else
            {
                float s = n.x >= 0 ? 1 : -1;
                uv = float2(-p.z * s, p.y); tu = float3(0, 0, -s); tv = float3(0, 1, 0);
            }
        }

        float2 WorldUv(float3 positionWS, float3 n)
        {
            float2 uv; float3 tu, tv;
            WorldFrame(positionWS, n, uv, tu, tv);
            return uv;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local _ _DAD_FACADE
            #pragma shader_feature_local _ _DAD_NEAR
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_WorldAlbedo);
            SAMPLER(sampler_WorldAlbedo);
            TEXTURE2D_ARRAY(_WorldNormal);
            TEXTURE2D_ARRAY(_WorldMask);
            TEXTURE2D(_WorldMacro);
            SAMPLER(sampler_WorldMacro);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            #ifdef _DAD_FACADE
                float4 facade : TEXCOORD0;  // u bays, v floors, w family + 8 shop + 16 seed + 4096 wall + 8192 roof floors, s metres along
            #endif
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;  // rgb linear albedo, a material id / 255
                half fogFactor : TEXCOORD1;
            #ifdef _DAD_FACADE
                float4 facade : TEXCOORD2;
            #endif
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = half4(SRGBToLinear(i.color.rgb) * _BaseColor.rgb, i.color.a);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
            #ifdef _DAD_FACADE
                o.facade = i.facade;
            #endif
                return o;
            }

            // lowbias32, as TileNoise.Hash (SRP core already has a Hash).
            uint LowBias(uint x)
            {
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return x;
            }

            float ToUnit(uint h) { return (h >> 8) * (1.0 / 16777216.0); }

            float PeriodicMod(float x, float m) { return x - m * floor(x / m); }

            // The stochastic sampling lattice: triangles of edges (2, 0) and (1, 2) metres in world
            // UVs, which repeat every 4,000 m along both axes. A vertex's id is its canonical index,
            // the same for all its copies 4,000 m apart. Keep in step with Dad.World.WorldUvLattice.
            #define LATTICE_SIZE 2.0
            #define LATTICE_PERIOD 2000.0
            uint LatticeVertexId(float2 v)
            {
                float jj = PeriodicMod(v.y, LATTICE_PERIOD);
                float k = round((v.y - jj) / LATTICE_PERIOD);
                float ii = PeriodicMod(v.x + k * (LATTICE_PERIOD * 0.5), LATTICE_PERIOD);
                return (uint)ii + (uint)LATTICE_PERIOD * (uint)jj;
            }

            struct LayerSample { half3 albedo; half2 normal; half4 mask; };

            LayerSample Fetch(float2 t, float layer, float2 dx, float2 dy)
            {
                LayerSample s;
                s.albedo = SAMPLE_TEXTURE2D_ARRAY_GRAD(_WorldAlbedo, sampler_WorldAlbedo, t, layer, dx, dy).rgb;
                s.normal = SAMPLE_TEXTURE2D_ARRAY_GRAD(_WorldNormal, sampler_WorldAlbedo, t, layer, dx, dy).rg * 2 - 1;
                s.mask = SAMPLE_TEXTURE2D_ARRAY_GRAD(_WorldMask, sampler_WorldAlbedo, t, layer, dx, dy);
                return s;
            }

            // A layer at world UV uv (metres). Stochastic modes take three samples, one per corner of
            // the lattice triangle holding the point, each with its corner's random offset (and
            // rotation in mode 2), and blend them by sharpened barycentric weights. Blending lowers
            // contrast, so the albedo's spread about the layer mean is restored (variance-preserving).
            LayerSample SampleLayer(float2 uv, float4 row, half3 mean)
            {
                float layer = row.x;
                uint mode = (uint)row.z;
                float2 t = uv / row.y, dx = ddx(uv) / row.y, dy = ddy(uv) / row.y;
                if (mode == 0) return Fetch(t, layer, dx, dy);

                float b = uv.y / LATTICE_SIZE, a = uv.x / LATTICE_SIZE - 0.5 * b;
                float2 c = floor(float2(a, b)), f = float2(a, b) - c;
                float2 v[3];
                float3 w;
                if (f.x + f.y < 1) { v[0] = c; v[1] = c + float2(1, 0); v[2] = c + float2(0, 1); w = float3(1 - f.x - f.y, f.x, f.y); }
                else { v[0] = c + 1; v[1] = c + float2(1, 0); v[2] = c + float2(0, 1); w = float3(f.x + f.y - 1, 1 - f.y, 1 - f.x); }
                w = w * w * w;
                w /= dot(w, 1);

                LayerSample r = (LayerSample)0;
                [unroll] for (int i = 0; i < 3; i++)
                {
                    uint h = LowBias(LatticeVertexId(v[i]) ^ ((uint)layer * 0x9e3779b9u));
                    float2 offset = float2(ToUnit(h), ToUnit(LowBias(h)));
                    float angle = mode == 2 ? ToUnit(LowBias(h + 1u)) * TWO_PI : 0;
                    float sn, cs;
                    sincos(angle, sn, cs);
                    float2x2 rot = float2x2(cs, -sn, sn, cs);
                    LayerSample s = Fetch(mul(rot, t) + offset, layer, mul(rot, dx), mul(rot, dy));
                    r.albedo += w[i] * (s.albedo - mean);
                    r.normal += w[i] * mul(transpose(rot), s.normal); // back from the rotated frame
                    r.mask += w[i] * s.mask;
                }
                r.albedo = max(mean + r.albedo * rsqrt(dot(w, w)), 0);
                return r;
            }

            // Repair patches (asphalt and concrete ground): a quarter of the 8 m cells of a hashed grid
            // hold one rectangle, 0.8-4.8 by 0.8-3.2 m, fresher or older than its surroundings, with a
            // dark seam. x: inside the patch, y: its tone, z: on the seam. The grid's hash wraps every
            // 500 cells (4,000 m).
            float3 Repair(float2 uv)
            {
                float2 cell = floor(uv / 8.0);
                uint h = LowBias((uint)PeriodicMod(cell.x, 500.0) + 500u * (uint)PeriodicMod(cell.y, 500.0) + 0x632be5abu);
                float2 halfSize = 0.4 + float2(ToUnit(LowBias(h + 2u)), ToUnit(LowBias(h + 3u))) * float2(2.0, 1.2);
                float2 centre = cell * 8.0 + halfSize + 0.1 + float2(ToUnit(LowBias(h + 4u)), ToUnit(LowBias(h + 5u))) * (8.0 - 2.0 * halfSize - 0.2);
                float2 d = abs(uv - centre) - halfSize;
                float edge = max(d.x, d.y);
                float aa = max(fwidth(edge), 1e-4);
                float on = ToUnit(h) < 0.27 ? 1 : 0;
                float inside = 1 - smoothstep(-aa, aa, edge);
                float seam = 1 - smoothstep(0.0, 0.03 + aa, abs(edge));
                return float3(inside, 0.8 + 0.3 * ToUnit(LowBias(h + 6u)), seam) * float3(on, 1, on);
            }

            static const half3 MaterialColours[MATERIAL_COUNT] =
            {
                half3(1, 0, 1), half3(0.55, 0.4, 0.25), half3(0.2, 0.7, 0.2), half3(0.1, 0.4, 0.9), half3(0.8, 0.8, 0.5),
                half3(0.15, 0.15, 0.15), half3(0.6, 0.6, 0.65), half3(0.95, 0.85, 0.7), half3(0.8, 0.3, 0.2),
                half3(0.7, 0.25, 0.1), half3(0.95, 0.95, 0.9), half3(0.75, 0.8, 0.85), half3(0.6, 0.3, 0.1), half3(0.2, 0.6, 0.7),
                half3(0.5, 0.55, 0.6), half3(0.9, 0.8, 0.6), half3(0.9, 0.8, 0.5), half3(1, 1, 0.2),
            };

            // ---- Far-tier facades (D-06a) ----
            // Keep the family numbers in step with Dad.World.FacadeFamily.
            #define FAMILY_WALKUP 1
            #define FAMILY_SHOPHOUSE 2
            #define FAMILY_COMMERCIAL 3
            #define FAMILY_INDUSTRIAL 4
            #define FAMILY_INSTITUTIONAL 5

            // sRGB colours as M2's kit (Facades.cs): glass, lighter glass, grille, shutter, sign colours.
            static const half3 FacadeGlass = half3(0x2f, 0x3a, 0x40) / 255.0, FacadeGlassLit = half3(0x6b, 0x6a, 0x5e) / 255.0,
                FacadeGrille = half3(0x2c, 0x2c, 0x2b) / 255.0, FacadeShutter = half3(0x8a, 0x8d, 0x8f) / 255.0;
            static const half3 FacadeSigns[6] =
            {
                half3(0xb0, 0x30, 0x2a) / 255.0, half3(0x2f, 0x7a, 0x3a) / 255.0, half3(0x2c, 0x4f, 0x8f) / 255.0,
                half3(0xd2, 0xb2, 0x3a) / 255.0, half3(0xe6, 0xe2, 0xd6) / 255.0, half3(0x7a, 0x2f, 0x6b) / 255.0,
            };
            static const half3 FamilyColours[6] =
            {
                half3(0.5, 0.5, 0.5), half3(0.9, 0.75, 0.45), half3(0.85, 0.35, 0.3), half3(0.3, 0.55, 0.9), half3(0.55, 0.55, 0.6), half3(0.35, 0.75, 0.45),
            };

            // Cover of the box r = (x0, y0, x1, y1) at p, in cell units, antialiased over aa; once
            // fade is 1, the box's area (its average over the cell) instead.
            half Box(float2 p, float4 r, float2 aa, half fade)
            {
                float2 lo = smoothstep(r.xy - aa, r.xy + aa, p), hi = 1 - smoothstep(r.zw - aa, r.zw + aa, p);
                return lerp(lo.x * lo.y * hi.x * hi.y, (r.z - r.x) * (r.w - r.y), fade);
            }

            // Thin lines across x at the given spacing (grille bars, shutter ribs), faded to their mean.
            half Lines(float x, float spacing, float width, float aa, half fade)
            {
                float d = abs(frac(x / spacing) - 0.5) * spacing;
                return lerp(1 - smoothstep(width * 0.5 - aa, width * 0.5 + aa, d), width / spacing, fade);
            }

            // Room lights, linear: tungsten and the white tube most homes use; a shop's; how bright a sign's panel is.
            static const half3 RoomWarm = half3(1.0, 0.60, 0.28) * 0.30, RoomCool = half3(0.78, 0.87, 1.0) * 0.20, ShopLight = half3(1.0, 0.80, 0.54) * 0.50;
            static const half SignGlow = 0.60;

            // A share's light on something of this rank: fades in as the share passes it. (Fading over 2 % of the share is a few minutes of the clock.)
            half Lit(half share, half rank) { return saturate((share - rank) * 50); }

            struct Facade { half glass, frame, shutter, sign, band; half3 glassColour, signColour, room; half signLit; };

            // The far-tier facade at facade coordinates f (see the header).
            Facade FacadeAt(float3 f)
            {
                Facade o = (Facade)0;
                uint w = (uint)round(f.z), family = w & 7u, shop = (w >> 3) & 1u, seed = (w >> 4) & 255u, roofFloors = w >> 13;
                if (family == 0u) return o;
                float2 cell = floor(f.xy), p = f.xy - cell;
                float2 fw = max(fwidth(f.xy), 1e-5);
                float2 aa = fw * 0.75;
                // Fade from about 6 pixels a bay or floor to 2.
                half fade = saturate((max(fw.x, fw.y) - 0.15) / 0.35);
                // The parapet: plain, with the roof's slab line at its foot (D-06b).
                if (roofFloors != 0u && cell.y >= (float)roofFloors)
                {
                    o.band = cell.y == (float)roofFloors ? Box(p, float4(-1, -1, 2, 0.05), aa, fade) : 0;
                    return o;
                }
                uint h = LowBias(seed * 0x9e3779b9u ^ (uint)cell.x * 0x85ebca6bu ^ (uint)cell.y * 0xc2b2ae35u);
                float r0 = ToUnit(h), r1 = ToUnit(LowBias(h + 1u));
                float r2 = ToUnit(LowBias(h + 2u)), r3 = ToUnit(LowBias(h + 3u));
                o.glassColour = SRGBToLinear(r0 < 0.18 ? FacadeGlassLit : FacadeGlass);
                o.signColour = SRGBToLinear(FacadeSigns[(uint)(r1 * 6) % 6u]);
                // The slab line at each floor above the ground.
                o.band = cell.y >= 1 ? Box(p, float4(-1, -1, 2, 0.035), aa, fade) : 0;
                bool ground = cell.y < 1;
                if (shop == 1u && ground && (family == FAMILY_SHOPHOUSE || family == FAMILY_COMMERCIAL))
                {
                    if (family == FAMILY_COMMERCIAL)
                        o.glass = Box(p, float4(0.05, 0, 0.95, 0.74), aa, fade);
                    else
                    {
                        // A rolling shutter: closed, half or rolled up, ribbed; the open part is the shop's dark inside.
                        float down = r0 < 0.35 ? 0.0 : r0 < 0.6 ? 0.4 : 0.62;
                        half opening = Box(p, float4(0.08, 0, 0.92, 0.7), aa, fade);
                        o.shutter = Box(p, float4(0.08, down, 0.92, 0.7), aa, fade) * (1 - 0.35 * Lines(p.y, 0.04, 0.012, aa.y, fade));
                        o.glass = saturate(opening - o.shutter);
                        o.glassColour = SRGBToLinear(half3(0x35, 0x30, 0x2b) / 255.0);
                    }
                    o.sign = Box(p, float4(0.03, 0.76, 0.97, 0.93), aa, fade);
                    o.room = ShopLight * Lit(_DadLights.z, r2 * 0.95);
                    // Not every shop has a lit sign.
                    o.signLit = r3 < 0.45 ? Lit(_DadLights.w, r2 * 0.9) * SignGlow : 0;
                    return o;
                }
                float4 win =
                    family == FAMILY_COMMERCIAL ? float4(0.08, 0.22, 0.92, 0.86) :
                    family == FAMILY_INDUSTRIAL ? float4(0.12, 0.62, 0.88, 0.86) :
                    family == FAMILY_INSTITUTIONAL ? float4(0.18, 0.26, 0.82, 0.80) :
                    float4(0.22, 0.30, 0.78, 0.78);
                // A few windows of homes are bricked up or boarded.
                if (family != FAMILY_COMMERCIAL && r1 > 0.93) return o;
                o.glass = Box(p, win, aa, fade);
                // A frame around the glass; on homes, a steel grille just inside (as M2).
                o.frame = saturate(Box(p, win + float4(-0.03, -0.04, 0.03, 0.02), aa, fade) - o.glass);
                if (family == FAMILY_WALKUP || family == FAMILY_SHOPHOUSE)
                    o.frame += o.glass * Lines(p.x - win.x, 0.09, 0.012, aa.x, fade) * 0.8;
                else if (family == FAMILY_COMMERCIAL)
                    o.frame += o.glass * Lines(p.x - 0.5, 1.0, 0.025, aa.x, fade);
                if (family == FAMILY_INSTITUTIONAL) o.band = max(o.band, Box(p, float4(-1, -1, 2, 0.08), aa, fade) * 0.6);
                // Offices and works empty in the evening; a home's window is warm or white by its own hash, dimmer towards the sill (curtains, the ceiling lit).
                half share = family == FAMILY_COMMERCIAL ? _DadLights.x * (0.25 + 0.75 * _DadLights.z) : family == FAMILY_WALKUP || family == FAMILY_SHOPHOUSE ? _DadLights.x : _DadLights.x * 0.4;
                half3 tone = r3 > (family == FAMILY_COMMERCIAL ? 0.35 : 0.68) ? RoomCool : RoomWarm;
                // Curtains drawn to the sides on about half, the ceiling lit on the rest; both average out once a window is only a few pixels.
                half across = saturate((p.x - win.x) / (win.z - win.x)), up = saturate((p.y - win.y) / (win.w - win.y));
                half room = r1 < 0.5 ? 0.25 + 0.75 * (1 - smoothstep(0.08, 0.42, abs(across - 0.5))) : 0.45 + 0.55 * up * up;
                o.room = tone * Lit(share, r2) * (0.45 + 1.1 * r0) * lerp(room, 0.6, fade);
                return o;
            }

            // 1 m lines one pixel wide, 10 m lines two pixels wide.
            half GridLines(float2 uv)
            {
                float2 w = max(fwidth(uv), 1e-5);
                float2 d1 = abs(frac(uv - 0.5) - 0.5) / w;
                float2 d10 = abs(frac(uv * 0.1 - 0.5) - 0.5) * 10 / w;
                return max(0.5 * (1 - saturate(min(d1.x, d1.y))), 1 - saturate(min(d10.x, d10.y) - 1));
            }

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef _DAD_NEAR
                NearDither(i.positionWS, i.positionCS);
            #endif
                float3 n = FaceNormal(i.positionWS);
                uint id = MaterialId(i.color.a);
                uint view = (uint)round(_DadWorldDebug);
                float4 row = MaterialTable[id];
                half4 wear = MaterialWear[id];
                float2 uv;
                float3 tu, tv;
                WorldFrame(i.positionWS, n, uv, tu, tv);
            #ifdef _DAD_FACADE
                // Building walls: metres along the wall and up (K-21).
                if (((uint)round(i.facade.z) & 4096u) != 0 && abs(n.y) <= 0.7)
                {
                    uv = float2(i.facade.w, i.positionWS.y);
                    tu = normalize(cross(n, float3(0, 1, 0)));
                    tv = float3(0, 1, 0);
                }
            #endif
                if (view == 2) return half4(MaterialColours[id] * (0.75 + 0.25 * n.y), 1);
            #ifdef _DAD_FACADE
                if (view == 5) return half4(FamilyColours[min((uint)round(i.facade.z) & 7u, 5u)] * (0.75 + 0.25 * n.y), 1);
            #else
                if (view == 5) return half4(FamilyColours[0] * (0.75 + 0.25 * n.y), 1);
            #endif
                if (view == 3) return half4(lerp(i.color.rgb * 0.6 + 0.3, half3(0.05, 0.05, 0.05), GridLines(uv)), 1);

                // Layer, scaled so its mean is the vertex colour.
                half3 mean = SAMPLE_TEXTURE2D_ARRAY_LOD(_WorldAlbedo, sampler_WorldAlbedo, float2(0.5, 0.5), row.x, 12).rgb;
                LayerSample L = SampleLayer(uv, row, mean);
                half3 albedo = i.color.rgb * L.albedo / max(mean, 0.01);
                if (id == 5u) albedo = min(albedo, i.color.rgb * 1.12); // Asphalt's stone chips stay in the surface instead of sparkling in the sun.
                else if (id == 17u) albedo *= 0.72;                    // Painted lines are years old.

                // Wear across the city.
                half4 macro = SAMPLE_TEXTURE2D(_WorldMacro, sampler_WorldMacro, uv / 500.0) * 0.6
                            + SAMPLE_TEXTURE2D(_WorldMacro, sampler_WorldMacro, uv / 50.0 + 0.37) * 0.4;
                albedo *= 1 + wear.x * (macro.r - 0.5) * 2;
                half dirt = saturate(wear.y * ((macro.g - 0.42) * 2.5 + L.mask.b * 0.8));
                albedo = lerp(albedo, albedo * half3(0.42, 0.38, 0.33), dirt * 0.85);
                half fade = wear.z * saturate((macro.b - 0.45) * 2.5);
                albedo = lerp(albedo, Luminance(albedo).xxx * 1.1, fade * 0.6);
                half smoothness = L.mask.a * (1 - 0.5 * dirt);
                half cavity = L.mask.b;
                float3 patch = Repair(uv) * (wear.w > 0 && abs(n.y) > 0.7 ? 1 : 0);
                albedo *= lerp(1, patch.y, patch.x) * (1 - 0.35 * patch.z);
                smoothness = saturate(smoothness + 0.06 * patch.x);
                albedo = min(albedo, 0.95);
                half metallic = L.mask.r, occlusion = L.mask.g * (1 - 0.3 * cavity * wear.y), flatten = 0;
                half3 emission = 0;

                // What is built rather than painted lights by its mark: Milestone 2's panes and shops, the near facades' signboards.
                uint mark = NightMark(i.color.a);
                if (mark == 7u) emission = albedo * SignGlow * Lit(_DadLights.w, 0.3);
                else if (mark == 6u)
                {
                    // The tube at the top of the shop, and shelves of goods below it.
                    float row = i.positionWS.y / 0.5;
                    uint item = (uint)floor(row) * 0x85ebca6bu ^ (uint)(int)floor(uv.x / 0.28) * 0xc2b2ae35u;
                    half3 goods = lerp(half3(1.0, 0.55, 0.30), half3(0.45, 0.65, 0.85), ToUnit(LowBias(item + 1u))) * (0.2 + 0.8 * ToUnit(LowBias(item)));
                    half up = saturate((i.positionWS.y - 0.4) / 2.7);
                    emission = ShopLight * Lit(_DadLights.z, 0.2) * lerp(goods * smoothstep(0.0, 0.12, frac(row)) * 0.7, 1, up * up * up * up);
                }
                else if (mark > 0u) emission = ((mark & 1u) != 0u ? RoomWarm : RoomCool) * 0.7 * Lit(_DadLights.x, mark * 0.13);

            #ifdef _DAD_FACADE
                // Far-tier facade: glass, frames, shutters and signs over the wall's own surface.
                Facade fc = FacadeAt(i.facade.xyz);
                half cover = saturate(fc.glass + fc.frame + fc.shutter + fc.sign);
                if (cover + fc.band > 0)
                {
                    albedo *= 1 - 0.18 * fc.band;
                    albedo = lerp(albedo, fc.glassColour, fc.glass);
                    albedo = lerp(albedo, SRGBToLinear(FacadeGrille), saturate(fc.frame));
                    albedo = lerp(albedo, SRGBToLinear(FacadeShutter) * L.albedo / max(mean, 0.01), fc.shutter);
                    albedo = lerp(albedo, fc.signColour, fc.sign);
                    smoothness = lerp(smoothness, 0.62, fc.glass);
                    smoothness = lerp(smoothness, 0.4, fc.shutter + fc.sign);
                    metallic = lerp(metallic, 0.6, fc.shutter);
                    occlusion = lerp(occlusion, 1, fc.glass);
                    flatten = cover;
                    emission += fc.room * fc.glass * (1 - saturate(fc.frame)) + fc.signColour * fc.sign * fc.signLit;
                }
            #endif

                // Normal: the face's frame, with the layer's normal map.
                tu = normalize(tu - n * dot(n, tu));
                tv = normalize(tv - n * dot(n, tv));
                half2 nxy = L.normal * row.w * (1 - flatten);
                float3 nw = normalize(tu * nxy.x + tv * nxy.y + n * sqrt(saturate(1 - dot(nxy, nxy))));

                if (view == 1) return half4(nw * 0.5 + 0.5, 1);
                if (view == 4) return half4(albedo, 1);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = nw;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogFactor;
                d.bakedGI = SampleSH(nw);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.smoothness = smoothness;
                s.metallic = metallic;
                s.occlusion = occlusion;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);

                half4 c = UniversalFragmentPBR(d, s);
                return half4(MixFog(c.rgb + emission, d.fogCoord), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_ShadowCull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ _DAD_NEAR
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            #ifdef _DAD_NEAR
                float3 positionWS : TEXCOORD0;
            #endif
            };

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(positionOS.xyz);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
            #else
                float3 l = _LightDirection;
            #endif
                // No vertex normal: depth bias only (see the header for why that is enough).
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, float3(0, 0, 0), l)));
            #ifdef _DAD_NEAR
                o.positionWS = p;
            #endif
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef _DAD_NEAR
                NearDither(i.positionWS, i.positionCS);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ _DAD_NEAR
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            #ifdef _DAD_NEAR
                float3 positionWS : TEXCOORD0;
            #endif
            };

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(positionOS.xyz);
                o.positionCS = TransformWorldToHClip(p);
            #ifdef _DAD_NEAR
                o.positionWS = p;
            #endif
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
            #ifdef _DAD_NEAR
                NearDither(i.positionWS, i.positionCS);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // World-space normals for SSAO (the renderer's SSAO reads depth and normals). The face
        // normal, not the normal-mapped one: SSAO works at the scale of the geometry.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ _DAD_NEAR
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef _DAD_NEAR
                NearDither(i.positionWS, i.positionCS);
            #endif
                return half4(FaceNormal(i.positionWS), 0);
            }
            ENDHLSL
        }
    }
}
