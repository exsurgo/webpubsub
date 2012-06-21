
$(function () {

    var users = [],
        userDisplay = $("#user-display"),
        userList = $("div > span"),
        textboxes = $("input[type=text],textarea"),
        selects = $("select"),
        save = $(":submit");

    PubSub.open(function () {

        //Query current users
        this.query("concurrency", function (others) {
            if (others) {
                users = others;
                displayUsers();
            }
        });

        //Subscribe to concurrency channel
        this.subscribe("concurrency", function (data) {

            //Lock
            if (data.type == "lock") $("[name=" + data.name + "]").attr("disabled", "true");

            //Unlock
            else if (data.type == "unlock") {
                var textbox = $("[name=" + data.name + "]");
                textbox.removeAttr("disabled").val(data.value);
            }

            //Key press
            else if (data.type == "keypress") {
                var textbox = $("[name=" + data.name + "]");
                var c = String.fromCharCode(data.key);
                textbox.val(textbox.val() + c);
            }

            //Select
            else if (data.type == "select") $("[name=" + data.name + "]").val(data.value);

            //Save
            else if (data.type == "save") {
                $("input,textarea,select").attr("disabled", "true");
                alert(data.user + " has saved the form.");
            }

        });

        //User connected
        this.connect("concurrency", function (user) {
            users.push(user);
            displayUsers();
        });

        //User disconnected
        this.disconnect("concurrency", function (user) {
            var newArray = [];
            $(users).each(function () {
                if (this.id != user.id) newArray.push(this);
            });
            users = newArray;
            displayUsers();
        });

        //Lock textbox on focus
        textboxes.focus(function () {
            var textbox = $(this);
            PubSub.publish("concurrency",
            {
                type: "lock",
                name: textbox.attr("name")
            });
        });

        //Unlock textbox on blur
        textboxes.blur(function () {
            var textbox = $(this);
            PubSub.publish("concurrency",
            {
                type: "unlock",
                name: textbox.attr("name"),
                value: textbox.val()
            });
        });

        //Textbox keypress
        textboxes.keypress(function (e) {
            if (e.which > 0 && // check that key code exists
                e.which != 8 && // allow backspace
                e.which != 32 && e.which != 45 && // allow space and dash
                !(e.which >= 48 && e.which <= 57) && // allow 0-9
                !(e.which >= 65 && e.which <= 90) && // allow A-Z
                !(e.which >= 97 && e.which <= 121)   // allow a-z
            ) {
                e.preventDefault();
            }
            else {
                PubSub.publish("concurrency",
                {
                    type: "keypress",
                    name: $(this).attr("name"),
                    key: e.which
                });
            }
        });

        //Select list change
        selects.change(function () {
            var select = $(this);
            PubSub.publish("concurrency",
            {
                type: "select",
                name: select.attr("name"),
                value: select.val()
            });
        });

        //Save
        save.click(function (e) {
            e.preventDefault();
            PubSub.publish("concurrency", { type: "save", user: USER.name });
            alert("Form saved");
        });

        //Helpers

        //Display other users editing form
        function displayUsers() {
            if (users && users.length) {
                var str = "";
                $(users).each(function () {
                    str += this.name + ", ";
                });
                str = str.replace(/, $/, "");
                userList.text(str);
                userDisplay.show();
            }
            else {
                userDisplay.hide();
            }
        }

    });

});