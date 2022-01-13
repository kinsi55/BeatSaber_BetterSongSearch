using BeatSaberMarkupLanguage.Attributes;
using BetterSongSearch.UI;
using BetterSongSearch.Util;
using Newtonsoft.Json;
using SongDetailsCache.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using BeatSaberMarkupLanguage.Components.Settings;
using UnityEngine;

namespace BetterSongSearch.Configuration {

	// Building this like this is kind of shit... But then again not really... Idk
	[JsonObject(MemberSerialization.OptIn)]
	class FilterOptions : NotifiableSettingsObj {
		public const float SONG_LENGTH_FILTER_MAX = 15f;
		public const float STAR_FILTER_MAX = 14f;
		public const float NJS_FILTER_MAX = 25f;
		public const float NPS_FILTER_MAX = 12f;

		[JsonProperty] public string existingSongs { get; private set; } = (string)downloadedFilterOptions[0];
		[JsonProperty] public string existingScore { get; private set; } = (string)scoreFilterOptions[0];
		internal DateTime hideOlderThan { get; private set; } = new DateTime(2018, 5, 1);
		[JsonProperty("hideOlderThan")]
		internal int _hideOlderThan {
			get => Math.Max(0, FilterView.hideOlderThanOptions.IndexOf(hideOlderThan));
			set => hideOlderThan = FilterView.hideOlderThanOptions[Mathf.Clamp(value, 0, FilterView.hideOlderThanOptions.Count - 1)];
		}

		[JsonProperty] public float minimumSongLength { get; private set; } = 0f;

		[JsonProperty("maximumSongLength")]
		public float _maximumSongLength { get; private set; } = 20f;
		public float maximumSongLength {
			get => _maximumSongLength >= SONG_LENGTH_FILTER_MAX ? float.MaxValue : _maximumSongLength;
			private set => _maximumSongLength = value;
		}

		[JsonProperty] public string rankedState { get; private set; } = (string)rankedFilterOptions[0];

		[JsonProperty] public float minimumStars { get; private set; } = 0f;

		[JsonProperty("maximumStars")] float _maximumStars = STAR_FILTER_MAX;
		public float maximumStars {
			get => _maximumStars >= STAR_FILTER_MAX ? float.MaxValue : _maximumStars;
			private set => _maximumStars = value;
		}

		[JsonProperty] public string mods { get; private set; } = (string)modOptions[0];

		[JsonProperty] public float minimumRating { get; private set; } = 0f;
		[JsonProperty] public int minimumVotes { get; private set; } = 0;

		string _uploadersString = "";
		[JsonProperty] public string uploadersString { 
			get => _uploadersString; 
			set {
				if(_uploadersString == value)
					return;

				_uploadersString = value;
				if(value.Length == 0) {
					uploaders.Clear();
					return;
				}

				uploadersBlacklist = value[0] == '!';
				if(uploadersBlacklist)
					value = value.Substring(1);

				uploaders = value.ToLowerInvariant().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToHashSet();
			}
		}


		public bool uploadersBlacklist { get; private set; } = false;
		public HashSet<string> uploaders { get; private set; } = new HashSet<string>();



		public int difficulty_int = -1;
		[JsonProperty]
		public string difficulty {
			get => difficulty_int == -1 ? "Any" : ((MapDifficulty)difficulty_int).ToString();
			private set => difficulty_int = Enum.TryParse<MapDifficulty>(value, out var pDiff) ? (int)pDiff : -1;
		}

		public int characteristic_int = -1;
		[JsonProperty]
		public string characteristic {
			get => characteristic_int == -1 ? "Any" : ((MapCharacteristic)characteristic_int).ToString();
			private set => characteristic_int = Enum.TryParse<MapCharacteristic>(value, out var pChar) ? (int)pChar : -1;
		}

		[JsonProperty] public float minimumNjs { get; set; } = 0;

		[JsonProperty("maximumNjs")] public float _maximumNjs = NJS_FILTER_MAX;
		public float maximumNjs {
			get => _maximumNjs >= NJS_FILTER_MAX ? float.MaxValue : _maximumNjs;
			private set => _maximumNjs = value;
		}


		[JsonProperty] public float minimumNps { get; set; } = 0;

		[JsonProperty("maximumNps")] public float _maximumNps = NPS_FILTER_MAX;
		public float maximumNps {
			get => _maximumNps >= NPS_FILTER_MAX ? float.MaxValue : _maximumNps;
			private set => _maximumNps = value;
		}


