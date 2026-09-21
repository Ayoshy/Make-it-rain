// Diorama Ocean - real-time water for one Battlestation dock.
//
// One analytic scene traced per pixel: the primary ray finds the nearest surface
// of a bounded water volume (top surface, the two visible cut faces, the stone
// base), the refracted ray reaches the submerged relief, and the colour is the
// single-scattering approximation of light crossing the water column.
//
// The reference shape is static. Interaction adds bounded wave packets sent by
// the widget; without a packet and without sound the shader returns the same
// image frame after frame - nothing scrolls, nothing drifts, no animation hides
// behind the still water.
//
// Shader model 3 constraints shape this file: every loop is a real dynamic loop,
// each helper is written once so it is inlined once, and every register declared
// below is used - an unused constant is dropped by the compiler and the Direct2D
// effect pipeline then rejects the shader.

sampler2D implicitInput : register(S0);
sampler2D bedTexture : register(S1);

float time : register(C0);
float2 resolution : register(C1);
float3 eye : register(C2);
float3 target : register(C3);
float fov : register(C4);
// Five wave packets: (x, z, amplitude) and the ring radius reached so far.
float3 ripple0 : register(C5);  float ring0 : register(C6);
float3 ripple1 : register(C7);  float ring1 : register(C8);
float3 ripple2 : register(C9);  float ring2 : register(C10);
float3 ripple3 : register(C11); float ring3 : register(C12);
float3 ripple4 : register(C13); float ring4 : register(C14);
float3 audio : register(C15);
float3 shallow : register(C16);
float3 deep : register(C17);
float3 sky : register(C18);
float3 params : register(C19);
float relief : register(C20);
float2 crop : register(C21);

static const float3 sunDirection = normalize(float3(.42f, .58f, .70f));
static const float trayHalf = 1.f;
static const float trayBottom = -.62f;
static const float stoneTop = -.40f;
static const float waterLine = 0.f;

// ---------------------------------------------------------------------------
// Water shape
// ---------------------------------------------------------------------------

// One circular wave packet: a crest at d = radius, bounded in amplitude, fading
// as it spreads. Radius and amplitude come from the widget, so a packet that has
// finished shrinking to zero stops changing the surface exactly.
void addPacket(float2 relative, float ring, float amplitude, inout float3 field)
{
    float d = length(relative);
    // A wider ring keeps the crest gently sloped: a wave, not a spike.
    float width = .075f + ring * .38f;
    float offset = (d - ring) / width;
    float offset2 = offset * offset;
    float profile = saturate(1.f - offset2 * 1.45f);
    float value = profile * profile * amplitude / (1.f + ring * 1.5f);
    // Gradient of the same packet, so crest normals are exact, not estimated.
    float slope = value * (-2.f * offset) / (width * max(d, .0015f));
    field.x += value;
    field.y += slope * relative.x;
    field.z += slope * relative.y;
}

