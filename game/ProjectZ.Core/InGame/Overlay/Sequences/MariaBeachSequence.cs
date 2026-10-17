using System;
using System.IO;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Overlay.Sequences
{
    class MarinBeachSequence : GameSequence
    {
        struct Seagull
        {
            public SeqAnimation Animation;
            public Vector2 Position;
            public Vector2 Direction;
            public float GlideCounter;
        }
        private readonly Seagull[] _seagulls = new Seagull[2];
        private readonly Seagull[] _smallSeagulls = new Seagull[5];

        private SeqAnimation _aniLink;
        private SeqAnimation _aniMarin;

        private Vector2 _seagullCenter;

        private float _shoreSoundCounter;
        private float _birdSoundCounter;

        private Color _beachSkyColor;

        // Values configurable via lahdmod.
        private int beach_sequence_sky_red = 66;
        private int beach_sequence_sky_grn = 89;
        private int beach_sequence_sky_blu = 254;

        public MarinBeachSequence()
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "MarinBeachSequence.lahdmod");
            ModFile.Parse(modFile, this);

            // Sets the color of the sky at the top of the screen.
            _beachSkyColor = new Color(beach_sequence_sky_red, beach_sequence_sky_grn, beach_sequence_sky_blu);

            // Sets the width and height of the sequence window.
            _sequenceWidth = 320;
            _sequenceHeight = 128 + 48;
            _usePixelGrid = true;
        }

        public override void OnStart()
        {
            Sprites.Clear();
            SpriteDict.Clear();

            var top = 48;
            var center = 160;

            // sky background
            Sprites.Add(new SeqColor(new Rectangle(0, 0, _sequenceWidth, top), _beachSkyColor, 0));

            // beach
            Sprites.Add(new SeqSprite("beach_background", new Vector2(0, top), 0));
            Sprites.Add(new SeqSprite("beach_palm", new Vector2(56, top + 32), 2));
            Sprites.Add(new SeqSprite("beach_palm", new Vector2(208, top + 32), 2));

            // waves
            for (int i = 0; i < 20; i++)
            {
                Sprites.Add(new SeqAnimation("Sequences/beach_water", "top", new Vector2(i * 16, top + 56), 1));
                Sprites.Add(new SeqAnimation("Sequences/beach_water", "bottom", new Vector2(i * 16, top + 72), 1));
            }

            // link and marin
            _aniLink = new SeqAnimation("link0", "ocean_sit", new Vector2(center - 14, top + 92), 1) { Shader = Resources.ColorShader, Color = Game1.GameManager.CloakColor };
            AddDrawable("link", _aniLink);
            _aniMarin = new SeqAnimation("NPCs/marin", "ocean_sit", new Vector2(center + 1, top + 92), 1);
            AddDrawable("marin", _aniMarin);

            _seagulls[0] = new Seagull()
            {
                Position = new Vector2(center - 90, top - 48),
                Direction = new Vector2(0.5f, 0.25f),
                GlideCounter = 1500,
            };
            _seagulls[1] = new Seagull()
            {
                Position = new Vector2(center + 64, top),
                Direction = new Vector2(-0.5f, 0.2f),
                GlideCounter = 2500,
            };

            Sprites.Add(_seagulls[0].Animation = new SeqAnimation("Sequences/seagull", "glide", _seagulls[0].Position, 1));
            Sprites.Add(_seagulls[1].Animation = new SeqAnimation("Sequences/seagull", "glide", _seagulls[1].Position, 1));

            _seagullCenter = new Vector2(center, top + 38);

            for (int i = 0; i < _smallSeagulls.Length; i++)
            {
                var posX = Game1.RandomNumber.Next(0, 32) - 16;
                var posY = Game1.RandomNumber.Next(0, 12);

                _smallSeagulls[i] = new Seagull()
                {
                    Position = new Vector2(_seagullCenter.X + posX, _seagullCenter.Y + posY),
                    Direction = new Vector2(0.5f, 0.25f),
                    //GlideCounter = Game1.RandomNumber.Next(0, 5000),
                };

                Sprites.Add(_smallSeagulls[i].Animation = new SeqAnimation("Sequences/seagull small", "idle", _smallSeagulls[i].Position, i + 1) { RoundPosition = true });
                _smallSeagulls[i].Animation.Animator.SetTime(Game1.RandomNumber.Next(0, _smallSeagulls[i].Animation.Animator.CurrentFrame.FrameTime));
            }

            // start the sequence path
            Game1.GameManager.SaveManager.SetString("seq_beach", "0");
            Game1.GameManager.StartDialogPath("seq_beach");

            base.OnStart();
        }

        public override void Update()
        {
            base.Update();

            UpdateSeagulls();
        }

        private void UpdateSeagulls()
        {
            _birdSoundCounter -= Game1.DeltaTime;
            if (_birdSoundCounter < 0)
            {
                _birdSoundCounter += Game1.RandomNumber.Next(1000, 3500);
                Game1.AudioManager.PlaySoundEffect("D360-33-21");
            }

            _shoreSoundCounter -= Game1.DeltaTime;
            if (_shoreSoundCounter < 0)
            {
                _shoreSoundCounter += 3500;
                Game1.AudioManager.PlaySoundEffect("D378-15-0F");
            }

            // update the seagulls far away
            for (int i = 0; i < _smallSeagulls.Length; i++)
            {
                _smallSeagulls[i].GlideCounter -= Game1.DeltaTime;

                if (_smallSeagulls[i].GlideCounter < 0)
                {
                    _smallSeagulls[i].GlideCounter = Game1.RandomNumber.Next(500, 1250);

                    // glide down
                    if (Game1.RandomNumber.Next(0, 4) == 0)
                    {
                        _smallSeagulls[i].Direction.X *= 0.25f;
                        _smallSeagulls[i].Direction.Y = 0.1f;
                        _smallSeagulls[i].Animation.Animator.Play("glide");
                    }
                    else
                    {
                        var centerDirection = _seagullCenter - _smallSeagulls[i].Position;
                        if (centerDirection != Vector2.Zero)
                        {
                            var distance = MathHelper.Clamp(1 - centerDirection.Length() / 48, 0, 1);
                            var radiant = MathF.Atan2(centerDirection.Y, centerDirection.X) + (MathF.PI - Game1.RandomNumber.Next(0, 628) / 100f) * distance;

                            _smallSeagulls[i].Direction = new Vector2(MathF.Cos(radiant), MathF.Sin(radiant)) * (Game1.RandomNumber.Next(8, 15) / 100f);
                        }

                        _smallSeagulls[i].Animation.Animator.Play("idle");
                    }
                }

                _smallSeagulls[i].Direction.X *= (float)Math.Pow(0.99f, Game1.TimeMultiplier);
                _smallSeagulls[i].Position += _smallSeagulls[i].Direction * Game1.TimeMultiplier;
                _smallSeagulls[i].Animation.Position = _smallSeagulls[i].Position;
            }


            // update the closer seagulls
            for (int i = 0; i < _seagulls.Length; i++)
            {
                if (_seagulls[i].GlideCounter > 0)
                {
                    _seagulls[i].GlideCounter -= Game1.DeltaTime;
                    if (_seagulls[i].GlideCounter <= 0)
                    {
                        _seagulls[i].Direction.Y = -_seagulls[i].Direction.Y;
                        _seagulls[i].Animation.Animator.Play("idle");
                    }
                }

                _seagulls[i].Position += _seagulls[i].Direction * Game1.TimeMultiplier;
                _seagulls[i].Animation.Position = _seagulls[i].Position;
            }
        }
    }
}
