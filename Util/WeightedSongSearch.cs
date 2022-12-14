using BetterSongSearch.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterSongSearch.Util {
	static class WeightedSongSearch {
		struct xd {
			public SongSearchSong song;
			public float searchWeight;
			public float sortWeight;
		}

		public static IEnumerable<SongSearchSong> Search(IList<SongSearchSong> inList, string filter, Func<SongSearchSong, float> ordersort) {
			var words = filter.ToLowerInvariant().Split((string[])null, StringSplitOptions.RemoveEmptyEntries);

			var possibleSongKey = 0u;

			if(words.Length == 1 && filter.Length >= 2 && filter.Length <= 7) {
				try {
					possibleSongKey = Convert.ToUInt32(filter, 16);
				} catch { }
			}

			// Slightly slower than just calling IsLetterOrDigit if its not a ' ', but in most of the cases it will be
			bool IsSpace(char x) => x == ' ' || !char.IsLetterOrDigit(x);

			var prefiltered = new List<xd>();

			var maxSearchWeight = 0f;
			var maxSortWeight = 0f;

			Parallel.ForEach(inList, new ParallelOptions() { MaxDegreeOfParallelism = 5 }, x => {
				var resultWeight = 0;
				var matchedAuthor = false;
				var prevMatchIndex = -1;

				var songe = x.detailsSong;
				var songeName = MakeStringSearchable(songe.songName);

				if(possibleSongKey != 0 && x.detailsSong.mapId == possibleSongKey)
					resultWeight = 30;

				var authorName = songe.songAuthorName;
				var authorFullMatch = filter.IndexOf(authorName, StringComparison.OrdinalIgnoreCase);
				var i = 0;
				if(authorName.Length > 4 && authorFullMatch != -1 && 
					// Checks if there is a space after the supposedly matched author name
					(filter.Length == authorName.Length || IsSpace(filter[authorName.Length]))
				) {
					matchedAuthor = true;
					resultWeight += authorName.Length > 5 ? 25 : 20;

					// This is super cheapskate - I'd have to replace the author from the filter and recreate the words array otherwise
					if(authorFullMatch == 0)
						i = 1;
				}

				for(; i < words.Length; i++) {
					// If the word matches the author 1:1 thats cool innit
					if(authorName.Length != 0) {
						if(!matchedAuthor && authorName.Equals(words[i], StringComparison.OrdinalIgnoreCase)) {
							matchedAuthor = true;

							resultWeight += 3 * (words[i].Length / 2);
							continue;
							// Otherwise we'll have to check if its contained within this word
						} else if(!matchedAuthor && words[i].Length >= 3) {
							var index = authorName.IndexOf(words[i], StringComparison.OrdinalIgnoreCase);

							if(index == 0 || (index > 0 && IsSpace(authorName[index - 1]))) {
								matchedAuthor = true;

								resultWeight += (int)Math.Round((index == 0 ? 4f : 3f) * ((float)words[i].Length / authorName.Length));
								continue;
							}
						}
					}

					// Match the current split word in the song name
					var matchpos = songeName.IndexOf(words[i], StringComparison.Ordinal);

					// If we found anything...
					if(matchpos != -1) {
						// Check if we matched the beginning of a word
						var wordStart = matchpos == 0 || songeName[matchpos - 1] == ' ';

						// If it was the beginning add 5 weighting, else 3
						resultWeight += wordStart ? 5 : 3;

						var posInName = matchpos + words[i].Length;

						/*
						 * Check if we are at the end of the song name, but only if it has at least 8 characters
						 * We do this because otherwise, when searching for "lowermost revolt", songs where the
						 * songName is exactly "lowermost revolt" would have a lower result weight than 
						 * "lowermost revolt (JoeBama cover)"
						 * 
						 * The 8 character limitation for this is so that super short words like "those" dont end
						 * up triggering this
						 */
						if(songeName.Length > 7 && songeName.Length == posInName) {
							resultWeight += 3;
						} else {
							// If we did match the beginning, check if we matched an entire word. Get the end index as indicated by our needle
							var maybeWordEnd = wordStart && posInName < songeName.Length;

							// Check if we actually end up at a non word char, if so add 2 weighting
							if(maybeWordEnd && songeName[matchpos + words[i].Length] == ' ')
								resultWeight += 2;
						}

						// If the word we just checked is behind the previous matched, add another 1 weight
						if(prevMatchIndex != -1 && matchpos > prevMatchIndex)
							resultWeight += 1;

						prevMatchIndex = matchpos;
					}
				}

				for(i = 0; i < words.Length; i++) {
					if(words[i].Length > 3 && songe.levelAuthorName.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) != -1) {
						resultWeight += 1;

						break;
					}
				}

				if(resultWeight > 0) {
					var sortWeight = ordersort(x);

					lock(prefiltered) {
						prefiltered.Add(new xd() {
							song = x,
							searchWeight = resultWeight,
							sortWeight = sortWeight
						});

#if DEBUG
						x.sortWeight = sortWeight;
						x.resultWeight = resultWeight;
#endif
						if(maxSearchWeight < resultWeight)
							maxSearchWeight = resultWeight;

						if(maxSortWeight < sortWeight)
							maxSortWeight = sortWeight;
					}
				}
			});

			if(!prefiltered.Any())
				return new List<SongSearchSong>();

			var maxSearchWeightInverse = 1f / maxSearchWeight;
			var maxSortWeightInverse = 1f / maxSortWeight;

			return prefiltered.OrderByDescending((s) => {
				var searchWeight = s.searchWeight * maxSearchWeightInverse;

				return searchWeight + Math.Min(searchWeight / 2, s.sortWeight * maxSortWeightInverse * (searchWeight / 2));
			}).Select(x => x.song);
		}



		public static Dictionary<string, string> cachedSearchableStrings;
		static unsafe string MakeStringSearchable(string s) {
			// Eh whatever
			if(s.Length > 255)
				return s;

			if(cachedSearchableStrings != null && cachedSearchableStrings.TryGetValue(s, out var _s))
				return _s ?? s;

			var normalizedString = s.Normalize(NormalizationForm.FormD);

			var pos = 0;
			var modified = false;
			char* challoc = stackalloc char[s.Length];

			for(var i = 0; i < normalizedString.Length; i++) {
				var c = normalizedString[i];

				var cat = CharUnicodeInfo.GetUnicodeCategory(c);

				if(cat == UnicodeCategory.NonSpacingMark) {
					modified = true;
					continue;
				}

				// adds 32 (Ascii ' ') to the A-Z charcode and thus converts it to a-z lmao
				if(cat == UnicodeCategory.LowercaseLetter || cat == UnicodeCategory.SpaceSeparator || cat == UnicodeCategory.DecimalDigitNumber) {
					challoc[pos++] = c;
				} else if(cat == UnicodeCategory.UppercaseLetter && c < '[') {
					challoc[pos++] = (char)(c + ' ');
					modified = true;
				}
			}

			cachedSearchableStrings ??= new Dictionary<string, string>(BSSFlowCoordinator.songsList?.Length ?? 69420);

			if(!modified && pos == s.Length) {
				cachedSearchableStrings[s] = null;
				return s;
			}

			return cachedSearchableStrings[s] = new string(challoc, 0, pos);
		}
	}
}
