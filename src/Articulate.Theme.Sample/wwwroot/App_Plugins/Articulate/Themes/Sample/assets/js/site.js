document.addEventListener("DOMContentLoaded", () => {
    const navToggle = document.querySelector("[data-nav-toggle]");
    const siteNav = document.getElementById("site-nav");
    const toast = document.querySelector("[data-site-toast]");
    let toastTimer;

    const showToast = (message) => {
        if (!(toast instanceof HTMLElement)) {
            return;
        }

        toast.textContent = message;
        toast.hidden = false;
        toast.classList.add("is-visible");
        clearTimeout(toastTimer);
        toastTimer = window.setTimeout(() => {
            toast.classList.remove("is-visible");
            window.setTimeout(() => {
                toast.hidden = true;
            }, 180);
        }, 1800);
    };

    if (navToggle instanceof HTMLButtonElement && siteNav) {
        navToggle.addEventListener("click", () => {
            const expanded = navToggle.getAttribute("aria-expanded") === "true";
            navToggle.setAttribute("aria-expanded", expanded ? "false" : "true");
            siteNav.classList.toggle("is-open", !expanded);
        });
    }

    document.querySelectorAll("[data-share-button]").forEach((button) => {
        button.addEventListener("click", async () => {
            const title = button.getAttribute("data-title") ?? document.title;
            const url = button.getAttribute("data-url") ?? window.location.href;

            try {
                if (navigator.share) {
                    await navigator.share({ title, url });
                    return;
                }

                if (navigator.clipboard?.writeText) {
                    await navigator.clipboard.writeText(url);
                    showToast("Link copied");
                    return;
                }
            } catch (error) {
                if (error instanceof DOMException && error.name === "AbortError") {
                    return;
                }
            }

            window.prompt("Copy this link", url);
        });
    });
});
