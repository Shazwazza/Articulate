(function () {
    "use strict";

    function articulateThemeCreateController($scope, articulateThemeResource, formHelper, navigationService) {

        var vm = this;
        vm.themeName = "";
        vm.buttonState = "init";
        vm.isReady = false;

        articulateThemeResource.getThemes().then(function (data) {
            vm.themes = data;
        });

        function init() {
            $scope.$watch(function () {
                return vm.themeName;
            },
                function (newVal, oldVal) {
                    if (!newVal) {
                        vm.isReady = false;
                    }
                    else if (vm.buttonState === "selected") {
                        vm.isReady = true;
                    }
                });
        }
        
        function selectTheme(theme) {
            angular.forEach(vm.themes, function (t) {
                t.selected = false;
            });
            theme.selected = true;
            vm.isReady = vm.themeName ? true: false;
        }

        function createTheme() {
            if (formHelper.submitForm({ scope: $scope })) {

                vm.buttonState = "busy";

                var selected = _.find(vm.themes, function (t) {
                    return t.selected === true;
                });

                articulateThemeResource.copyTheme(vm.themeName, selected.name).then(function (data) {
                    vm.buttonState = "success";                    
                    navigationService.syncTree({ tree: "articulatethemes", path: data.path, forceReload: true });
                    $scope.nav.hideDialog();
                }, function (err) {
                    vm.buttonState = "error";
                    formHelper.handleError(err);
                });

            }
        }

        init();

        vm.createTheme = createTheme;
        vm.selectTheme = selectTheme;
        
    }

    angular.module("umbraco").controller("Articulate.Editors.ThemeCreateController", articulateThemeCreateController);


})();