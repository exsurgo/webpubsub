
//Open connection
PubSub.open(function () {

    //Create a channel, Can be any string
    var channel = "my-channel";

    //Subscribe to the channel
    //"this" keyword refers to PubSub object
    this.subscribe(channel, function (data) {
        //...   
    })
    //Query other users subscribed to the channel
    //methods can also be chained
    .query(channel, function (otherUsers) {
        //...
    })
    //Another user has connected to the channel
    .connect(channel, function (user) {
        alert("User has id " + user.id + " and name " + user.name);
    })
    //Another user has disconnected from the channel
    .disconnect(channel, function (user) {
        //...
    })
    //Publish a message to the channel
    .publish(channel, "Hello World")
    //Can publish strings or objects
    .publish(channel, { id: 123, value: "Another Message" });

});


