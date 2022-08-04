var elem = document.getElementById("heart-rate-value");
var body = document.querySelector("body");

function _clear() {
	elem.innerText = "?";
	body.setAttribute("data-invalid", "invalid");
}

function on_response() {
	var data = this.responseText;
	try {
		data = JSON.parse(data);
	} catch (e) {
		// Ignore exception, try again
		console.log("Error parsing JSON", e);
		_clear();
		return;
	}
	elem.innerText = data.value;
	body.removeAttribute("data-invalid");
}

function on_error() {
	console.log(`Error getting response from server: ${this.statusText}`);
	_clear();
}

function send_request() {
	var xhr = new XMLHttpRequest();
	xhr.addEventListener("load", on_response);
	xhr.addEventListener("error", on_error);
	xhr.open("GET", body.getAttribute("data-url"));
	xhr.send();
}

setInterval(send_request, 1000);
