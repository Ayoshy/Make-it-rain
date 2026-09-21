// Aquarium water. Compiled to ps_3_0 by scripts/Build-Battlestation.ps1 (fxc) and
// loaded as an embedded shader by WaterEffect. The shader adds the liquid part —
// domain-warped caustics, surface refraction and the pointer wake — on top of the
// themed water brush given as implicitInput. Nothing here is a hand-drawn stroke.
sampler2D implicitInput : register(s0);

float Time : register(c0);          // seconds, advanced only while the dock is exposed
float2 Resolution : register(c1);   // dock size in pixels
float2 Pointer : register(c2);      // cursor in pixels, (-1,-1) outside
float Bass : register(c3);
float Mid : register(c4);
float Treble : register(c5);
float Energy : register(c6);
float3 Trail0 : register(c7);       // xy = last cursor positions, z = fresh 1 -> 0
float3 Trail1 : register(c8);
float3 Trail2 : register(c9);
float3 Trail3 : register(c10);
float3 Trail4 : register(c11);
float3 Tint : register(c12);        // caustic colour, from the running theme
float3 Light : register(c13);       // highlight colour, from the running theme
float Level : register(c14);        // water depth as a fraction of the dock (0.80)
float3 Deep : register(c15);        // water colour at the floor
float3 Body : register(c16);        // water colour just under the surface
float3 Air : register(c17);         // thin tint of the air above the surface
float Scene : register(c18);         // 1 = a rendered scene is the input, 0 = painted water

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    for (int octave = 0; octave < 3; octave++)
    {
        value += amplitude * noise(p);
        p *= 2.03;
        amplitude *= 0.5;
    }
    return value;
}

// The cursor drags the water with it: every recent position pushes a ring that
// keeps travelling and fading, which reads as a wake rather than a decal.
float2 Ring(float2 p, float3 trailPoint, float phase, float weight)
{
    float2 delta = p - trailPoint.xy + 0.001;
    float distance = length(delta);
    float strength = trailPoint.z * exp(-distance * 0.014) * weight;
    return normalize(delta) * sin(distance * 0.20 - Time * 7.0 - phase) * strength * 11.0;
}

float2 Wake(float2 p)
{
    return Ring(p, Trail0, 0.0, 1.0)
         + Ring(p, Trail1, 0.9, 0.80)
         + Ring(p, Trail2, 1.8, 0.62)
         + Ring(p, Trail3, 2.7, 0.46)
         + Ring(p, Trail4, 3.6, 0.33);
}

// Surface height in pixels, positive upwards: swells (bass), chop (treble), drift.
float Height(float x)
{
    return sin(x * 0.0125 + Time * (0.55 + Mid * 0.50)) * (1.5 + Bass * 9.0)
         + sin(x * 0.0550 - Time * 1.35) * (1.0 + Treble * 4.0);
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 source = tex2D(implicitInput, uv);
    float2 p = uv * Resolution;
    float2 q = p + Wake(p);

    // Free surface: slow swells from the bass, chop from the treble, a little noise.
    float surface = (Height(q.x) + (fbm(float2(q.x * 0.030, Time * 0.35)) - 0.5) * (2.5 + Bass * 7.0)) / Resolution.y;
    float surfaceUv = (1.0 - Level) - surface;
    float slope = (Height(q.x + 2.0) - Height(q.x - 2.0)) / 4.0;

    float below = uv.y - surfaceUv;                  // > 0 underwater, < 0 in the air
    float depth = saturate(below / max(0.02, 1.0 - (1.0 - Level)));
    float near = 1.0 - depth;                        // 1 at the surface, 0 at the floor

    // Caustics braided by domain warping, strongest just under the surface.
    float2 warp = float2(fbm(q * 0.021 + Time * 0.05), fbm(q * 0.021 - Time * 0.042 + 3.7));
    float filaments = fbm(q * 0.030 + warp * 1.3 + float2(Time * (0.09 + Bass * 0.12), -Time * 0.06));
    float caustic = pow(saturate(1.0 - abs(filaments - 0.5) * 3.6), 6.0);
    float causticFine = pow(saturate(1.0 - abs(fbm(q * 0.062 - Time * 0.11) - 0.5) * 3.4), 8.0);
    float shafts = pow(saturate(fbm(float2(q.x * 0.010, q.y * 0.0045) + float2(Time * 0.05, Time * 0.02))), 3.0);

    float3 water = lerp(Body, Deep, pow(depth, 2.0));
    water += Tint * caustic * (0.26 + Bass * 0.55 + Energy * 0.15) * (0.45 + 0.55 * near);
    water += Tint * causticFine * (0.09 + Treble * 0.18) * (0.35 + 0.65 * near);
    water += Light * shafts * (0.030 + Bass * 0.040) * (0.30 + 0.70 * near);
    // Everything bends near the surface: a bright rim and a shallow mirror band.
    water += Light * exp(-abs(below) * Resolution.y * 0.85) * (0.13 + Bass * 0.16);
    water += Light * pow(saturate(1.0 - abs(slope) * 0.35), 6.0) * exp(-abs(below) * Resolution.y * 0.22) * 0.07;

    // A rendered scene keeps its own colours, and answers the music the way the
    // painted water did: the swell and the chop move the image itself, the caustics
    // brighten under the bass and the surface light follows it. The cursor wake is
    // the same displacement, which is why it reads the same on both waters.
    float2 wobble = (Wake(p) * 0.55
        + float2(slope * (4.0 + Bass * 16.0 + Treble * 8.0), surface * (0.35 + Bass * 0.9)))
        / max(Resolution, float2(1.0, 1.0));
    // The surface moves most of it: the deeper the pixel, the calmer the image.
    wobble *= 0.40 + 0.60 * near;
    float3 rendered = tex2D(implicitInput, uv + wobble).rgb;
    float3 sceneWater = rendered * (0.93 + 0.07 * near + Energy * 0.06);
    sceneWater += Tint * caustic * (0.08 + Bass * 0.45) * (near * near);
    sceneWater += Tint * causticFine * (0.04 + Treble * 0.22) * (near * near);
    sceneWater += Light * shafts * (0.010 + Bass * 0.045) * (near * near);
    sceneWater += Light * exp(-abs(below) * Resolution.y * 0.85) * (0.10 + Bass * 0.22);
    water = lerp(water, sceneWater, Scene);

    // Air above the surface: the glass tint plus the halo the water throws up.
    // Bounded glow around the free surface: it must decay in both directions, or
    // the exponential diverges under water and wipes the volume out.
    float halo = exp(-abs(below) * Resolution.y * 0.60);
    float3 air = Air + Light * halo * (0.06 + Bass * 0.06);
    air = lerp(air, source.rgb, Scene);

    float wet = smoothstep(-0.0015, 0.0015, below);
    float3 colour = lerp(air, water, wet);
    float alpha = saturate(lerp(0.05 + halo * 0.10, 0.86, wet));
    alpha = lerp(alpha, 1.0, Scene);

    // A soft halo follows the cursor, so the water answers before it even moves.
    float underCursor = distance(p, Pointer);
    colour += Light * exp(-underCursor * 0.020) * 0.035 * step(0.0, Pointer.x);

    colour = saturate(colour);
    return float4(colour * alpha, alpha);   // WPF composites in premultiplied alpha
}
