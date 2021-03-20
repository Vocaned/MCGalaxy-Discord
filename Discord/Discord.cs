using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Events;
using MCGalaxy.Tasks;
using Newtonsoft.Json;
using System.Threading;
using WebSocketSharp;

namespace Discord {
	public class Discord : IDisposable {
		REST rest;
		WebSocket ws;

		public Constants.User user;
		public bool authenticated, beat;
		string gatewayURL, botToken, channelID, session_id;	

		SchedulerTask heartbeatTask;
		int sequence;
		bool resuming;

		List<object> dataQueue = new List<object>();
		List<string> msgQueue = new List<string>();
		List<Constants.Embed> embedQueue = new List<Constants.Embed>();

		public Discord(string token, string channelid) {
			botToken = token;
			channelID = channelid;

			Init();
		}

		public void Init() {
			rest = new REST(botToken);
			gatewayURL = GetGateway();
			ws = new WebSocket(gatewayURL + "?v=8&encoding=json");
			ws.OnMessage += OnMessage;
			ws.OnClose += OnClose;
			ws.Connect();
		}

		public void Dispose() {
			rest.Dispose();
			Server.Background.Cancel(heartbeatTask);
			if (ws != null && ws.IsAlive) ws.Close(CloseStatusCode.Normal);
		}

		public void Reset(CloseStatusCode statusCode = CloseStatusCode.Normal) {
			rest.Dispose();
			Server.Background.Cancel(heartbeatTask);
			if (ws != null && ws.IsAlive) ws.Close(statusCode);
			authenticated = false;

			resuming = true;
			Init();
		}

		string GetGateway() {
			Constants.Gateway data = rest.GET<Constants.Gateway>(REST.BaseURL + "/gateway");
			return data.url;
		}

		void OnClose(object sender, CloseEventArgs e) {
			Debug("Closed connection with code " + e.Code + " (" + e.Reason + ")");
			if (e.Code.IsCloseStatusCode() && e.Code == (uint)CloseStatusCode.Normal) Dispose();
			else {
				Reset();
				SendMessage(channelID, "<@177424155371634688> reset after closing with " + e.Code.ToString());
			}
		}

		void Debug(string message) {
			MCGalaxy.Logger.Log(LogType.Debug, message);
		}



		void Beat(SchedulerTask task) {
			SendOP(Constants.OPCODE_HEARTBEAT);
		}

		public void SendStatusUpdate(string status, string activity, int type) {
			SendData(new Constants.StatusUpdate(status, activity, type));
		}

		void SendOP(int opcode) {
			object data = null;

			switch (opcode) {
				case Constants.OPCODE_HEARTBEAT:
					// Beat variable should pulse between true and false, true when heartbeat is sent and false when heartbeat is received
					// In the case the beat is true for 2 heartbeats in a row, consider the connection dead and create a new one
					if (beat) { Reset(CloseStatusCode.Abnormal); return; }
					beat = true;
					data = new Constants.HeartBeat(sequence);
					break;
				case Constants.OPCODE_IDENTIFY:
					data = new Constants.Identify(botToken);
					break;
				case Constants.OPCODE_RESUME:
					if (session_id == null || sequence == 0) return;
					data = new Constants.Resume(botToken, session_id, sequence);
					break;
			}

			if (data == null) return;
			SendData(data, true);
		}

		void SendData(object data, bool NoAuthCheck = false) {
			if (!authenticated && !NoAuthCheck) {
				Debug("Data queued for later. Not yet authenticated.");
				dataQueue.Add(data);
				return;
			}

			// Deal with data in queue first so things don't get sent out of order
			if (dataQueue.Count > 0) {
				object obj = dataQueue[0];
				dataQueue.RemoveAt(0);
				SendData(obj);
			}

			string j = JsonConvert.SerializeObject(data);
			ws.Send(j);
		}

