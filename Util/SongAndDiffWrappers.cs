using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BetterSongSearch.Configuration;
using BetterSongSearch.UI;
using SongDetailsCache.Structs;
using static BetterSongSearch.UI.DownloadHistoryView;

namespace BetterSongSearch.Util {
	class SongSearchSong {
		const bool showVotesInsteadOfRating = true;

		public readonly Song detailsSong;

		string _hash = null;
		public string hash => _hash ??= detailsSong.hash;

		string _uploaderNameLowercase = null;
		public string uploaderNameLowercase => _uploaderNameLowercase ??= detailsSong.uploaderName.ToLowerInvariant();

		public readonly SongSearchDiff[] diffs;

		#region BSML stuffs
		public IOrderedEnumerable<SongSearchDiff> sortedDiffs {
			get {
				// Matching Standard > Matching Non-Standard > Non-Matching Standard > Non-Matching Non-Standard
				var y = diffs.OrderByDescending(x =>
					(x.passesFilter ? 1 : -3) + (x.detailsDiff.characteristic == MapCharacteristic.Standard ? 1 : 0)
				);

				// If we are sorting by something that is on a diff-level, sort the diffy as well!
				if(SongListController.sortModesDiffSort.TryGetValue(SongListController.selectedSortMode, out var diffSorter))
					y = y.ThenBy(diffSorter);

				return y.ThenByDescending(x => x.songSearchSong.isRanked() ? 1 : 0);
			}
		}

		public bool CheckIsDownloadedAndLoaded() => SongCore.Collections.songWithHashPresent(detailsSong.hash);

		public bool CheckIsDownloaded() {
			return
				BSSFlowCoordinator.downloadHistoryView.downloadList.Any(
					x => x.key == detailsSong.key &&
					x.status == DownloadHistoryEntry.DownloadStatus.Downloaded
				) || CheckIsDownloadedAndLoaded();
		}

		public bool CheckIsDownloadable() {
			var dlElem = BSSFlowCoordinator.downloadHistoryView.downloadList.FirstOrDefault(x => x.key == detailsSong.key);

			var downloadingStates = DownloadHistoryEntry.DownloadStatus.Preparing | DownloadHistoryEntry.DownloadStatus.Downloading | DownloadHistoryEntry.DownloadStatus.Queued;

			return dlElem == null || (
				(dlElem.retries == 3 && dlElem.status == DownloadHistoryEntry.DownloadStatus.Failed) ||
				(!dlElem.IsInAnyOfStates(downloadingStates) && !CheckIsDownloaded())
			);
		}

		public bool CheckHasScore() => BSSFlowCoordinator.songsWithScores.ContainsKey(hash);

		public bool isQualified(RankedStates state = RankedStates.BeatleaderQualified | RankedStates.ScoresaberQualified) => (detailsSong.rankedStates & state) != 0;
		public bool isRanked(RankedStates state = RankedStates.BeatleaderRanked | RankedStates.ScoresaberRanked) => (detailsSong.rankedStates & state) != 0;

		public string fullFormattedSongName => $"{detailsSong.songAuthorName} - {detailsSong.songName}";
		public string uploadDateFormatted => detailsSong.uploadTime.ToString("dd. MMM yyyy", CultureInfo.InvariantCulture);
		public string songLength => string.Format("{0:00}:{1:00}", (int)detailsSong.songDuration.TotalMinutes, detailsSong.songDuration.Seconds);
		public string songRating => showVotesInsteadOfRating ? $"<color=#9C9>👍 {detailsSong.upvotes} <color=#C99>👎 {detailsSong.downvotes}" : $"{detailsSong.rating:0.0%}";

#if !DEBUG
		public string songLengthAndRating => $"{(isQualified() ? "<color=#96C>🚩 Qualified</color> " : "")}⏲ {songLength}  {songRating}";
#else
		public float sortWeight = 0;
		public float resultWeight = 0;
		public string songLengthAndRating => $"RW{resultWeight} - SW{sortWeight} - {(isQualified() ? "<color=#96C>🚩 Qualified</color> " : "")}⏲ {songLength}  {songRating}";
#endif
		//public string levelAuthorName => song.levelAuthorName;
		#endregion

		public string GetCustomLevelIdString() => CustomLevelLoader.kCustomLevelPrefixId + detailsSong.hash.ToUpperInvariant();
		public SongSearchDiff GetFirstPassingDifficulty() => sortedDiffs.FirstOrDefault();
		public SongSearchSong(in Song song) {
			detailsSong = song;
			diffs = new SongSearchDiff[song.diffCount];

			// detailsSong.difficulties has an overhead of creating the ArraySegment - This doesnt 👍;
			for(var i = 0; i < diffs.Length; i++)
				diffs[i] = new SongSearchDiff(this, in BSSFlowCoordinator.songDetails.difficulties[i + (int)song.diffOffset]);
		}

