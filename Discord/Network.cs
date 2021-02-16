using System;
using System.Net;
using Newtonsoft.Json;

namespace Discord {
	public class REST : IDisposable {
		public const string UserAgent = "MCGalaxy-Discord (https://github.com/Fam0r/MCGalaxy-Discord)";
		public const string BaseURL = "https://discord.com/api/v8";
		CustomWebClient wc;

		public class CustomWebClient : WebClient {
			string token = "";
			protected override WebRequest GetWebRequest(Uri address) {
				HttpWebRequest req = (HttpWebRequest)base.GetWebRequest(address);
				req.UserAgent = UserAgent;
				req.ContentType = "application/json; charset=utf-8";
				if (req.Method == "POST") req.Headers.Set("Authorization", "Bot " + token);
				return (WebRequest)req;
			}

			public CustomWebClient(string BotToken) {
				token = BotToken;
			}
		}

		public REST(string BotToken) {
			wc = new CustomWebClient(BotToken);
		}

		public void Dispose() {
			wc.Dispose();
		}

		public T GET<T>(string url) {
			return JsonConvert.DeserializeObject<T>(wc.DownloadString(url));
		}

		public void POST(string url, object data) {
			wc.UploadString(url, JsonConvert.SerializeObject(data));
		}

		public T POST<T>(string url, object data) {
			return JsonConvert.DeserializeObject<T>(wc.UploadString(url, JsonConvert.SerializeObject(data)));
		}
	}

	public static class WS {
		public static Constants.WSPayload Deserialize(string data) {
			return JsonConvert.DeserializeObject<Constants.WSPayload>(data);
		}
	}
}
