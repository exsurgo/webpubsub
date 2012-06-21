
//Open connection
PubSub.open(function () {

    //Create a channel, Can be any string
    var channel = "my-channel";
    
    //Subscribe to the channel
    this.subscribe(channel, function (data) {
        //...   
    })
    //Query other users current on the channel
    .query(channel, function (otherUsers) {
        //...
    })
    //Another user has connected to the channel
    .connect(channel, function (user) {
        //...
    })
    //Another user has disconnected from the channel
    .disconnect(channel, function (user) {
        //...
    })
    //Publish a message to the channel
    .publish(channel, { data: "Hello World" });

});


