
PubSub.open(function () {

    var channel = "my-channel";

    this.subscribe(channel, function (data) {

    })
    .query(channel, function (users) {

    })
    .connect(channel, function (user) {

    })
    .disconnect(channel, function (user) {

    })
    .publish(channel, { data: "Hello World" });

});


