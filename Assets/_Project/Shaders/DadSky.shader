// The sky (D-03): a skybox driven by DayCycle's globals, so it always agrees with the light.
// - Gradient from the horizon to the zenith; the horizon is warmer towards a low sun.
// - Forward scattering round the sun in the haze (Henyey-Greenstein), and the sun's disc.
// - The moon's disc, shaded as a sphere lit from the sun's direction, so its phase and bright
//   limb are right for the date; earthshine on the dark part.
// - One cloud layer 2.5 km up, from the periodic macro noise, drifting with the clock.
// - Stars on a hashed grid in equatorial coordinates, turned by the sidereal time; few, for
//   Dhaka's sky glow.
// The sun and moon discs are drawn larger than life (1° and 1.5° across) to read on screen.
Shader "Dad/Sky"
{
    Properties
    {
        [NoScaleOffset] _CloudNoise("Cloud noise (periodic)", 2D) = "grey" {}
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudNoise);
            SAMPLER(sampler_CloudNoise);
            float4 _DadSunDir, _DadMoonDir, _DadSunLight, _DadMoonLight;
            float4 _DadZenith, _DadHorizon, _DadHorizonAway;
            float4 _DadClouds; // x cover, yz drift (km), w star visibility
            float4 _DadCloudLit, _DadCloudShade;
            float4x4 _DadStarRotation;
            float _DadNight;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.direction = i.positionOS.xyz;
                return o;
            }

            uint LowBias(uint x)
            {
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return x;
            }

            float ToUnit(uint h) { return (h >> 8) * (1.0 / 16777216.0); }

            float HenyeyGreenstein(float mu, float g)
            {
                float g2 = g * g;
                return (1 - g2) / (4 * PI * pow(abs(1 + g2 - 2 * g * mu), 1.5));
            }

            // Colour of a light at full strength: hue kept, the brightest channel set to 1.
            float3 Hue(float3 c) { return c / max(max(c.r, max(c.g, c.b)), 1e-5); }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 s = _DadSunDir.xyz, m = _DadMoonDir.xyz;
                float up = d.y;

                // Gradient. Towards a low sun the horizon is its warm colour; away from it, cooler.
                float2 flatD = normalize(d.xz + 1e-5), flatS = normalize(s.xz + 1e-5);
                float towards = saturate(dot(flatD, flatS) * 0.5 + 0.5);
                float3 horizon = lerp(_DadHorizonAway.rgb, _DadHorizon.rgb, towards * towards);
                float3 sky = lerp(horizon, _DadZenith.rgb, pow(saturate(up), 0.45));
                sky = lerp(sky, horizon * 0.55, saturate(-up * 6)); // below the horizon: haze over the ground

                // Haze glow round the sun and, fainter, the moon.
                float mu = dot(d, s), muMoon = dot(d, m);
                float aboveSun = saturate(s.y * 8 + 0.5);
                sky += _DadSunLight.rgb * (HenyeyGreenstein(mu, 0.78) * 0.09 + HenyeyGreenstein(mu, 0.3) * 0.05) * aboveSun;
                sky += _DadMoonLight.rgb * HenyeyGreenstein(muMoon, 0.85) * 0.3;

                // Clouds.
                float cover = _DadClouds.x, density = 0;
                float3 cloud = 0;
                if (up > 0.0)
                {
                    float2 p = d.xz / max(up, 0.02) * 2.5 + _DadClouds.yz; // km on a layer 2.5 km up
                    float n = SAMPLE_TEXTURE2D(_CloudNoise, sampler_CloudNoise, p / 12.0).r * 0.65
                            + SAMPLE_TEXTURE2D(_CloudNoise, sampler_CloudNoise, p / 3.0 + 0.5).g * 0.35;
                    density = saturate((n - (1 - cover) * 0.85 - 0.12) * 3.5) * smoothstep(0.0, 0.12, up);
                    float lit = saturate(0.55 + 0.45 * mu) * (1 - 0.35 * density);
                    cloud = lerp(_DadCloudShade.rgb, _DadCloudLit.rgb, lit);
                    cloud += _DadSunLight.rgb * HenyeyGreenstein(mu, 0.6) * 0.08 * (1 - density); // bright edges towards the sun
                }

                // Stars, behind the clouds and the haze.
                float stars = 0;
                if (_DadClouds.w > 0 && up > 0)
                {
                    float3 c = mul((float3x3)_DadStarRotation, d);
                    float3 cell = floor(c * 90);
                    uint h = LowBias(asuint(cell.x + 1000) ^ LowBias(asuint(cell.y + 2000) ^ LowBias(asuint(cell.z + 3000))));
                    float3 star = normalize(cell + float3(ToUnit(h), ToUnit(LowBias(h + 1)), ToUnit(LowBias(h + 2))));
                    float magnitude = ToUnit(LowBias(h + 3));
                    float size = max(length(fwidth(c)), 1e-5) * 1.2;
                    float glint = 1 - smoothstep(0, size, length(c - star));
                    // One cell in 200 holds a star bright enough to show through the glow.
                    float bright = saturate((magnitude - 0.995) / 0.005);
                    stars = glint * bright * bright * 1.6 * _DadClouds.w * smoothstep(0.0, 0.25, up);
                }
                sky += stars * (1 - density);

                // The moon: a sphere seen from the earth, lit from the sun's direction.
                const float moonRadius = 0.0131; // 0.75 degrees
                float3 t1 = normalize(cross(m, abs(m.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 t2 = cross(t1, m);
                float2 q = float2(dot(d, t1), dot(d, t2)) / moonRadius;
                float r2 = dot(q, q);
                if (muMoon > 0 && r2 < 1)
                {
                    float z = sqrt(1 - r2);
                    float3 n = q.x * t1 + q.y * t2 - z * m;
                    float maria = SAMPLE_TEXTURE2D(_CloudNoise, sampler_CloudNoise, q * 0.18 + 0.3).b;
                    float albedo = 0.72 + 0.4 * (maria - 0.5);
                    float light = saturate(dot(n, s) * 4) + 0.03; // earthshine
                    float3 moon = albedo * light * lerp(float3(1, 0.93, 0.8), float3(0.92, 0.95, 1), _DadNight);
                    float edge = 1 - smoothstep(0.92, 1.0, r2);
                    float show = edge * smoothstep(-0.01, 0.01, m.y) * (1 - density * 0.9) * lerp(0.35, 1, _DadNight);
                    sky = lerp(sky, max(sky, moon), show);
                }

                // The sun's disc.
                float sunEdge = smoothstep(0.999955, 0.999965, mu); // 0.5 degree radius
                float sunStrength = max(_DadSunLight.r, max(_DadSunLight.g, _DadSunLight.b));
                sky += Hue(_DadSunLight.rgb) * saturate(sunStrength * 40) * sunEdge * smoothstep(-0.01, 0.005, up) * (1 - density);

                sky = lerp(sky, cloud, density * 0.95);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
