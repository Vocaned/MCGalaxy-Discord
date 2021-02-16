using System;
using System.Net;
using Newtonsoft.Json;

namespace Discord {
	public class REST : IDisposable {
		public const string UserAgent = "MCGalaxy-Discord (https://github.com/Fam0r/MCGalaxy-Discord)";
		public const string BaseURL = "https://discord.com/api/v8";
		CustomWebClient wc = new CustomWebClient();

		public class CustomWebClient : WebClient {
			protected override WebRequest GetWebRequest(Uri address) {
				HttpWebRequest req = (HttpWebRequest)base.GetWebRequest(address);
				req.UserAgent = UserAgent;
				req.ContentType = "application/json; charset=utf-8";
				return (WebRequest)req;
			}
		}

		public void Dispose() {
			wc.Dispose();
		}

		public T GET<T>(string url) {
			return JsonConvert.DeserializeObject<T>(wc.DownloadString(url));
		}
	}

	public static class WS {
		public static Constants.WSPayload Deserialize(string data) {
			return JsonConvert.DeserializeObject<Constants.WSPayload>(data);
		}
	}
}
