// Effects: game .fx shaders re-implemented per backend (GLSL / Cg), looked up by asset name.
#include "gfx_internal.h"
#include "platform.h"
#include <cstring>
#include <unordered_map>

using namespace ::System;

namespace fw::gfx {

// Shader sources, hand-ported from ProjectZ.Content/Content/Shader/*.fx (see effects_src.inc).
#include "effects_src.inc"

const EffectDef* findEffect(const char* name) {
    for (auto& d : kEffects)
        if (std::strcmp(d.name, name) == 0) return &d;
    return nullptr;
}

}  // namespace fw::gfx

namespace Microsoft::Xna::Framework::Graphics {
using namespace fw::gfx;

Effect* Effect::Load(GraphicsDevice* gd, String* name) {
    auto* e = New<Effect>();
    e->device = gd;
    e->name = name;
    e->parameters = New<EffectParameterCollection>();
    e->parameters->list = New<::System::Collections::Generic::List<EffectParameter*>>();
    auto* pass = New<EffectPass>();
    pass->effect = e;
    auto* passes = New<EffectPassCollection>();
    passes->pass = pass;
    e->technique = New<EffectTechnique>();
    e->technique->passes = passes;
    const EffectDef* def = findEffect(name->ToUtf8());
    if (def) {
        for (size_t i = 0; i < def->params.size(); i++) {
            auto* p = New<EffectParameter>();
            p->effect = e;
            p->name = String::FromUtf8(def->params[i].name);
            p->index = (int32_t)i;
            std::memcpy(p->value, def->params[i].def, sizeof(float) * 4);
            e->parameters->list->Add(p);
        }
    }
    // Compile at load time so the first frame using the effect doesn't hitch.
    effectProgram(e);
    return e;
}
Type* Effect::__type() {
    static Type* t = Type::Create(u"Effect", u"Microsoft.Xna.Framework.Graphics.Effect", Object::__type());
    return t;
}
EffectParameter* EffectParameterCollection::get_Item(String* name) {
    for (int32_t i = 0; i < list->size; i++)
        if (String::op_Equality(list->items->data[i]->name, name)) return list->items->data[i];
    // Unknown parameter (unported effect): create a dummy so SetValue works.
    auto* p = New<EffectParameter>();
    p->name = name;
    list->Add(p);
    return p;
}
void EffectParameter::SetValue(float v) { value[0] = v; }
void EffectParameter::SetValue(int32_t v) { value[0] = (float)v; }
void EffectParameter::SetValue(Vector2 v) { value[0] = v.X; value[1] = v.Y; }
void EffectParameter::SetValue(Vector3 v) { value[0] = v.X; value[1] = v.Y; value[2] = v.Z; }
void EffectParameter::SetValue(Vector4 v) { value[0] = v.X; value[1] = v.Y; value[2] = v.Z; value[3] = v.W; }
void EffectParameter::SetValue(Matrix m) { std::memcpy(value, &m.M11, sizeof(float) * 16); }
void EffectParameter::SetValue(Texture* t) { texture = t; }
void EffectPass::Apply() {
    if (effect && effect->device) effect->device->activeEffect = effect;
}

}  // namespace Microsoft::Xna::Framework::Graphics
