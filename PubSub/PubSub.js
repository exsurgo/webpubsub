
/* 
*   PubSub Client 
*   
*   PubSub.open(callback);
*   PubSub.subscribe(channel, callback);
*   PubSub.unsubscribe(channel);
*   PubSub.publish(channel, data);
*   PubSub.query(channel, users);
*   PubSub.connect(channel, callback(user));
*   PubSub.disconnect(channel, callback(user));
*
*   Set global constant PUB_SUB_URL, or property PubSub.url
*
*   Requires shim for JSON.parse and JSON.stringify
*   Requires shim for WebSocket
*/
var PubSub = (function () {

    //Locals 
    var _websocket,
        _pubsub = this,
        _publishCallbacks = {},
        _connectCallbacks = {},
        _disconnectCallbacks = {},
        _queryCallbacks = {};

    /**** Public ****/

    //Open connection
    this.open = function (callback) {

        //Ensure open connection exists
        if (
                !_websocket //Not yet created
                || _websocket.readyState == 1 //Connection not yet established
                || _websocket.readyState == 4 //Connection closed
           ) {

            //Ensure websocket URL is set
            if (!window.PUB_SUB_URL && !_pubsub.url) {
                alert("Please set PUB_SUB_URL or PubSub.url");
            }

            //Open
            else {
                //Create connection
                _websocket = new WebSocket(PUB_SUB_URL || PubSub.url);
                //Open, change context to PubSub
                _websocket.onopen = function () { callback.call(_pubsub); };
                //Close
                _websocket.onclose = function () {
                    _websocket = null;
                    _publishCallbacks = {};
                    _connectCallbacks = {};
                    _disconnectCallbacks = {};
                    _queryCallbacks = {};
                };
                //Error
                _websocket.onerror = function (error) { console.log(error); };
                //Message
                _websocket.onmessage = onMessage;
            }

        }

        //Else run callback if connection exists
        else callback();

    };

    //Subscribe to channel
    this.subscribe = function (channel, callback) {

        //Persist subscription
        _publishCallbacks[channel] = callback;

        //Stringify message
        var data = JSON.stringify({ action: "subscribe", channel: channel });

        //Subscribe to channel
        _websocket.send(data);

        return _pubsub;
    };

    //Unsubscribe from channel
    this.unsubscribe = function (channel) {

        //Stringify message
        var data = JSON.stringify({ action: "unsubscribe", channel: channel });

        //Subscribe to channel
        _websocket.send(data);

        return _pubsub;

    };

    //Publish message to channel
    this.publish = function (channel, data) {

        //Stringify message
        data = JSON.stringify({ channel: channel, data: data });

        //Publish message
        _websocket.send(data);

        return _pubsub;
    };

    //User connected to channel
    this.connect = function (channel, callback) {
        _connectCallbacks[channel] = callback;
        return _pubsub;
    };

    //User disconnected from channel
    this.disconnect = function (channel, callback) {
        _disconnectCallbacks[channel] = callback;
        return _pubsub;
    };

    //Query all users on channel
    this.query = function (channel, callback) {

        //Save callback
        _queryCallbacks[channel] = callback;

        //Stringify message
        var data = JSON.stringify({ action: "query", channel: channel });

        //Send query message
        _websocket.send(data);

        return _pubsub;
    };

    /**** Private ****/

    //Message has been recieved
    function onMessage(message) {

        //Deserialize JSON
        message = JSON.parse(message.data);
        var data = message.data;

        //Connect
        if (message.action == "connect") {
            var callback = _connectCallbacks[message.channel];
            if (callback) callback(data);
        }

        //Disconnect
        else if (message.action == "disconnect") {
            var callback = _disconnectCallbacks[message.channel];
            if (callback) callback(data);
        }

        //Query
        else if (message.action == "query") {
            var callback = _queryCallbacks[message.channel];
            if (callback) {
                //Users are connected
                if (data && data.length) {
                    var users = [];
                    for (var i = 0; i < data.length; i++) {
                        users.push({ id: data[i][0], name: data[i][1] });
                    }
                    callback(users);
                }
                //No users connected
                else callback();
            }
        }

        //Publish
        else {
            var callback = _publishCallbacks[message.channel];
            if (callback) callback(message.data);
        }

    }

    return this;

})();