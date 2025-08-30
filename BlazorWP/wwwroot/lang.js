window.setHtmlLang = function (code) {
  document.documentElement.setAttribute("lang", code);
};
window.saveLang = function (code) {
  localStorage.setItem("ui-lang", code);
};
window.loadLang = function () {
  return localStorage.getItem("ui-lang");
};
