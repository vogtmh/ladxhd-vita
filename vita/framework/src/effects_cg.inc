// Cg versions of the effect shaders for the GXM backend (same names and parameters as effects_src.inc).
// Varyings: v_tex TEXCOORD0, v_color TEXCOORD1, v_fc TEXCOORD2 (gl_FragCoord semantics, as in MonoGame GL).

static const char* kCgSpriteVS =
    "void main(float2 a_pos, float4 a_color, float2 a_tex0, uniform float4x4 u_matrix,\n"
    "          float4 out v_position : POSITION, float2 out v_tex : TEXCOORD0, float4 out v_color : TEXCOORD1)\n"
    "{\n"
    "    v_position = mul(float4(a_pos, 0.0f, 1.0f), u_matrix);\n"
    "    v_tex = a_tex0;\n"
    "    v_color = a_color;\n"
    "}\n";

static const char* kCgSpriteFcVS =
    "void main(float2 a_pos, float4 a_color, float2 a_tex0, uniform float4x4 u_matrix, uniform float4 u_glvp,\n"
    "          uniform float u_fcsign, float4 out v_position : POSITION, float2 out v_tex : TEXCOORD0,\n"
    "          float4 out v_color : TEXCOORD1, float2 out v_fc : TEXCOORD2)\n"
    "{\n"
    "    v_position = mul(float4(a_pos, 0.0f, 1.0f), u_matrix);\n"
    "    v_tex = a_tex0;\n"
    "    v_color = a_color;\n"
    "    float2 ndc = v_position.xy / v_position.w;\n"
    "    v_fc = float2(ndc.x * 0.5f + 0.5f, ndc.y * u_fcsign * 0.5f + 0.5f) * u_glvp.zw + u_glvp.xy;\n"
    "}\n";

static const char* kCgSpriteFS =
    "float4 main(float2 v_tex : TEXCOORD0, float4 v_color : TEXCOORD1, uniform sampler2D s0 : TEXUNIT0) : COLOR\n"
    "{\n"
    "    return tex2D(s0, v_tex) * v_color;\n"
    "}\n";

#define CG_FS_HEAD "uniform sampler2D s0 : TEXUNIT0;\n"
#define CG_MAIN "float4 main(float2 v_tex : TEXCOORD0, float4 v_color : TEXCOORD1) : COLOR\n"
#define CG_MAIN_FC "float4 main(float2 v_tex : TEXCOORD0, float4 v_color : TEXCOORD1, float2 v_fc : TEXCOORD2) : COLOR\n"

struct CgEffect {
    const char* name;
    const char* vs;  // null = sprite vertex shader (with v_fc when the fragment shader reads it)
    const char* fs;
};

