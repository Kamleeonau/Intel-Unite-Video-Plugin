function get_html5_video(rootnote){
  // try to get HTML5 video directly
  var videotags = rootnote.getElementsByTagName('video');

  if (videotags.length > 0){

    // look for the src attribute
    if (videotags[0].src.length > 0){
      return videotags[0].src
    }else{
      // look for source tags instead
      var sourcetags = videotags[0].getElementsByTagName('source');
      for (var i=0; i<sourcetags.length; i++){
        if (sourcetags[i].src.length > 0){
          // let's use this one.
          return sourcetags[i].src;
        }
      }
    }
  }

  return "";

}

function unite_find_video_url(){
  // Some video URLs can be played directly via VLC. Is our URL one of those?
  var url = window.location.toString();

  var arrURLMatch = Array(
    RegExp(/^https?:\/\/(www|go)\.twitch\.tv\/.+/),
    RegExp(/^https?:\/\/(www|gaming)\.youtube\.com\/(watch\?|live\?|v\/|embed\/).+/)
  )

  for (var i=0; i<arrURLMatch.length; i++){
    if (arrURLMatch[i].test(url)){
        console.log ("Matched rule");
        return url;
    }
  }

  // Handler for ClickView
  if (RegExp(/^https?:\/\/online\.clickview\.com\.au\/.+\/videos\/.+/).test(url)){
    console.log("ClickView detected");
    //https://webplayer.clickview.com.au/home/ev?vid=5790748
  }

  // Look in the DOM tree for a <video> tag
  var vid = get_html5_video(document);
  if (vid.length > 0){
    return vid;
  }

  // try to see if the video is in an iframe
  var iframes = document.getElementsByTagName('iframe');
  for (var i=0; i<iframes.length; i++){
    var iframe = iframes[i];

    // try to get the iFrame content directly
    var iframeDocument = iframe.contentDocument;
    if (iframeDocument != null){
      console.log("Got iframe content");
      vid = get_html5_video(iframeDocument);
      if (vid.length > 0){
        return vid;
      }
    }else{
      console.log("Unable to get iFrame content, trying to load URL");
      var request = new XMLHttpRequest();
      request.open('GET', iframe.src, false);  // `false` makes the request synchronous
      request.send(null);

      if (request.status === 200) {
        // got the iframe content, grab the JSON
        var r = RegExp(/.param\s*=\s*({.*})/gm).exec(request.responseText);
        if (r.length == 2){
          // we got the param
          var params = JSON.parse(r[1]);
          if (params.Video.Chapters.length == 1){ // currently we only support streams with a single chapter
            // how to we pick a suitable stream quality?
            if (params.Video.Chapters[0].Has1080p){
              return params.Video.Chapters[0].P1080;
            }
            if (params.Video.Chapters[0].Has720p){
              return params.Video.Chapters[0].P720;
            }
            if (params.Video.Chapters[0].Has480p){
              return params.Video.Chapters[0].P480;
            }
            if (params.Video.Chapters[0].Has360p){
              return params.Video.Chapters[0].P360;
            }
            if (params.Video.Chapters[0].Has240p){
              return params.Video.Chapters[0].P240;
            }
            console.log("No suitable stream found");
          }
        }
      }
    }

  }
  // nothing found
  return "";
}

var url = unite_find_video_url();

if (url == ""){
  alert ("Unable to locate suitable video for native playback. You will need to present a display instead.")
}else{
  console.log("Sending video URL to Unite: " + url);
  chrome.runtime.sendMessage(url);
}