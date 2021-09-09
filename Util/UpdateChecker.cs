using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BetterSongSearch.Util {
	//class UpdateChecker {
	//	public class Update {
	//		public string currentVersion;
	//		public string versionTag;
	//		public string versionUrl;
	//		public DateTime releaseDate;
	//		public string releaseText;
	//	}

	//	bool hasChecked = false;

	//	static Version VersionStringToVersion(string str) => new Version(str.Substring(1));

	//	public async Task<Update> CheckForUpdate(string RepoPath) {
	//		if(hasChecked)
	//			return null;

	//		using(var client = new HttpClient(new HttpClientHandler() {
	//			AutomaticDecompression = DecompressionMethods.GZip
	//		})) {
	//			client.DefaultRequestHeaders.Add("User-Agent", "UpdateChecker");
	//			client.DefaultRequestHeaders.ConnectionClose = true;
	//			client.Timeout = TimeSpan.FromSeconds(10);

	//			using(var resp = await client.GetAsync($"https://api.github.com/repos/{RepoPath}/releases/latest", HttpCompletionOption.ResponseHeadersRead)) {
	//				if(resp.StatusCode != HttpStatusCode.OK)
	//					throw new Exception($"Unexpected HTTP response: {resp.StatusCode} {resp.ReasonPhrase}");

	//				using(var reader = new StreamReader(await resp.Content.ReadAsStreamAsync()))
	//				using(var jsonReader = new JsonTextReader(reader)) {
	//					JsonSerializer ser = new JsonSerializer();

	//					var x = ser.Deserialize<JObject>(jsonReader);

	//					var vStr = x.GetValue("tag_name").Value<string>();
	//					var v = VersionStringToVersion(vStr);

	//					var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

	//					hasChecked = true;

	//					if(v <= assemblyVersion)
	//						return null;

	//					return new Update() {
	//						currentVersion = $"v{assemblyVersion.ToString(3)}",
	//						versionTag = vStr,
	//						versionUrl = x.GetValue("html_url")?.Value<string>() ?? $"https://github.com/{RepoPath}/releases/latest",
	//						releaseDate = DateTime.Parse(x.GetValue("published_at").Value<string>(), null, System.Globalization.DateTimeStyles.RoundtripKind),
	//						releaseText = x.GetValue("body")?.Value<string>() ?? "No release notes"
	//					};
	//				}
	//			}
	//		}
	//	}
	//}
}
