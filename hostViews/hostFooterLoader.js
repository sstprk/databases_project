document.addEventListener("DOMContentLoaded", function () {
    fetch("hostFooter.html")
      .then(response => response.text())
      .then(data => {
        document.getElementById("footer-container").innerHTML = data;
      })
      .catch(error => console.error("Footer yüklenemedi:", error));
  });
  