(function () {
    "use strict";

    function articulateThemeCreateController($scope, articulateThemeResource, formHelper, navigationService) {

        var vm = this;
        vm.themeName = "";
        vm.buttonState = "none";

        articulateThemeResource.getThemes().then(function (data) {
            vm.themes = data;
        });

        function init() {
            $scope.$watch(function () {
                return vm.themeName;
            },
                function (newVal, oldVal) {
                    if (!newVal) {
                        vm.buttonState = "none";
                    }
                    else if (vm.buttonState === "selected") {
                        vm.buttonState = "ready";
                    }
                });
        }

        function selectTheme(theme) {
            angular.forEach(vm.themes, function (t) {
                t.selected = false;
            });
            theme.selected = true;
            vm.buttonState = vm.themeName ? "ready" : "selected";
        }

        function createTheme() {
            if (formHelper.submitForm({ scope: $scope })) {

                var selected = _.find(vm.themes, function (t) {
                    return t.selected === true;
                });

                articulateThemeResource.copyTheme(vm.themeName, selected.name).then(function (data) {
                    $scope.nav.hideDialog();
                    navigationService.syncTree({ tree: "articulatethemes", path: data.path, forceReload: true });
                });

            }
        }

        init();

        vm.createTheme = createTheme;
        vm.selectTheme = selectTheme;

    }

    angular.module("umbraco").controller("Articulate.Editors.ThemeCreateController", articulateThemeCreateController);


})();