angular.module('umbraco').controller("Articulate.PropertyEditors.ThemePicker", function ($scope, umbRequestHelper, $http) {

    var url = Umbraco.Sys.ServerVariables["articulate"]["articulatePropertyEditorsBaseUrl"] + "GetThemes";

    umbRequestHelper.resourcePromise(
            $http.get(url),
            'Failed to retrieve themes for Articulate')
        .then(function (data) {
            $scope.themes = data;
        });

});