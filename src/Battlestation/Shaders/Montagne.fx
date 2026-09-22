// Montagne - perpetual snow over a snowy peak, for one Battlestation dock.
//
// One pixel shader (ps_3_0, compiled by scripts/Build-Battlestation.ps1 and
// embedded as a WPF resource) paints the whole slice: a cold dusk sky with a
// low moon and a sparse starfield, a distant hazed ridge, then the main peak
// whose face mixes sunlit snow, blue shadow and exposed rock. Three parallax
// layers of discrete flakes fall forever, drift with the wind, and give way
// under the cursor. The music dock's published spectrum drives the weather -
// bass raises the wind and the fall, treble makes the snow sparkle - and a
// beat sends one bounded gust through the flakes. Masked, nothing runs.
//
// The geometry numbers here mirror src/Battlestation/MontagneCamera.cs exactly,
// so the fallback drawing and the shader share one ridge.

sampler2D implicitInput : register(S0);

float time : register(C0);
float2 resolution : register(C1);
float2 pointer : register(C2);   // cursor in pixels, far away when outside
float2 stir : register(C3);      // decaying mouse stir, in pixels
float3 audio : register(C4);     // bass, mid, treble, each already smoothed

// The slice: the horizontal extent follows the dock aspect, the vertical range
// is fixed, so a resize widens the view instead of stretching the mountain.
static const float yTop = 1.0f;
static const float yBottom = -.70f;
static const float widthPerAspect = 1.05f;

// A cold dusk palette, deliberately independent of the desktop theme.
static const float3 zenithC  = float3(.050f, .070f, .155f);
static const float3 horizonC = float3(.330f, .355f, .520f);
static const float3 hazeC    = float3(.415f, .465f, .615f);
static const float3 moonC    = float3(.925f, .945f, .995f);
static const float3 rockDark = float3(.185f, .205f, .265f);
static const float3 rockLit  = float3(.360f, .395f, .480f);
static const float3 snowDark = float3(.585f, .665f, .855f);
static const float3 snowLit  = float3(.930f, .950f, 1.00f);

float hash21(float2 p)
{
    p = frac(p * float2(123.34f, 456.21f));
    p += dot(p, p + 45.32f);
    return frac(p.x * p.y);
}

// A cheap fractal sum of incommensurate plane waves, warped once so it reads as
// drifted snow. Unlike an interpolated value noise it has no cell lattice: the
// ps_3_0 interpolator leaves thin horizontal seams on lattice-based noise, and
// a smooth sum of sines never does.
float fbm(float2 p)
{
    float2 q = p + float2(sin(p.y * 1.31f + .4f), cos(p.x * 1.17f + 1.1f)) * .55f;
    float n = .50f * (sin(dot(q, float2(1.00f, .37f)) * 1.70f) * .5f + .5f);
    n += .25f * (sin(dot(q, float2(-.62f, .95f)) * 2.90f + 1.7f) * .5f + .5f);
    n += .125f * (sin(dot(q, float2(.83f, -.71f)) * 5.30f + .9f) * .5f + .5f);
    n += .0625f * (sin(dot(q, float2(.42f, .91f)) * 9.70f + 2.3f) * .5f + .5f);
    return n * .9375f;
}

// The main ridge: one dominant peak, a lower shoulder to its left, a slow base
// sine and three fine incommensurate sines of relief. The summit is a smooth
// bell, so the face never shows the seam a cusp would leave. Mirrored in C#.
// One asymmetric summit: different widths and powers on each face, so the ridge
// never reads as a folded paper triangle. Height and its analytic slope leave
// together; the slope picks a face on the crest itself so no column is lit
// twice, which is what would draw a seam along the ridge line.
float peakHeight(float x, float center, float widthL, float widthR, float height, float powerL, float powerR)
{
    float d = x - center;
    float w = d < 0.f ? widthL : widthR;
    float p = d < 0.f ? powerL : powerR;
    return height * pow(saturate(1.f - abs(d) / w), p);
}

float peakSlope(float x, float center, float widthL, float widthR, float height, float powerL, float powerR)
{
    float d = x - center;
    float w = d < 0.f ? widthL : widthR;
    float p = d < 0.f ? powerL : powerR;
    float t = saturate(1.f - abs(d) / w);
    if (t <= .0001f) return 0.f;
    float side = d >= 0.f ? 1.f : -1.f;
    return -side * height * p / w * pow(t, p - 1.f);
}

float ridgeHeight(float x)
{
    float base = -.52f + .06f * sin(x * 1.7f + .6f);
    float rough = .030f * sin(x * 3.1f + 1.2f)
                + .016f * sin(x * 6.7f + .5f)
                + .008f * sin(x * 13.3f + 2.2f)
                + .004f * sin(x * 27.7f + .9f);
    return base
        + peakHeight(x, .14f, .86f, 1.12f, .94f, 1.30f, 1.45f)
        + peakHeight(x, 1.42f, .50f, .40f, .30f, 1.40f, 1.40f)
        + peakHeight(x, .86f, .28f, .26f, .16f, 1.50f, 1.30f)
        + peakHeight(x, -1.38f, .68f, .56f, .50f, 1.45f, 1.30f)
        + rough;
}