		public class SongSearchDiff {
			internal readonly SongSearchSong songSearchSong;
			internal readonly SongDifficulty detailsDiff;
			internal bool? _passesFilter = null;
			internal bool passesFilter => _passesFilter ??= BSSFlowCoordinator.filterView.DifficultyCheck(in detailsDiff) && BSSFlowCoordinator.filterView.SearchDifficultyCheck(this);

			internal string serializedDiff => $"{detailsDiff.characteristic}_{detailsDiff.difficulty}";

			public bool CheckHasScore() => songSearchSong.CheckHasScore() && BSSFlowCoordinator.songsWithScores[songSearchSong.hash].ContainsKey(serializedDiff);
			internal float localScore => BSSFlowCoordinator.songsWithScores[songSearchSong.hash][serializedDiff];

			string GetCombinedShortDiffName() {
				var retVal = $"{(detailsDiff.song.diffCount > 5 ? shortMapDiffNames[detailsDiff.difficulty] : detailsDiff.difficulty.ToString())}";

				if(customCharNames.TryGetValue(detailsDiff.characteristic, out var customCharName))
					retVal += $"({customCharName})";

				return retVal;
			}

			public RankedStates GetTargetedRankLeaderboardService() {
				var rStates = detailsDiff.song.rankedStates;

				if(rStates.HasFlag(RankedStates.ScoresaberRanked) &&
					// Not Filtering by BeatLeader ranked
					FilterView.currentFilter.rankedState != (string)FilterOptions.rankedFilterOptions[2] &&
					(
						PluginConfig.Instance.preferredLeaderboard != "BeatLeader" ||
						!rStates.HasFlag(RankedStates.BeatleaderRanked) ||
						// Filtering by SS ranked
						FilterView.currentFilter.rankedState == (string)FilterOptions.rankedFilterOptions[1]
					)
				)
					return RankedStates.ScoresaberRanked;

				if(rStates.HasFlag(RankedStates.BeatleaderRanked))
					return RankedStates.BeatleaderRanked;

				return RankedStates.Unranked;
			}

			public bool isRanked => detailsDiff.stars > 0f || detailsDiff.starsBeatleader > 0f;

			string GetFormattedRankDisplay() {
				if(!passesFilter)
					return "";

				var lbsvc = GetTargetedRankLeaderboardService();

				if(lbsvc == RankedStates.ScoresaberRanked && detailsDiff.stars > 0) {
					return $" <color=#{(passesFilter ? "D91" : "650")}>{Math.Round(detailsDiff.stars, 1):0.0}⭐</color>";
				} else if(lbsvc == RankedStates.BeatleaderRanked && detailsDiff.starsBeatleader > 0) {
					return $" <color=#{(passesFilter ? "B1D" : "606")}>{Math.Round(detailsDiff.starsBeatleader, 1):0.0}⭐</color>";
				}

				return "";
			}

			public string formattedDiffDisplay => $"<color=#{(passesFilter ? "EEE" : "888")}>{GetCombinedShortDiffName()}</color>{GetFormattedRankDisplay()}";

			public float GetStars() => GetStars(GetTargetedRankLeaderboardService());

			public float GetStars(RankedStates state) {
				if(state.HasFlag(RankedStates.ScoresaberRanked) && detailsDiff.stars > 0)
					return detailsDiff.stars;

				if(state.HasFlag(RankedStates.BeatleaderRanked))
					return detailsDiff.starsBeatleader;

				return 0;
			}

			public SongSearchDiff(SongSearchSong songSearchSong, in SongDifficulty diff) {
				this.detailsDiff = diff;
				this.songSearchSong = songSearchSong;
			}

			static readonly IReadOnlyDictionary<MapDifficulty, string> shortMapDiffNames = new Dictionary<MapDifficulty, string> {
				{ MapDifficulty.Easy, "Easy" },
				{ MapDifficulty.Normal, "Norm" },
				{ MapDifficulty.Hard, "Hard" },
				{ MapDifficulty.Expert, "Ex" },
				{ MapDifficulty.ExpertPlus, "Ex+" }
			};

			static readonly IReadOnlyDictionary<MapCharacteristic, string> customCharNames = new Dictionary<MapCharacteristic, string> {
				{ MapCharacteristic.NinetyDegree, "90" },
				{ MapCharacteristic.ThreeSixtyDegree, "360" },
				{ MapCharacteristic.Lawless, "☠" },
				{ MapCharacteristic.Custom, "?" },
				{ MapCharacteristic.Lightshow, "💡" }
			};
		}
	}
}
