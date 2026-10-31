// Ported effect shaders (GLSL 1.10 style, see gfx.cpp preludes). Pixel shaders receive v_color/v_tex from the
// sprite vertex shader; shaders that read SV_Position use v_fc (gl_FragCoord semantics, as in MonoGame GL).
#define FS_HEAD             \
    "varying vec4 v_color;\n" \
    "varying vec2 v_tex;\n"   \
    "uniform sampler2D s0;\n"
#define FS_HEAD_POS FS_HEAD "varying vec2 v_fc;\n"

using P = ParamDef;

static const std::vector<EffectDef> kEffects = {
    {"Shader/LightShader", nullptr,
     FS_HEAD_POS
     "uniform sampler2D s1;\n"
     "uniform float lightState;\n"
     "uniform float mode;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     "void main() {\n"
     "  vec4 texColor = texture2D(s0, v_tex);\n"
     "  vec4 lightColor = texture2D(s1, vec2(v_fc.x / width, v_fc.y / height));\n"
     "  lightColor.r = clamp(lightColor.r, 0.0, 1.0);\n"
     "  lightColor.b = clamp(lightColor.b, 0.0, 1.0);\n"
     "  vec3 lerpTarget = vec3(1.0, 1.0, 1.0);\n"
     "  if (mode > 0.5) lerpTarget = texColor.rgb;\n"
     "  gl_FragColor = vec4(mix(texColor.rgb * lightColor.rgb, lerpTarget, lightState), texColor.a) * v_color;\n"
     "}\n",
     {{"sprLight", P::Tex, 1, {}}, {"lightState", P::Float, 0, {}}, {"mode", P::Int, 0, {}},
      {"width", P::Int, 0, {}}, {"height", P::Int, 0, {}}}},

    {"Shader/DamageShader", nullptr,
     FS_HEAD
     "uniform float mark0;\n"
     "uniform float mark1;\n"
     "uniform vec4 Color0;\n"
     "uniform vec4 Color1;\n"
     "uniform vec4 Color2;\n"
     "void main() {\n"
     "  vec4 color = texture2D(s0, v_tex) * v_color;\n"
     "  float sum = (color.r + color.g + color.b) * 0.333;\n"
     "  if (sum < mark0) gl_FragColor = Color0 * color.a * v_color.a;\n"
     "  else if (sum < mark1) gl_FragColor = Color1 * color.a * v_color.a;\n"
     "  else gl_FragColor = Color2 * color.a * v_color.a;\n"
     "}\n",
     {{"mark0", P::Float, 0, {}}, {"mark1", P::Float, 0, {}}, {"Color0", P::Vec4, 0, {}},
      {"Color1", P::Vec4, 0, {}}, {"Color2", P::Vec4, 0, {}}}},

    {"Shader/BBlurH", nullptr,
     FS_HEAD
     "uniform float pixelX;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     "uniform float mult2;\n"
     "float A(float dx) { return texture2D(s0, vec2(v_tex.x + pixelX * dx, v_tex.y)).a; }\n"
     "void main() {\n"
     "  float val0 = A(0.0);\n"
     "  if (val0 == 1.0) { gl_FragColor = vec4(0.0, 0.0, 0.0, val0); return; }\n"
     "  float val1 = A(-1.0), val2 = A(-2.0), val3 = A(-3.0), val4 = A(1.0), val5 = A(2.0), val6 = A(3.0);\n"
     "  float valOut = val0;\n"
     "  if (val1 == 1.0 || val4 == 1.0) valOut = max(max(val1 * mult0, val4 * mult0), valOut);\n"
     "  else if (val2 == 1.0 || val5 == 1.0) valOut = max(max(val2 * mult1, val5 * mult1), valOut);\n"
     "  else if (val3 == 1.0 || val6 == 1.0) valOut = max(max(val3 * mult2, val6 * mult2), valOut);\n"
     "  else {\n"
     "    float mult = 1.95;\n"
     "    valOut = max(val1 * mult0 * mult, valOut);\n"
     "    valOut = max(val2 * mult1 * mult, valOut);\n"
     "    valOut = max(val3 * mult2 * mult, valOut);\n"
     "    valOut = max(val4 * mult0 * mult, valOut);\n"
     "    valOut = max(val5 * mult1 * mult, valOut);\n"
     "    valOut = max(val6 * mult2 * mult, valOut);\n"
     "  }\n"
     "  gl_FragColor = vec4(0.0, 0.0, 0.0, valOut);\n"
     "}\n",
     {{"pixelX", P::Float, 0, {}}, {"mult0", P::Float, 0, {0.25f}}, {"mult1", P::Float, 0, {0.125f}},
      {"mult2", P::Float, 0, {0.075f}}}},

    {"Shader/BBlurV", nullptr,
     FS_HEAD
     "uniform float pixelY;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     "uniform float mult2;\n"
     "float A(float dy) { return texture2D(s0, vec2(v_tex.x, v_tex.y + pixelY * dy)).a; }\n"
     "void main() {\n"
     "  float valMax = A(0.0);\n"
     "  valMax = max(A(-1.0) * mult0, valMax);\n"
     "  valMax = max(A(-2.0) * mult1, valMax);\n"
     "  valMax = max(A(-3.0) * mult2, valMax);\n"
     "  valMax = max(A(1.0) * mult0, valMax);\n"
     "  valMax = max(A(2.0) * mult1, valMax);\n"
     "  valMax = max(A(3.0) * mult2, valMax);\n"
     "  gl_FragColor = vec4(0.0, 0.0, 0.0, valMax);\n"
     "}\n",
     {{"pixelY", P::Float, 0, {}}, {"mult0", P::Float, 0, {0.25f}}, {"mult1", P::Float, 0, {0.125f}},
      {"mult2", P::Float, 0, {0.075f}}}},

    {"Shader/BlurH", nullptr,
     FS_HEAD
     "uniform float pixelX;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     "void main() {\n"
     "  vec4 c = texture2D(s0, vec2(v_tex.x - pixelX * 0.5, v_tex.y)) * mult0;\n"
     "  c += texture2D(s0, vec2(v_tex.x + pixelX * 0.5, v_tex.y)) * mult0;\n"
     "  c += texture2D(s0, vec2(v_tex.x - pixelX * 2.5, v_tex.y)) * mult1;\n"
     "  c += texture2D(s0, vec2(v_tex.x + pixelX * 2.5, v_tex.y)) * mult1;\n"
     "  gl_FragColor = c;\n"
     "}\n",
     {{"pixelX", P::Float, 0, {}}, {"mult0", P::Float, 0, {}}, {"mult1", P::Float, 0, {}}}},

    {"Shader/BlurV", nullptr,
     FS_HEAD
     "uniform float pixelY;\n"
     "uniform float mult0;\n"
     "uniform float mult1;\n"
     "void main() {\n"
     "  vec4 c = texture2D(s0, vec2(v_tex.x, v_tex.y - pixelY * 0.5)) * mult0;\n"
     "  c += texture2D(s0, vec2(v_tex.x, v_tex.y + pixelY * 0.5)) * mult0;\n"
     "  c += texture2D(s0, vec2(v_tex.x, v_tex.y - pixelY * 2.5)) * mult1;\n"
     "  c += texture2D(s0, vec2(v_tex.x, v_tex.y + pixelY * 2.5)) * mult1;\n"
     "  gl_FragColor = c;\n"
     "}\n",
     {{"pixelY", P::Float, 0, {}}, {"mult0", P::Float, 0, {}}, {"mult1", P::Float, 0, {}}}},

    {"Shader/CircleShader", nullptr,
     FS_HEAD
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
     "void main() {\n"
     "  vec2 px = vec2(v_tex.x * width, v_tex.y * height);\n"
     "  vec2 phase = vec2(gridPhaseX, gridPhaseY);\n"
     "  vec2 snapped = (floor(px / scale - phase) + phase + 0.5) * scale;\n"
     "  px = mix(px, snapped, pixelSnap);\n"
     "  vec2 pos = vec2(px.x - centerX, px.y - centerY);\n"
     "  float white = clamp((size - length(pos)) / softRad, 0.0, 1.0);\n"
     "  float black = 1.0 - white;\n"
     "  gl_FragColor = v_color * (black * black * black);\n"
     "}\n",
     {{"softRad", P::Float, 0, {30}}, {"size", P::Float, 0, {}}, {"centerX", P::Float, 0, {}},
      {"centerY", P::Float, 0, {}}, {"width", P::Int, 0, {}}, {"height", P::Int, 0, {}}, {"scale", P::Float, 0, {}},
      {"pixelSnap", P::Float, 0, {}}, {"gridPhaseX", P::Float, 0, {}}, {"gridPhaseY", P::Float, 0, {}}}},

    {"Shader/ColorCloud", nullptr,
     FS_HEAD_POS
     "uniform float scale;\n"
     "uniform float scaleX;\n"
     "uniform float scaleY;\n"
     "uniform vec4 color0;\n"
     "uniform vec4 color1;\n"
     "uniform vec2 offset;\n"
     "void main() {\n"
     "  vec4 texColor = texture2D(s0, v_tex);\n"
     "  vec4 insideColor = color0;\n"
     "  float a = float(int((v_fc.x / scaleX + offset.x / scale) / 4.0));\n"
     "  float b = float(int((v_fc.y / scaleY + offset.y / scale) / 4.0));\n"
     "  if (mod(abs(a + b), 2.0) < 0.5) insideColor = color1;\n"
     "  gl_FragColor = insideColor * texColor;\n"
     "}\n",
     {{"scale", P::Float, 0, {}}, {"scaleX", P::Float, 0, {}}, {"scaleY", P::Float, 0, {}},
      {"color0", P::Vec4, 0, {1, 0, 0, 1}}, {"color1", P::Vec4, 0, {1, 1, 1, 1}}, {"offset", P::Vec2, 0, {}}}},

    {"Shader/ColorShader", nullptr,
     FS_HEAD
     "void main() {\n"
     "  vec4 color = texture2D(s0, v_tex);\n"
     "  if (color.r == 1.0 && color.g == 0.0 && color.b != 0.0) gl_FragColor = v_color;\n"
     "  else gl_FragColor = color;\n"
     "}\n",
     {}},

    {"Shader/EffectBlur", nullptr,
     FS_HEAD_POS
     "uniform sampler2D s1;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     "void main() {\n"
     "  vec4 textureSample = texture2D(s0, v_tex);\n"
     "  vec2 screenUV = vec2(v_fc.x / width, 1.0 - v_fc.y / height);\n"
     "  vec4 blur = texture2D(s1, screenUV);\n"
     "  gl_FragColor = blur * v_color * textureSample.r + textureSample * (1.0 - textureSample.r) * v_color;\n"
     "}\n",
     {{"sprBlur", P::Tex, 1, {}}, {"width", P::Int, 0, {}}, {"height", P::Int, 0, {}}}},

    {"Shader/FullShadowEffect",
     "attribute vec2 a_pos;\n"
     "attribute vec4 a_color;\n"
     "attribute vec2 a_tex0;\n"
     "attribute vec2 a_tex1;\n"
     "attribute vec2 a_tex2;\n"
     "uniform mat4 xViewProjection;\n"
     "uniform float offsetX;\n"
     "uniform float height;\n"
     "uniform float u_flip;\n"
     "varying vec4 v_color;\n"
     "varying vec2 v_tex;\n"
     "void main() {\n"
     "  vec4 pos = vec4(a_pos, 0.0, 1.0);\n"
     "  pos.x += (a_tex2.y - pos.y + a_tex1.y) * offsetX;\n"
     "  pos.y -= (-(a_tex1.y + a_tex2.y) + pos.y) * (1.0 - height);\n"
     "  vec4 p = xViewProjection * pos;\n"
     "  p.y *= u_flip;\n"
     "  gl_Position = p;\n"
     "  v_tex = a_tex0;\n"
     "  v_color = a_color;\n"
     "}\n",
     FS_HEAD
     "void main() { gl_FragColor = texture2D(s0, v_tex).a * v_color; }\n",
     {{"xViewProjection", P::Mat4, 0, {}}, {"offsetX", P::Float, 0, {}}, {"height", P::Float, 0, {}}}},

    {"Shader/GBCColorCorrection", nullptr,
     // Profile 1 / no lightening are constants (the game never sets the parameters); see effects_cg.inc.
     FS_HEAD
     "void main() {\n"
     "  vec4 tex = texture2D(s0, v_tex);\n"
     "  vec3 lin = exp2(log2(clamp(tex.rgb, 0.0, 1.0)) * 2.2);\n"
     "  vec3 mixed = vec3(dot(vec3(0.82355, 0.17745, -0.091), lin),\n"
     "                    dot(vec3(0.091, 0.5915, 0.2275), lin),\n"
     "                    dot(vec3(0.143325, 0.129675, 0.637), lin));\n"
     "  vec3 outColor = exp2(log2(clamp(mixed, 0.0, 1.0)) * (1.0 / 2.2));\n"
     "  gl_FragColor = vec4(outColor, tex.a) * v_color;\n"
     "}\n",
     {{"ColorProfile", P::Float, 0, {1}}, {"LightenScreen", P::Float, 0, {}}}},

    {"Shader/LightFadeShader", nullptr,
     FS_HEAD
     "void main() {\n"
     "  vec4 texColor = texture2D(s0, v_tex);\n"
     "  float dist = 1.0 - clamp(((0.05 + texColor.a * 0.95) - v_color.a) / 0.05, 0.0, 1.0);\n"
     "  gl_FragColor = vec4(0.0, 0.0, 0.0, dist);\n"
     "}\n",
     {}},

    {"Shader/PixelGrid", nullptr,
     FS_HEAD
     "uniform vec2 TextureSize;\n"
     "uniform vec2 Offset;\n"
     "uniform float GridOpacity;\n"
     "void main() {\n"
     "  vec2 gridUV = v_tex * TextureSize + Offset;\n"
     "  vec2 cell = fract(gridUV);\n"
     "  vec2 fw = fwidth(gridUV);\n"
     "  vec2 gridLine = step(cell, fw) + step(1.0 - cell, fw);\n"
     "  float gridMask = clamp(max(gridLine.x, gridLine.y), 0.0, 1.0) * GridOpacity;\n"
     "  vec4 tex = texture2D(s0, v_tex);\n"
     "  gl_FragColor = mix(tex, vec4(0.0, 0.0, 0.0, tex.a), gridMask) * v_color;\n"
     "}\n",
     {{"TextureSize", P::Vec2, 0, {190, 128}}, {"Offset", P::Vec2, 0, {0.5f, 0.5f}},
      {"GridOpacity", P::Float, 0, {0.4f}}}},

    {"Shader/RoundedCorner", nullptr,
     FS_HEAD
     "uniform float radius;\n"
     "uniform float scale;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     "void main() {\n"
     "  vec4 texColor = texture2D(s0, v_tex);\n"
     "  float posX = v_tex.x * width;\n"
     "  float posY = v_tex.y * height;\n"
     "  float distX = min(posX, abs(posX - width));\n"
     "  float distY = min(posY, abs(posY - height));\n"
     "  float circle;\n"
     "  if (distX < radius && distY < radius) {\n"
     "    float a = radius - distX;\n"
     "    float b = radius - distY;\n"
     "    circle = clamp((radius - sqrt(a * a + b * b)) * scale, 0.0, 1.0);\n"
     "  } else circle = clamp(min(distX, distY) * scale, 0.0, 1.0);\n"
     "  gl_FragColor = texColor * v_color * circle;\n"
     "}\n",
     {{"radius", P::Float, 0, {2.5f}}, {"scale", P::Float, 0, {1}}, {"centerX", P::Float, 0, {}},
      {"centerY", P::Float, 0, {}}, {"width", P::Float, 0, {}}, {"height", P::Float, 0, {}}}},

    {"Shader/RoundedCornerEffectBlur", nullptr,
     FS_HEAD_POS
     "uniform sampler2D s1;\n"
     "uniform vec4 blurColor;\n"
     "uniform float blurEnabled;\n"
     "uniform float radius;\n"
     "uniform float scale;\n"
     "uniform float width;\n"
     "uniform float height;\n"
     "uniform float screenWidth;\n"
     "uniform float screenHeight;\n"
     "void main() {\n"
     "  float posX = v_tex.x * width;\n"
     "  float posY = v_tex.y * height;\n"
     "  float distX = min(posX, abs(posX - width));\n"
     "  float distY = min(posY, abs(posY - height));\n"
     "  float circle;\n"
     "  if (distX < radius && distY < radius) {\n"
     "    float a = radius - distX;\n"
     "    float b = radius - distY;\n"
     "    circle = clamp((radius - sqrt(a * a + b * b)) * scale, 0.0, 1.0);\n"
     "  } else circle = clamp(min(distX, distY) * scale, 0.0, 1.0);\n"
     "  if (radius == 0.0) circle = 1.0;\n"
     "  vec4 textureSample = texture2D(s0, v_tex);\n"
     "  vec2 screenUV = vec2(v_fc.x / screenWidth, 1.0 - v_fc.y / screenHeight);\n"
     "  vec4 blurSample = texture2D(s1, screenUV) * blurEnabled;\n"
     "  gl_FragColor = ((blurSample * blurColor * (1.0 - v_color.a) + v_color) * textureSample.r\n"
     "                  + textureSample * (1.0 - textureSample.r) * blurColor) * circle;\n"
     "}\n",
     {{"sprBlur", P::Tex, 1, {}}, {"blurColor", P::Vec4, 0, {}}, {"blurEnabled", P::Float, 0, {1}},
      {"radius", P::Float, 0, {2.5f}}, {"scale", P::Float, 0, {1}}, {"width", P::Int, 0, {}}, {"height", P::Int, 0, {}},
      {"textureWidth", P::Int, 0, {}}, {"textureHeight", P::Int, 0, {}}, {"screenWidth", P::Int, 0, {}},
      {"screenHeight", P::Int, 0, {}}}},

    {"Shader/SaturationFilter", nullptr,
     FS_HEAD
     "uniform float percentage;\n"
     "void main() {\n"
     "  vec4 color = texture2D(s0, v_tex);\n"
     "  float intensity = dot(color.rgb, vec3(0.3, 0.59, 0.11));\n"
     "  gl_FragColor = mix(color, vec4(intensity, intensity, intensity, color.a), percentage) * v_color;\n"
     "}\n",
     {{"percentage", P::Float, 0, {}}}},

    {"Shader/ShockEffect", nullptr,
     FS_HEAD
     "uniform float mark0;\n"
     "uniform float mark1;\n"
     "uniform float mark2;\n"
     "void main() {\n"
     "  vec4 color = texture2D(s0, v_tex) * v_color;\n"
     "  float sum = 0.4 * color.r + 0.2 * color.g + 0.4 * color.b;\n"
     "  if (sum <= mark0) gl_FragColor = color;\n"
     "  else if (sum <= mark1) gl_FragColor = vec4(1.0, 1.0, 0.482, 1.0) * color.a * v_color.a;\n"
     "  else if (sum <= mark2) gl_FragColor = vec4(0.063, 0.129, 0.192, 1.0) * color.a * v_color.a;\n"
     "  else gl_FragColor = vec4(1.0, 1.0, 1.0, 1.0) * color.a * v_color.a;\n"
     "}\n",
     {{"mark0", P::Float, 0, {}}, {"mark1", P::Float, 0, {}}, {"mark2", P::Float, 0, {}}}},

    {"Shader/ThanosShader", nullptr,
     FS_HEAD
     "uniform sampler2D s1;\n"
     "uniform float Percentage;\n"
     "uniform vec2 Scale;\n"
     "void main() {\n"
     "  vec4 texColor = texture2D(s0, v_tex);\n"
     "  if ((Percentage > 0.0 && Percentage >= texture2D(s1, v_tex * Scale).r) || Percentage == 1.0)\n"
     "    gl_FragColor = vec4(0.0, 0.0, 0.0, 0.0);\n"
     "  else gl_FragColor = texColor * v_color;\n"
     "}\n",
     {{"NoiceTexture", P::Tex, 1, {1}}, {"Percentage", P::Float, 0, {}}, {"Scale", P::Vec2, 0, {}}}},

    {"Shader/WaleShader", nullptr,
     FS_HEAD
     "uniform float Offset;\n"
     "uniform float Period;\n"
     "uniform float Time;\n"
     "void main() {\n"
     "  vec2 c = v_tex;\n"
     "  c.x += sin(c.y * Period + Time) * Offset;\n"
     "  gl_FragColor = texture2D(s0, c) * v_color;\n"
     "}\n",
     {{"Offset", P::Float, 0, {}}, {"Period", P::Float, 0, {}}, {"Time", P::Float, 0, {}}}},

    {"Shader/WobbleShader", nullptr,
     FS_HEAD
     "uniform float width;\n"
     "uniform float height;\n"
     "uniform float scale;\n"
     "uniform float brightness;\n"
     "uniform float offset;\n"
     "uniform float offsetWidth;\n"
     "uniform float offsetHeight;\n"
     "uniform float pixelSnap;\n"
     "uniform float gridPhaseY;\n"
     "void main() {\n"
     "  const float PI = 3.1415926538;\n"
     "  float gameW = width / scale;\n"
     "  float gameH = height / scale;\n"
     "  float gy = v_tex.y * gameH;\n"
     "  float snappedY = floor(gy - gridPhaseY) + gridPhaseY + 0.5;\n"
     "  float waveY = mix(gy, snappedY, pixelSnap);\n"
     "  float amp = sin(offset + waveY / offsetHeight * PI + PI / 2.0) * offsetWidth;\n"
     "  amp = mix(amp, floor(amp + 0.5), pixelSnap);\n"
     "  float offsetX = amp / gameW;\n"
     "  gl_FragColor = texture2D(s0, vec2(v_tex.x + offsetX, v_tex.y)) * (1.0 - brightness) + vec4(1.0, 1.0, 1.0, 1.0) * brightness;\n"
     "}\n",
     {{"width", P::Int, 0, {}}, {"height", P::Int, 0, {}}, {"scale", P::Float, 0, {}}, {"brightness", P::Float, 0, {}},
      {"offset", P::Float, 0, {}}, {"offsetWidth", P::Float, 0, {}}, {"offsetHeight", P::Float, 0, {}},
      {"pixelSnap", P::Float, 0, {}}, {"gridPhaseY", P::Float, 0, {}}}},
};