float ridgeSlope(float x)
{
    float base = .06f * 1.7f * cos(x * 1.7f + .6f);
    float rough = .030f * 3.1f * cos(x * 3.1f + 1.2f)
                + .016f * 6.7f * cos(x * 6.7f + .5f)
                + .008f * 13.3f * cos(x * 13.3f + 2.2f)
                + .004f * 27.7f * cos(x * 27.7f + .9f);
    return base
        + peakSlope(x, .14f, .86f, 1.12f, .94f, 1.30f, 1.45f)
        + peakSlope(x, 1.42f, .50f, .40f, .30f, 1.40f, 1.40f)
        + peakSlope(x, .86f, .28f, .26f, .16f, 1.50f, 1.30f)
        + peakSlope(x, -1.38f, .68f, .56f, .50f, 1.45f, 1.30f)
        + rough;
}

// The distant range behind: lower contrast, seen through haze.
float farRidgeHeight(float x)
{
    float ax = x + 1.30f;
    float a = exp(-ax * ax / (1.15f * 1.15f));
    float bx = x - 1.45f;
    float b = exp(-bx * bx / (.95f * .95f));
    return -.10f + .38f * a + .18f * b + .02f * sin(x * 2.3f + .4f);
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float aspect = max(.25f, resolution.x / max(1.f, resolution.y));
    float halfWidth = aspect * widthPerAspect;
    float x = lerp(-halfWidth, halfWidth, uv.x);
    float y = lerp(yTop, yBottom, uv.y);
    float2 pixel = uv * resolution;

    float ridge = ridgeHeight(x);
    float farRidge = farRidgeHeight(x);

    // Sky: a cold gradient, a low moon and a sparse, slowly twinkling field of
    // stars that only shows above the ridge.
    float3 sky = lerp(horizonC, zenithC, pow(saturate((y + .25f) / 1.25f), .9f));
    float2 moonP = float2(halfWidth * .52f, .58f);
    float2 moonD = float2(x, y) - moonP;
    float moonDist = length(moonD);
    float moonDisc = smoothstep(.072f, .056f, moonDist);
    float moonGlow = exp(-moonDist * moonDist * 2.4f);
    sky += moonC * (moonDisc * .95f + moonGlow * .30f);

    float2 starCell = floor(float2(x, y) * 46.f + 3.1f);
    float starSeed = hash21(starCell);
    float starSeed2 = frac(starSeed * 57.31f + .137f);
    float2 starPos = (starCell + float2(starSeed, starSeed2)) / 46.f;
    float starDist = length(float2(x, y) - starPos);
    float star = step(.9965f, starSeed) * smoothstep(.0068f, .0016f, starDist) * saturate((y - ridge) * 6.f);
    star *= .45f + .55f * sin(time * 1.7f + starSeed * 40.f);
    sky += moonC * star * (1.f - moonGlow);

    float3 color = sky;
    float alpha = .62f;

    // The distant range, faded by the atmosphere.
    if (y < farRidge)
    {
        float h = farRidge - y;
        color = lerp(hazeC, moonC * .88f, saturate(h * 1.6f) * .55f);
        alpha = .80f;
    }

    // The main peak: snow clings to the gentler slopes and the higher ground,
    // the cliffs keep their rock, and one slow noise breaks the boundary.
    float snowMask = 0.f;
    if (y < ridge)
    {
        // The ridge wanders a little with height, so the crease between the lit
        // and shadowed faces is never a ruler-straight vertical line.
        float wander = (fbm(float2(y * 1.35f + 4.1f, x * .30f + 7.3f)) - .5f) * .07f;
        float slope = ridgeSlope(x - wander);
        float3 normal = normalize(float3(-slope, 1.f, 0.f));
        float3 lightDir = normalize(float3(.42f, .70f, 0.f));
        float lit = saturate(dot(normal, lightDir));

        float patch = fbm(float2(x * 1.7f, y * 1.1f));
        // Gullies: fine vertical streaks on the faces, the way wind and old
        // avalanches groove a snow slope. They keep each face from reading as
        // one flat panel.
        float gully = fbm(float2(x * 5.2f, y * .75f));
        float steep = saturate(1.f - abs(slope) * .62f);
        float altitude = smoothstep(-.42f, .08f, y);
        snowMask = saturate(steep * .52f + altitude * .30f + (patch - .5f) * .80f + (gully - .5f) * .55f);
        snowMask = smoothstep(.30f, .70f, snowMask);

        // Snow keeps a high ambient floor: a crease between two faces reads as a
        // fold in the surface, never as a seam between two flat panels.
        float litGully = saturate(lit + (gully - .5f) * .22f + (patch - .5f) * .10f);
        float3 snow = lerp(snowDark, snowLit, .30f + .70f * pow(litGully, .8f));
        float3 rock = lerp(rockDark, rockLit, litGully);
        rock *= .90f + .20f * fbm(float2(x * 6.3f, y * 6.3f));
        color = lerp(rock, snow, snowMask);

        // The rim: a thin, bright line hugs the silhouette, the way a snow edge
        // catches the low moon. It also keeps the outline crisp at any size.
        float rim = exp(-(ridge - y) * 55.f) * saturate(lit * 1.4f + .35f);
        color = lerp(color, snowLit, rim * .55f * snowMask);

        // The valley: the lower the ground, the more the haze takes over, so the
        // base melts into the night instead of ending on a painted edge.
        float base = saturate((ridge - y) * .70f);
        color = lerp(color, hazeC, base * .62f);
        alpha = 1.f;

        // A few facets catch the low light with the treble.
        float2 glintCell = floor(float2(x, y) * 70.f + 9.7f);
        float glintSeed = hash21(glintCell);
        float glintSeed2 = frac(glintSeed * 57.31f + .137f);
        float2 glintPos = (glintCell + float2(glintSeed, glintSeed2)) / 70.f;
        float glintDist = length(float2(x, y) - glintPos);
        float glint = step(.9935f, glintSeed) * smoothstep(.0026f, .0006f, glintDist);
        color += moonC * glint * (.35f + .65f * audio.z) * snowMask;
    }

    // Snowfall: three parallax layers of discrete flakes. Each cell of the grid
    // carries one flake; positions cycle through the whole block so the fall is
    // endless, the wind drifts the field, and the cursor pushes the nearer
    // flakes aside.
    float windBase = 16.f + audio.x * 95.f;
    float stirMag = length(stir);
    float flakeAlpha = 0.f;
    [unroll]
    for (int layer = 0; layer < 3; layer++)
    {
        float size = layer == 0 ? 46.f : (layer == 1 ? 32.f : 21.f);
        float radius = layer == 0 ? 2.4f : (layer == 1 ? 1.7f : 1.1f);
        float speed = layer == 0 ? 58.f : (layer == 1 ? 38.f : 24.f);
        float bright = layer == 0 ? 1.f : (layer == 1 ? .72f : .46f);
        float parallax = layer == 0 ? 1.f : (layer == 1 ? .62f : .38f);
        float wind = windBase * parallax;
        float sway = (5.f + audio.x * 22.f) * parallax;

        float2 cell = floor(pixel / size);
        [unroll]
        for (int j = 0; j < 2; j++)
        {
            [unroll]
            for (int i = 0; i < 2; i++)
            {
                float2 id = cell + float2(i, j);
                float r = hash21(id + layer * 23.7f);
                float r2 = frac(r * 57.31f + .137f);

                // Base position inside the cell, then cycle across the block.
                float2 base = (id + float2(r, r2)) * size;
                float2 flake;
                flake.x = frac((base.x + wind * time) / resolution.x) * resolution.x;
                flake.y = frac((base.y - speed * (1.f + audio.x * .55f) * time) / resolution.y) * resolution.y;
                flake.x += sin(time * .8f + r * 6.283f) * sway;

                // The cursor: a gentle push away from it, and the shared stir.
                float2 toCursor = flake - pointer;
                float cursorDist = length(toCursor);
                flake += stir * parallax;
                flake += toCursor / max(cursorDist, 1.f) * exp(-cursorDist * .0143f) * parallax * 16.f;

                float keep = step(.38f, r);
                float dist = length(pixel - flake);
                float dot_ = smoothstep(radius * (0.75f + .5f * r2), radius * .25f, dist) * bright * keep;
                float twinkle = .80f + .20f * sin(time * 2.6f + r * 40.f + layer * 3.f);
                flakeAlpha = max(flakeAlpha, dot_ * twinkle);
            }
        }
    }
    flakeAlpha = saturate(flakeAlpha);
    color = lerp(color, float3(1.f, 1.f, 1.f), flakeAlpha);
    alpha = max(alpha, flakeAlpha);

    // The block is a rounded window inside the native glass frame: everything
    // outside the rounded inset stays transparent, and the layer composites
    // premultiplied over what is under.
    float2 halfSize = resolution * .5f;
    float inset = 8.f;
    float radiusM = 17.f;
    float2 q = abs(uv * resolution - halfSize) - (halfSize - inset - radiusM);
    float d = length(max(q, 0.f)) + min(max(q.x, q.y), 0.f) - radiusM;
    float mask = 1.f - saturate(d / 1.25f);
    alpha *= mask;
    float4 under = tex2Dlod(implicitInput, float4(uv, 0, 0));
    return float4(color * alpha + under.rgb * (1.f - alpha), alpha + under.a * (1.f - alpha));
}
