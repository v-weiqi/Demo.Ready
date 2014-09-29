var keepTracking = true;
var GlobalTransactionId = "";
var GlobalFeed = "";



$(document).ready(function () {
    if (window.location.hash.replace("#", "") != "") {
        $('#Links').show();
        var workFlowId = window.location.hash.replace("#", "").split('|')[0];
        var title = window.location.hash.replace("#", "").split('|')[1];
        WorkFlow(workFlowId, title);
    }
});


function GoTo(appId, appVersion) {
    var url = "/FMS/AppDetail/?appId=" + appId + "&version=" + appVersion;
    window.location = url;
}

function DisplayFeed(feed, nextAction) {
    keepTracking = false;
    var div = $('#Continue');
    div.html(nextAction);
    var userDiv = $('#UserResponse');
    userDiv.show("slow");
    $("#feed").val(feed);
    GlobalFeed = feed;
}

function ShowStatus(transactionId) {

    var url = "/FMS/GetStatus/?transactionId=" + transactionId;

    $.ajax({
        url: url,
        cache: false
    }).done(function (response) {

        var div = $("#status");
        var trackHtml = "<table width='100%' style='border-width: 0px;'>";
        
        $.each(response, function (displayName, status) {
            trackHtml += "<tr><td colspan=2><B>" + displayName + "</B></td></tr>";
            for (var i = 0; i < status.length; i++) {
                var color = "red";
                if (status[i].Pass) {
                    color = "green";
                }

                if (!status[i].Pass && status[i].Status == "Vendor TC2 Validation") {
                    $('#FailReason').show();
                }

                var date = new Date(parseInt(status[i].Date.replace("/Date(", "").replace(")/", ""), 10));
                var divNumber = "a_"+Math.floor(Math.random() * 10000);

                trackHtml += "<tr>";

                
                var message = "";
                if (status[i].Log) {
                    message = "<img src='/Content/images/plus.gif'/><a href='javascript:void(0)' onclick='DisplayModal(\"" + divNumber + "\");'> <small>Message</small></a>";
                }

                var s = status[i].Status;
                if (s == "Feed Difference" && displayName == "Generate & Validate") {
                    s = "Perform <a href='javascript:void(0)' onclick='PerformFeedDiff(\"http://webpitest.blob.core.windows.net/appfeed/WebApplicationList.xml\");'>Feed Difference</a>";
                }
                
                if (s == "Feed Difference" && displayName == "Continue to Production") {
                    s = "Perform <a href='javascript:void(0)' onclick='PerformFeedDiff(\"http://go.microsoft.com/fwlink/?LinkID=233772\");'>Feed Difference</a>";
                }
                
                trackHtml += "<td style='border-width: 0px;'>&nbsp;&nbsp;&nbsp;" +
                "<span style='color: " + color + "'>" + s +"</span> " + message+
                "</td>";
                
                trackHtml += "<td style='border-width: 0px; width: 125px;'><small>";
                trackHtml += date.format("dd mmm yyyy hh:MMtt");
                trackHtml += "</small></td>";
                
                trackHtml += "</tr>";
                            
                if (status[i].Log) {
                    trackHtml += "<tr><td style='border-width: 0px;padding:0px;margin:0px;' colspan=2><span id='" + divNumber + "' style='display: none;'>" +
                        "<h2>" + status[i].Status + "</h2>" +
                        "<BR/>" + status[i].Log + "</span></td></tr>";
                }
            }

        });
        
        

        trackHtml += "</table>";
        div.html(trackHtml);


    });
}

function ShowErrors(transactionId) {

    var url = "/FMS/GetFaults/?transactionId=" + transactionId;

    $.getJSON(url, null, function (faults) {
        var div = $("#errors");
        var trackHtml = "<table width='100%' style='border-width: 0px;'>";
        for (var i = 0; i < faults.length; i++) {

            var date = new Date(parseInt(faults[i].TimeCreated.replace("/Date(", "").replace(")/", ""), 10));
            trackHtml += "<tr><td style='border-width: 0px;'>";
            trackHtml += date.format("dd mmm yyyy hh:MMtt");
            trackHtml += "" +
                "<BR/><b>" + faults[i].ActivityName +
                "</b><BR/><small>" + faults[i].FaultDetails +
                "</small></td></tr>";

        }
        trackHtml += "</table>";
        div.html(trackHtml);
    });
}

function DisplayTrackStatus(transactionId) {
    Track(transactionId);
    ShowStatus(transactionId);
    ShowErrors(transactionId);
    if (keepTracking) {
        setTimeout("DisplayTrackStatus('" + transactionId + "')", 2000);
    }
}

function Track(transactionId) {
    GlobalTransactionId = transactionId;
    $.getJSON("/FMS/Track/?transactionId=" + transactionId, null, function (data) {
        var div = $('#Track');
        var trackHtml = "<table><tr><th>Activity</th><th>State</th><th>Time</th></tr>";
        var nest = 0;
        var previousState = "";
        for (var i = 0; i < data.length; i++) {
            if (previousState == data[i].State && data[i].State == "Executing") {
                nest += 1;
            }
            if (previousState == data[i].State && data[i].State == "Closed") {
                nest -= 1;
            }
            previousState = data[i].State;
            trackHtml += printTrack(div, data[i], nest);
        }
        trackHtml += "</table>";
        div.html(trackHtml);
        div.show("slow");
    });
}

function printTrack(div, track, nest) {
    var jsonDate = track.TimeCreated;  // returns "/Date(1245398693390)/"; 
    var re = /-?\d+/;
    var m = re.exec(jsonDate);
    var myDate = new Date(parseInt(m[0]));
    var d = myDate.getHours() + ":" + myDate.getMinutes() + ":" + myDate.getSeconds() + ":" + myDate.getMilliseconds();

    var space = "";
    for (var i = 0; i < nest; i++) {
        space += "&nbsp;&nbsp;&nbsp;&nbsp;";
    }
    if (space != "") {
        space += "- ";
    }

    return ("<tr><td>" + space + track.ActivityName + "&nbsp;&nbsp;</td><td>" + track.State + "&nbsp;&nbsp;</td><td>" + d + "&nbsp;&nbsp;</td></tr>");
}


function hide(div) {
    var element = "#" + div;
    if ($(element).is(":visible")) {
        $(element + "_img").attr('src', '/Content/images/expand.png');
        $(element).hide("slow");
    } else {
        $(element + "_img").attr('src', '/Content/images/collapse.png');
        $(element).show("slow");
    }
}

function hide_small(div) {
    var element = "#" + div;
    if ($(element).is(":visible")) {
        $(element + "_img").attr('src', '/Content/images/plus.gif');
        $(element).hide("slow");
    } else {
        $(element + "_img").attr('src', '/Content/images/minus.gif');
        $(element).show("slow");
    }
}

