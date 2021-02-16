using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Discord {
	public abstract class Constants {

		#region opcodes
		public const int OPCODE_DISPACH = 0;
		public const int OPCODE_HEARTBEAT = 1;
		public const int OPCODE_IDENTIFY = 2;
		public const int OPCODE_PRESENCE_UPDATE = 3;
		public const int OPCODE_VOICE_STATE_UPDATE = 4;
		public const int OPCODE_RESUME = 6;
		public const int OPCODE_RECONNECT = 7;
		public const int OPCODE_REQUEST_GUILD_MEMBERS = 8;
		public const int OPCODE_INVALID_SESSION = 9;
		public const int OPCODE_HELLO = 10;
		public const int OPCODE_ACK = 11;
		#endregion

		#region intents
		const int INTENT_GUILDS = 1 << 0;
		const int INTENT_GUILD_MEMBERS = 1 << 1; // Privileged
		const int INTENT_GUILD_BANS = 1 << 2;
		const int INTENT_GUILD_EMOJIS = 1 << 3;
		const int INTENT_GUILD_INTEGRATIONS = 1 << 4;
		const int INTENT_GUILD_WEBHOOKS = 1 << 5;
		const int INTENT_GUILD_INVITES = 1 << 6;
		const int INTENT_GUILD_VOICE_STATES = 1 << 7;
		const int INTENT_GUILD_PRESENCES = 1 << 8; // Privileged
		const int INTENT_GUILD_MESSAGES = 1 << 9;
		const int INTENT_GUILD_MESSAGE_REACTIONS = 1 << 10;
		const int INTENT_GUILD_MESSAGE_TYPING = 1 << 11;
		const int INTENT_DIRECT_MESSAGES = 1 << 12;
		const int INTENT_DIRECT_MESSAGE_REACTIONS = 1 << 13;
		const int INTENT_DIRECT_MESSAGE_TYPING = 1 << 14;
		#endregion

		#region Common
		public class User {
			public string id { get; set; }
			public string username { get; set; }
			public string discriminator { get; set; }
			public string avatar { get; set; }
		}

		public class Channel {

		}

		public class Message {
			public string id { get; set; }
			public string channel_id { get; set; }
			public string guild_id { get; set; }
			public User author { get; set; }
			public string content { get; set; }
			public bool tts { get; set; }

		}

		public class Guild {

		}

		//public class Application {}
		#endregion

		#region REST
		public class Gateway {
			public string url { get; set; }
		}

		public class NewMessage {
			public string content { get; set; }
			// embed
			public AllowedMentions allowed_mentions { get; set; }

			public class AllowedMentions {
				public string[] parse { get; set; }

				public AllowedMentions() {
					parse = new string[0]; // no pings allowed for now
				}
			}

			public NewMessage(string msgcontent) {
				allowed_mentions = new AllowedMentions();
				content = msgcontent;
			}
		}
		#endregion


		#region WebSocket Payloads
		public class WSPayload {
			public int op { get; set; } // opcode
			public int? s { get; set; } // sequence
			public string t { get; set; } // type
			public dynamic d { get; set; } // data: JObject, int or null
		}

		public class HeartBeat : WSPayload {
			public HeartBeat(int sequence) {
				op = OPCODE_HEARTBEAT;
				d = sequence;
			}
		}

		public class Ready : WSPayload {
			public class Data {
				public int v { get; set; }
				public User user { get; set; }
				public Channel[] private_channels { get; set; }
				public Guild[] guilds { get; set; }
				public string session_id { get; set; }
				// public Application application { get; set; }
			}
			public Data data { get; set; }
			public Ready(JObject d) {
				data = d.ToObject<Data>();
			}
		}

		public class StatusUpdate : WSPayload {
			public class Data {
				public int? since { get; set; }
				public Activity[] activities { get; set; }
				public string status { get; set; }
				public bool afk { get; set; }
			}
			public StatusUpdate(string status, string activity, int type) {
				op = OPCODE_PRESENCE_UPDATE;

				Data opts = new Data();
				opts.since = null;
				Activity[] activities = new Activity[1];
				activities[0] = new Activity {
					name = activity,
					type = type // watching
				};
				opts.activities = activities;
				opts.status = status;
				opts.afk = false;

				d = JObject.FromObject(opts);
			}
		}

		public class Identify : WSPayload {
			class Data {
				public string token { get; set; }
				public properties properties { get; set; }
				public int intents { get; set; }
				public bool guild_subscriptions { get; set; }
				// presence

				public Data() {
					properties = new properties();
				}
			}
			class properties {
				public string os { get; set; }
				public string browser { get; set; }
				public string device { get; set; }
			}

			public Identify(string token) {
				op = OPCODE_IDENTIFY;

				Data opts = new Data();
				opts.token = token;
				opts.intents = INTENT_GUILD_MESSAGES;
				opts.guild_subscriptions = false;

				opts.properties.os = opts.properties.browser = opts.properties.device = "MCGalaxy-Discord";

				d = JObject.FromObject(opts);
			}
		}
		#endregion

		#region Gateway objects
		public class Activity {
			public string name { get; set; }
			public int type { get; set; }
		}
		#endregion
	}
}
