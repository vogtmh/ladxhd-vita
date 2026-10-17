using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Audio
{
    public class AudioManager
    {
        private class PlayingSoundEffect
        {
            public bool LowerMusicVolume;
            public float Volume;
            public double EndTime;
            public SoundEffectInstance Instance;
        }
        private float _musicVolumeMultiplier = 1.0f;
        private float _duckMultiplier = 1.0f;

        private GameManager GM => Game1.GameManager;

        // Sound effects that are currently playing.
        private Dictionary<string, PlayingSoundEffect> CurrentSoundEffects = new Dictionary<string, PlayingSoundEffect>();

        // 0: Map Music, 1: PowerUp Music, 2: Marin Singing
        private const int MusicChannels = 3;
        private int[] _musicArray = new int[MusicChannels];

        // Counters used to stop music.
        private float[] _musicCounter = new float[MusicChannels];

        // Tracks the current music track fade and the next track to play.
        private bool  _fadeOut;
        private int   _fadeOutNextSong;
        private int   _fadeOutPriority = -1;
        private float _fadeOutTimeTick = 0;
        private float _fadeOutDuration = 0;

        // Tracks a lower priority music track that was set but can't be changed immediately.
        private int   _backgroundSong = -1;
        private int   _backgroundPriority = -1;

        // Muting the sound requires overwriting effect volume so store user setting.
        private int _curEffectVolume = GameSettings.EffectVolume;
        private bool _lastStateSet;
        private bool _muteInactive;
        private bool _mp3WasPlaying;

        // The MP3 player allows playing custom music.
        internal MusicPlayer _musicPlayer = new MusicPlayer();

        // Quick reference to "ObjLink" in MapManager.
        private ObjLink Link => MapManager.ObjLink;

        // Checks if the track is village music or powerup music. The "Moblin" music is included in village music (13).
        private bool IsVillageMusic(int musicTrack) => (musicTrack == 3 || musicTrack == 10);
        private bool IsPowerupMusic(int musicTrack) => (musicTrack == 38 || musicTrack == 72);

        public bool MusicLocked;

        public void HandleInactiveWindow(bool IsActive)
        {
            // We don't need this to run every single game tick.
            if (IsActive != _lastStateSet)
            {
                if (!IsActive && GameSettings.MuteInactive)
                {
                    Game1.GbsPlayer.SetVolume(0f);
                    _musicPlayer.SetVolume(0f);
                    _muteInactive = true;
                }
                else
                {
                    var vol = GameSettings.MusicVolume / 100.0f;
                    Game1.GbsPlayer.SetVolume(vol);
                    _musicPlayer.SetVolume(vol);
                    _muteInactive = false;
                }
                _curEffectVolume = _muteInactive ? 0 : GameSettings.EffectVolume;

                foreach (var soundEffect in CurrentSoundEffects)
                    soundEffect.Value.Instance.Volume = soundEffect.Value.Volume * _curEffectVolume / 100f * Values.SoundEffectVolumeMult;
            }
            _lastStateSet = IsActive;
        }

        public void InitGuardianAcorn()
        {
            // Grant the achievement for grabbing both powerups
            // simultaneously but deactivate the first one grabbed.
            if (GM.PieceOfPowerIsActive)
            {
                AchievementManager.Earn(7);
                StopPieceOfPower();
            }
            // Start the effect and reset damage counter.
            GM.GuardianAcornIsActive = true;
            GM.GuardianAcornDamageCount = 0;

            // Start the music if it's enabled.
            if (!GameSettings.MutePowerups)
                StartPowerupMusic(0);
            else
                PlaySoundEffect("D360-23-17");
        }

        public void StopGuardianAcorn()
        {
            StopPowerupMusic();
            GM.GuardianAcornIsActive = false;
        }

        public void InitPieceOfPower()
        {
            // Grant the achievement for grabbing both powerups
            // simultaneously but deactivate the first one grabbed.
            if (GM.GuardianAcornIsActive)
            {
                AchievementManager.Earn(7);
                StopGuardianAcorn();
            }
            // Start the effect and reset damage counter.
            GM.PieceOfPowerIsActive = true;
            GM.PieceOfPowerDamageCount = 0;

            // Start the music if it's enabled.
            if (!GameSettings.MutePowerups)
                StartPowerupMusic(0);
            else
                PlaySoundEffect("D360-23-17");
        }

        public void StopPieceOfPower()
        {
            StopPowerupMusic();
            GM.PieceOfPowerIsActive = false;
        }

        public void StartPowerupMusic(int Variation)
        {
            // 0: Delayed with sound effect
            // 1: Music starts instantly.
            int trackId = Variation == 0 ? 38 : 72;
            SetMusic(trackId, 1);

            // @HACK: When music is restarted for any reason: map/area transition, healing
            // from a great fairy, etc. we want the version without the starting sound effect.
            if (Variation == 0)
            {
                Game1.GbsPlayer.CurrentTrack = 72;
                _musicArray[1] = 72;
            }
        }

        private void StopPowerupMusic()
        {
            // When inside a village, revert the hack which stores the piece of power music in slot 1 and
            // the village music inside slot 2. This is mostly for Mabe Village where dogs can attack.
            if (IsPowerupMusic(_musicArray[0]) && IsVillageMusic(_musicArray[1]))
            {
                _musicArray[0] = _musicArray[1];
                _musicArray[1] = -1;
                return;
            }
            // Any other time we can just set slot 2 to -1.
            SetMusic(-1, 1, true);
        }

        public void UpdateMusic()
        {
            // If "StopMusic()" was called, this stops the music after the time set to "_musicCounter[i]".
            for (var i = 0; i < MusicChannels; i++)
            {
                // Nothing to do if it's already zero.
                if (_musicCounter[i] == 0)
                    continue;

                // Subtract delta time from the counter.
                _musicCounter[i] -= Game1.DeltaTime;

                // Remove the song from the current priority and play whatever is in the next priority down.
                if (_musicCounter[i] <= 0)
                {
                    _musicArray[i] = -1;
                    _musicCounter[i] = 0;
                    PlayMusic();
                }
            }

            // If the current track was set to fade out before the next track is played.
            if (_fadeOut)
            {
                // Increase the fade out amount relative to delta time.
                _fadeOutTimeTick += Game1.DeltaTime;

                // Calculate the reduction in volume for this frame.
                var playerVolume = GameSettings.MusicVolume / 100f;
                var transitionState = _fadeOutTimeTick / _fadeOutDuration;
                var newVolume = playerVolume - MathHelper.Clamp(transitionState, 0, playerVolume);

                // While a powerup is active, separate standard overworld music changes (like Mysterious Forest to Goponga Swamp) from village music 
                // changes (to/from Mabe Village or Animal Village). We want music to fade when entering a village, but not when changing "areas".
                var overworldNoFade = MapManager.ObjLink.HasPowerup && _fadeOutNextSong != 13 && _musicArray[2] != 13 && !IsVillageMusic(_fadeOutNextSong) && !IsVillageMusic(_musicArray[1]);

                // Conditions of when NOT to fade. There will probably be more conditions added over time, so just make a list.
                bool[] conditions = new bool[4];
                conditions[0] = newVolume <= 0;             // The fade is finished so just set the track.
                conditions[1] = Link.IsTransitioning;       // Do not fade on initial load or between map transitions.
                conditions[2] = _musicArray[2] == 33;       // Do not fade when owl music is playing.
                conditions[3] = overworldNoFade;            // Do not fade when under the effect of a powerup unless it's a village.

                // Loop through the conditions.
                for (int i = 0; i < conditions.Length; i++)
                {
                    // If any of the conditions pass, either finish or skip fading and play the next track.
                    if (conditions[i])
                    {
                        // If a track was stored, set it to the proper slot now that the fade has ended.
                        if (_backgroundSong > -1)
                        {
                            SetMusic(_backgroundSong, _backgroundPriority, false);
                            _backgroundSong = -1;
                            _backgroundPriority = -1;
                        }
                        // Set the volume to the player's maximum and play the next track.
                        Game1.AudioManager.SetMusicVolume(playerVolume);
                        Game1.AudioManager.SetMusic(_fadeOutNextSong, _fadeOutPriority, true);

                        // Reset everything to default values.
                        _fadeOut = false;
                        _fadeOutPriority = -1;
                        _fadeOutDuration = 0;
                        _fadeOutTimeTick = 0;
                        return;
                    }
                }
                // Apply fade to the currently playing music track.
                Game1.AudioManager.SetMusicVolume(newVolume);
            }
        }

        public bool CheckSetMusicConditions(int trackID, int priority)
        {
            // Don't restart the overworld track if the version with the intro was already started. But if it's the part
            // of the game where Marin joins the player and the beach photo is taken, we need to allow song 4 to replace 48.
            if (trackID == 4 && _musicArray[priority] == 48 && GM.SaveManager.GetString("maria_state") != "3")
                return false;

            // Make sure to not restart the music while showing the overworld in the final sequence. 
            if (priority != 2 && _musicArray[2] == 62)
                return false;

            // When entering a village (3: Mabe Village, 10: Animal Village) with (72: Piece of Power Music)
            // backup the piece of power music and force the new song onto the piece of power slot.
            if (IsVillageMusic(trackID) && IsPowerupMusic(_musicArray[1]) && priority == 0 && !Link.IsTransitioning)
            {
                _musicArray[0] = 72;
                _musicArray[1] = trackID;
            }
            // When leaving the village, restore piece of power music and write new track to it's proper slot.
            else if (!IsVillageMusic(trackID) && IsPowerupMusic(_musicArray[0]) && priority == 0 && !Link.IsTransitioning)
            {
                _musicArray[0] = trackID;
                _musicArray[1] = 72;
            }
            // In any other cases, just handle music normally.
            else
            {
                _musicArray[priority] = trackID;
            }
            // Play the music.
            return true;
        }

        public void SetMusic(int trackID, int priority, bool startPlaying = true)
        {
            // See if we should play music and if there is any nuances to take care of before playing the music.
            if (CheckSetMusicConditions(trackID, priority))
                PlayMusic(startPlaying);
        }

        public void SetMusicFadeTransition(int trackID, int priority, float fadeTime)
        {
            // If a track is already set to fade out, check if the priority is greater than or equal to the
            // current priority. If it's higher or equal to, we overwrite the "next" track with this one.
            if (priority >= _fadeOutPriority)
            {
                // We don't want to lose the track that was set to fade out, so at least store it in the proper
                // priority slot. If it's the same priority, it will simply get overwritten below this check.
                if (priority > _fadeOutPriority && _fadeOutPriority > -1)
                    SetMusic(_fadeOutNextSong, _fadeOutPriority, false);

                // Set the track to switch to when the fade ends. 
                _fadeOut = true;
                _fadeOutNextSong = trackID;
                _fadeOutPriority = priority;
                _fadeOutDuration = fadeTime;
                _fadeOutTimeTick = 0;
            }
            // If it's a lower priority than the current fade track, it still needs to be stored, but we don't
            // want to store it right away because it may be the track that is currently fading out. So store it
            // in the "_background" variables until the fade ends where it will then be set to its proper slot.
            else
            {
                _backgroundSong = trackID;
                _backgroundPriority = priority;
            }
        }

        public int[] GetMusicTracks()
        {
            // Returns the currently stored tracks in priorities 0-2.
            return _musicArray;
        }

        public bool IsMusicPlayerActive()
        {
            return _musicPlayer.IsPlaying;
        }

        public void SetMusicPlayerStopTime(float seconds)
        {
            _musicPlayer.SetStopTime(seconds);
        }

        public bool IsMusicPlayerStopped()
        {
            return _musicPlayer.WasStopped;
        }

        private string GetModMusicPath(int trackId)
        {
            var path = Path.Combine(Values.PathMusicMods, $"{trackId}.ogg");
            return GameFS.Exists(path) ? path : null;
        }

        public void PlayMusic(bool startPlaying = true)
        {
            // Suppress playback during map transitions. When the map is fully loaded, it will
            // call "PlayMusic" again so if the priority is active the song will not be skipped.
            if (startPlaying && Link != null && Link.IsTransitioning)
                startPlaying = false;

            // Search the music priority array from highest to lowest to figure out which track to play.
            for (var i = MusicChannels - 1; i >= 0; i--)
            {
                // We found a track to play (it was not -1 which means the slot is empty).
                if (_musicArray[i] >= 0)
                {
                    // Pull out the song number and potentially the path to a custom music file.
                    var songNumber = (byte)_musicArray[i];
                    var songPath = GetModMusicPath(songNumber);

                    // If the track doesn't exist or the path is invalid.
                    if (!string.IsNullOrEmpty(songPath))
                    {
                        // Stop the GBS Player before playing a new song.
                        Game1.GbsPlayer.Stop();

                        // If a custom music track is not currently playing then start the new track.
                        if (startPlaying)
                        {
                            _musicPlayer.SetVolume(_muteInactive ? 0f : GameSettings.MusicVolume / 100.0f);
                            _musicPlayer.Play(songPath, songNumber);
                        }
                        // When transitioning maps custom music must be stopped so it doesn't spill over
                        // into the next map. The "EndTransition" function will take care of starting the
                        // next track in the "MapTransitionSystem" so the track will still see a "PlayMusic".
                        else if (_musicPlayer.CurrentTrack != songNumber)
                        {
                            _musicPlayer.Stop();
                        }
                        return;
                    }
                    // If a custom track can not be found then fall back to the GBS Player music.
                    _musicPlayer.Stop();
                    if (Game1.GbsPlayer.CurrentTrack != songNumber)
                        Game1.GbsPlayer.StartTrack(songNumber);
                    if (startPlaying)
                        Game1.GbsPlayer.Play();
                    return;
                }
            }
            // If there isn't a valid track in any slot then stop the music.
            _musicPlayer.Stop();
            Game1.GbsPlayer.Stop();
        }

        public void StopMusic(bool reset = false)
        {
            if (reset)
                ResetMusic();
            _musicPlayer.Stop();
            Game1.GbsPlayer.Stop();
        }

        public void StopMusic(int time, int priority)
        {
            // Schedule a music slot to be cleared after "time" milliseconds.
            _musicCounter[priority] = time;
        }

        public void StopAllMusic(int time)
        {
            // Schedules all music slots to be cleared after "time" milliseconds.
            for (var i = 0; i < MusicChannels; i++)
                StopMusic(time, i);
        }

        public void SetMusicStopTime(float stopTime)
        {
            Game1.GbsPlayer.SoundGenerator.SetStopTime(stopTime);
            SetMusicPlayerStopTime(stopTime);
        }

        public bool GetMusicStopTimeExpired()
        {
            if (IsMusicPlayerActive())
                return _musicPlayer.WasStopped;
            return Game1.GbsPlayer.SoundGenerator.WasStopped && Game1.GbsPlayer.SoundGenerator.FinishedPlaying();
        }

        public void PauseMusic()
        {
            if (MusicLocked)
                return;

            _mp3WasPlaying = _musicPlayer.IsPlaying;
            if (_mp3WasPlaying)
                _musicPlayer.Pause();
            else
                Game1.GbsPlayer.Pause();
        }

        public void ResumeMusic()
        {
            if (MusicLocked)
                return;

            if (_mp3WasPlaying)
                _musicPlayer.Resume();
            else
                Game1.GbsPlayer.Resume();
        }

        public void ResetMusic()
        {
            for (var i = 0; i < MusicChannels; i++)
            {
                _musicArray[i] = -1;
                _musicCounter[i] = 0;
            }
        }

        public float GetMusicVolumeMultiplier()
        {
            return _musicVolumeMultiplier;
        }

        public void SetMusicVolume(float volume)
        {
            Game1.GbsPlayer.SetVolume(volume);
            _musicPlayer.SetVolume(volume);
        }

        private void ApplyMusicVolumeMultiplier()
        {
            var combined = _musicVolumeMultiplier * _duckMultiplier;
            Game1.GbsPlayer.SetVolumeMultiplier(combined);
            _musicPlayer.SetVolumeMultiplier(combined);
        }

        public void SetMusicVolumeMultiplier(float mult)
        {
            _musicVolumeMultiplier = mult;
            ApplyMusicVolumeMultiplier();
        }

        public void SetMusicPlaybackSpeed(float speed)
        {
            Game1.GbsPlayer.SetPlaybackSpeed(speed);
            _musicPlayer.SetPlaybackSpeed(speed);
        }

        public int GetCurrentMusic()
        {
            for (var i = _musicArray.Length - 1; i >= 0; i--)
                if (_musicArray[i] >= 0)
                    return _musicArray[i];
            return -1;
        }

        public void UpdateSoundEffects()
        {
            var lowerVolume = false;

            // Set the volume to 0 if window is inactive otherwise use the volume set by the player.
            _curEffectVolume = _muteInactive ? 0 : GameSettings.EffectVolume;

            // we use ToList to be able to remove entries in the foreach loop
            foreach (var soundEffect in CurrentSoundEffects.ToList())
            {
                if (CurrentSoundEffects[soundEffect.Key].LowerMusicVolume)
                    lowerVolume = true;

                // update the volume of the sound effects to match the current settings
                soundEffect.Value.Instance.Volume = CurrentSoundEffects[soundEffect.Key].Volume * _curEffectVolume / 100 * Values.SoundEffectVolumeMult;
                soundEffect.Value.Instance.IsLooped = false;

                if (soundEffect.Value.EndTime != 0 && soundEffect.Value.EndTime < Game1.TotalGameTime)
                    soundEffect.Value.Instance.Stop();

                // finished playing?
                if (soundEffect.Value.Instance.State == SoundState.Stopped)
                    CurrentSoundEffects.Remove(soundEffect.Key);
            }
            // This method is called every frame from GameManager which can cause the music
            // to get loud prematurely during transitions. Prevent this by checking for one.
            if (lowerVolume)
            {
                _duckMultiplier = 0.35f;
                ApplyMusicVolumeMultiplier();
            }
            else if (Link == null || !Link.IsTransitioning)
            {
                _duckMultiplier = 1.0f;
                ApplyMusicVolumeMultiplier();
            }
        }

        public void PauseSoundEffects()
        {
            foreach (var soundEffect in CurrentSoundEffects)
                if (soundEffect.Value.Instance.State == SoundState.Playing)
                    soundEffect.Value.Instance.Pause();
        }

        public void ContinueSoundEffects()
        {
            foreach (var soundEffect in CurrentSoundEffects)
                if (soundEffect.Value.Instance.State == SoundState.Paused)
                    soundEffect.Value.Instance.Resume();
        }

        public void PlaySoundEffect(string name, bool restart, Vector2 position, float range = 256)
        {
            var playerDistance = Link.EntityPosition.Position - position;
            var volume = 1 - playerDistance.Length() / range;

            if (volume > 0)
                PlaySoundEffect(name, restart, volume);
        }

        public void PlaySoundEffect(string name, bool restart = true, float volume = 1, float pitch = 0, bool lowerMusicVolume = false, float playtime = 0)
        {
            CurrentSoundEffects.TryGetValue(name, out var entry);

            // if the same sound is playing it will be stopped and replaced with the new instance
            if (restart && entry!= null && entry.Instance != null)
            {
                entry.Instance.Stop();
                CurrentSoundEffects.Remove(name);
            }
            if (!restart && entry != null && entry.Instance != null)
            {
                entry.Volume = volume;
                if (playtime != 0)
                    entry.EndTime = Game1.TotalGameTime + playtime;

                entry.Instance.Volume = volume * _curEffectVolume / 100f * Values.SoundEffectVolumeMult;
                entry.Instance.Pitch = pitch;
                
                return;
            }

            entry = new PlayingSoundEffect() { Volume = volume, LowerMusicVolume = lowerMusicVolume };
            entry.Instance = Resources.SoundEffects[name].CreateInstance();

            // the volume of the sound effects is higher than the music; so scale effect volume a little down
            entry.Instance.Volume = volume * _curEffectVolume / 100f * Values.SoundEffectVolumeMult;
            entry.Instance.Pitch = pitch;

            if (playtime != 0)
            {
                entry.Instance.IsLooped = true;
                entry.EndTime = Game1.TotalGameTime + playtime;
            }

            entry.Instance.Play();

            CurrentSoundEffects.Add(name, entry);
        }

        public void StopSoundEffect(string name)
        {
            if (CurrentSoundEffects.TryGetValue(name, out var entry))
                entry.Instance.Stop();
        }

        public bool IsPlaying(string name)
        {
            if (CurrentSoundEffects.TryGetValue(name, out var entry))
                return entry.Instance.State == SoundState.Playing;

            return false;
        }
    }
}
