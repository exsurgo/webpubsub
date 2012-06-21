using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Security;
using SuperWebSocket;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketEngine;

namespace WebPubSub
{
    /// <summary>
    /// Provides access to the PubSub server
    /// </summary>
    public static class PubSub
    {
        #region Public

        /// <summary>
        /// Start the PubSub server on application start
        /// </summary>
        public static void StartServer()
        {
            PubSubServer.Current.Start();
        }

        /// <summary>
        /// Shut down the PubSub server on application end
        /// </summary>
        public static void StopServer()
        {
            PubSubServer.Current.Start();
        }

        /// <summary>
        /// Publish data to all users subscribed to provided channel
        /// </summary>
        /// <remarks>If the current user's ID is provided, they will not receive the message</remarks>
        public static void Publish(string channel, object data, object userId = null)
        {
            var user = new PubSubServer.PubSubUser();
            if (userId != null) user.id = userId.ToString();

            //Send message to all users in channel
            PubSubServer.Current.PublishToChannel(
                    PubSubServer.OutgoingAction.Publish,
                    channel,
                    data,
                    user
                );
        }

        /// <summary>
        /// Get all users subscribed to provided channel
        /// </summary>
        /// <returns>
        /// Returns a dictionary of users, with key as ID, and value as Name
        /// </returns>
        public static Dictionary<string, string> GetUsers(string channel)
        {
            return PubSubServer.Current.GetUsers(channel);
        }

        #endregion

        #region Internal

        internal class PubSubServer : IDisposable
        {
            #region Instances

            private Timer _timer;
            private WebSocketServer _server;

            private static PubSubServer _current;
            public static PubSubServer Current
            {
                get
                {
                    if (_current == null) _current = new PubSubServer();
                    return _current;
                }
            }

            #endregion

            #region State

            private readonly object SessionLock = new object();
            private readonly object ChannelLock = new object();

            private Dictionary<string, WebSocketSession> _sessions;
            public Dictionary<string, WebSocketSession> Sessions
            {
                get
                {
                    if (_sessions == null) _sessions = new Dictionary<string, WebSocketSession>();
                    return _sessions;
                }
            }

            private Dictionary<string, List<string>> _channels;
            public Dictionary<string, List<string>> Channels
            {
                get
                {
                    if (_channels == null) _channels = new Dictionary<string, List<string>>();
                    return _channels;
                }
            }

            #endregion

            #region WebSocket Events

            private void OnOpen(WebSocketSession session)
            {
                //Get user's id
                var user = GetUserInfo(session);

                //Only store forms auth cookie, Remove the rest
                if (session.Cookies != null)
                {
                    var authCookieName = FormsAuthentication.FormsCookieName.ToLower();
                    var toRemove = new List<string>();
                    foreach (DictionaryEntry cookie in session.Cookies)
                    {
                        var key = cookie.Key.ToString().ToLower();
                        if (key != authCookieName) toRemove.Add(key);
                    }
                    foreach (var key in toRemove) session.Cookies.Remove(key);
                }

                //Store user session
                if (user != null && !Sessions.ContainsKey(user.id))
                {
                    lock (SessionLock)
                    {

                        Sessions.Add(user.id, session);
                    }
                }
            }

            private void OnMessage(WebSocketSession session, string data)
            {
                var serializer = new JavaScriptSerializer();
                var obj = (IncomingMessage)serializer.Deserialize(data, typeof(IncomingMessage));
                var user = GetUserInfo(session);

                //Subscribe
                if (obj.Action == IncomingAction.Subscribe) SubscribeToChannel(obj.Channel, user);

                //Unsubscribe
                else if (obj.Action == IncomingAction.Unsubscribe) UnsubscribeToChannel(obj.Channel, user);

                //Query
                else if (obj.Action == IncomingAction.Query) ReturnUsersInChannel(obj.Channel, session);

                //Publish
                else PublishToChannel(OutgoingAction.Publish, obj.Channel, obj.Data, user);
            }

            private void OnClose(WebSocketSession session, CloseReason reason)
            {
                //Do nothing if is shutdown
                if (reason == CloseReason.ServerShutdown) return;

                //Get user's id
                var user = GetUserInfo(session);

                //Remove user
                if (user != null && user.id != null)
                {
                    lock (SessionLock)
                    {
                        //Remove user session
                        Sessions.Remove(user.id);

                        //Check each channel one by one
                        //TODO: May need to create an index in future
                        var toNotify = new List<string>();
                        foreach (var channel in Channels)
                        {
                            //Remove user from channels
                            if (channel.Value.Remove(user.id))
                            {
                                //Notify other users after loop has completed
                                toNotify.Add(channel.Key);
                            }
                        }

                        //Notify all other users in channel that user has disconnected
                        for (int i = 0; i < toNotify.Count; i++)
                        {
                            var channelName = toNotify[i];
                            PublishToChannel(OutgoingAction.Disconnect, channelName, user, user);
                        }
                    }
                }
            }

