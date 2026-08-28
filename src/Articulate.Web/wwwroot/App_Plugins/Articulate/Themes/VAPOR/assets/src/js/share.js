document.addEventListener('DOMContentLoaded', function () {
    const shareButtons = document.querySelectorAll('[data-share-button]');

    // If Web Share API is not supported, hide the buttons
    if (!navigator.share) {
        shareButtons.forEach(btn => btn.style.display = 'none');
        return;
    }

    shareButtons.forEach(btn => {
        btn.addEventListener('click', async function (e) {
            e.preventDefault();

            const title = this.getAttribute('data-title') || document.title;
            const text = this.getAttribute('data-text') || '';
            // Fallback to current URL if data-url is missing
            const url = this.getAttribute('data-url') || window.location.href;

            try {
                await navigator.share({
                    title: title,
                    text: text,
                    url: url
                });
            } catch (err) {
                // AbortError is normal (user cancelled the share)
                if (err.name !== 'AbortError') {
                    console.error('Error sharing:', err);
                }
            }
        });
    });
});
