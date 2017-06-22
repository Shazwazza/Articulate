(function () {
  "use strict";

  function articulateThemeCreateController($scope, articulateThemeResource, formHelper) {

    var vm = this;
    vm.themeName = "";

    articulateThemeResource.getThemes().then(function(data) {
      vm.themes = data;
    });

    function init() {

    }

    function createTheme(themeName, copy) {
      if (formHelper.submitForm({ scope: $scope })) {

        articulateThemeResource.copyTheme(themeName, copy).then(function (data) {
          alert("done!");
        });

      }
    }

    init();

    vm.createTheme = createTheme;

  }

  angular.module("umbraco").controller("Articulate.Editors.ThemeCreateController", articulateThemeCreateController);


})();