            #endregion

            #region Methods

            //Start the server
            internal void Start()
            {
                //Security
                var secure = GetSetting<bool>("PubSubEnableSecurity");
                CertificateConfig cert = null;
                if (secure) cert = new CertificateConfig
                {
                    FilePath = HttpContext.Current.Server.MapPath(GetSetting<string>("PubSubCertPath")),
                    Password = GetSetting<string>("PubSubCertPassword"),
                    IsEnabled = true
                };

                //Setup server
                _server = new WebSocketServer();
                _server.Setup(new RootConfig(),
                             new ServerConfig
                             {
                                 Name = "PubSub",
                                 ServiceName = "PubSubServer",
                                 Ip = "Any",
                                 Port = GetSetting<int>("PubSubPort"),
                                 Mode = SocketMode.Async,
                                 Security = secure ? "tls" : null,
                                 Certificate = cert
                             }, SocketServerFactory.Instance);
                _server.NewMessageReceived += OnMessage;
                _server.NewSessionConnected += OnOpen;
                _server.SessionClosed += OnClose;
                _server.Start();

                //Start flash socket policy server
                if (GetSetting<bool>("PubSubEnableFlashPolicyServer")) StartFlashPolicyServer();

                //Clean up empty sessions and channels on timer
                var span = new TimeSpan(0, 1, 0, 0, 0); //30 minutes
                _timer = new Timer(CleanUp, null, span, span);
            }

            //Stop the server
            internal void Stop()
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                _timer.Dispose();
                _tcpListener.Stop();
                SocketServerManager.Stop();
            }

            //Get all users in a channel
            internal Dictionary<string, string> GetUsers(string channel)
            {
                //Check if channel exists
                if (Channels.ContainsKey(channel))
                {
                    //Get channel users
                    var users = Channels[channel];

                    //Return dictionary with id as key and full name as value
                    var dictionary = new Dictionary<string, string>();
                    foreach (var id in users)
                    {
                        if (Sessions.ContainsKey(id))
                        {
                            var session = Sessions[id];
                            var user = GetUserInfo(session);
                            if (user != null)
                            {
                                dictionary.Add(user.id, user.name);
                            }
                        }
                    }

                    return dictionary;
                }

                //If doesn't exist, return null
                return null;
            }

            //Publish data to a specified channel
            internal void PublishToChannel(OutgoingAction action, string channel, object data, PubSubUser pubSubUser)
            {
                //Don't publish null or empty string
                if (data == null || (data is string && (string)data == "")) return;

                //Get channel if exists
                if (Channels.ContainsKey(channel))
                {
                    //Get channel users
                    var users = Channels[channel];

                    //Get each user in channel
                    foreach (var userId in users)
                    {
                        //Don't publish to current user
                        if (userId == pubSubUser.id) continue;

                        //Get user's websocket session
                        var session = Sessions[userId];
                        if (session != null)
                        {
                            //Create string data object
                            var serializer = new JavaScriptSerializer();
                            string actionStr = (action == OutgoingAction.Publish ? null : action.ToString().ToLower());
                            var stringData = serializer.Serialize(new { action = actionStr, channel, data });

                            //Send message to user
                            session.SendResponseAsync(stringData);
                        }

                        //Remove user from channel if no session exists
                        else
                        {
                            lock (ChannelLock)
                            {
                                users.Remove(userId);
                            }
                        }
                    }

                    //Remove channel if no users
                    if (users.Count == 0) Channels.Remove(channel);
                }
            }

            //Subscribe user to channel
            internal void SubscribeToChannel(string channel, PubSubUser pubSubUser)
            {
                //Ensure channel exists
                if (!Channels.ContainsKey(channel))
                {
                    lock (ChannelLock)
                    {
                        Channels.Add(channel, new List<string>());
                    }
                }
                var users = Channels[channel];

                //Add user to channel if not exists
                if (!users.Contains(pubSubUser.id))
                {
                    users.Add(pubSubUser.id);

                    //Send notification to all users in channel that user has connected
                    PublishToChannel(OutgoingAction.Connect, channel, pubSubUser, pubSubUser);
                }
            }

            //Unsubscribe user from channel
            internal void UnsubscribeToChannel(string channel, PubSubUser pubSubUser)
            {
                if (Channels.ContainsKey(channel))
                {
                    //Get channel
                    var users = Channels[channel];
                    if (users != null)
                    {
                        //Remove user from channel
                        if (users.Remove(pubSubUser.id))
                        {
                            //Remove channel if empty
                            if (users.Count == 0)
                            {
                                lock (ChannelLock)
                                {
                                    Channels.Remove(channel);
                                }
                            }

                            //Notify all other users in channel that user has disconnected
                            PublishToChannel(OutgoingAction.Disconnect, channel, pubSubUser, pubSubUser);
                        }
                    }
                }
            }