static const CgEffect kCgEffects[] = {
    {"Shader/LightShader", nullptr,
     CG_FS_HEAD
     "uniform sampler2D s1 : TEXUNIT1;\n"
     "uniform float lightState;\n"
     "uniform float mode;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     CG_MAIN_FC
     "{\n"
     "    float4 texColor = tex2D(s0, v_tex);\n"
     "    float4 lightColor = tex2D(s1, float2(v_fc.x / width, v_fc.y / height));\n"
     "    lightColor.r = clamp(lightColor.r, 0.0f, 1.0f);\n"
     "    lightColor.b = clamp(lightColor.b, 0.0f, 1.0f);\n"
     "    float3 lerpTarget = float3(1.0f, 1.0f, 1.0f);\n"
     "    if (mode > 0.5f) lerpTarget = texColor.rgb;\n"
     "    return float4(lerp(texColor.rgb * lightColor.rgb, lerpTarget, lightState), texColor.a) * v_color;\n"
     "}\n"},

    {"Shader/DamageShader", nullptr,
     CG_FS_HEAD
     "uniform float mark0;\n"
     "uniform float mark1;\n"
     "uniform float4 Color0;\n"
     "uniform float4 Color1;\n"
     "uniform float4 Color2;\n"
     CG_MAIN
     "{\n"
     "    float4 color = tex2D(s0, v_tex) * v_color;\n"
     "    float sum = (color.r + color.g + color.b) * 0.333f;\n"
     "    if (sum < mark0) return Color0 * color.a * v_color.a;\n"
     "    if (sum < mark1) return Color1 * color.a * v_color.a;\n"
     "    return Color2 * color.a * v_color.a;\n"
     "}\n"},

    {"Shader/BBlurH", nullptr,
     CG_FS_HEAD
     "uniform float pixelX;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     "uniform float mult2;\n"
     CG_MAIN
     "{\n"
     "    float val0 = tex2D(s0, v_tex).a;\n"
     "    if (val0 == 1.0f) return float4(0.0f, 0.0f, 0.0f, val0);\n"
     "    float val1 = tex2D(s0, float2(v_tex.x - pixelX * 1.0f, v_tex.y)).a;\n"
     "    float val2 = tex2D(s0, float2(v_tex.x - pixelX * 2.0f, v_tex.y)).a;\n"
     "    float val3 = tex2D(s0, float2(v_tex.x - pixelX * 3.0f, v_tex.y)).a;\n"
     "    float val4 = tex2D(s0, float2(v_tex.x + pixelX * 1.0f, v_tex.y)).a;\n"
     "    float val5 = tex2D(s0, float2(v_tex.x + pixelX * 2.0f, v_tex.y)).a;\n"
     "    float val6 = tex2D(s0, float2(v_tex.x + pixelX * 3.0f, v_tex.y)).a;\n"
     "    float valOut = val0;\n"
     "    if (val1 == 1.0f || val4 == 1.0f) valOut = max(max(val1 * mult0, val4 * mult0), valOut);\n"
     "    else if (val2 == 1.0f || val5 == 1.0f) valOut = max(max(val2 * mult1, val5 * mult1), valOut);\n"
     "    else if (val3 == 1.0f || val6 == 1.0f) valOut = max(max(val3 * mult2, val6 * mult2), valOut);\n"
     "    else {\n"
     "        float mult = 1.95f;\n"
     "        valOut = max(val1 * mult0 * mult, valOut);\n"
     "        valOut = max(val2 * mult1 * mult, valOut);\n"
     "        valOut = max(val3 * mult2 * mult, valOut);\n"
     "        valOut = max(val4 * mult0 * mult, valOut);\n"
     "        valOut = max(val5 * mult1 * mult, valOut);\n"
     "        valOut = max(val6 * mult2 * mult, valOut);\n"
     "    }\n"
     "    return float4(0.0f, 0.0f, 0.0f, valOut);\n"
     "}\n"},

    {"Shader/BBlurV", nullptr,
     CG_FS_HEAD
     "uniform float pixelY;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     "uniform float mult2;\n"
     CG_MAIN
     "{\n"
     "    float valMax = tex2D(s0, v_tex).a;\n"
     "    valMax = max(tex2D(s0, float2(v_tex.x, v_tex.y - pixelY * 1.0f)).a * mult0, valMax);\n"
     "    valMax = max(tex2D(s0, float2(v_tex.x, v_tex.y - pixelY * 2.0f)).a * mult1, valMax);\n"
     "    valMax = max(tex2D(s0, float2(v_tex.x, v_tex.y - pixelY * 3.0f)).a * mult2, valMax);\n"
     "    valMax = max(tex2D(s0, float2(v_tex.x, v_tex.y + pixelY * 1.0f)).a * mult0, valMax);\n"
     "    valMax = max(tex2D(s0, float2(v_tex.x, v_tex.y + pixelY * 2.0f)).a * mult1, valMax);\n"
     "    valMax = max(tex2D(s0, float2(v_tex.x, v_tex.y + pixelY * 3.0f)).a * mult2, valMax);\n"
     "    return float4(0.0f, 0.0f, 0.0f, valMax);\n"
     "}\n"},

    {"Shader/BlurH", nullptr,
     CG_FS_HEAD
     "uniform float pixelX;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     CG_MAIN
     "{\n"
     "    float4 c = tex2D(s0, float2(v_tex.x - pixelX * 0.5f, v_tex.y)) * mult0;\n"
     "    c += tex2D(s0, float2(v_tex.x + pixelX * 0.5f, v_tex.y)) * mult0;\n"
     "    c += tex2D(s0, float2(v_tex.x - pixelX * 2.5f, v_tex.y)) * mult1;\n"
     "    c += tex2D(s0, float2(v_tex.x + pixelX * 2.5f, v_tex.y)) * mult1;\n"
     "    return c;\n"
     "}\n"},

    {"Shader/BlurV", nullptr,
     CG_FS_HEAD
     "uniform float pixelY;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     CG_MAIN
     "{\n"
     "    float4 c = tex2D(s0, float2(v_tex.x, v_tex.y - pixelY * 0.5f)) * mult0;\n"
     "    c += tex2D(s0, float2(v_tex.x, v_tex.y + pixelY * 0.5f)) * mult0;\n"
     "    c += tex2D(s0, float2(v_tex.x, v_tex.y - pixelY * 2.5f)) * mult1;\n"
     "    c += tex2D(s0, float2(v_tex.x, v_tex.y + pixelY * 2.5f)) * mult1;\n"
     "    return c;\n"
     "}\n"},

    {"Shader/CircleShader", nullptr,
     "uniform float softRad;\n"
     "uniform float size;\n"
     "uniform float centerX;\n"
     "uniform float centerY;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     "uniform float scale;\n"
     "uniform float pixelSnap;\n"
     "uniform float gridPhaseX;\n"
     "uniform float gridPhaseY;\n"
     CG_MAIN
     "{\n"
     "    float2 px = float2(v_tex.x * width, v_tex.y * height);\n"
     "    float2 phase = float2(gridPhaseX, gridPhaseY);\n"
     "    float2 snapped = (floor(px / scale - phase) + phase + 0.5f) * scale;\n"
     "    px = lerp(px, snapped, pixelSnap);\n"
     "    float2 pos = float2(px.x - centerX, px.y - centerY);\n"
     "    float white = clamp((size - length(pos)) / softRad, 0.0f, 1.0f);\n"
     "    float black = 1.0f - white;\n"
     "    return v_color * (black * black * black);\n"
     "}\n"},

    {"Shader/ColorCloud", nullptr,
     CG_FS_HEAD
     "uniform float scale;\n"
     "uniform float scaleX;\n"
     "uniform float scaleY;\n"
     "uniform float4 color0;\n"
     "uniform float4 color1;\n"
     "uniform float2 offset;\n"
     CG_MAIN_FC
     "{\n"
     "    float4 texColor = tex2D(s0, v_tex);\n"
     "    float4 insideColor = color0;\n"
     "    float ax = (v_fc.x / scaleX + offset.x / scale) / 4.0f;\n"
     "    float ay = (v_fc.y / scaleY + offset.y / scale) / 4.0f;\n"
     "    float a = sign(ax) * floor(abs(ax)) + sign(ay) * floor(abs(ay));\n"
     "    if (fmod(abs(a), 2.0f) < 0.5f) insideColor = color1;\n"
     "    return insideColor * texColor;\n"
     "}\n"},

    {"Shader/ColorShader", nullptr,
     CG_FS_HEAD
     CG_MAIN
     "{\n"
     "    float4 color = tex2D(s0, v_tex);\n"
     "    if (color.r == 1.0f && color.g == 0.0f && color.b != 0.0f) return v_color;\n"
     "    return color;\n"
     "}\n"},

    {"Shader/EffectBlur", nullptr,
     CG_FS_HEAD
     "uniform sampler2D s1 : TEXUNIT1;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     CG_MAIN_FC
     "{\n"
     "    float4 textureSample = tex2D(s0, v_tex);\n"
     "    float2 screenUV = float2(v_fc.x / width, 1.0f - v_fc.y / height);\n"
     "    float4 blur = tex2D(s1, screenUV);\n"
     "    return blur * v_color * textureSample.r + textureSample * (1.0f - textureSample.r) * v_color;\n"
     "}\n"},

    {"Shader/FullShadowEffect",
     "void main(float2 a_pos, float4 a_color, float2 a_tex0, float2 a_tex1, float2 a_tex2,\n"
     "          uniform float4x4 xViewProjection, uniform float offsetX, uniform float height,\n"
     "          float4 out v_position : POSITION, float2 out v_tex : TEXCOORD0, float4 out v_color : TEXCOORD1)\n"
     "{\n"
     "    float4 pos = float4(a_pos, 0.0f, 1.0f);\n"
     "    pos.x += (a_tex2.y - pos.y + a_tex1.y) * offsetX;\n"
     "    pos.y -= (-(a_tex1.y + a_tex2.y) + pos.y) * (1.0f - height);\n"
     "    v_position = mul(pos, xViewProjection);\n"
     "    v_tex = a_tex0;\n"
     "    v_color = a_color;\n"
     "}\n",
     CG_FS_HEAD
     CG_MAIN
     "{\n"
     "    return tex2D(s0, v_tex).a * v_color;\n"
     "}\n"},

    {"Shader/GBCColorCorrection", nullptr,
     // The game never sets ColorProfile/LightenScreen, so their defaults (profile 1, no lightening) are
     // constants here: no per-pixel branches, the 0.91 luminance folded into the matrix.
     CG_FS_HEAD
     CG_MAIN
     "{\n"
     "    float4 tex = tex2D(s0, v_tex);\n"
     "    float3 lin = exp2(log2(clamp(tex.rgb, 0.0f, 1.0f)) * 2.2f);\n"
     "    float3 mixed = float3(dot(float3(0.82355f, 0.17745f, -0.091f), lin),\n"
     "                          dot(float3(0.091f, 0.5915f, 0.2275f), lin),\n"
     "                          dot(float3(0.143325f, 0.129675f, 0.637f), lin));\n"
     "    float3 outColor = exp2(log2(clamp(mixed, 0.0f, 1.0f)) * (1.0f / 2.2f));\n"
     "    return float4(outColor, tex.a) * v_color;\n"
     "}\n"},

    {"Shader/LightFadeShader", nullptr,
     CG_FS_HEAD
     CG_MAIN
     "{\n"
     "    float4 texColor = tex2D(s0, v_tex);\n"
     "    float dist = 1.0f - clamp(((0.05f + texColor.a * 0.95f) - v_color.a) / 0.05f, 0.0f, 1.0f);\n"
     "    return float4(0.0f, 0.0f, 0.0f, dist);\n"
     "}\n"},

    {"Shader/PixelGrid", nullptr,
     CG_FS_HEAD
     "uniform float2 TextureSize;\n"
     "uniform float2 Offset;\n"
     "uniform float GridOpacity;\n"
     CG_MAIN
     "{\n"
     "    float2 gridUV = v_tex * TextureSize + Offset;\n"
     "    float2 cell = frac(gridUV);\n"
     "    float2 fw = fwidth(gridUV);\n"
     "    float2 gridLine = step(cell, fw) + step(1.0f - cell, fw);\n"
     "    float gridMask = clamp(max(gridLine.x, gridLine.y), 0.0f, 1.0f) * GridOpacity;\n"
     "    float4 tex = tex2D(s0, v_tex);\n"
     "    return lerp(tex, float4(0.0f, 0.0f, 0.0f, tex.a), gridMask) * v_color;\n"
     "}\n"},

    {"Shader/RoundedCorner", nullptr,
     CG_FS_HEAD
     "uniform float radius;\n"
     "uniform float scale;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     CG_MAIN
     "{\n"
     "    float4 texColor = tex2D(s0, v_tex);\n"
     "    float posX = v_tex.x * width;\n"
     "    float posY = v_tex.y * height;\n"
     "    float distX = min(posX, abs(posX - width));\n"
     "    float distY = min(posY, abs(posY - height));\n"
     "    float circle;\n"
     "    if (distX < radius && distY < radius) {\n"
     "        float a = radius - distX;\n"
     "        float b = radius - distY;\n"
     "        circle = clamp((radius - sqrt(a * a + b * b)) * scale, 0.0f, 1.0f);\n"
     "    } else circle = clamp(min(distX, distY) * scale, 0.0f, 1.0f);\n"
     "    return texColor * v_color * circle;\n"
     "}\n"},

    {"Shader/RoundedCornerEffectBlur", nullptr,
     CG_FS_HEAD
     "uniform sampler2D s1 : TEXUNIT1;\n"
     "uniform float4 blurColor;\n"
     "uniform float blurEnabled;\n"
     "uniform float radius;\n"
     "uniform float scale;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     "uniform float screenWidth;\n"
     "uniform float screenHeight;\n"
     CG_MAIN_FC
     "{\n"
     "    float posX = v_tex.x * width;\n"
     "    float posY = v_tex.y * height;\n"
     "    float distX = min(posX, abs(posX - width));\n"
     "    float distY = min(posY, abs(posY - height));\n"
     "    float circle;\n"
     "    if (distX < radius && distY < radius) {\n"
     "        float a = radius - distX;\n"
     "        float b = radius - distY;\n"
     "        circle = clamp((radius - sqrt(a * a + b * b)) * scale, 0.0f, 1.0f);\n"
     "    } else circle = clamp(min(distX, distY) * scale, 0.0f, 1.0f);\n"
     "    if (radius == 0.0f) circle = 1.0f;\n"
     "    float4 textureSample = tex2D(s0, v_tex);\n"
     "    float2 screenUV = float2(v_fc.x / screenWidth, 1.0f - v_fc.y / screenHeight);\n"
     "    float4 blurSample = tex2D(s1, screenUV) * blurEnabled;\n"
     "    return ((blurSample * blurColor * (1.0f - v_color.a) + v_color) * textureSample.r\n"
     "            + textureSample * (1.0f - textureSample.r) * blurColor) * circle;\n"
     "}\n"},

    {"Shader/SaturationFilter", nullptr,
     CG_FS_HEAD
     "uniform float percentage;\n"
     CG_MAIN
     "{\n"
     "    float4 color = tex2D(s0, v_tex);\n"
     "    float intensity = dot(color.rgb, float3(0.3f, 0.59f, 0.11f));\n"
     "    return lerp(color, float4(intensity, intensity, intensity, color.a), percentage) * v_color;\n"
     "}\n"},

    {"Shader/ShockEffect", nullptr,
     CG_FS_HEAD
     "uniform float mark0;\n"
     "uniform float mark1;\n"
     "uniform float mark2;\n"
     CG_MAIN
     "{\n"
     "    float4 color = tex2D(s0, v_tex) * v_color;\n"
     "    float sum = 0.4f * color.r + 0.2f * color.g + 0.4f * color.b;\n"
     "    if (sum <= mark0) return color;\n"
     "    if (sum <= mark1) return float4(1.0f, 1.0f, 0.482f, 1.0f) * color.a * v_color.a;\n"
     "    if (sum <= mark2) return float4(0.063f, 0.129f, 0.192f, 1.0f) * color.a * v_color.a;\n"
     "    return float4(1.0f, 1.0f, 1.0f, 1.0f) * color.a * v_color.a;\n"
     "}\n"},

    {"Shader/ThanosShader", nullptr,
     CG_FS_HEAD
     "uniform sampler2D s1 : TEXUNIT1;\n"
     "uniform float Percentage;\n"
     "uniform float2 Scale;\n"
     CG_MAIN
     "{\n"
     "    float4 texColor = tex2D(s0, v_tex);\n"
     "    if ((Percentage > 0.0f && Percentage >= tex2D(s1, v_tex * Scale).r) || Percentage == 1.0f)\n"
     "        return float4(0.0f, 0.0f, 0.0f, 0.0f);\n"
     "    return texColor * v_color;\n"
     "}\n"},

    {"Shader/WaleShader", nullptr,
     CG_FS_HEAD
     "uniform float Offset;\n"
     "uniform float Period;\n"
     "uniform float Time;\n"
     CG_MAIN
     "{\n"
     "    float2 c = v_tex;\n"
     "    c.x += sin(c.y * Period + Time) * Offset;\n"
     "    return tex2D(s0, c) * v_color;\n"
     "}\n"},

    {"Shader/WobbleShader", nullptr,
     CG_FS_HEAD
     "uniform float width;\n"
     "uniform float height;\n"
     "uniform float scale;\n"
     "uniform float brightness;\n"
     "uniform float offset;\n"
     "uniform float offsetWidth;\n"
     "uniform float offsetHeight;\n"
     "uniform float pixelSnap;\n"
     "uniform float gridPhaseY;\n"
     CG_MAIN
     "{\n"
     "    const float PI = 3.1415926538f;\n"
     "    float gameW = width / scale;\n"
     "    float gameH = height / scale;\n"
     "    float gy = v_tex.y * gameH;\n"
     "    float snappedY = floor(gy - gridPhaseY) + gridPhaseY + 0.5f;\n"
     "    float waveY = lerp(gy, snappedY, pixelSnap);\n"
     "    float amp = sin(offset + waveY / offsetHeight * PI + PI / 2.0f) * offsetWidth;\n"
     "    amp = lerp(amp, floor(amp + 0.5f), pixelSnap);\n"
     "    float offsetX = amp / gameW;\n"
     "    return tex2D(s0, float2(v_tex.x + offsetX, v_tex.y)) * (1.0f - brightness) + float4(1.0f, 1.0f, 1.0f, 1.0f) * brightness;\n"
     "}\n"},
};
