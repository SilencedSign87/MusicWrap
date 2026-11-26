
sampler2D Input : register(s0);

// x = time 0..1
float4 Time : register(c0);

// x = distortion strength
float4 Strength : register(c1);

float4 TintColor : register(c2);

// x = Tint opacity
// y = Tint luminosity opacity
// z = 1: use TintLuminosityOpacity, 0: auto
// w = Global Opacity of the noise
float4 AcrylicParameters : register(c3);

// random seeds
float4 Seeds : register(c4);

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

float Lum(float3 c)
{
    return dot(c, float3(0.30, 0.59, 0.11));
}

// Ajusta el color para que su luminosidad quede dentro del gamut RGB.
float3 ClipColor(float3 c)
{
    float l = Lum(c);
    float n = min(c.r, min(c.g, c.b));

    if (n < 0.0)
    {
        float d = l - n;
        if (d > 1e-6)
            c = l + ((c - l) * l) / d;
    }

    float x = max(c.r, max(c.g, c.b));

    if (x > 1.0)
    {
        float d = x - l;
        if (d > 1e-6)
            c = l + ((c - l) * (1.0 - l)) / d;
    }

    return saturate(c);
}

float3 SetLum(float3 c, float targetLum)
{
    return ClipColor(c + (targetLum - Lum(c)));
}

// Luminosity blend: conserva el tono y la saturación del fondo.
float3 BlendLuminosity(float3 backdrop, float3 source)
{
    return SetLum(backdrop, Lum(source));
}

// Color blend: conserva la luminosidad del fondo y usa el tono/saturación del tinte.
float3 BlendColor(float3 backdrop, float3 source)
{
    return SetLum(source, Lum(backdrop));
}

float3 RGBToHSV(float3 c)
{
    float hi = max(c.r, max(c.g, c.b));
    float lo = min(c.r, min(c.g, c.b));
    float d = hi - lo;

    float h = 0.0;
    float s = (hi > 1e-6) ? d / hi : 0.0;

    if (d > 1e-6)
    {
        if (hi == c.r)
            h = (c.g - c.b) / d;
        else if (hi == c.g)
            h = 2.0 + (c.b - c.r) / d;
        else
            h = 4.0 + (c.r - c.g) / d;

        h = frac(h / 6.0);
    }

    return float3(h, s, hi);
}

float3 HSVToRGB(float3 hsv)
{
    float3 p = abs(frac(hsv.xxx + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return hsv.z * lerp(float3(1.0, 1.0, 1.0), saturate(p - 1.0), hsv.y);
}

float QuantizeAlpha(float a)
{
    return floor(saturate(a) * 255.0 + 0.5) / 255.0;
}

float GetTintOpacityModifier(float3 tint)
{
    float3 hsv = RGBToHSV(tint);
    float v = hsv.z;
    float s = hsv.y;

    float modifier = 0.90;

    if (v != 0.50)
    {
        float lowestMaxOpacity;
        float maxDeviation;

        if (v > 0.50)
        {
            lowestMaxOpacity = 0.45;
            maxDeviation = 0.50;
        }
        else
        {
            lowestMaxOpacity = 0.85;
            maxDeviation = 0.50;
        }

        float suppression = (0.90 - lowestMaxOpacity)
                          * (abs(v - 0.50) / maxDeviation);

        suppression *= max(1.0 - (s * 2.0), 0.0);
        modifier = 0.90 - suppression;
    }

    return modifier;
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
    
    float3 backdrop = tex2D(Input, finalUV).rgb;
    
    float tintOpacity = saturate(AcrylicParameters.x);
    float useExplicitLuminosityOpacity = step(0.5, AcrylicParameters.z);
    
    float3 tintHSV = RGBToHSV(TintColor.rgb);
    float3 luminosityRGB = HSVToRGB(float3(
        tintHSV.x,
        tintHSV.y,
        clamp(tintHSV.z, 0.125, 0.965)
    ));
    
    float tintAlphaForLuminosity = QuantizeAlpha(TintColor.a * tintOpacity);
    
    float automaticLuminosityOpacity = min(0.15 + tintAlphaForLuminosity * 0.88, 1.0);
    
    float tintLum = Lum(TintColor.rgb);
    
    float darkFalloff = saturate(tintLum / 0.5);
    darkFalloff *= darkFalloff;
    automaticLuminosityOpacity *= darkFalloff;
    
    float luminosityOpacity = lerp(
        automaticLuminosityOpacity,
        saturate(AcrylicParameters.y),
        useExplicitLuminosityOpacity
    );
    
    float3 luminosityBlend = BlendLuminosity(backdrop, luminosityRGB);
    float3 afterLuminosity = lerp(
        backdrop,
        luminosityBlend,
        luminosityOpacity
    );
    
    float tintModifier = lerp(
        GetTintOpacityModifier(TintColor.rgb),
        1.0,
        useExplicitLuminosityOpacity
    );
    
    float effectiveTintOpacity = QuantizeAlpha(TintColor.a * tintOpacity * tintModifier);
    
    float3 colorBlend = BlendColor(afterLuminosity, TintColor.rgb);
    float3 acrylic = lerp(
        afterLuminosity,
        colorBlend,
        effectiveTintOpacity
    );
    
    // noise
    float noise = random(uv) - 0.5;
    float noiseWeight = Lum(acrylic);
    float3 result = acrylic + noise * AcrylicParameters.w * noiseWeight;
    
    return float4(saturate(result), 1.0);
}
