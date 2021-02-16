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
		public bool authenticated;
		string gatewayURL, botToken, channelID;	

		SchedulerTask heartbeatTask;
		int sequence;

		public Discord(string token, string channelid) {
			botToken = token;
			channelID = channelid;

			rest = new REST(token);
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

		string GetGateway() {
			Constants.Gateway data = rest.GET<Constants.Gateway>(REST.BaseURL + "/gateway");
			return data.url;
		}

		void OnClose(object sender, CloseEventArgs e) {
			Debug("Closed connection with code " + e.Code + " (" + e.Reason + ")");
			Dispose();
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
					data = new Constants.HeartBeat(sequence);
					break;
				case Constants.OPCODE_IDENTIFY:
					data = new Constants.Identify(botToken);
					break;
			}

			if (data == null) return;
			SendData(data, true);
		}

		void SendData(object data, bool NoAuthCheck = false) {
			if (!authenticated && !NoAuthCheck) {
				// TODO: Queue system?
				Debug("Data not sent. Not authenticated.");
				return;
			}
			string j = JsonConvert.SerializeObject(data);
			ws.Send(j);
		}

		public void SendMessage(string ChannelID, string content) {
			Constants.NewMessage newmsg = new Constants.NewMessage(content);
			rest.POST(REST.BaseURL + "/channels/" + ChannelID + "/messages", newmsg);
		}

		void Dispach(Constants.WSPayload payload) {
			switch (payload.t) {
				case "READY":
					Constants.Ready ready = new Constants.Ready(payload.d);
					user = ready.data.user;
				
					MCGalaxy.Logger.Log(LogType.ConsoleMessage, "Logged in as " + user.username + "#" + user.discriminator);
					break;

				case "MESSAGE_CREATE":
					Constants.Message msg = new Constants.Message(payload.d);
					if (msg.data.channel_id != channelID || msg.data.author.id == user.id) break;

					string nick = msg.data.author.username;
					if (msg.data.member.nick != null) nick = msg.data.member.nick;

					OnMessageReceivedEvent.Call(nick, msg.data.content);
					break;

				case "GUILD_CREATE":
					Constants.GuildCreate data = new Constants.GuildCreate(payload.d);
					foreach (Constants.Channel channel in data.data.channels) {
						if (channel.id == channelID) {
							Debug("Successfully authenticated!");
							authenticated = true;
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

					SendOP(Constants.OPCODE_IDENTIFY); // Init identify
					break;

				case Constants.OPCODE_ACK:
					// TODO: Handle acks.
					// "If a client does not receive a heartbeat ack between its attempts at sending heartbeats, it should immediately terminate the connection with a non-1000 close code, reconnect, and attempt to resume."
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