            //Return all users subscribed to channel
            private void ReturnUsersInChannel(string channel, WebSocketSession session)
            {
                var serializer = new JavaScriptSerializer();
                Array[] rows = null;

                //Get users in channel
                var users = GetUsers(channel);

                //If users exists
                if (users != null && users.Count > 0)
                {
                    //Create array with user id and name
                    var count = users.Count;
                    rows = new Array[count];
                    var i = 0;
                    foreach (var item in users)
                    {
                        rows[i] = new[] { item.Key, item.Value }; // [[id,name],[id,name]]
                        i++;
                    }
                }

                //Serialize data
                var data = serializer.Serialize(new
                {
                    action = OutgoingAction.Query.ToString().ToLower(),
                    channel,
                    data = rows
                });

                //Return array of users to current user
                //If no users, then send null
                session.SendResponseAsync(data);
            }

            //Clean up empty/null sessions and channels
            private void CleanUp(object state)
            {
                //Items to remove
                var sessionsToRemove = new List<string>();
                var channelsToRemove = new List<string>();

                //Check each session
                foreach (var session in Sessions)
                {
                    //Check for null
                    if (session.Value == null) sessionsToRemove.Add(session.Key);
                }

                //Check each channel
                foreach (var channel in Channels)
                {
                    //Check for empty or null
                    if (channel.Value.Count == 0 || channel.Value == null) channelsToRemove.Add(channel.Key);
                }

                //Remove null sessions
                lock (SessionLock)
                {
                    foreach (var session in sessionsToRemove) Sessions.Remove(session);
                }

                //Remove empty/null channels
                lock (ChannelLock)
                {
                    foreach (var channel in channelsToRemove) Channels.Remove(channel);
                }
            }

            //Get user id and name from forms auth cookie
            //Important, Auth cookie must be set to HttpOnly if using Flash fallback
            private PubSubUser GetUserInfo(WebSocketSession session)
            {
                //TODO: Add additional options here
                if (session.Cookies != null && session.Cookies[FormsAuthentication.FormsCookieName] != null)
                {
                    var cookie = session.Cookies[FormsAuthentication.FormsCookieName];
                    var data = FormsAuthentication.Decrypt(cookie);
                    var user = new PubSubUser();
                    user.id = data.Name;
                    user.name = data.UserData;
                    return user;
                }
                return null;
            }

            #endregion

            #region Helpers

            //Get config setting
            private T GetSetting<T>(string setting)
            {
                if (ConfigurationManager.AppSettings[setting] != null)
                {
                    var value = ConfigurationManager.AppSettings[setting];
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
                    return (T)converter.ConvertFrom(value);
                }
                else
                {
                    throw new Exception("Application setting '" + setting + "' does not exist.");
                }
            }

            #endregion

            #region Policy Server

            private TcpListener _tcpListener;

            private void StartFlashPolicyServer()
            {
                _tcpListener = new TcpListener(IPAddress.Any, 843);
                _tcpListener.Start();
                _tcpListener.BeginAcceptSocket(ReturnFlashPolicy, _tcpListener);
            }

            private void ReturnFlashPolicy(IAsyncResult result)
            {
                TcpListener listener = (TcpListener)result.AsyncState;
                Socket client = listener.EndAcceptSocket(result);
                NetworkStream stream = new NetworkStream(client);
                StreamReader reader = new StreamReader(stream);
                StreamWriter writer = new StreamWriter(stream);

                //Create policy XML
                var port = GetSetting<int>("PubSubPort");
                var response = "<cross-domain-policy>" +
                                    "<site-control permitted-cross-domain-policies=\"all\" />" +
                                    "<allow-access-from domain=\"*\" to-ports=\"" + port + "\" />" +
                                "</cross-domain-policy>\0";

                //Return policy
                reader.Read();
                writer.Write(response);
                writer.Flush();
                stream.Flush();

                //Close all
                writer.Close();
                reader.Close();
                stream.Close();
                client.Close();

                //Restart listener
                _tcpListener.BeginAcceptSocket(new AsyncCallback(ReturnFlashPolicy), _tcpListener);
            }

            #endregion

            #region Internal Objects

            internal class PubSubUser
            {
                public string id { get; set; }
                public string name { get; set; }
            }

            internal class IncomingMessage
            {
                public IncomingAction Action { get; set; }
                public string Channel { get; set; }
                public object Data { get; set; }
            }

            internal enum IncomingAction
            {
                Publish,
                Subscribe,
                Unsubscribe,
                Query
            }

            internal enum OutgoingAction
            {
                Publish,
                Connect,
                Disconnect,
                Query
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                Current.Stop();
            }

            #endregion
        }

        #endregion
    }
}