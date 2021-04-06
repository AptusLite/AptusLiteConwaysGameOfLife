using System;
using System.IO;
using System.Media;

namespace ConwaysGameOfLife.Sound
{
    public class SoundManager
    {
        private static readonly SoundPlayer s_player = new SoundPlayer(Resource.Theme);

        static SoundManager()
        {
            s_player.Stream = Resource.Theme;
        }

        public static void PlayTheme()
        {
            s_player.PlayLooping();
        }

        public static void StopTheme()
        {
            s_player.Stop();
        }
    }
}