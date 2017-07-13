(function () {
    "use strict";

    function articulateThemeEditController($scope, $routeParams, articulateThemeResource, assetsService, notificationsService, editorState, navigationService, appState, macroService, angularHelper, $timeout, contentEditingHelper, localizationService, templateHelper, macroResource) {

        var vm = this;
        var localizeSaving = localizationService.localize("general_saving");

        vm.page = {};
        vm.page.loading = true;

        //menu
        vm.page.menu = {};
        vm.page.menu.currentSection = appState.getSectionState("currentSection");
        vm.page.menu.currentNode = null;

        // bind functions to view model
        vm.save = save;

        /* Functions bound to view model */

        function save() {

            vm.page.saveButtonState = "busy";
            vm.themeFile.content = vm.editor.getValue();

            contentEditingHelper.contentEditorPerformSave({
                statusMessage: localizeSaving,
                saveMethod: articulateThemeResource.saveThemeFile,
                scope: $scope,
                content: vm.themeFile,
                // We do not redirect on failure for partial view macros - this is because it is not possible to actually save the partial view
                // when server side validation fails - as opposed to content where we are capable of saving the content
                // item if server side validation fails
                redirectOnFailure: false,
                rebindCallback: function (orignal, saved) { }
            }).then(function (saved) {
                // create macro if needed
                if ($routeParams.create && $routeParams.nomacro !== "true") {
                    macroResource.createPartialViewMacroWithFile(saved.virtualPath, saved.name).then(function (created) {
                        completeSave(saved);
                    }, function (err) {
                        //show any notifications
                        if (angular.isArray(err.data.notifications)) {
                            for (var i = 0; i < err.data.notifications.length; i++) {
                                notificationsService.showNotification(err.data.notifications[i]);
                            }
                        }
                    });
                } else {
                    completeSave(saved);
                }

            }, function (err) {

                vm.page.saveButtonState = "error";

                localizationService.localize("speechBubbles_validationFailedHeader").then(function (headerValue) {
                    localizationService.localize("speechBubbles_validationFailedMessage").then(function (msgValue) {
                        notificationsService.error(headerValue, msgValue);
                    });
                });

            });

        }

        function completeSave(saved) {

            localizationService.localize("speechBubbles_partialViewSavedHeader").then(function (headerValue) {
                localizationService.localize("speechBubbles_partialViewSavedText").then(function (msgValue) {
                    notificationsService.success(headerValue, msgValue);
                });
            });

            //check if the name changed, if so we need to redirect
            if (vm.themeFile.id !== saved.id) {
                contentEditingHelper.redirectToRenamedContent(saved.id);
            }
            else {
                vm.page.saveButtonState = "success";
                vm.themeFile = saved;

                //sync state
                editorState.set(vm.themeFile);

                // normal tree sync
                navigationService.syncTree({ tree: "articulatethemes", path: vm.themeFile.path, forceReload: true }).then(function (syncArgs) {
                    vm.page.menu.currentNode = syncArgs.node;
                });

                // clear $dirty state on form
                setFormState("pristine");
            }

        }

        /* Local functions */

        function init() {
            //we need to load this somewhere, for now its here.
            assetsService.loadCss("lib/ace-razor-mode/theme/razor_chrome.css");

            if ($routeParams.create) {

                var fileType = "cshtml";

                if ($routeParams.filetype) {
                    fileType = $routeParams.filetype;
                }

                articulateThemeResource.getScaffold($routeParams.id, fileType).then(function (themeFile) {
                    if ($routeParams.name) {
                        themeFile.name = $routeParams.name;
                    }
                    ready(themeFile, false);
                });

            }
            else {
                articulateThemeResource.getByPath($routeParams.id).then(
                    function (themeFile) {
                        ready(themeFile, true);
                    });
            }
        }

        function ready(themeFile, syncTree) {

            vm.page.loading = false;
            vm.themeFile = themeFile;

            //sync state
            editorState.set(vm.themeFile);

            if (syncTree) {
                navigationService.syncTree({ tree: "articulatethemes", path: vm.themeFile.path, forceReload: true }).then(function (syncArgs) {
                    vm.page.menu.currentNode = syncArgs.node;
                });
            }

            // ace configuration

            var fileext = themeFile.name.split('.').pop().toLowerCase();
            var mode = fileext === "cshtml"
                ? "razor"
                : fileext === "css"
                    ? "text"
                    : fileext === "js"
                        ? "javascript"
                        : "razor";
            
            vm.aceOption = {
                mode: mode,
                theme: "chrome",
                showPrintMargin: false,
                advanced: {
                    fontSize: '14px'
                },
                onLoad: function (_editor) {
                    vm.editor = _editor;

                    // initial cursor placement
                    // Keep cursor in name field if we are create a new template
                    // else set the cursor at the bottom of the code editor
                    if (!$routeParams.create) {
                        $timeout(function () {
                            vm.editor.navigateFileEnd();
                            vm.editor.focus();
                            persistCurrentLocation();
                        });
                    }

                    //change on blur, focus
                    vm.editor.on("blur", persistCurrentLocation);
                    vm.editor.on("focus", persistCurrentLocation);
                    vm.editor.on("change", changeAceEditor);

                }
            }

        }

        function persistCurrentLocation() {
            vm.currentPosition = vm.editor.getCursorPosition();
        }

        function changeAceEditor() {
            setFormState("dirty");
        }

        function setFormState(state) {

            // get the current form
            var currentForm = angularHelper.getCurrentForm($scope);

            // set state
            if (state === "dirty") {
                currentForm.$setDirty();
            } else if (state === "pristine") {
                currentForm.$setPristine();
            }
        }


        init();

    }

    angular.module("umbraco").controller("Articulate.Editors.ThemeEditController", articulateThemeEditController);


})();