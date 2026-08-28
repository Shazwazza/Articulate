// This is kinda dumb, but it's how google made the material design, this ensures media cards are clickable if they contain a link
document.addEventListener('DOMContentLoaded', function () {
  Array.prototype.forEach.call(document.querySelectorAll('.mdl-card__media'), function (el) {
    var link = el.querySelector('a');
    if (!link) {
      return;
    }
    var target = link.getAttribute('href');
    if (!target) {
      return;
    }
    // Validate URL scheme to prevent javascript: protocol XSS
    if (target.startsWith('http://') || target.startsWith('https://') || target.startsWith('/')) {
      el.addEventListener('click', function () {
        location.href = target;
      });
    }
  });
});
