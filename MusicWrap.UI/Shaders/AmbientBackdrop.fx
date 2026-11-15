sampler2D Input : register(s0);

float4 Time : register(c0); // x = fase in [0,1)
float4 Strength : register(c1); // x = intensity

static const float TAU = 6.28318530718;

float2 field(float2 p, float ph)
{
    float a = ph * TAU;

    float2 f;
    f.x = sin(p.y * 4.0 + a * 1.0 + sin(p.x * 3.0 + a * 2.0));
    f.y = cos(p.x * 4.0 - a * 1.0 + cos(p.y * 3.0 - a * 2.0));
    return f;
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float ph = Time.x;
    float a = ph * TAU;
    float k = Strength.x;

    float2 p = uv - 0.5;

    float2 w1 = field(p * 1.3, ph);

    float2 w2 = field(p * 1.8 + w1 * 0.6 + float2(1.7, 9.2), ph);

    float2 w3 = field(p * 2.6 + w2 * 0.5 + float2(8.3, 2.8), ph + 0.33);

    float mask = 0.5 + 0.5 * sin(p.x * 2.5 + p.y * 2.0 + a * 1.0 + w1.x);
    mask = lerp(0.35, 1.0, mask);

    float2 distortion = (w2 * 0.045 + w3 * 0.018) * mask;

    float2 duv = uv + distortion * k;

    duv = clamp(duv, 0.001, 0.999);

    float4 color = tex2D(Input, duv);
    
    //float glow = (sin(w1.x * 2.0 + w2.y * 2.0 + a) * 0.5 + 0.5) * 0.06 - 0.03;
    //color.rgb += glow * k;

    return color;
}