// Still water: two crossed swells. Height and analytic gradient leave together,
// which keeps the normals free and the surface exactly repeatable.
float3 waveField(float2 p)
{
    float3 field = float3(0, 0, 0);
    // Six unrelated plane waves: swells, wind chop and a fine ripple band, each
    // with its own direction. Their gradients are exact, so the normals cost
    // nothing extra and the surface never repeats like a texture.
    float phase = p.x * 1.9f + p.y * 1.15f;
    field.x += .0165f * sin(phase);
    field.y += .0165f * cos(phase) * 1.9f;
    field.z += .0165f * cos(phase) * 1.15f;
    float phase2 = -p.x * 1.15f + p.y * 2.7f + .9f;
    field.x += .0118f * sin(phase2);
    field.y -= .0118f * cos(phase2) * 1.15f;
    field.z += .0118f * cos(phase2) * 2.7f;
    float phase3 = p.x * 2.6f - p.y * 1.4f + 2.1f;
    field.x += .0128f * cos(phase3);
    field.y -= .0128f * sin(phase3) * 2.6f;
    field.z += .0128f * sin(phase3) * 1.4f;
    float phase4 = (p.x + p.y) * 4.1f - .6f;
    field.x += .0040f * sin(phase4);
    field.y += .0040f * cos(phase4) * 4.1f;
    field.z += .0040f * cos(phase4) * 4.1f;
    // Two finer bands: the frozen detail of a real surface. They never move on
    // their own, they only give the light something to catch.
    float phase5 = p.x * 14.3f - p.y * 8.7f + .3f;
    field.x += .0046f * sin(phase5);
    field.y += .0046f * cos(phase5) * 14.3f;
    field.z -= .0046f * cos(phase5) * 8.7f;
    float phase6 = p.x * 9.1f + p.y * 22.9f + 1.3f;
    field.x += .0022f * sin(phase6);
    field.y += .0022f * cos(phase6) * 9.1f;
    field.z += .0022f * cos(phase6) * 22.9f;
    addPacket(p - ripple0.xy, ring0, ripple0.z, field);
    addPacket(p - ripple1.xy, ring1, ripple1.z, field);
    addPacket(p - ripple2.xy, ring2, ripple2.z, field);
    addPacket(p - ripple3.xy, ring3, ripple3.z, field);
    addPacket(p - ripple4.xy, ring4, ripple4.z, field);
    // Fine wind texture rides on the treble only, and only travels while it is
    // heard: silence leaves the surface exactly still.
    float2 grainDirection = float2(27.f, 14.f);
    float grainPhase = dot(p, grainDirection) - time * 2.4f;
    float fine = sin(grainPhase) * audio.z * .0030f;
    field.x += fine;
    field.y += cos(grainPhase) * grainDirection.x * audio.z * .0030f;
    field.z += cos(grainPhase) * grainDirection.y * audio.z * .0030f;
    // Bass feeds a broader swell that travels across the tray: energy in, motion
    // out, and both stop together when the music does.
    float swellPhase = p.x * 1.1f + p.y * .7f - time * 1.5f;
    float swell = audio.x * .0105f;
    field.x += swell * sin(swellPhase);
    field.y += swell * cos(swellPhase) * 1.1f;
    field.z += swell * cos(swellPhase) * .7f;
    field *= params.y;
    return field;
}

// ---------------------------------------------------------------------------
// Submerged relief
// ---------------------------------------------------------------------------

// Tray floor plus a few rocks. The baked bed from the Blender asset replaces the
// procedural relief as soon as it is present; both describe the same thing, a
// height under the water, so nothing else in the shader changes.
float bedHeight(float2 p)
{
    float baked = tex2Dlod(bedTexture, float4(p * .5f + .5f, 0, 0)).a * 2.f - 1.f;
    // Sand dunes under the water: incommensurate frequencies and one domain warp
    // keep the relief from reading as a grid.
    float warp = sin(p.x * 1.7f + p.y * 1.1f);
    float procedural = stoneTop + .014f * sin(p.x * 2.3f + warp) + .011f * sin(p.y * 3.1f - warp * 1.4f);
    procedural += .008f * sin(p.x * 6.7f + p.y * 5.1f);
    procedural += .052f * exp(-dot(p + float2(.60f, -.28f), p + float2(.60f, -.28f)) * 9.f);
    procedural += .074f * exp(-dot(p - float2(.52f, .38f), p - float2(.52f, .38f)) * 8.f);
    procedural += .036f * exp(-dot(p - float2(-.24f, .62f), p - float2(-.24f, .62f)) * 16.f);
    return lerp(procedural, stoneTop + baked * .30f, relief);
}

float3 bedColor(float2 p, float height)
{
    float4 baked = tex2Dlod(bedTexture, float4(p * .5f + .5f, 0, 0));
    float sand = smoothstep(stoneTop + .01f, stoneTop + .12f, height);
    // Broad, low-frequency variation only: a regular pattern would read as a grid
    // through the water instead of as ground.
    float patch = .5f + .5f * sin(p.x * 3.1f + 1.2f) * sin(p.y * 2.6f - .4f);
    float3 rock = float3(.29f, .29f, .31f);
    float3 ochre = float3(.66f, .55f, .43f);
    return lerp(lerp(rock, ochre, sand), baked.rgb, relief) * (.90f + .10f * patch);
}

// Beer-Lambert across the water column. "deep" holds what the water absorbs:
// red disappears first, so a thin layer stays clear and a thick one turns
// turquoise then dark green, the way real water does.
float3 transmit(float depth)
{
    return exp(-deep * params.x * depth * 11.f);
}

