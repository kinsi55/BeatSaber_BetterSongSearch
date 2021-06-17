using BetterSongSearch.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BetterSongSearch.Util {
	static class WeightedSongSearch {
		public static IEnumerable<SongSearchSong> Search(IEnumerable<SongSearchSong> inList, string filter) {
			var words = filter.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

			var possibleSongKey = 0u;

			if(words.Length == 1 && filter.Length >= 2 && filter.Length <= 7) {
				try {
					possibleSongKey = Convert.ToUInt32(filter, 16);
				} catch { }
			}

			// Slightly slower than just calling IsLetterOrDigit if its not a ' ', but in most of the cases it will be
			bool IsSpace(char x) => x == ' ' || !char.IsLetterOrDigit(x);

			return inList.Where(x => {
				int resultWeight = 0;
				bool matchedAuthor = false;
				int prevMatchIndex = -1;

				var songe = x.detailsSong;
				var songeName = songe.songName;

				if(possibleSongKey != 0 && x.detailsSong.mapId == possibleSongKey)
					resultWeight = 10;

				for(int i = 0; i < words.Length; i++) {
					// Match the current split word in the song name
					var matchpos = songeName.IndexOf(words[i], StringComparison.OrdinalIgnoreCase);

					// If we found anything...
					if(matchpos != -1) {
						// Check if we matched the beginning of a word
						var wordStart = matchpos == 0 || IsSpace(songeName[matchpos - 1]);

						// If it was the beginning add 5 weighting, else 3
						resultWeight += wordStart ? 5 : 3;

						// If we did match the beginning, check if we matched an entire word. Get the end index as indicated by our needle
						var maybeWordEnd = wordStart && matchpos + words[i].Length < songeName.Length;

						// Check if we actually end up at a non word char, if so add 2 weighting
						if(maybeWordEnd && IsSpace(songeName[matchpos + words[i].Length]))
							resultWeight += 2;

						// If the word we just checked is behind the previous matched, add another 1 weight
						if(prevMatchIndex != -1 && matchpos > prevMatchIndex)
							resultWeight += 1;

						prevMatchIndex = matchpos;
					}

					if(!matchedAuthor && songe.songAuthorName.Equals(words[i], StringComparison.OrdinalIgnoreCase)) {
						matchedAuthor = true;

						resultWeight += 3 * (words[i].Length / 2);
					} else if(!matchedAuthor && songe.songAuthorName.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) != -1) {
						matchedAuthor = true;

						resultWeight += 2 * (words[i].Length / 2);
					}
				}

				for(int i = 0; i < words.Length; i++) {
					if(songe.levelAuthorName.IndexOf(words[i], StringComparison.OrdinalIgnoreCase) != -1) {
						resultWeight += 1;

						break;
					}
				}

				x.searchResultWeight = resultWeight;

				return resultWeight != 0;
			}).OrderByDescending(x => x.searchResultWeight);
		}
	}
}
