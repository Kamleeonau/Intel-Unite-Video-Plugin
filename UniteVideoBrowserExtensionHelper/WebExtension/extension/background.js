var port = chrome.runtime.connectNative("au.com.paprice.intelunite");

port.onMessage.addListener((response) => {
  console.log("Received: " + response);
  var message = response;
  switch (response){
    case "ERR:NOTCONNECT":
      message = "Unable to send video to Intel Unite(R) App.\\n\\nYou do not appear to be connected.";
      break;
    case "ERR:CONNECTERROR":
      message = "Unable to connect to Intel Unite(R) App for Native Playback.\\n\\nThe Native Video plugin may not be installed.";
      break;
    case "ERR:NOTALLOWED":
      message = "Sorry, you do not have permission to present to this display currently.";
      break;
    case "OK":
      message = "Video has been sent to Intel Unite App for playback.\\n\\nYou may use your computer as normal, and control video playback from inside the Intel Unite(R) Client.";
  }
  messagebox(message);
});


chrome.runtime.onMessage.addListener((response) => {
  console.log("Got message from the browser");
  console.log(response);
  if (response.length > 0){
    // assumed to be a url to parse to the player
    port.postMessage(response);
  }
});

/*
On a click on the browser action, send the app a message.
*/
chrome.browserAction.onClicked.addListener(() => {
  console.log("Button clicked");
  // inject the content script into the current tab
  chrome.tabs.executeScript({file : '/content.js'});
});

function messagebox(message){
  console.log(message);
  chrome.tabs.executeScript({code : 'alert("' + message + '");'});
}