float3 skyRadiance(float3 direction)
{
    float height = saturate(direction.y * .5f + .5f);
    float3 radiance = lerp(sky * .84f + .16f, sky * .58f, height * height);
    float sun = saturate(dot(direction, sunDirection));
    sun = sun * sun; sun = sun * sun; sun = sun * sun; sun = sun * sun;
    return radiance + sun * float3(1.f, .97f, .90f) * 4.f;
}

// ---------------------------------------------------------------------------
// The bounded volume: a box whose water is a slab between the surface and the
// relief. Only the three faces the camera can see are tested.
// ---------------------------------------------------------------------------

struct Volume
{
    float distance;
    float3 normal;
    float3 hit;
};

void faces(float3 origin, float3 direction, inout Volume volume)
{
    // The camera sits on the +x/+z side, so the two visible cut faces are the
    // ones the rays enter through.
    if (direction.x < -.0001f)
    {
        float t = (trayHalf - origin.x) / direction.x;
        float3 p = origin + direction * t;
        if (t > 0.f && t < volume.distance && abs(p.z) <= trayHalf && p.y >= trayBottom && p.y <= waterLine)
        {
            volume.distance = t;
            volume.normal = float3(1, 0, 0);
        }
    }
    if (direction.z < -.0001f)
    {
        float t = (trayHalf - origin.z) / direction.z;
        float3 p = origin + direction * t;
        if (t > 0.f && t < volume.distance && abs(p.x) <= trayHalf && p.y >= trayBottom && p.y <= waterLine)
        {
            volume.distance = t;
            volume.normal = float3(0, 0, 1);
        }
    }
    volume.hit = origin + direction * volume.distance;
}

// The still surface is an implicit height field. The short vertical march
// converges on the surface itself, and the field gradient leaves with the hit.
bool traceWater(float3 origin, float3 direction, out float3 hit, out float3 normal, out float height)
{
    hit = float3(0, 0, 0);
    normal = float3(0, 1, 0);
    height = 0.f;
    if (direction.y >= -.0006f) return false;
    float t = (waterLine - origin.y) / direction.y;
    if (t <= 0.f) return false;
    float3 field = float3(0, 0, 0);
    [loop] for (int step = 0; step < 6; step++)
    {
        float3 p = origin + direction * t;
        if (any(abs(p.xz) > trayHalf)) return false;
        field = waveField(p.xz);
        t += (field.x - p.y) / direction.y;
    }
    hit = origin + direction * t;
    if (any(abs(hit.xz) > trayHalf) || hit.y > .05f) return false;
    height = field.x;
    // The slope is bounded: a crest can lean, never fold over itself, which keeps
    // the refraction (and the absorption that follows it) inside the volume.
    normal = normalize(float3(clamp(-field.y, -1.15f, 1.15f), 1.f, clamp(-field.z, -1.15f, 1.15f)));
    return true;
}

