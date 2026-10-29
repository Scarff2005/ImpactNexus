(function () {
  "use strict";

  const sidebar = document.getElementById("appSidebar");
  const toggle = document.getElementById("sidebarToggle");
  if (toggle && sidebar) {
    toggle.addEventListener("click", function (e) {
      e.preventDefault();
      sidebar.classList.toggle("open");
    });
    document.addEventListener("click", function (e) {
      if (window.innerWidth < 992 && sidebar.classList.contains("open") &&
          !sidebar.contains(e.target) && !toggle.contains(e.target)) {
        sidebar.classList.remove("open");
      }
    });
  }

  // Prevent double-submit on destructive forms.
  document.querySelectorAll("form[data-confirm]").forEach(function (form) {
    form.addEventListener("submit", function (e) {
      const message = form.getAttribute("data-confirm") || "Continue?";
      if (!window.confirm(message)) {
        e.preventDefault();
      }
    });
  });

  // Dismiss success/error alerts after a while.
  document.querySelectorAll(".alert[data-auto-dismiss]").forEach(function (alert) {
    setTimeout(function () {
      alert.style.transition = "opacity .4s";
      alert.style.opacity = "0";
      setTimeout(function () { alert.remove(); }, 450);
    }, 6000);
  });
})();
