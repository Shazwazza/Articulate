$(document).ready(function () {

    function getFeedContent(df) {
        $.get(df.data("feed-base-url") + "ArticulateFeeds/" + df.data("feed-action") + "/" + df.data("feed-id"), function (data) {
            df.html(data);
        });
    }

    var feeds = $("#feeds .feed");
    feeds.each(function(i, f) {
        getFeedContent($(f));
    });
});