// ---------------------------------------------------------------------------

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float aspect = max(.25f, resolution.x / max(1.f, resolution.y));
    // "crop" is the projected centre of the block, measured in the same units as
    // the ray coordinates: adding it recentres the framing at every dock size.
    float2 ndc = float2(uv.x * 2.f - 1.f, 1.f - uv.y * 2.f) + crop;
    float3 forward = normalize(target - eye);
    float3 right = normalize(cross(forward, float3(0, 1, 0)));
    float3 up = cross(right, forward);
    float3 direction = normalize(forward + right * ndc.x * fov * aspect + up * ndc.y * fov);

    Volume volume;
    volume.distance = 1e9f;
    volume.normal = float3(0, 1, 0);
    volume.hit = float3(0, 0, 0);
    faces(eye, direction, volume);
    bool faceHit = volume.distance < 1e8f;
    float3 waterHit;
    float3 waterNormal;
    float waterHeight;
    bool surfaceHit = traceWater(eye, direction, waterHit, waterNormal, waterHeight) && (!faceHit || distance(eye, waterHit) < volume.distance);
    // Nothing of the block crosses this pixel: the dock shows its own glass.
    if (!faceHit && !surfaceHit) return float4(0, 0, 0, 0);

    float3 normal = volume.normal;
    float3 bed = volume.hit;
    float3 body = float3(0, 0, 0);
    float thickness = 0.f;
    float reflectivity = .05f;
    float focus = 1.f;
    float foam = 0.f;
    float stone = 0.f;
    float alpha = 1.f;
    float isSurface = surfaceHit ? 1.f : 0.f;

    if (surfaceHit)
    {
        normal = waterNormal;
        float3 inside = refract(direction, normal, .7502f);
        if (dot(inside, inside) < .0001f) inside = direction;
        float3 current = waterHit;
        [loop] for (int step = 0; step < 9; step++)
        {
            float floorHeight = bedHeight(current.xz);
            if (current.y <= floorHeight || any(abs(current.xz) > trayHalf)) break;
            current += inside * max(.05f, (floorHeight - current.y) / min(inside.y, -.08f) * .62f);
        }
        current.xz = clamp(current.xz, -trayHalf, trayHalf);
        bed = current;
        thickness = clamp(length(current - waterHit), .02f, .62f);
        body = shallow * .40f;
        reflectivity = .02f + .98f * pow(1.f - saturate(dot(normal, -direction)), 5.f);
        // Caustics: the surface bends the light onto the bottom. The pattern is
        // driven by the surface height, so it warps with the water and stays put
        // when the water is still.
        float warp = waterHeight * 52.f;
        float web = .5f + .5f * sin(current.x * 9.3f + warp) * sin(current.z * 8.1f - warp * .8f + current.x * 2.3f);
        focus = .84f + .40f * web * web;
        // Foam marks the crests and the shallow relief, the way the reference
        // breaks white along the top of a wave.
        float crest = saturate((waterHeight - .010f) * 52.f);
        foam = saturate(saturate(1.f - (waterHit.y - current.y) * 6.f) * .30f
             + crest * .85f + saturate((1.f - waterNormal.y) * 9.f) * .25f + audio.x * .18f) * params.z * .55f;
        alpha = saturate(.96f + reflectivity * .15f);
    }
    else
    {
        // The cut: the column of water seen through the side of the block, and the
        // slab of stone it rests on.
        float bottom = bedHeight(volume.hit.xz);
        bed = float3(volume.hit.x, bottom, volume.hit.z);
        // The cut shows the column as it is: darker as it goes down, so the slab
        // reads as a volume and not as a painted band.
        thickness = max(.02f, waterLine - volume.hit.y);
        body = shallow * 1.05f;
        reflectivity = .07f + pow(1.f - saturate(abs(dot(direction, normal))), 4.f) * .28f;
        // A bright meniscus marks where the cut meets the air.
        foam = smoothstep(-.025f, .0f, volume.hit.y) * .16f;
        stone = step(volume.hit.y, bottom);
        thickness = lerp(thickness, 0.f, stone);
        alpha = lerp(.96f, 1.f, stone);
    }

    float3 tint = transmit(thickness);
    // Scattering grows with the optical depth while the bottom loses its own
    // colours: thin water stays transparent, thick water turns into its own
    // turquoise body before it goes dark.
    float opacity = 1.f - exp(-thickness * params.x * 4.8f);
    float3 color = bedColor(bed.xz, bed.y) * tint * focus + body * opacity;
    color += skyRadiance(reflect(direction, normal)) * reflectivity;
    // A sharp glint on the crests: the frozen detail is what makes the surface
    // read as liquid rather than as a smooth block.
    float3 half3 = normalize(sunDirection - direction);
    color += pow(saturate(dot(normal, half3)), 22.f) * float3(1.f, .99f, .96f) * (1.15f * isSurface);
    color = lerp(color, float3(.90f, .96f, .97f), foam);
    // The tray itself: pale stone over a warmer base, the block the reference
    // cuts and leaves on the desk.
    float3 tray = lerp(float3(.91f, .88f, .82f), float3(.80f, .55f, .37f), saturate((waterLine - volume.hit.y) * 2.2f));
    tray *= .93f + .07f * saturate(dot(normal, sunDirection));
    // A thin dark seam under the waterline separates the block from its base.
    tray *= lerp(.82f, 1.f, saturate((waterLine - volume.hit.y) * 6.f));
    color = lerp(color, tray, stone);

    // The block sits over whatever the dock painted under its water layer, so the
    // layer keeps the usual premultiplied compositing instead of punching a hole.
    float4 under = tex2Dlod(implicitInput, float4(uv, 0, 0));
    float3 premultiplied = color * alpha + under.rgb * (1.f - alpha);
    return float4(premultiplied, alpha + under.a * (1.f - alpha));
}
