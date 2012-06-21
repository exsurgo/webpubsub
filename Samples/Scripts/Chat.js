
$(function () {

    var chat = $("td:first"),
        textbox = $("[type=text]"),
        button = $("[type=button]"),
        usersList = $("ul:first");

    //Open chat pub/sub
    PubSub.open(function () {

        //Query current users
        this.query("chat", function (users) {
            //All all users to list
            $(users).each(function () {
                addUser(this);
            });
        });

        //Subscribe to chat
        this.subscribe("chat", function (data) {
            write(data);
        });

        //Add current user
        addUser({ id: USER.id, name: USER.name });
        write(USER.name + " entered the chat");

        //User connected
        this.connect("chat", function (user) {
            write(user.name + " entered the chat");
            addUser(user);
        });

        //User disconnected
        this.disconnect("chat", function (user) {
            write(user.name + " left the chat");
            $("#" + user.id).remove();
        });

        //Publish message to chat
        function send() {
            var message = textbox.val();
            PubSub.publish("chat", message);
            textbox.val("");
            write(message);
        }
        button.click(send);
        textbox.keypress(function (e) {
            if (e.which == 13) send();
        });

        //Helpers

        //Write to chat
        function write(text) {
            chat.append("<div style='padding:3px'>" + text + "</div>");
        }

        //Add user to list
        function addUser(user) {
            if (!usersList.find("#" + user.id).length) {
                usersList.append("<li id='" + user.id + "'>" + user.name + "</li>");
            }
        }

    });

});