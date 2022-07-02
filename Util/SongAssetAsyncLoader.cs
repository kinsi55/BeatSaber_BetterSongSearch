using BetterSongSearch.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SongDetailsCache.Structs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterSongSearch.Util {
	class SongAssetAsyncLoader : IDisposable {
		static Dictionary<uint, Sprite> _spriteCache;
		static Dictionary<uint, AudioClip> _previewCache;

		public SongAssetAsyncLoader() {
			_spriteCache ??= new Dictionary<uint, Sprite>();
			_previewCache ??= new Dictionary<uint, AudioClip>();
		}

		async Task<string> ApiRequest(string key, Func<JObject, string> valueGetter, CancellationToken token) {
			var baseUrl = PluginConfig.Instance.apiUrlOverride;

			if(baseUrl.Length == 0)
				baseUrl = BeatSaverRegionManager.detailsDownloadUrl;

			var c = await UnityWebrequestWrapper.DownloadBytes($"{baseUrl}/{key.ToLowerInvariant()}", token);
			
			using(var jsonReader = new JsonTextReader(new StreamReader(new MemoryStream(c)))) {
				var ser = new JsonSerializer();

				return valueGetter(ser.Deserialize<JObject>(jsonReader));
			}
		}

		public Task<string> GetSongDescription(string key, CancellationToken token) {
			return ApiRequest(key, (x) => x.GetValue("description").Value<string>(), token);
		}
		public Task<string> GetPreviewURL(string key, CancellationToken token) {
			return ApiRequest(key, (x) => x.SelectToken("versions[0].coverURL", false).Value<string>(), token);
		}

		public async Task<Sprite> LoadCoverAsync(Song song, CancellationToken token) {
			var path = PluginConfig.Instance.coverUrlOverride;

			if(path.Length == 0)
				path = BeatSaverRegionManager.coverDownloadUrl;

			path += $"/{song.hash.ToLowerInvariant()}.jpg";

			var mid = song.mapId;

			if(_spriteCache.TryGetValue(mid, out Sprite sprite))
				return sprite;

			var cover = await UnityWebrequestWrapper.DownloadSprite(path, token);

			if(cover != null)
				return _spriteCache[mid] = cover;

			return SongCore.Loader.defaultCoverImage;
		}

		public async Task<AudioClip> LoadPreviewAsync(Song song, CancellationToken token) {
			var path = PluginConfig.Instance.previewUrlOverride;

			if(path.Length == 0)
				path = BeatSaverRegionManager.previewDownloadUrl;

			path += $"/{song.hash.ToLowerInvariant()}.mp3";

			var mid = song.mapId;

			if(_previewCache.TryGetValue(mid, out AudioClip ac))
				return ac;

			var preview = await UnityWebrequestWrapper.DownloadAudio(path, token, AudioType.MPEG);

			if(preview != null && preview.loadState == AudioDataLoadState.Loaded)
				return _previewCache[mid] = preview;

			return null;
		}

		public void Dispose() {
			foreach(var x in _spriteCache.Keys.ToArray()) {
				var s = _spriteCache[x];

				if(s == BSSFlowCoordinator.songListView.selectedSongView.coverImage.sprite)
					continue;

				_spriteCache.Remove(x);

				GameObject.DestroyImmediate(s.texture);
				GameObject.DestroyImmediate(s);
			}

			foreach(var x in _previewCache.Values)
				GameObject.Destroy(x);

			_previewCache.Clear();
		}
	}
}
