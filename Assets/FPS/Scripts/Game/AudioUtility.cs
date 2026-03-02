using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    public class AudioUtility
    {
        static AudioManager s_AudioManager;

        public enum AudioGroups
        {
            Music,
            Sfx,
            UI,
            Ambience,
            Ducker,
            DamageTick,
            Impact,
            EnemyDetection,
            Pickup,
            WeaponShoot,
            WeaponOverheat,
            WeaponChargeBuildup,
            WeaponChargeLoop,
            HUDVictory,
            HUDObjective,
            EnemyAttack,
            // 与 MainAudioMixer 一致，以下为补充的混音组
            EnemyDeath,
            EnemyMovement,
            Jetpack,
            PlayerMovement,
            General,
            Misc
        }

        public static void CreateSFX(AudioClip clip, Vector3 position, AudioGroups audioGroup, float spatialBlend,
            float rolloffDistanceMin = 1f)
        {
            _ = SfxService.Instance;
            SfxService.Play(clip, position, audioGroup, spatialBlend, rolloffDistanceMin);
        }

        public static void PlaySfx(SfxKey key, Vector3 position)
        {
            _ = SfxService.Instance;
            if (key != SfxKey.None)
            {
                _ = SfxService.Play(key, position);
            }
        }

        /// <summary>
        /// 播放音效，音源会挂载到 parent 下（传入父物体用于挂载），播放期间随父物体移动，播完后自动解挂并归还对象池；parent 为 null 则在世界坐标 position 处播放。
        /// </summary>
        public static void PlaySfx(SfxKey key, Vector3 position, Transform parent)
        {
            _ = SfxService.Instance;
            if (key != SfxKey.None)
            {
                _ = SfxService.Play(key, position, parent);
            }
        }

        public static AudioMixerGroup GetAudioGroup(AudioGroups group)
        {
            if (s_AudioManager == null)
                s_AudioManager = GameObject.FindObjectOfType<AudioManager>();

            if (s_AudioManager == null)
            {
                return null;
            }

            AudioMixerGroup[] groups = s_AudioManager.FindMatchingGroups(group.ToString());
            if ((groups == null || groups.Length == 0) && TryGetFallbackGroupName(group, out string fallbackName))
            {
                groups = s_AudioManager.FindMatchingGroups(fallbackName);
            }

            if (groups == null || groups.Length == 0)
            {
                Debug.LogWarning("Didn't find audio group for " + group);
                return null;
            }

            return groups[0];
        }

        static bool TryGetFallbackGroupName(AudioGroups group, out string fallbackName)
        {
            switch (group)
            {
                case AudioGroups.Music:
                    fallbackName = "Ambience";
                    return true;
                case AudioGroups.Sfx:
                    fallbackName = "SFX";
                    return true;
                case AudioGroups.UI:
                    fallbackName = "HUDObjective";
                    return true;
                case AudioGroups.Ambience:
                    fallbackName = "Ambience";
                    return true;
                case AudioGroups.Ducker:
                    fallbackName = "EnemyDetection";
                    return true;
                default:
                    fallbackName = null;
                    return false;
            }
        }

        public static void SetMasterVolume(float value)
        {
            _ = AudioMixerController.Instance;
            AudioMixerController.SetLinearVolume(AudioMixerController.MasterVolumeParameter, value);
        }

        public static float GetMasterVolume()
        {
            _ = AudioMixerController.Instance;
            return AudioMixerController.GetLinearVolume(AudioMixerController.MasterVolumeParameter, 1f);
        }

        public static void SetMusicVolume(float value)
        {
            _ = AudioMixerController.Instance;
            AudioMixerController.SetLinearVolume(AudioMixerController.MusicVolumeParameter, value);
        }

        public static float GetMusicVolume(float fallback = 1f)
        {
            _ = AudioMixerController.Instance;
            return AudioMixerController.GetLinearVolume(AudioMixerController.MusicVolumeParameter, fallback);
        }

        public static void SetSfxVolume(float value)
        {
            _ = AudioMixerController.Instance;
            AudioMixerController.SetLinearVolume(AudioMixerController.SfxVolumeParameter, value);
        }

        public static float GetSfxVolume(float fallback = 1f)
        {
            _ = AudioMixerController.Instance;
            return AudioMixerController.GetLinearVolume(AudioMixerController.SfxVolumeParameter, fallback);
        }
    }
}
