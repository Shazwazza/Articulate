angular.module("umbraco").controller("Articulate.Dashboard.ThemeManagement",
  function ($scope, umbRequestHelper, formHelper, assetsService, notificationsService, $http) {

    var vm = this;    

    function copyTheme() {
      return umbRequestHelper.resourcePromise(
        $http.post(
          Umbraco.Sys.ServerVariables["articulate"]["articulateThemeEditorBaseUrl"] + "PostCopyTheme", {
            themeName: vm.themeName,
            newThemeName: vm.newThemeName
        }),
        'Failed to copy Articulate theme');
    }

    function getThemes() {
      return umbRequestHelper.resourcePromise(
        $http.post(
          Umbraco.Sys.ServerVariables["articulate"]["articulateThemeEditorBaseUrl"] + "GetThemes"),
        'Failed to copy Articulate theme');
    }

    vm.buttonState = "init";
    vm.themeName = "";

    assetsService.loadCss('~/app_plugins/articulate/backoffice/assets/themes.css', $scope).then(function () {
      getThemes()
        .then(function (data) {
          vm.themes = data;
        });
    });

    vm.select = function (theme) {
      _.each(vm.themes, function (t) {
        t.selected = false;
      });
      theme.selected = true;
      vm.themeName = theme.name;
    }

    vm.copyTheme = function () {

      vm.status = "";

      if (formHelper.submitForm({ scope: $scope, formCtrl: vm.articulateThemeForm })) {

        vm.buttonState = "busy";

        copyTheme()
          .then(function (data) {
            formHelper.resetForm({ scope: $scope, formCtrl: vm.articulateThemeForm });
            vm.status = "done";
            vm.buttonState = "success";
          }, function (err) {
            formHelper.resetForm({ scope: $scope, formCtrl: vm.articulateThemeForm , hasErrors: true });
            formHelper.handleError(err);
            vm.buttonState = "error";
          });
      }
      else {
        vm.buttonState = "error";
      }
    }
  });