		public void SendMessage(string ChannelID, string content) {
			if (!authenticated) {
				msgQueue.Add(content); return;
			}

			// Deal with data in queue first so things don't get sent out of order
			if (msgQueue.Count > 0) {
				string msg = msgQueue[0];
				msgQueue.RemoveAt(0);
				SendMessage(ChannelID, msg);
			}

			Constants.NewMessage newmsg = new Constants.NewMessage(content);
			int status = rest.POST(REST.BaseURL + "/channels/" + ChannelID + "/messages", newmsg);

			if (status == 429) {
				// Wait 2 seconds and retry when too many requests
				Thread.Sleep(2000);
				SendMessage(ChannelID, content);
			}
		}

		public void SendMessage(string ChannelID, Constants.Embed embed) {
			if (!authenticated) {
				embedQueue.Add(embed); return;
			}

			// Deal with data in queue first so things don't get sent out of order
			if (msgQueue.Count > 0) {
				Constants.Embed e = embedQueue[0];
				embedQueue.RemoveAt(0);
				SendMessage(ChannelID, e);
			}

			Constants.NewMessage newmsg = new Constants.NewMessage(embed, "");
			int status = rest.POST(REST.BaseURL + "/channels/" + ChannelID + "/messages", newmsg);

			if (status == 429) {
				// Wait 2 seconds and retry when too many requests
				Thread.Sleep(2000);
				SendMessage(ChannelID, embed);
			}
		}

		void Dispach(Constants.WSPayload payload) {
			switch (payload.t) {
				case "READY":
					Constants.Ready ready = new Constants.Ready(payload.d);
					user = ready.data.user;
					session_id = ready.data.session_id;
				
					MCGalaxy.Logger.Log(LogType.ConsoleMessage, "Logged in as " + user.username + "#" + user.discriminator);
					authenticated = true;
					break;

				case "MESSAGE_CREATE":
					Constants.Message msg = new Constants.Message(payload.d);
					if (msg.data.channel_id != channelID || msg.data.author.id == user.id) break;

					string nick = msg.data.author.username;
					if (msg.data.member.nick != null) nick = msg.data.member.nick;

					OnMessageReceivedEvent.Call(nick, msg.data.content);
					break;

				case "MESSAGE_UPDATE":
				case "CHANNEL_UPDATE":
					break;

				case "GUILD_CREATE":
					Constants.GuildCreate data = new Constants.GuildCreate(payload.d);
					foreach (Constants.Channel channel in data.data.channels) {
						if (channel.id == channelID) {
							Debug("Successfully authenticated!");
						}
					}
					break;

				default:
					Debug("Unhandled dispach " + payload.t + ": " + payload.d);
					break;
			}
		}

		void OnMessage(object sender, MessageEventArgs e) {
			Constants.WSPayload payload = WS.Deserialize(e.Data);

			if (payload.s != null) sequence = payload.s.Value;

			switch (payload.op) {
				case Constants.OPCODE_DISPACH:
					Dispach(payload);
					break;

				case Constants.OPCODE_HELLO:
					TimeSpan delay = TimeSpan.FromMilliseconds(payload.d.Value<int>("heartbeat_interval"));
					if (heartbeatTask == null) heartbeatTask = Server.Background.QueueRepeat(Beat, null, delay);
					else heartbeatTask.Delay = delay;

					if (resuming) SendOP(Constants.OPCODE_RESUME);
					else SendOP(Constants.OPCODE_IDENTIFY);
					resuming = false;
					break;

				case Constants.OPCODE_ACK:
					beat = false;
					break;

				case Constants.OPCODE_INVALID_SESSION:
					Thread.Sleep(4000); // supposed to be a random number between 1 and 5 seconds. I swear I rolled the dice
					SendOP(Constants.OPCODE_IDENTIFY);
					break;

				case Constants.OPCODE_RECONNECT:
					Reset();
					break;

				default:
					Debug("Unhandled opcode " + payload.op.ToString() + ": " + payload.d);
					break;
			}
		}
	}

	public delegate void OnMessageReceived(string nick, string message);
	public sealed class OnMessageReceivedEvent : IEvent<OnMessageReceived> {
		public static void Call(string nick, string message) {
			if (handlers.Count == 0) return;
			CallCommon(pl => pl(nick, message));
		}
	}
}
