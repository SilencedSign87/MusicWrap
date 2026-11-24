
sampler2D Input : register(s0);

// x = time 0..1
float4 Time : register(c0);

// x = distortion strength
float4 Strength : register(c1);

float4 OverlayColor : register(c2);

// x = Tint opacity
// y = Saturation
// z = Contrast
// w = Noise intensity
float4 Parameters : register(c3);

// x = Tint opacity
float4 TintLuminosity : register(c4);

// random seeds
float4 Seeds : register(c5);

static const float TAU = 6.28318530718;

float2 flowField(float2 p, float t)
{
    float2 f;

    f.x = sin(
        p.y * 4.0 + t +
        sin(p.x * 2.5 + t * 2.0)
    );

    f.y = cos(
        p.x * 4.0 - t +
        cos(p.y * 2.5 - t * 2.0)
    );

    return f;
}

float2 globalFlow(float2 uv, float t)
{
    float2 p = uv - 0.5;

    float2 warp;

    warp.x =
        sin(p.y * 5.0 + t) +
        sin(p.x * 3.0 - t * 2.0) * 0.5 +
        cos((p.x + p.y) * 3.0 + t) * 0.4;

    warp.y =
        cos(p.x * 4.0 - t) +
        cos(p.y * 3.0 + t * 2.0) * 0.5 +
        sin((p.x - p.y) * 2.5 - t * 3.0) * 0.4;

    float2 warped = p + warp * 0.075;

    float2 f1 = flowField(warped, t);

    float2 f2 = flowField(
        warped * 1.35 + float2(1.7, 3.2),
        t + 1.2
    );

    return
        f1 * 0.095 +
        f2 * 0.035 +
        warp * 0.025;
}

float2 rotatingDisc(float2 uv, float t, float saturation, float3 seed)
{
    float2 center = float2(-0.55, 0.38) + (seed.xy - 0.5) * 0.30;

    float2 q = uv - center;
    q.y *= 0.72;

    float phase = seed.z * TAU;
    float s = sin(t + phase);
    float c = cos(t + phase);

    float2 rotated;

    rotated.x = q.x * c - q.y * s;
    rotated.y = q.x * s + q.y * c;

    rotated.y /= 0.72;

    float2 rotatedUV = center + rotated;

    float saturationWeight = saturate(saturation * 1.8);

    float radialWeight = exp(
        -dot(q, q) * 0.28
    );

    float weight =
        saturationWeight *
        radialWeight *
        0.24;

    return lerp(
        uv,
        rotatedUV,
        weight
    ) - uv;
}

float2 blobFlow(
    float2 uv,
    float2 center,
    float radius,
    float t,
    float phase
)
{
    float2 d = uv - center;
    float distanceSq = dot(d, d);

    float influence = exp(
        -distanceSq / (radius * radius * 1.8)
    );

    float radialMotion = sin(t * 2.0 + phase);
    float rotationalMotion = cos(t * 3.0 + phase);

    float2 radial = d * radialMotion * 0.45;
    float2 tangent = float2(-d.y, d.x) * rotationalMotion * 1.0;

    return (radial + tangent) * influence * 0.20;
}

float2 localBlobs(float2 uv, float t, float3 seed)
{
    float3 sp = seed * TAU;
    
    float2 c1 = float2(
        0.50 + 0.65 * sin(t + 0.7 + sp.x),
        0.50 + 0.58 * cos(t * 2.0 + 1.3 + sp.y)
    );

    float2 c2 = float2(
        0.50 + 0.58 * cos(t * 2.0 + 2.8 + sp.y),
        0.50 + 0.65 * sin(t * 3.0 + 0.4 + sp.z)
    );

    float2 c3 = float2(
        0.50 + 0.65 * sin(t * 3.0 + 4.1 + sp.z),
        0.50 + 0.58 * cos(t + 3.2 + sp.x)
    );

    float r1 = 0.42 + 0.09 * sin(t * 2.0 + 0.3 + sp.x);
    float r2 = 0.48 + 0.10 * cos(t * 3.0 + 1.7 + sp.y);
    float r3 = 0.40 + 0.09 * sin(t * 2.0 + 3.1 + sp.z);

    float2 result = 0;

    result += blobFlow(uv, c1, r1, t, 0.7);
    result += blobFlow(uv, c2, r2, t, 2.8);
    result += blobFlow(uv, c3, r3, t, 4.1);

    return result;
}


float random(float2 uv)
{
    return frac(
        sin(dot(uv, float2(12.9898, 78.233))) *
        43758.5453
    );
}

float3 ClampHsvValue(float3 rgb, float minV, float maxV)
{
    float v = max(rgb.r, max(rgb.g, rgb.b));
    float nv = clamp(v, minV, maxV);
    return (v > 1e-5) ? rgb * (nv / v) : nv.xxx;
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float t = Time.x * TAU;
    float k = Strength.x;
    
    float4 original = tex2D(Input, uv);
    
    float maxColor = max(
        original.r,
        max(original.g, original.b)
    );
    
    float minColor = min(
        original.r,
        min(original.g, original.b)
    );
    
    float saturation = maxColor - minColor;

    float3 seed = Seeds.xyz;

    float2 displacement =
        globalFlow(uv, t) +
        rotatingDisc(uv, t, saturation, seed) +
        localBlobs(uv, t, seed);

    float2 finalUV = clamp(
        uv + displacement * k,
        0.001,
        0.999
    );

    float3 color = tex2D(Input, finalUV).rgb;

    // Saturation

    float luminance = dot(
        color,
        float3(0.2126, 0.7152, 0.0722)
    );

    color = lerp(
        luminance.xxx,
        color,
        Parameters.y
    );

    // Contrast
    
    color = (
        color - 0.5
    ) * Parameters.z + 0.5;
    
    // Luminosity adjustment
    
    float3 lumColor = ClampHsvValue(OverlayColor.rgb, 0.125, 0.965);
    color = lerp(
        color,
        lumColor,
        TintLuminosity.x
    );
    
    // Acrylic tint

    color = lerp(
        color,
        OverlayColor.rgb,
        Parameters.x
    );

    // Film grain

    float noise = random(uv) - 0.5;

    color += noise * Parameters.w;

    return float4(
        saturate(color),
        1.0
    );
}
