using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.InGame.Graphics
{
    // Maybe this was actually unnecessary and we could have just used variations of Effects.
    // Currently we do not need to dynamically set the parameters and only use this for the damage shader that has 2 variants.
    public class SpriteShader
    {
        public Effect Effect;
        public Dictionary<string, float> FloatParameter = new Dictionary<string, float>();
        public Dictionary<string, Vector4> Vector4Parameter = new();

        public SpriteShader(Effect effect)
        {
            Effect = effect;
        }

        public Vector4 this[string key]
        {
            get => Vector4Parameter[key];
            set => Vector4Parameter[key] = value;
        }
    }
}
