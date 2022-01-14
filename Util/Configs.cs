using IPA.Utilities;
using System.IO;

namespace BetterSongSearch.Util {
	static class ConfigUtil {
		public static readonly string ConfigDir = Path.Combine(UnityGame.UserDataPath, "BetterSongSearch");
		public static readonly string PresetDir = Path.Combine(ConfigDir, "Presets");
		public static string GetPresetPath(string name) {
			return Path.Combine(PresetDir, $"{name}.json");
		}
	}
}
