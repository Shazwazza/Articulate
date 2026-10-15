// Wait for DOM to be ready before binding events
document.addEventListener('DOMContentLoaded', function() {
  // Find the first element with class 'skip-link' and add a click event listener
  const skipLink = document.querySelector('.skip-link');

  if (!skipLink) {
    return; // Exit gracefully if skip-link doesn't exist
  }

  skipLink.addEventListener('click', function(e) {
    // Get the href value (like '#main-content') and find that element
    const target = document.querySelector(this.getAttribute('href'));

    // If the target element exists
    if (target) {
      // Prevent the default anchor link behavior
      e.preventDefault();

      // Make the target focusable (if it isn't already)
      if (!target.hasAttribute('tabindex')) {
        target.setAttribute('tabindex', '-1');
      }
      // Move focus to the target element
      target.focus();
    }
  });
});