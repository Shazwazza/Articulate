(function () {
    "use strict";

    function ThemeDeleteController($scope, articulateThemeResource, treeService, navigationService) {

        $scope.performDelete = function () {

            //mark it for deletion (used in the UI)
            $scope.currentNode.loading = true;
            
            articulateThemeResource.deleteItem($scope.currentNode.id)
                .then(function () {
                    $scope.currentNode.loading = false;
                    treeService.removeNode($scope.currentNode);
                    navigationService.hideMenu();
                });
            
        };

        $scope.cancel = function () {
            navigationService.hideDialog();
        };
    }

    angular.module("umbraco").controller("Articulate.Editors.ThemeDeleteController", ThemeDeleteController);
})();