		[UIComponent("hideOlderThanSlider")] internal SliderSetting hideOlderThanSlider = null;
		[UIComponent("rankedToggle")] internal ToggleSetting rankedToggle = null;

		[UIAction("UpdateData")] public static void UpdateData(object _) => SharedCoroutineStarter.instance.StartCoroutine(FilterView.limitedUpdateData.CallNextFrame());

		[UIValue("difficulties")] public static readonly List<object> difficulties = Enum.GetNames(typeof(MapDifficulty)).Prepend("Any").ToList<object>();
		[UIValue("characteristics")] public static readonly List<object> characteristics = Enum.GetNames(typeof(MapCharacteristic)).Prepend("Any").ToList<object>();
		[UIValue("downloadedFilterOptions")] public static readonly List<object> downloadedFilterOptions = new List<object> { "Show all", "Only downloaded", "Hide downloaded" };
		[UIValue("scoreFilterOptions")] public static readonly List<object> scoreFilterOptions = new List<object> { "Show all", "Hide passed", "Only passed" };
		[UIValue("rankedFilterOptions")] public static readonly List<object> rankedFilterOptions = new List<object> { "Show all", "Only Ranked", "Only Qualified" };

		[UIValue("modOptions")] public static readonly List<object> modOptions = new List<object> { "Any", "Noodle Extensions", "Mapping Extensions", "Chroma", "Cinema" };

		#region uiformatters
		static string DateTimeToStr(int d) => FilterView.hideOlderThanOptions[d].ToString("MMM yyyy", new CultureInfo("en-US"));
		static string FormatSongLengthLimitFloat(float d) => d >= SONG_LENGTH_FILTER_MAX ? "Unlimited" : TimeSpan.FromMinutes(d).ToString("mm\\:ss");
		static string FormatMaxStarsFloat(float d) => d >= STAR_FILTER_MAX ? "Unlimited" : d.ToString("0.0");
		static string PercentFloat(float d) => d.ToString("0.0%");
		static string FormatMaxNjs(float d) => d >= NJS_FILTER_MAX ? "Unlimited" : d.ToString();
		static string FormatMaxNps(float d) => d >= NPS_FILTER_MAX ? "Unlimited" : d.ToString();
		static string FormatShortFloat(float d) => d.ToString("0.0");
		string FormatUploaderShortInfo(string d) {
			uploadersString = d;

			if(d == "")
				return "Show all";

			return $"{(uploadersBlacklist ? "Hiding" : "Show only")} <color=#CCC>{uploaders.Count}</color> uploader{(uploaders.Count == 1 ? "" : "s")}";
		}
		#endregion

		public FilterOptions Clone() => (FilterOptions)MemberwiseClone();
	}

	static class FilterPresets {
		public static Dictionary<string, FilterOptions> presets { get; private set; }

		public static void Init() {
			if(!Directory.Exists(ConfigUtil.PresetDir))
				Directory.CreateDirectory(ConfigUtil.PresetDir);

			if(presets != null)
				return;

			presets = new Dictionary<string, FilterOptions>();

			foreach(var preset in Directory.GetFiles(ConfigUtil.PresetDir, "*.json")) {
				try {
					presets.Add(Path.GetFileNameWithoutExtension(preset), JsonConvert.DeserializeObject<FilterOptions>(File.ReadAllText(preset), JsonHelpers.leanDeserializeSettings));
				} catch(Exception ex) {
					Plugin.Log.Warn($"Failed to load Filter preset {Path.GetFileName(preset)}");
					Plugin.Log.Error(ex);
				}
			}
		}

		public static void Save(string name) {
			if(!Directory.Exists(ConfigUtil.PresetDir))
				Directory.CreateDirectory(ConfigUtil.PresetDir);

			name = string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

			name = presets.Keys.FirstOrDefault(x => x.Equals(name, StringComparison.InvariantCultureIgnoreCase)) ?? name;

			if(name.Length == 0)
				name = "Unnamed";

			presets[name] = FilterView.currentFilter.Clone();

			File.WriteAllText(ConfigUtil.GetPresetPath(name), JsonConvert.SerializeObject(FilterView.currentFilter, Formatting.Indented));
		}

		public static void Delete(string name) {
			if(!presets.ContainsKey(name))
				return;

			presets.Remove(name);

			File.Delete(ConfigUtil.GetPresetPath(name));
		}
	}
}
