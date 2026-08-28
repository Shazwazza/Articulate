import { apiService } from "./apiService.js";
import { authService } from "./authService.js";
import { config, initConfig } from "./config.js";
import { formatApiError } from "./error-formatter.js";

const DEFAULT_RETRY_LABEL = "Retry now";

function createEmptyPost(existingNodeId) {
  return {
    articulateBlogNode: existingNodeId ?? null,
    title: "",
    body: "",
    excerpt: "",
    tags: "",
    categories: "",
    slug: "",
  };
}

document.addEventListener("alpine:init", () => {
  const alpine = window.Alpine;
  if (!alpine) {
    console.error(
      "[MarkdownEditor] Alpine.js is not available on window. Component initialisation aborted."
    );
    return;
  }

  alpine.data("markdownEditor", () => ({
    isLoading: true,
    errorDetails: null,
    titleTouched: false,
    post: createEmptyPost(),
    fileMap: new Map(),
    successUrl: null,
    currentUser: null,
    retryAction: null,
    retryLabel: DEFAULT_RETRY_LABEL,
    caption: "Create New Post",
    currentStep: "loading",
    editorInstance: null,
    loginNotice: "Please sign in below.",

    get hasErrorDetails() {
      return (
        this.errorDetails &&
        this.errorDetails.details &&
        this.errorDetails.details.length > 0
      );
    },

    get errorTitle() {
      if (!this.errorDetails || !this.errorDetails.title) {
        return "";
      }

      return this.errorDetails.title.toString().trim();
    },

    get isNotLoading() {
      return !this.isLoading;
    },

    get userInitials() {
      if (!this.currentUser || !this.currentUser.name) return "...";
      const nameParts = this.currentUser.name.trim().split(" ");
      if (nameParts.length > 1) {
        return `${nameParts[0][0]}${nameParts[nameParts.length - 1][0]
          }`.toUpperCase();
      }
      return nameParts[0][0] ? nameParts[0][0].toUpperCase() : "";
    },

    get postTitle() {
      return this.post.title;
    },

    get postTags() {
      return this.post.tags;
    },

    get postCategories() {
      return this.post.categories;
    },

    get postExcerpt() {
      return this.post.excerpt;
    },

    get postSlug() {
      return this.post.slug;
    },

    get titleErrorClass() {
      const hasTitle = this.post.title.trim().length > 0;
      return !hasTitle && this.titleTouched ? "is-invalid" : "";
    },

    get titleCssClass() {
      const classes = [];
      if ((this.post.title ?? "").trim().length > 0) {
        classes.push("is-dirty");
      }

      if (this.titleTouched) {
        classes.push("is-touched");
      }

      const errorClass = this.titleErrorClass;
      if (errorClass) {
        classes.push(errorClass);
      }

      return classes.join(" ");
    },

    get tagsCssClass() {
      return this.computeFieldCssClass("tags");
    },

    get categoriesCssClass() {
      return this.computeFieldCssClass("categories");
    },

    get excerptCssClass() {
      return this.computeFieldCssClass("excerpt");
    },

    get slugCssClass() {
      return this.computeFieldCssClass("slug");
    },

    get bodyLabelClass() {
      const hasContent = (this.post.body ?? "").trim().length > 0;
      return hasContent ? "sr-only" : "";
    },

    computeFieldCssClass(field) {
      const value = (this.post[field] ?? "").trim();
      return value.length > 0 ? "is-dirty" : "";
    },

    createUploadToken() {
      const timestamp = Date.now().toString(36);
      const randomPart = Math.random().toString(36).slice(2, 10);
      return `tmp:${timestamp}_${randomPart}`;
    },

    escapeMarkdownText(value) {
      if (!value) {
        return "";
      }

      return value.replace(/([\\\[\]])/g, "\\$1");
    },

    get isEditorStep() {
      return this.currentStep === "editor";
    },

    get isOptionalStep() {
      return this.currentStep === "optional";
    },

    get isSuccessStep() {
      return this.currentStep === "success";
    },

    get isLoginStep() {
      return this.currentStep === "login";
    },

    get isLoginContext() {
      return this.currentStep === "login" || this.currentStep === "loading";
    },

    get loginBodyClass() {
      return {
        "is-login-step": this.isLoginContext,
        "is-editor-step": this.isEditorStep,
        "is-optional-step": this.isOptionalStep,
        "is-success-step": this.isSuccessStep,
      };
    },

    get showHeader() {
      return !this.isLoginContext;
    },

    get hasRetryAction() {
      return typeof this.retryAction === "function";
    },

    get isRetryDisabled() {
      return this.isLoading || !this.hasRetryAction;
    },

    get canShowNextButton() {
      return this.currentStep === "editor";
    },

    get canShowPublishButton() {
      return this.currentStep === "optional";
    },

    get canProceedToOptional() {
      const titleFilled = this.post.title.trim().length > 0;
      const bodyFilled = (this.post.body ?? "").trim().length > 0;
      return titleFilled && bodyFilled;
    },

    get canPublish() {
      return this.canProceedToOptional;
    },

    get disableNextButton() {
      return !this.canProceedToOptional;
    },

    get disablePublishButton() {
      return !this.canPublish;
    },

    focusRef(refName) {
      const reference = this.$refs?.[refName];
      const element = Array.isArray(reference) ? reference[0] : reference;
      if (element && typeof element.focus === "function") {
        element.focus();
      }
    },

    setStep(step) {
      const previousStep = this.currentStep;
      this.currentStep = step;

      this.$nextTick(() => {
        const componentHandler = window.componentHandler;
        if (componentHandler && typeof componentHandler.upgradeDom === "function") {
          componentHandler.upgradeDom();
        }
      });

      const shouldResetBanner =
        previousStep !== step &&
        (step === "editor" || step === "optional" || step === "success");
      if (shouldResetBanner) {
        this.errorDetails = null;
        this.clearRetryAction();
      }

      switch (step) {
        case "editor":
          this.caption = "New Post";
          break;
        case "optional":
          this.caption = "Optional Details";
          break;
        case "success":
          this.caption = "Post Successful";
          break;
        case "login":
          this.caption = "Sign in";
          break;
        default:
          this.caption = "Create New Post";
          break;
      }

      if (step === "editor") {
        this.$nextTick(() => {
          this.initializeEditor();
          this.focusRef("titleInput");
        });
      } else if (previousStep === "editor") {
        this.destroyEditor();
      }

      if (step === "optional") {
        this.$nextTick(() => this.focusRef("tagsInput"));
      } else if (step === "success") {
        this.$nextTick(() => this.focusRef("viewPostButton"));
      }
    },

    clearRetryAction() {
      this.retryAction = null;
      this.retryLabel = DEFAULT_RETRY_LABEL;
    },

    setRetryAction(operation, options = {}) {
      this.clearRetryAction();
      if (typeof operation !== "function") {
        return;
      }

      const wrappedOperation = () => {
        this.clearRetryAction();
        operation();
      };

      this.retryAction = wrappedOperation;
      this.retryLabel = options.label ?? DEFAULT_RETRY_LABEL;
    },

    scheduleRetry(operation) {
      this.setRetryAction(operation);
    },

    triggerRetry() {
      if (this.retryAction) {
        this.retryAction();
      }
    },

    redirectToLogin() {
      authService.redirectToLogin();
    },

    startLogin() {
      this.isLoading = true;
      this.redirectToLogin();
    },

    destroyEditor() {
      if (!this.editorInstance) {
        return;
      }

      const textarea = this.editorInstance.textarea;

      try {
        this.editorInstance.destroy?.();
      } catch (error) {
        console.warn("[MarkdownEditor] Failed to destroy TinyMDE instance", error);
      }

      if (textarea) {
        textarea.style.removeProperty("display");
      }

      this.editorInstance = null;
    },

    initializeEditor() {
      if (!this.isEditorStep) {
        return;
      }

      const textarea = this.$refs.editor;
      if (!textarea) {
        return;
      }

      const tinyMde = window.TinyMDE;
      if (!tinyMde || !tinyMde.Editor) {
        console.error(
          "[MarkdownEditor] TinyMDE is not available; ensure the script is loaded."
        );
        return;
      }

      if (this.editorInstance) {
        if (this.editorInstance.textarea === textarea) {
          if (
            typeof this.editorInstance.getContent === "function" &&
            this.post.body !== this.editorInstance.getContent()
          ) {
            this.editorInstance.setContent(this.post.body ?? "");
          }
          return;
        }

        // Stale instance on different element? Destroy it.
        this.destroyEditor();
      }

      this.editorInstance = new tinyMde.Editor({
        textarea,
        content: this.post.body ?? "",
      });

      if (typeof this.editorInstance.getContent === "function") {
        this.post.body = this.editorInstance.getContent() ?? "";
      }

      const onEditorInput = (event) => {
        const value =
          (event && event.content !== undefined
            ? event.content
            : this.editorInstance.getContent?.()) ?? "";
        this.post.body = value;
      };

      this.editorInstance.addEventListener("change", onEditorInput);
      this.editorInstance.addEventListener("input", onEditorInput);
    },

    async init() {
      try {
        console.info("[MarkdownEditor] initializing component");
        initConfig(document.body.dataset);

        const parsedNodeId = Number(config.articulateBlogNode);
        this.post.articulateBlogNode = Number.isNaN(parsedNodeId)
          ? config.articulateBlogNode
          : parsedNodeId;



        const params = new URLSearchParams(window.location.search);
        if (params.has("code")) {
          await authService.handleLoginCallback();
        }

        const token = authService.getAccessToken();


        if (!token) {
          console.info(
            '[MarkdownEditor] no access token found; showing login view'
          );
          this.isLoading = false;
          this.errorDetails = null;
          this.showLogin('Please sign in below.');
          return;
        }

        const sessionEstablished = authService.hasValidAccessToken();
        if (!sessionEstablished) {
          console.info(
            '[MarkdownEditor] access token missing or expired; showing login view'
          );
          this.isLoading = false;
          this.errorDetails = null;
          this.showLogin(
            'Your session has timed out. Please sign in again below.'
          );
          return;
        }

        this.currentUser = await apiService.getCurrentUser();
        console.info(
          "[MarkdownEditor] authenticated as:",
          this.currentUser?.name ?? this.currentUser?.username ?? "unknown"
        );

        this.setStep("editor");
        await this.$nextTick();
        this.initializeEditor();
        console.info("[MarkdownEditor] initialization complete");
      } catch (error) {
        console.error("Initialization failed:", error);
        const formattedError = await formatApiError(error);
        const friendlyDetail = formattedError.details?.[0];

        if (formattedError.isAuthError) {
          this.errorDetails = null;
          this.showLogin("Please sign in below.");
        } else if (formattedError.isNetworkError) {
          this.errorDetails = this.normalizeErrorDetails(
            formattedError,
            "Connection issue"
          );
          this.showLogin(
            friendlyDetail ||
            "We couldn't reach Umbraco. Please check your connection and try again.",
            { preserveErrors: true }
          );
          this.setRetryAction(() => window.location.reload(), {
            label: "Reload page",
          });
        } else {
          this.errorDetails = this.normalizeErrorDetails(
            formattedError,
            "Something went wrong"
          );
          this.showLogin(
            friendlyDetail ||
            "We ran into a problem. Please sign in again once the issue is resolved.",
            { preserveErrors: true }
          );
        }
      } finally {
        this.isLoading = false;
      }
    },

    async handlePublish() {
      if (!this.canPublish) {
        this.errorDetails = {
          title: "Missing required fields",
          details: ["Title and content are required before publishing."],
        };
        return;
      }

      this.isLoading = true;
      this.errorDetails = null;

      try {
        const bodyText = this.post.body || "";
        for (const token of this.fileMap.keys()) {
          if (!bodyText.includes(token)) {
            this.fileMap.delete(token);
          }
        }

        const result = await apiService.createPost(this.post, this.fileMap);
        if (result.url) {
          this.successUrl = result.url;
          this.setStep("success");
          console.info(
            "[MarkdownEditor] publish complete; success url:",
            result.url
          );
          this.clearRetryAction();
        } else {
          throw new Error("Received an empty or invalid URL from the server.");
        }
      } catch (error) {
        console.error("[MarkdownEditor] handlePublish failed", error);
        const formattedError = await formatApiError(error);
        this.errorDetails = this.normalizeErrorDetails(
          formattedError,
          "Publish failed"
        );

        if (
          formattedError.isAuthError ||
          formattedError.isNetworkError ||
          formattedError.isForbidden
        ) {
          if (formattedError.isAuthError) {
            this.setRetryAction(() => {
              this.redirectToLogin();
            }, {
              label: "Sign in again",
            });
          } else {
            this.scheduleRetry(() => this.handlePublish());
          }
        }
      } finally {
        this.isLoading = false;
      }
    },

    goToOptionalStep() {
      if (!this.canProceedToOptional) {
        return;
      }
      this.setStep("optional");
    },

    goToEditorStep() {
      this.setStep("editor");
    },

    resetForNewPost() {
      const nodeId = this.post.articulateBlogNode;
      this.post = createEmptyPost(nodeId);
      this.titleTouched = false;
      this.fileMap.clear();
      this.successUrl = null;
      this.errorDetails = null;
      this.setStep("editor");
    },

    updatePostTitle(event) {
      this.post.title = event?.target?.value ?? "";
    },

    markTitleTouched(event) {
      this.titleTouched = true;
    },

    updatePostField(event) {
      const field = event?.target?.id;
      const value = event?.target?.value ?? "";

      switch (field) {
        case "tags":
          this.post.tags = value;
          break;
        case "categories":
          this.post.categories = value;
          break;
        case "excerpt":
          this.post.excerpt = value;
          break;
        case "slug":
          this.post.slug = value;
          break;
        default:
          break;
      }
    },

    handleImageUpload(event) {
      const files = Array.from(event?.target?.files ?? []);
      if (!files.length) {
        return;
      }

      //TODO: Consider passing from config and server side validation (IArticulateImportMediaService)
      const MAX_UPLOAD_BYTES = 50 * 1024 * 1024; // 50MB
      const allowedTypes = ["image/jpeg", "image/png", "image/gif"];

      files.forEach((file) => {
        if (!file) return;
        const typeOk = allowedTypes.includes(file.type);
        const sizeOk = typeof file.size === "number" && file.size <= MAX_UPLOAD_BYTES;
        if (!typeOk || !sizeOk) {
          const reasons = [];
          if (!typeOk) reasons.push("unsupported file type");
          if (!sizeOk) reasons.push("file too large (max 25MB)");
          this.errorDetails = {
            title: "Image upload not allowed",
            details: [
              `${(file.name || "file").trim()}: ${reasons.join(", ")}. Allowed: JPEG, PNG, GIF.`,
            ],
          };
          return;
        }

        const uploadToken = this.createUploadToken();
        this.fileMap.set(uploadToken, file);

        const displayName = (file?.name ?? "").trim() || "file";
        const altText = this.escapeMarkdownText(`Uploading ${displayName}...`);
        const placeholder = `![${altText}](${uploadToken})\n`;


        if (
          this.editorInstance &&
          typeof this.editorInstance.paste === "function"
        ) {
          this.editorInstance.paste(placeholder);
        } else {
          this.post.body = `${this.post.body}${placeholder}`;
        }
      });
      if (event?.target) {
        event.target.value = "";
      }
    },
    triggerImageUpload() {
      this.$refs.imageUpload?.click();
    },

    triggerCamera() {
      this.$refs.cameraUpload?.click();
    },

    async logout() {
      this.isLoading = true;
      try {
        await authService.logout();
        this.showLogin("You have been signed out. Sign in again to continue.");
      } catch (error) {
        console.error("[MarkdownEditor] logout failed", error);
        const formattedError = await formatApiError(error, "Sign-out failed");
        this.errorDetails = this.normalizeErrorDetails(
          formattedError,
          "Sign-out failed"
        );
        this.setRetryAction(() => this.redirectToLogin(), {
          label: "Continue to login",
        });
      } finally {
        this.isLoading = false;
      }
    },

    showLogin(message, options = {}) {
      const { preserveErrors = false } = options;
      this.currentUser = null;
      this.loginNotice = message?.trim().length
        ? message.trim()
        : "Please sign in below.";
      if (!preserveErrors) {
        this.errorDetails = null;
      }
      this.setStep("login");
      this.$nextTick(() => this.focusRef("loginButton"));
    },

    normalizeErrorDetails(error, fallbackTitle) {
      if (!error) {
        return null;
      }

      const details = Array.isArray(error.details)
        ? error.details.filter(Boolean)
        : [];

      const title = (error.title || fallbackTitle || "Something went wrong")
        .toString()
        .trim();

      return {
        ...error,
        title,
        details,
      };
    },
  }));
});
