(function () {
    "use strict";

    function articulateThemeCreateController($scope, $location, $routeParams, articulateThemeResource, formHelper, navigationService) {

        var vm = this;
        vm.themeName = "";
        vm.fileName = "";
        vm.buttonState = "init";
        vm.isReady = false;
        vm.isRoot = false;

        var currId = decodeURIComponent($scope.currentNode.id);

        function init() {

            
            if (currId === "-1") {
                vm.isRoot = true;
                //if this is the root folder
                articulateThemeResource.getThemes().then(function (data) {
                    vm.themes = data;
                    addWatch("themeName", vm.themes);
                });
            }
            else {
                vm.fileTypes = [
                    { name: "Folder", icon: "icon-folder" },
                    { name: "JavaScript", icon: "icon-script" },
                    { name: "Css", icon: "icon-brackets" },
                    { name: "Razor", icon: "icon-newspaper" }
                ];

                addWatch("fileName", vm.fileTypes);
            }
        }

        function addWatch(propName, collection) {
            $scope.$watch(function () {
                return vm[propName];
            },
                function (newVal, oldVal) {
                    if (!newVal) {
                        vm.isReady = false;
                    } else {
                        angular.forEach(collection,
                            function (t) {
                                if (t.selected) {
                                    vm.isReady = true;
                                }
                            });
                    }
                });
        }

        function selectFileType(fileType) {
            angular.forEach(vm.fileTypes, function (t) {
                t.selected = false;
            });
            fileType.selected = true;
            vm.isReady = vm.fileName ? true : false;
        }

        function selectTheme(theme) {
            angular.forEach(vm.themes, function (t) {
                t.selected = false;
            });
            theme.selected = true;
            vm.isReady = vm.themeName ? true : false;
        }

        function actionSuccess(data) {
            vm.buttonState = "success";
            navigationService.syncTree({ tree: "articulatethemes", path: data.path, forceReload: true });
            $scope.nav.hideDialog();
        }

        function createTheme() {
            if (formHelper.submitForm({ scope: $scope })) {

                vm.buttonState = "busy";

                var selected = _.find(vm.themes, function (t) {
                    return t.selected === true;
                });

                articulateThemeResource.copyTheme(vm.themeName, selected.name).then(function (data) {
                    actionSuccess(data);
                }, function (err) {
                    vm.buttonState = "error";
                    formHelper.handleError(err);
                });

            }
        }

        function createFile() {
            if (formHelper.submitForm({ scope: $scope })) {

                vm.buttonState = "busy";

                var selected = _.find(vm.fileTypes, function (t) {
                    return t.selected === true;
                });

                articulateThemeResource.createFile(currId, vm.fileName, selected.name).then(function (data) {
                    actionSuccess(data);
                    if (data.fileType) {
                        //change to new path
                        $location.path("/" + $routeParams.section + "/articulatethemes/edit/" + data.id);    
                    }
                }, function (err) {
                    vm.buttonState = "error";
                    formHelper.handleError(err);
                });

            }
        }

        init();

        vm.createTheme = createTheme;
        vm.createFile = createFile;
        vm.selectTheme = selectTheme;
        vm.selectFileType = selectFileType;

    }

    angular.module("umbraco").controller("Articulate.Editors.ThemeCreateController", articulateThemeCreateController);


})();