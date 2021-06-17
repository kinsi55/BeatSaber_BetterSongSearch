using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.Util;
using HMUI;
using SongDetailsCache.Structs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace BetterSongSearch.UI {

	[HotReload(RelativePathToLayout = @"Views\FilterView.bsml")]
	[ViewDefinition("BetterSongSearch.UI.Views.FilterView.bsml")]
	public class FilterView : BSMLAutomaticViewController {
		static List<DateTime> hideOlderThanOptions;

		public void Awake() {
			if(hideOlderThanOptions != null)
				return;

			hideOlderThanOptions = new List<DateTime>();

			var x = new DateTime(2018, 5, 1);

			while(x < DateTime.Now) {
				hideOlderThanOptions.Add(x);

				x = x.AddMonths(1);
			}
		}

		[UIComponent("hideOlderThanSlider")] SliderSetting hideOlderThanSlider = null;

		[UIAction("#post-parse")]
		void Parsed() {
			hideOlderThanSlider.slider.maxValue = hideOlderThanOptions.Count - 1;
			hideOlderThanSlider.Value = 0;
			hideOlderThanSlider.ReceiveValue();
		}

		const float SONG_LENGTH_FILTER_MAX = 15f;
		const float STAR_FILTER_MAX = 14f;
		const float NJS_FILTER_MAX = 25f;
		const float NPS_FILTER_MAX = 10f;

		#region uiproperties
		public string opt_existingSongs { get; private set; } = (string)downloadedFilterOptions[0];
		public DateTime opt_hideOlderThan { get; private set; } = DateTime.MinValue;
		public int _opt_hideOlderThan {
			get => hideOlderThanOptions.IndexOf(opt_hideOlderThan);
			set {
				opt_hideOlderThan = hideOlderThanOptions[value];
			}
		}


		public float opt_minimumSongLength { get; private set; } = 0f;

		public float _opt_maximumSongLength { get; private set; } = 20f;
		public float opt_maximumSongLength {
			get => _opt_maximumSongLength >= SONG_LENGTH_FILTER_MAX ? float.MaxValue : _opt_maximumSongLength;
			private set => _opt_maximumSongLength = value;
		}


		public bool opt_hideUnranked { get; private set; } = false;


		public int opt_minimumPP { get; private set; } = 0;

		public float opt_minimumStars { get; private set; } = 0f;
		
		static float _opt_maximumStars = STAR_FILTER_MAX;
		public float opt_maximumStars {
			get => _opt_maximumStars >= STAR_FILTER_MAX ? float.MaxValue : _opt_maximumStars;
			private set => _opt_maximumStars = value;
		}


		public float opt_minimumRating { get; private set; } = 0f;
		public int opt_minimumVotes { get; private set; } = 0;


		public int opt_difficulty_int = -1;
		public string opt_difficulty {
			get => opt_difficulty_int == -1 ? "Any" : ((MapDifficulty)opt_difficulty_int).ToString();
			private set => opt_difficulty_int = Enum.TryParse<MapDifficulty>(value, out var pDiff) ? (int)pDiff : -1;
		}

		public int opt_characteristic_int = -1;
		public string opt_characteristic {
			get => opt_characteristic_int == -1 ? "Any" : ((MapCharacteristic)opt_characteristic_int).ToString();
			private set => opt_characteristic_int = Enum.TryParse<MapCharacteristic>(value, out var pChar) ? (int)pChar : -1;
		}


		public float opt_minimumNjs = 0;
		
		public float _opt_maximumNjs = NJS_FILTER_MAX;
		public float opt_maximumNjs {
			get => _opt_maximumNjs >= NJS_FILTER_MAX ? float.MaxValue : _opt_maximumNjs;
			private set => _opt_maximumNjs = value;
		}


		public float opt_minimumNps = 0;
		
		public float _opt_maximumNps = NPS_FILTER_MAX;
		public float opt_maximumNps {
			get => _opt_maximumNps >= NPS_FILTER_MAX ? float.MaxValue : _opt_maximumNps;
			private set => _opt_maximumNps = value;
		}
		#endregion

		#region filters
		public bool DifficultyCheck(in SongDifficulty diff) {
			if(opt_hideUnranked && !diff.ranked)
				return false;

			if(diff.stars < opt_minimumStars || diff.stars > opt_maximumStars)
				return false;

			if(opt_difficulty_int != -1 && (int)diff.difficulty != opt_difficulty_int)
				return false;

			if(opt_characteristic_int != -1 && (int)diff.characteristic != opt_characteristic_int)
				return false;

			if(diff.njs < opt_minimumNjs || diff.njs > opt_maximumNjs)
				return false;

			if(diff.song.songDurationSeconds > 0) {
				var nps = diff.notes / (float)diff.song.songDurationSeconds;

				if(nps < opt_minimumNps || nps > opt_maximumNps)
					return false;
			}

			if(opt_minimumPP != 0 && opt_minimumPP > diff.approximatePpValue)
				return false;

			return true;
		}

		public bool SongCheck(in Song song) {
			if(song.uploadTime < opt_hideOlderThan)
				return false;

			if(song.songDurationSeconds > 0f && (song.songDurationSeconds < opt_minimumSongLength * 60 || song.songDurationSeconds > opt_maximumSongLength * 60))
				return false;

			var voteCount = song.downvotes + song.upvotes;

			if(voteCount < opt_minimumVotes)
				return false;

			if(opt_minimumRating > 0f && (opt_minimumRating > song.rating || voteCount == 0))
				return false;

			if(opt_existingSongs != (string)downloadedFilterOptions[0]) {
				if(SongCore.Collections.songWithHashPresent(song.hash) == (opt_existingSongs == (string)downloadedFilterOptions[2]))
					return false;
			}

			return true;
		}
		#endregion

		static RatelimitCoroutine limitedUpdateData = new RatelimitCoroutine(UIMainFlowCoordinator.FilterSongs, 0.3f);

		[UIAction("UpdateData")] static void UpdateData(object _) => SharedCoroutineStarter.instance.StartCoroutine(limitedUpdateData.Call());

		[UIValue("difficulties")] private static readonly List<object> difficulties = new object[] { "Any" }.AsEnumerable().Concat(Enum.GetNames(typeof(MapDifficulty))).ToList();
		[UIValue("characteristics")] private static readonly List<object> characteristics = new object[] { "Any" }.AsEnumerable().Concat(Enum.GetNames(typeof(MapCharacteristic))).ToList();
		[UIValue("downloadedFilterOptions")] private static readonly List<object> downloadedFilterOptions = new List<object> { "Show All", "Show downloaded", "Hide downloaded" };

		[UIComponent("datasetInfoLabel")] private TextMeshProUGUI _datasetInfoLabel = null;
		public TextMeshProUGUI datasetInfoLabel => _datasetInfoLabel;

		#region uiformatters
		static string DateTimeToStr(int d) => hideOlderThanOptions[d].ToString("MMM yy", new CultureInfo("en-US"));
		static string FormatSongLengthLimitFloat(float d) => d >= SONG_LENGTH_FILTER_MAX ? "Unlimited" : TimeSpan.FromMinutes(d).ToString("mm\\:ss");
		static string FormatMaxStarsFloat(float d) => d >= STAR_FILTER_MAX ? "Unlimited" : d.ToString("0.0");
		static string FormatPP(int d) => $"~{d} PP";
		static string PercentFloat(float d) => d.ToString("0.0%");
		static string FormatMaxNjs(float d) => d >= NJS_FILTER_MAX ? "Unlimited" : d.ToString();
		static string FormatMaxNps(float d) => d >= NPS_FILTER_MAX ? "Unlimited" : d.ToString();
		#endregion
	}
}
